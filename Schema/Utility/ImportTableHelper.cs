// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;

namespace Schema.Utility;

/// <summary>
/// Helper class for preserving data delivery settings and custom (dynamic) properties
/// when re-importing a table from the database.
/// </summary>
public static class ImportTableHelper
{
    /// <summary>
    /// Copies data delivery configuration and custom dynamic properties from an original table
    /// to a newly imported table, preserving user customizations across re-imports.
    /// </summary>
    /// <param name="isVariantActive">
    /// Evaluates a variant's <c>ShouldApplyExpression</c> against the extraction source (true = active
    /// on this database). Drives gate-aware attribution of an extracted component to its variant set.
    /// May throw — the caller lets it propagate so a broken gate fails extraction (fail-closed).
    /// </param>
    public static void PreserveDataDeliveryAndCustomProperties(Table tableObj, Table original, Func<string, bool> isVariantActive)
    {
        // Data delivery is authored config; extraction cannot reconstruct gated/variant deliveries, so
        // the original list is the truth. Multi-entry variant sets survive wholesale; a gated single
        // delivery survives even when absent on this target (absence is the gate's doing, not a drop);
        // an ungated single delivery carries its original config onto the reimport.
        var origDeliveries = original.DataDelivery ?? [];
        if (origDeliveries.Count > 1)
            tableObj.DataDelivery = origDeliveries;
        else if (origDeliveries.Count == 1 && (tableObj.DataDelivery?.Count ?? 0) == 0)
            tableObj.DataDelivery = origDeliveries;
        else if (origDeliveries.Count == 1 && tableObj.DataDelivery is { Count: 1 })
            // Both present: the original replaces the extracted entry outright (no field merge) —
            // DataDelivery is authored config, not something extraction can reconstruct, so the original is the truth.
            tableObj.DataDelivery[0] = origDeliveries[0];

        // Ensure MergeType default for backward compat on the surviving single delivery.
        if (tableObj.DataDelivery is { Count: 1 } && string.IsNullOrWhiteSpace(tableObj.DataDelivery[0].MergeType))
            tableObj.DataDelivery[0].MergeType = "None";

        tableObj.OldName = original.OldName;

        // Copy dynamic (custom) properties at table level
        CopyDynamicProperties(original, tableObj, true);

        // Copy component-level dynamic properties
        CopyComponentDynamicProperties(original.Columns, tableObj.Columns, isVariantActive, true);
        CopyComponentDynamicProperties(original.Indexes, tableObj.Indexes, isVariantActive);
        CopyComponentDynamicProperties(original.ForeignKeys, tableObj.ForeignKeys, isVariantActive);
        CopyComponentDynamicProperties(original.CheckConstraints, tableObj.CheckConstraints, isVariantActive);

        // Platform-specific component collections
        if (original is SqlServerTable ssOriginal && tableObj is SqlServerTable ssNew)
        {
            CopyComponentDynamicProperties(ssOriginal.XmlIndexes, ssNew.XmlIndexes, isVariantActive);
            CopyComponentDynamicProperties(ssOriginal.Statistics, ssNew.Statistics, isVariantActive);
            // FT has no Name to match on, so the whole list is one variant group. A variant set gets
            // gate-aware attribution (the extractor DOES reconstruct the FT index); a single authored
            // FT carries its dynamic props onto the extracted one, and a gated single absent on this
            // target survives — absence is the gate's doing, not a drop.
            if (ssOriginal.FullTextIndex is { Count: > 1 })
            {
                var extractedFt = ssNew.FullTextIndex is { Count: > 0 } ? ssNew.FullTextIndex[0] : null;
                ssNew.FullTextIndex = AttributeVariantGroup(ssOriginal.FullTextIndex, extractedFt, isVariantActive, false);
            }
            else if (ssOriginal.FullTextIndex is { Count: 1 } && ssNew.FullTextIndex is { Count: 1 })
                CopyDynamicProperties(ssOriginal.FullTextIndex[0], ssNew.FullTextIndex[0]);
            else if (ssOriginal.FullTextIndex is { Count: 1 } && ssNew.FullTextIndex is { Count: 0 }
                     && !string.IsNullOrWhiteSpace(ssOriginal.FullTextIndex[0].ShouldApplyExpression))
                ssNew.FullTextIndex = ssOriginal.FullTextIndex;
        }

        if (original is PostgreSqlTable pgOriginal2 && tableObj is PostgreSqlTable pgNew2)
        {
            CopyComponentDynamicProperties(pgOriginal2.Statistics, pgNew2.Statistics, isVariantActive);
            CopyComponentDynamicProperties(pgOriginal2.ExcludeConstraints, pgNew2.ExcludeConstraints, isVariantActive);
        }

        if (original is MySqlTable myOriginal && tableObj is MySqlTable myNew)
        {
            CopyComponentDynamicProperties(myOriginal.FullTextIndexes, myNew.FullTextIndexes, isVariantActive);
        }
    }

    private static void CopyComponentDynamicProperties<T>(List<T> originalComponents, List<T> newComponents, Func<string, bool> isVariantActive, bool copyOldName = false) where T : DynamicBase
    {
        if (originalComponents == null || newComponents == null) return;

        // Gate-aware attribution of an authored variant set (2+ same-named entries). Extraction can
        // see only the one deployed shape, so we evaluate the variants' gates against the source and
        // fold that shape into the ACTIVE variant (refreshing it) instead of discarding it. When no
        // single variant is active, the extracted shape is kept ungated so DuplicationCheck surfaces
        // the drift. Variants absent from extraction (nothing to attribute) survive as authored.
        var variantGroups = originalComponents
            .GroupBy(c => GetTrimmedName(c), System.StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key != "" && g.Count() > 1)
            .ToList();
        foreach (var group in variantGroups)
        {
            var members = group.ToList();
            var extracted = newComponents.FirstOrDefault(x => GetTrimmedName(x).Equals(group.Key, System.StringComparison.OrdinalIgnoreCase));
            var insertAt = newComponents.FindIndex(x => GetTrimmedName(x).Equals(group.Key, System.StringComparison.OrdinalIgnoreCase));
            newComponents.RemoveAll(x => GetTrimmedName(x).Equals(group.Key, System.StringComparison.OrdinalIgnoreCase));

            var resultGroup = AttributeVariantGroup(members, extracted, isVariantActive, copyOldName);
            newComponents.InsertRange(insertAt < 0 ? newComponents.Count : insertAt, resultGroup);
        }
        var variantNames = new HashSet<string>(variantGroups.Select(g => g.Key), System.StringComparer.OrdinalIgnoreCase);

        foreach (var originalComponent in originalComponents)
        {
            var originalName = GetTrimmedName(originalComponent);
            if (variantNames.Contains(originalName)) continue;

            var match = newComponents.FirstOrDefault(x => GetTrimmedName(x).Equals(originalName, System.StringComparison.OrdinalIgnoreCase));

            if (match == null && copyOldName)
            {
                var originalOldName = GetTrimmedOldName(originalComponent);
                if (!string.IsNullOrEmpty(originalOldName))
                    match = newComponents.FirstOrDefault(x => GetTrimmedName(x).Equals(originalOldName, System.StringComparison.OrdinalIgnoreCase));
            }

            // A gated component absent from this extraction is the gate's doing, not a drop —
            // the authored declaration is still truth. Ungated absentees are real deletions.
            if (match == null && !string.IsNullOrWhiteSpace(GetShouldApplyExpression(originalComponent)))
            {
                newComponents.Add(originalComponent);
                continue;
            }

            CopyDynamicProperties(originalComponent, match, copyOldName);
        }
    }

    /// <summary>
    /// Copies every property marked <c>[SchemaProperty(AuthoredOnly = true)]</c> from the file being
    /// overwritten onto the freshly extracted object. These are deploy-behaviour switches the catalog
    /// cannot report, so extraction leaves them at their defaults and a re-extract would otherwise revert
    /// whatever was authored -- silently, since the resulting package is still perfectly valid.
    /// </summary>
    internal static void CopyAuthoredOnlyProperties(object original, object current)
    {
        if (original == null || current == null) return;
        // The extracted object is the more-derived-or-equal type in practice, but take the intersection
        // rather than assume it: a mismatch should copy nothing, not throw mid-extract.
        foreach (var prop in current.GetType().GetProperties())
        {
            if (prop.GetCustomAttribute<SchemaPropertyAttribute>() is not { AuthoredOnly: true }) continue;
            if (!prop.CanWrite) continue;
            var source = original.GetType().GetProperty(prop.Name);
            if (source == null || !source.CanRead || source.PropertyType != prop.PropertyType) continue;
            prop.SetValue(current, source.GetValue(original));
        }
    }

    private static void CopyDynamicProperties(DynamicBase original, DynamicBase current, bool copyOldName = false)
    {
        if (current == null) return;
        if (original.Extensions != null)
            current.Extensions = original.Extensions.DeepClone();

        // Authored-only properties are copied by reflection, not enumerated. Enumerating is what let
        // UpdateFillFactor, SampleSize and the whole Drop*RemovedFromProduct family go missing: each was a
        // line somebody had to remember to add, and nothing failed when they did not. Marking the property
        // is now the whole job -- AuthoredOnlyPropertyTests fails the build for an unmarked, unextractable
        // one, so the next property cannot repeat it.
        CopyAuthoredOnlyProperties(original, current);

        ((dynamic)current).ShouldApplyExpression = ((dynamic)original).ShouldApplyExpression ?? "";
        ((dynamic)current).VariantName = ((dynamic)original).VariantName ?? "";
        if (copyOldName)
            ((dynamic)current).OldName = ((dynamic)original).OldName ?? "";
    }

    /// <summary>
    /// Reconciles an <paramref name="extracted"/> shape against an authored variant group. Nothing
    /// extracted → the authored group survives (gated-off variants aren't drops). Exactly one variant
    /// active on the source → the extracted shape refreshes it (carrying that variant's gate/label/
    /// custom). Zero or many active → the extracted shape is appended ungated so SS-DUP-001 surfaces
    /// the unreconciled drift. Shared by named component groups and the (nameless) SQL Server FT list.
    /// </summary>
    private static List<T> AttributeVariantGroup<T>(IReadOnlyList<T> members, T extracted, Func<string, bool> isVariantActive, bool copyOldName) where T : DynamicBase
    {
        if (extracted == null) return new List<T>(members);

        var decision = VariantAttribution.Decide(members, GetShouldApplyExpression, isVariantActive);
        if (decision.Action == VariantAction.RefreshActive)
        {
            CopyDynamicProperties(members[decision.ActiveIndex], extracted, copyOldName);
            return new List<T>(members) { [decision.ActiveIndex] = extracted };
        }
        return new List<T>(members) { extracted };
    }

    private static string GetTrimmedName(DynamicBase obj)
    {
        var name = ((dynamic)obj).Name as string;
        return TrimAllQuotes(name ?? "");
    }

    private static string GetTrimmedOldName(DynamicBase obj)
    {
        try
        {
            var oldName = ((dynamic)obj).OldName as string;
            return TrimAllQuotes(oldName ?? "");
        }
        catch
        {
            return "";
        }
    }

    private static string GetShouldApplyExpression(DynamicBase obj)
    {
        return ((dynamic)obj).ShouldApplyExpression as string ?? "";
    }

    private static string TrimAllQuotes(string value)
    {
        return value.Trim().Trim('[', ']', '"', '`');
    }
}
