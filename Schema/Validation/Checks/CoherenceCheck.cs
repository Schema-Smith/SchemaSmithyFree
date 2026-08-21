// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Delivery;
using Schema.Domain;
using Index = Schema.Domain.Index;

namespace Schema.Validation.Checks;

/// <summary>
/// Structural cross-object reference checks the JSON schema can't express: FK local/related
/// columns, related-table resolution (incl. schema defaulting), FK column-count cardinality, and
/// index-column existence. Deliberately NO type-agreement checks and NO DeleteAction/UpdateAction
/// checks — those are out of scope for this check (see task brief); they belong to JSON-schema
/// lint or a future slice.
/// </summary>
public sealed class CoherenceCheck : ISchemaCheck
{
    private const string LocalColumnCode = "SS-FK-001";
    private const string RelatedTableCode = "SS-FK-002";
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

        var findings = new List<Finding>();
        foreach (var template in ctx.Templates)
        foreach (var table in template.Tables)
        {
            var location = $"Template '{template.Name}' / Table '{table.Name}'";
            foreach (var fk in table.ForeignKeys)
                findings.AddRange(CheckForeignKey(table, fk, location, tablesByKey));

            foreach (var index in table.Indexes)
                findings.AddRange(CheckIndex(table, index, location));
        }

        return findings;
    }

    private static IEnumerable<Finding> CheckForeignKey(
        Table table,
        ForeignKey fk,
        string tableLocation,
        IReadOnlyDictionary<(string Schema, string Name), List<Table>> tablesByKey)
    {
        var location = $"{tableLocation} / FK '{fk.Name}'";
        var localColumnNames = ColumnNames(table);
        var fkColumns = SplitNames(fk.Columns);
        var fkRelatedColumns = SplitNames(fk.RelatedColumns);

        foreach (var column in fkColumns.Where(column => !localColumnNames.Contains(NormalizeIdentifier(column))))
            yield return new Finding(Severity.Error, LocalColumnCode, Category, location,
                $"{location}: local column '{column}' referenced in Columns does not exist on table '{table.Name}'.");

        // Cardinality is a pure string-count comparison — independent of whether the related
        // table resolves, so it always runs.
        if (fkColumns.Count != fkRelatedColumns.Count)
            yield return new Finding(Severity.Error, CardinalityCode, Category, location,
                $"{location}: Columns has {fkColumns.Count} entries but RelatedColumns has {fkRelatedColumns.Count} — FK column lists must be the same length.");

        var (schema, name) = ResolveRelatedTarget(table, fk);

        if (!tablesByKey.TryGetValue((schema, name), out var relatedTables))
        {
            yield return new Finding(Severity.Error, RelatedTableCode, Category, location,
                $"{location}: RelatedTable '{fk.RelatedTable}' does not resolve to any known table (resolved schema '{schema}').");
            yield break;
        }

        var relatedColumnNames = new HashSet<string>(
            relatedTables.SelectMany(ColumnNames),
            StringComparer.OrdinalIgnoreCase);

        foreach (var column in fkRelatedColumns.Where(column => !relatedColumnNames.Contains(NormalizeIdentifier(column))))
            yield return new Finding(Severity.Error, RelatedColumnCode, Category, location,
                $"{location}: related column '{column}' referenced in RelatedColumns does not exist on related table '{fk.RelatedTable}'.");
    }

    private static IEnumerable<Finding> CheckIndex(Table table, Index index, string tableLocation)
    {
        var location = $"{tableLocation} / Index '{index.Name}'";
        var localColumnNames = ColumnNames(table);

        foreach (var column in SplitNames(index.IndexColumns).Select(StripOrderingSuffix)
                     .Where(column => !IsExpressionKeyPart(column))
                     .Where(column => !localColumnNames.Contains(NormalizeIdentifier(column))))
            yield return new Finding(Severity.Error, IndexColumnCode, Category, location,
                $"{location}: index column '{column}' referenced in IndexColumns does not exist on table '{table.Name}'.");
    }

    /// <summary>
    /// Resolves a FK's target (schema, name). Schema precedence: RelatedTableSchema (SS/PG
    /// platform property — SchemaDefaultResolver.ResolveRelatedTableSchema always fills this
    /// during Template.Load with a concrete schema, so it's never ambiguous) if set, else a
    /// "schema." prefix parsed off RelatedTable, else the OWNING table's schema — this last
    /// default is what makes an unqualified same-schema reference work, including inside a
    /// schema template where every table's schema is the same "{{SchemaName}}" token. On MySQL,
    /// RelatedTableSchema is always null and MySqlTable.Schema is always null, so resolution
    /// collapses to Name-only identity — consistent with the (schema,name) table lookup.
    /// </summary>
    private static (string Schema, string Name) ResolveRelatedTarget(Table owningTable, ForeignKey fk)
    {
        var relatedTableSchema = (fk as IDeliverableForeignKey)?.RelatedTableSchema;
        var hasExplicitSchemaProperty = !string.IsNullOrEmpty(relatedTableSchema);

        var rawRelatedTable = fk.RelatedTable ?? "";
        var dotIndex = rawRelatedTable.IndexOf('.');
        var hasDotPrefix = dotIndex > 0;
        var prefixSchema = hasDotPrefix ? rawRelatedTable[..dotIndex] : null;
        var name = IdentityKey(hasDotPrefix ? rawRelatedTable[(dotIndex + 1)..] : rawRelatedTable);

        var schema = hasExplicitSchemaProperty ? relatedTableSchema
            : prefixSchema ?? NormalizedSchema(owningTable);

        return (IdentityKey(schema), name);
    }

    // IDeliverableTable.Schema is resolved uniformly across platforms (SchemaDefaultResolver
    // fills "dbo"/"public"/the "{{SchemaName}}" token; MySqlTable's explicit interface
    // implementation always returns null) — matches the identity accessor DuplicationCheck uses.
    // Both sides of the identity comparison must strip identifier wrapping. SchemaDefaultResolver
    // preserves a declared Schema verbatim -- "[dbo]" stays bracketed -- but an FK that OMITS
    // RelatedTableSchema has it filled with the platform default, "dbo", unbracketed. Comparing the raw
    // strings therefore made every such FK unresolvable, which is the ordinary hand-authored shape: two
    // shipped demos reported SS-FK-002 against a table sitting in the same template. Packages that spell
    // RelatedTableSchema out explicitly matched by luck, because then both sides carry the brackets.
    private static string IdentityKey(string identifier) =>
        NormalizeIdentifier(identifier).ToLowerInvariant();

    private static (string Schema, string Name) TableKey(Table table) =>
        (NormalizedSchema(table), IdentityKey(table.Name));

    private static string NormalizedSchema(Table table) =>
        IdentityKey((table as IDeliverableTable)?.Schema ?? "");

    private static HashSet<string> ColumnNames(Table table) =>
        new(table.Columns.Select(c => NormalizeIdentifier(c.Name ?? "")), StringComparer.OrdinalIgnoreCase);

    // Mirrors SchemaSmith_StripBacktickWrapping.sql (source of truth for the backtick case — keep in
    // sync), generalized to the other two engines' wrapping so a hand-authored/hand-edited package
    // can mix quoting styles without producing a false SS-FK-*/SS-IDX-001: backtick (MySQL/MariaDB),
    // [bracket] (SQL Server), "double-quote" (PostgreSQL). Strips only a matched pair around the
    // WHOLE identifier — a lone/unbalanced quote character is left untouched, and interior content is
    // never touched (in particular, no expression key part reaches here — those are filtered out
    // before comparison). Comparison is OrdinalIgnoreCase everywhere in this class, so an unwrapped
    // PostgreSQL identifier (folds to lower case) and a "quoted" one (case-sensitive) are compared
    // case-insensitively too — a deliberate slight loosening: the failure mode this check should have
    // is a missed nit, never a false error on a valid package.
    private static string NormalizeIdentifier(string identifier)
    {
        var trimmed = (identifier ?? "").Trim();
        if (trimmed.Length < 2)
            return trimmed;

        if (trimmed[0] == '`' && trimmed[^1] == '`')
            return trimmed[1..^1].Replace("``", "`");
        if (trimmed[0] == '[' && trimmed[^1] == ']')
            return trimmed[1..^1];
        if (trimmed[0] == '"' && trimmed[^1] == '"')
            return trimmed[1..^1];

        return trimmed;
    }

    // Mirrors SchemaSmith_NormalizeIndexColumns.sql's top-level-comma split (source of truth — keep
    // in sync, along with StripOrderingSuffix below): splits on a comma only at paren depth 0 and
    // outside a backtick-quoted span, so a functional/expression key part's own internal comma (e.g.
    // `(concat(\`a\`,\`b\`))`) isn't mistaken for a key-part boundary. Also used for FK Columns/
    // RelatedColumns — a no-op there, since FK column lists never contain parens or backticks.
    private static List<string> SplitNames(string csv)
    {
        var text = csv ?? "";
        var len = text.Length;
        var result = new List<string>();
        var pos = 0;

        while (pos < len)
        {
            var depth = 0;
            var inBacktick = false;
            var comma = -1;

            for (var scan = pos; scan < len; scan++)
            {
                var c = text[scan];
                if (c == '`')
                    inBacktick = !inBacktick;
                else if (!inBacktick && c == '(')
                    depth++;
                else if (!inBacktick && c == ')')
                    depth--;
                else if (!inBacktick && c == ',' && depth == 0)
                {
                    comma = scan;
                    break;
                }
            }
            if (comma < 0)
                comma = len;

            var part = text[pos..comma].Trim();
            if (part.Length > 0)
                result.Add(part);

            pos = comma + 1;
        }

        return result;
    }

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

    // A functional/expression key part starts with '(' rather than a backtick — extraction always
    // backtick-wraps a plain column name, so this is an unambiguous discriminator (mirrors
    // SchemaSmith_NormalizeIndexColumns.sql). Validating the identifiers inside the expression would
    // need a real SQL parser and is out of scope — skip rather than false-flag.
    private static bool IsExpressionKeyPart(string column) => column.StartsWith('(');
}
