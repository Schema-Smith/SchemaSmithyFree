// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
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
    public static void PreserveDataDeliveryAndCustomProperties(Table tableObj, Table original)
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
        CopyComponentDynamicProperties(original.Columns, tableObj.Columns, true);
        CopyComponentDynamicProperties(original.Indexes, tableObj.Indexes);
        CopyComponentDynamicProperties(original.ForeignKeys, tableObj.ForeignKeys);
        CopyComponentDynamicProperties(original.CheckConstraints, tableObj.CheckConstraints);

        // Platform-specific component collections
        if (original is SqlServerTable ssOriginal && tableObj is SqlServerTable ssNew)
        {
            CopyComponentDynamicProperties(ssOriginal.XmlIndexes, ssNew.XmlIndexes);
            CopyComponentDynamicProperties(ssOriginal.Statistics, ssNew.Statistics);
            // FT variants are hand-authored conditional config the extractor cannot see; no Name to match on,
            // so multi-variant declarations survive wholesale, and a gated single variant absent on this target
            // survives too — absence is the gate's doing, not a drop.
            if (ssOriginal.FullTextIndex is { Count: > 1 })
                ssNew.FullTextIndex = ssOriginal.FullTextIndex;
            else if (ssOriginal.FullTextIndex is { Count: 1 } && ssNew.FullTextIndex is { Count: 1 })
                CopyDynamicProperties(ssOriginal.FullTextIndex[0], ssNew.FullTextIndex[0]);
            else if (ssOriginal.FullTextIndex is { Count: 1 } && ssNew.FullTextIndex is { Count: 0 }
                     && !string.IsNullOrWhiteSpace(ssOriginal.FullTextIndex[0].ShouldApplyExpression))
                ssNew.FullTextIndex = ssOriginal.FullTextIndex;
        }

        if (original is PostgreSqlTable pgOriginal2 && tableObj is PostgreSqlTable pgNew2)
        {
            CopyComponentDynamicProperties(pgOriginal2.Statistics, pgNew2.Statistics);
            CopyComponentDynamicProperties(pgOriginal2.ExcludeConstraints, pgNew2.ExcludeConstraints);
        }

        if (original is MySqlTable myOriginal && tableObj is MySqlTable myNew)
        {
            CopyComponentDynamicProperties(myOriginal.FullTextIndexes, myNew.FullTextIndexes);
        }
    }

    private static void CopyComponentDynamicProperties<T>(List<T> originalComponents, List<T> newComponents, bool copyOldName = false) where T : DynamicBase
    {
        if (originalComponents == null || newComponents == null) return;

        // An authored variant set (2+ same-named entries gated by mutually exclusive expressions)
        // survives wholesale: extraction can only ever see the one deployed winner, so the
        // original group is the truth worth keeping.
        var variantGroups = originalComponents
            .GroupBy(c => GetTrimmedName(c), System.StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key != "" && g.Count() > 1)
            .ToList();
        foreach (var group in variantGroups)
        {
            var insertAt = newComponents.FindIndex(x => GetTrimmedName(x).Equals(group.Key, System.StringComparison.OrdinalIgnoreCase));
            newComponents.RemoveAll(x => GetTrimmedName(x).Equals(group.Key, System.StringComparison.OrdinalIgnoreCase));
            newComponents.InsertRange(insertAt < 0 ? newComponents.Count : insertAt, group);
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

    private static void CopyDynamicProperties(DynamicBase original, DynamicBase current, bool copyOldName = false)
    {
        if (current == null) return;
        if (original.Extensions != null)
            current.Extensions = original.Extensions.DeepClone();

        ((dynamic)current).ShouldApplyExpression = ((dynamic)original).ShouldApplyExpression ?? "";
        ((dynamic)current).VariantName = ((dynamic)original).VariantName ?? "";
        if (copyOldName)
            ((dynamic)current).OldName = ((dynamic)original).OldName ?? "";
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
