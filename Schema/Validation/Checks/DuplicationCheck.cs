// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Delivery;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;

namespace Schema.Validation.Checks;

/// <summary>
/// Flags accidental same-name duplicates at every level (column, index, foreign key, check
/// constraint, platform-specific variant-bearing collections, table, and product-level template
/// order) while treating legitimate <c>ShouldApplyExpression</c>-gated variant sets as valid.
/// Mirrors the variant-set definition <see cref="Schema.Utility.ImportTableHelper"/> already uses
/// for re-import preservation (same-named entries, Count() > 1) so this check is consistent with
/// the rest of the codebase's understanding of "variant set."
/// <para>Static limitation: this check cannot verify that a group's gates are mutually
/// <em>exclusive</em> — that requires evaluating the predicates against a live database. Presence
/// of gating on every member is all a load-time check can verify.</para>
/// </summary>
public sealed class DuplicationCheck : ISchemaCheck
{
    private const string DuplicateCode = "SS-DUP-001";
    private const string MissingVariantNameCode = "SS-DUP-VAR-002";
    private const string Category = "Duplicate";

    public IEnumerable<Finding> Run(ValidationContext ctx)
    {
        var findings = new List<Finding>();

        foreach (var template in ctx.Templates)
        {
            foreach (var table in template.Tables)
                findings.AddRange(CheckTableLevels(template, table));

            // Per-template: table-name collisions, keyed by schema-qualified identity so two
            // explicitly-different-schema tables sharing a bare name are not false positives.
            // Schema templates share the literal "{{SchemaName}}" token across every table, so
            // name collisions within one template are still caught.
            findings.AddRange(CheckNamedGroup(
                template.Tables,
                TableIdentity,
                t => t.ShouldApplyExpression,
                t => t.VariantName,
                "table",
                $"Template '{template.Name}'"));
        }

        // Per-product: TemplateOrder has no gate concept — any repeated name is unconditionally
        // an error. Reused via the same generic helper by passing an always-blank gate, which
        // forces every group of size > 1 down the "not all gated" (Error) path.
        findings.AddRange(CheckNamedGroup(
            ctx.Product.TemplateOrder ?? new List<string>(),
            name => name,
            _ => "",
            _ => null,
            "template",
            $"Product '{ctx.Product.Name}' / TemplateOrder"));

        return findings;
    }

    private static IEnumerable<Finding> CheckTableLevels(Template template, Table table)
    {
        var location = $"Template '{template.Name}' / Table '{table.Name}'";
        var findings = new List<Finding>();

        findings.AddRange(CheckNamedGroup(table.Columns, c => c.Name, c => c.ShouldApplyExpression, c => c.VariantName, "column", location));
        findings.AddRange(CheckNamedGroup(table.Indexes, i => i.Name, i => i.ShouldApplyExpression, i => i.VariantName, "index", location));
        findings.AddRange(CheckNamedGroup(table.ForeignKeys, f => f.Name, f => f.ShouldApplyExpression, f => f.VariantName, "foreign key", location));
        findings.AddRange(CheckNamedGroup(table.CheckConstraints, c => c.Name, c => c.ShouldApplyExpression, c => c.VariantName, "check constraint", location));

        switch (table)
        {
            case SqlServerTable ssTable:
                findings.AddRange(CheckNamedGroup(ssTable.XmlIndexes, x => x.Name, x => x.ShouldApplyExpression, x => x.VariantName, "XML index", location));
                findings.AddRange(CheckNamedGroup(ssTable.Statistics, s => s.Name, s => s.ShouldApplyExpression, s => s.VariantName, "statistic", location));
                // SqlServer FullTextIndex has no Name property (hand-authored conditional config —
                // see ImportTableHelper's own comment on the same limitation) — skipped, nothing to
                // group same-name entries on.
                break;
            case PostgreSqlTable pgTable:
                findings.AddRange(CheckNamedGroup(pgTable.Statistics, s => s.Name, s => s.ShouldApplyExpression, s => s.VariantName, "statistic", location));
                findings.AddRange(CheckNamedGroup(pgTable.ExcludeConstraints, e => e.Name, e => e.ShouldApplyExpression, e => e.VariantName, "exclude constraint", location));
                break;
            case MySqlTable myTable:
                findings.AddRange(CheckNamedGroup(myTable.FullTextIndexes, f => f.Name, f => f.ShouldApplyExpression, f => f.VariantName, "full-text index", location));
                break;
        }

        return findings;
    }

    // IDeliverableTable.Schema is resolved uniformly across platforms (SchemaDefaultResolver fills
    // "dbo"/"public"/the "{{SchemaName}}" token; MySqlTable's explicit interface implementation
    // always returns null), so casting to the interface gives one schema-qualification path for
    // all three platforms instead of a per-platform switch.
    private static string TableIdentity(Table table)
    {
        var schema = (table as IDeliverableTable)?.Schema;
        return string.IsNullOrEmpty(schema) ? table.Name : $"{schema}.{table.Name}";
    }

    /// <summary>
    /// Groups <paramref name="items"/> by <paramref name="name"/> (case-insensitive, blank names
    /// ignored) and applies the ShouldApply-aware duplication rule to every group of size > 1:
    /// every member gated → valid variant set (warn if any lacks a VariantName label); any member
    /// ungated → accidental duplicate (Error).
    /// </summary>
    private static IEnumerable<Finding> CheckNamedGroup<T>(
        IEnumerable<T> items,
        Func<T, string> name,
        Func<T, string> gate,
        Func<T, string> variantName,
        string level,
        string location)
    {
        var groups = items
            .Where(i => !string.IsNullOrWhiteSpace(name(i)))
            .GroupBy(i => name(i), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var members = group.ToList();
            if (members.Count <= 1) continue;

            var allGated = members.All(m => !string.IsNullOrWhiteSpace(gate(m)));
            if (!allGated)
            {
                yield return new Finding(Severity.Error, DuplicateCode, Category, location,
                    $"Duplicate {level} name '{group.Key}' at {location} — {members.Count} entries share this name and at least one is not gated by ShouldApplyExpression.");
                continue;
            }

            if (members.Any(m => string.IsNullOrWhiteSpace(variantName(m))))
            {
                yield return new Finding(Severity.Warning, MissingVariantNameCode, Category, location,
                    $"Variant set '{group.Key}' ({level}) at {location} has {members.Count} gated entries but not all specify VariantName — label each for clarity.");
            }
        }
    }
}
