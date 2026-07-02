// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Delivery;
using Schema.Domain;
using Index = Schema.Domain.Index;

namespace SchemaQuench.Validation.Checks;

/// <summary>
/// Structural cross-object reference checks the JSON schema can't express: FK local/related
/// columns, related-table resolution (incl. schema defaulting and cross-schema ambiguity), FK
/// column-count cardinality, and index-column existence. Deliberately NO type-agreement checks
/// and NO DeleteAction/UpdateAction checks — those are out of scope for this check (see task
/// brief); they belong to JSON-schema lint or a future slice.
/// </summary>
public sealed class CoherenceCheck : ISchemaCheck
{
    private const string LocalColumnCode = "SS-FK-001";
    private const string RelatedTableCode = "SS-FK-002";
    private const string AmbiguousRelatedTableCode = "SS-FK-003";
    private const string RelatedColumnCode = "SS-FK-004";
    private const string CardinalityCode = "SS-FK-005";
    private const string IndexColumnCode = "SS-IDX-001";
    private const string Category = "Coherence";

    public IEnumerable<Finding> Run(ValidationContext ctx)
    {
        // (schema, name) identity groups multiple GATED variants of the same logical table
        // together (DuplicationCheck already established this is one logical table, not a
        // collision) — union their columns so a column that exists only on one variant still
        // counts as present on "the table".
        var tablesByKey = ctx.AllTables
            .GroupBy(t => TableKey(t))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Unqualified-name ambiguity is a cross-schema concept: how many DISTINCT schemas does
        // this bare name appear in at all, regardless of which (schema,name) group it lands in.
        var schemasByName = ctx.AllTables
            .GroupBy(t => t.Name?.Trim().ToLowerInvariant() ?? "")
            .ToDictionary(g => g.Key, g => g.Select(t => NormalizedSchema(t)).Distinct().ToList());

        var findings = new List<Finding>();
        foreach (var template in ctx.Templates)
        foreach (var table in template.Tables)
        {
            var location = $"Template '{template.Name}' / Table '{table.Name}'";
            foreach (var fk in table.ForeignKeys)
                findings.AddRange(CheckForeignKey(table, fk, location, tablesByKey, schemasByName));

            foreach (var index in table.Indexes)
                findings.AddRange(CheckIndex(table, index, location));
        }

        return findings;
    }

    private static IEnumerable<Finding> CheckForeignKey(
        Table table,
        ForeignKey fk,
        string tableLocation,
        IReadOnlyDictionary<(string Schema, string Name), List<Table>> tablesByKey,
        IReadOnlyDictionary<string, List<string>> schemasByName)
    {
        var location = $"{tableLocation} / FK '{fk.Name}'";
        var localColumnNames = ColumnNames(table);
        var fkColumns = SplitNames(fk.Columns);
        var fkRelatedColumns = SplitNames(fk.RelatedColumns);

        foreach (var column in fkColumns)
        {
            if (!localColumnNames.Contains(column))
                yield return new Finding(Severity.Error, LocalColumnCode, Category, location,
                    $"{location}: local column '{column}' referenced in Columns does not exist on table '{table.Name}'.");
        }

        // Cardinality is a pure string-count comparison — independent of whether the related
        // table resolves, so it always runs.
        if (fkColumns.Count != fkRelatedColumns.Count)
            yield return new Finding(Severity.Error, CardinalityCode, Category, location,
                $"{location}: Columns has {fkColumns.Count} entries but RelatedColumns has {fkRelatedColumns.Count} — FK column lists must be the same length.");

        var (schema, name, wasUnqualified) = ResolveRelatedTarget(table, fk);

        if (wasUnqualified && schemasByName.TryGetValue(name, out var schemas) && schemas.Count > 1)
        {
            yield return new Finding(Severity.Error, AmbiguousRelatedTableCode, Category, location,
                $"{location}: RelatedTable '{fk.RelatedTable}' is unqualified and ambiguous — it exists in {schemas.Count} different schemas ({string.Join(", ", schemas)}); qualify it with a schema.");
            yield break;
        }

        if (!tablesByKey.TryGetValue((schema, name), out var relatedTables))
        {
            yield return new Finding(Severity.Error, RelatedTableCode, Category, location,
                $"{location}: RelatedTable '{fk.RelatedTable}' does not resolve to any known table (resolved schema '{schema}').");
            yield break;
        }

        var relatedColumnNames = new HashSet<string>(
            relatedTables.SelectMany(ColumnNames),
            StringComparer.OrdinalIgnoreCase);

        foreach (var column in fkRelatedColumns)
        {
            if (!relatedColumnNames.Contains(column))
                yield return new Finding(Severity.Error, RelatedColumnCode, Category, location,
                    $"{location}: related column '{column}' referenced in RelatedColumns does not exist on related table '{fk.RelatedTable}'.");
        }
    }

    private static IEnumerable<Finding> CheckIndex(Table table, Index index, string tableLocation)
    {
        var location = $"{tableLocation} / Index '{index.Name}'";
        var localColumnNames = ColumnNames(table);

        foreach (var rawColumn in SplitNames(index.IndexColumns))
        {
            var column = StripOrderingSuffix(rawColumn);
            if (!localColumnNames.Contains(column))
                yield return new Finding(Severity.Error, IndexColumnCode, Category, location,
                    $"{location}: index column '{column}' referenced in IndexColumns does not exist on table '{table.Name}'.");
        }
    }

    /// <summary>
    /// Resolves a FK's target (schema, name) plus whether the reference was fully unqualified
    /// (no RelatedTableSchema AND no "schema." prefix on RelatedTable — the only shape ambiguity
    /// applies to). Schema precedence: RelatedTableSchema (SS/PG platform property) if set, else
    /// a "schema." prefix parsed off RelatedTable, else the OWNING table's schema — this last
    /// default is what makes an unqualified same-schema reference work, including inside a
    /// schema template where every table's schema is the same "{{SchemaName}}" token.
    /// </summary>
    private static (string Schema, string Name, bool WasUnqualified) ResolveRelatedTarget(Table owningTable, ForeignKey fk)
    {
        var relatedTableSchema = (fk as IDeliverableForeignKey)?.RelatedTableSchema;
        var hasExplicitSchemaProperty = !string.IsNullOrEmpty(relatedTableSchema);

        var rawRelatedTable = fk.RelatedTable ?? "";
        var dotIndex = rawRelatedTable.IndexOf('.');
        var hasDotPrefix = dotIndex > 0;
        var prefixSchema = hasDotPrefix ? rawRelatedTable[..dotIndex] : null;
        var name = (hasDotPrefix ? rawRelatedTable[(dotIndex + 1)..] : rawRelatedTable).Trim().ToLowerInvariant();

        var schema = hasExplicitSchemaProperty ? relatedTableSchema
            : prefixSchema ?? NormalizedSchema(owningTable);

        var wasUnqualified = !hasExplicitSchemaProperty && !hasDotPrefix;

        return (schema.Trim().ToLowerInvariant(), name, wasUnqualified);
    }

    // IDeliverableTable.Schema is resolved uniformly across platforms (SchemaDefaultResolver
    // fills "dbo"/"public"/the "{{SchemaName}}" token; MySqlTable's explicit interface
    // implementation always returns null) — matches the identity accessor DuplicationCheck uses.
    private static (string Schema, string Name) TableKey(Table table) =>
        (NormalizedSchema(table), table.Name?.Trim().ToLowerInvariant() ?? "");

    private static string NormalizedSchema(Table table) =>
        ((table as IDeliverableTable)?.Schema ?? "").Trim().ToLowerInvariant();

    private static HashSet<string> ColumnNames(Table table) =>
        new(table.Columns.Select(c => c.Name?.Trim() ?? ""), StringComparer.OrdinalIgnoreCase);

    private static List<string> SplitNames(string csv) =>
        (csv ?? "")
            .Split(',')
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToList();

    // Mirrors SchemaSmith_NormalizeIndexColumns.sql's DESC/ASC suffix handling (source of truth —
    // keep in sync): a trailing " DESC" or " ASC" (case-insensitive) is ordering, not part of the
    // column name.
    private static string StripOrderingSuffix(string column)
    {
        if (column.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
            return column[..^5].TrimEnd();
        if (column.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
            return column[..^4].TrimEnd();
        return column;
    }
}
