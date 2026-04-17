// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Isolators;

namespace Schema.Delivery;

/// <summary>
/// IDataDelivery implementation providing 2-pass FK-aware data delivery.
/// Orchestrates topological ordering and delegates SQL fragment generation
/// via IMergeScriptHelper callbacks. Owns assembly logic for deferred merge
/// scripts and CASCADE validation.
/// </summary>
public class DataDeliveryProcessor : IDataDelivery
{
    public static IDataDelivery GetFromFactory()
        => FactoryContainer.ResolveOrCreate<IDataDelivery, DataDeliveryProcessor>();

    public void DeliverTables(DataDeliveryContext context)
    {
        if (context?.Tables == null || context.Tables.Count == 0) return;

        var platform = context.Platform;
        var helper = context.ScriptHelper ?? throw new InvalidOperationException("DataDeliveryContext.ScriptHelper is required");
        var log = context.ProgressLog ?? (_ => { });
        var logError = context.ProgressLogError ?? (_ => { });

        var tablesToDeliver = context.Tables
            .Where(t => t.DataDelivery != null &&
                        !string.IsNullOrEmpty(t.DataDelivery.MergeType) &&
                        !t.DataDelivery.MergeType.Equals("None", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => DataDeliveryHelper.GetTableKey(t, platform))
            .ToList();

        if (tablesToDeliver.Count == 0) return;
        log("  Delivering table data");

        var mergeTypeErrors = ValidateMergeTypes(tablesToDeliver);
        if (mergeTypeErrors.Count > 0)
        {
            foreach (var error in mergeTypeErrors)
                logError($"    {error}");
            throw new InvalidOperationException("Data delivery aborted: Invalid MergeType values detected.");
        }

        if (platform.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            var cascadeErrors = ValidateDeleteCascade(context.Command, context.DatabaseName,
                tablesToDeliver.Where(t => (t.DataDelivery.MergeType ?? "").IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(t => DataDeliveryHelper.TrimIdentifierQuotes(t.Name, platform)).ToList());
            if (cascadeErrors.Count > 0)
            {
                foreach (var error in cascadeErrors)
                    logError($"    {error}");
                throw new InvalidOperationException("Data delivery aborted: Delete merge type with CASCADE delete detected.");
            }
        }

        var deliverySet = DataDeliveryHelper.BuildDeliveryTableSet(tablesToDeliver, platform);

        var tableEdges = new Dictionary<IDeliverableTable, (HashSet<string> RequiredDeps, List<string> DeferredColumns)>();
        var tableDataMap = new Dictionary<IDeliverableTable, string>();

        foreach (var table in tablesToDeliver.ToList())
        {
            tableEdges[table] = DataDeliveryHelper.ClassifyFKEdges(table, deliverySet, platform);

            var contentPath = ResolveContentFilePath(context.TemplateRootPath, table.DataDelivery.ContentFile);
            if (contentPath == null || context.ReadFileContent == null)
            {
                logError($"    SKIPPING {DataDeliveryHelper.GetTableKey(table, platform)}. Unable to locate content file: '{table.DataDelivery.ContentFile}'");
                tablesToDeliver.Remove(table);
                continue;
            }

            try
            {
                var content = context.ReadFileContent(contentPath);
                if (content == null)
                {
                    logError($"    SKIPPING {DataDeliveryHelper.GetTableKey(table, platform)}. Content file not found: '{contentPath}'");
                    tablesToDeliver.Remove(table);
                    continue;
                }
                tableDataMap[table] = content;
            }
            catch (Exception ex)
            {
                logError($"    SKIPPING {DataDeliveryHelper.GetTableKey(table, platform)}. Error reading content file: '{contentPath}' - {ex.Message}");
                tablesToDeliver.Remove(table);
            }
        }

        var delivered = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var pass2Tables = new List<IDeliverableTable>();
        var lastCount = -1;

        while (tablesToDeliver.Count > 0 && tablesToDeliver.Count != lastCount)
        {
            lastCount = tablesToDeliver.Count;
            var remaining = new List<IDeliverableTable>();

            foreach (var table in tablesToDeliver)
            {
                var (requiredDeps, deferredColumns) = tableEdges[table];
                var tableKey = DataDeliveryHelper.GetTableKey(table, platform);

                if (requiredDeps.Any(dep => !delivered.Contains(dep)))
                {
                    remaining.Add(table);
                    continue;
                }

                try
                {
                    DeliverTable(context, table, tableDataMap, deferredColumns, delivered, pass2Tables, false);
                }
                catch
                {
                    remaining.Add(table);
                }
            }

            tablesToDeliver = remaining;
        }

        foreach (var table in tablesToDeliver)
        {
            try
            {
                DeliverTable(context, table, tableDataMap, new List<string>(), delivered, pass2Tables, true);
            }
            catch (Exception ex)
            {
                logError($"    Error delivering {DataDeliveryHelper.GetTableKey(table, context.Platform)}: {ex.Message}");
            }
        }

        foreach (var table in pass2Tables)
        {
            try
            {
                var tableKey = DataDeliveryHelper.GetTableKey(table, platform);
                log($"    Delivering {tableKey} (pass 2 - updating deferred FK columns)");

                var delivery = table.DataDelivery;
                var schemaOrDb = GetSchemaOrDb(table, context.DatabaseName, platform);
                var keyColumns = string.IsNullOrWhiteSpace(delivery.MatchColumns)
                    ? helper.GetKeyColumns(context.Command, schemaOrDb, table.Name)
                    : delivery.MatchColumns;
                var tableData = tableDataMap.TryGetValue(table, out var data) ? data : "";
                var update = (delivery.MergeType ?? "").IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0;
                var delete = (delivery.MergeType ?? "").IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0;

                var mergeScript = helper.BuildMergeScript(context.Command, schemaOrDb, table.Name,
                    tableData, keyColumns, update, delete, delivery.MergeDisableTriggers, false, delivery.MergeFilter,
                    delivery.MergeDisableRules, delivery.MergeUpdateDescendents);

                if (!context.WhatIf)
                    context.ExecuteScript?.Invoke(table.Name, mergeScript);
            }
            catch (Exception ex)
            {
                logError($"    Error in pass 2 for {DataDeliveryHelper.GetTableKey(table, platform)}: {ex.Message}");
            }
        }
    }

    private void DeliverTable(DataDeliveryContext context, IDeliverableTable table,
        Dictionary<IDeliverableTable, string> tableDataMap, List<string> deferredColumns,
        HashSet<string> delivered, List<IDeliverableTable> pass2Tables, bool isCircularFallback)
    {
        var platform = context.Platform;
        var helper = context.ScriptHelper;
        var log = context.ProgressLog ?? (_ => { });
        var delivery = table.DataDelivery;
        var tableKey = DataDeliveryHelper.GetTableKey(table, platform);
        var schemaOrDb = GetSchemaOrDb(table, context.DatabaseName, platform);

        var keyColumns = string.IsNullOrWhiteSpace(delivery.MatchColumns)
            ? helper.GetKeyColumns(context.Command, schemaOrDb, table.Name)
            : delivery.MatchColumns;
        var tableData = tableDataMap.TryGetValue(table, out var data) ? data : "";

        if (deferredColumns.Count > 0 && !isCircularFallback)
        {
            log($"    Delivering {tableKey} (pass 1 - deferred columns as NULL)");

            var mergeScript = BuildDeferredMergeScript(context, schemaOrDb, table, tableData, keyColumns, deferredColumns);

            if (!context.WhatIf)
                context.ExecuteScript?.Invoke(table.Name, mergeScript);

            pass2Tables.Add(table);
        }
        else
        {
            log($"    Delivering {tableKey}");
            var update = (delivery.MergeType ?? "").IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0;
            var delete = (delivery.MergeType ?? "").IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0;
            var mergeScript = helper.BuildMergeScript(context.Command, schemaOrDb, table.Name,
                tableData, keyColumns, update, delete, delivery.MergeDisableTriggers, false, delivery.MergeFilter,
                delivery.MergeDisableRules, delivery.MergeUpdateDescendents);

            if (!context.WhatIf)
                context.ExecuteScript?.Invoke(table.Name, mergeScript);
        }

        delivered.Add(tableKey);
    }

    internal static string BuildDeferredMergeScript(DataDeliveryContext context, string schemaOrDb,
        IDeliverableTable table, string tableData, string keyColumns, List<string> deferredColumns)
    {
        return DeferredMergeBuilder.Build(context.ScriptHelper, context.Command, context.Platform,
            schemaOrDb, table.Name, tableData, keyColumns,
            table.DataDelivery.MergeDisableTriggers, deferredColumns,
            table.DataDelivery.MergeDisableRules, table.DataDelivery.MergeUpdateDescendents);
    }

    /// <summary>
    /// MySQL-only: validates that tables using Delete merge type don't have referencing
    /// FKs with CASCADE delete. Deletes would cascade to child tables, which is almost
    /// never intended during data delivery.
    /// </summary>
    internal static List<string> ValidateDeleteCascade(System.Data.IDbCommand cmd, string databaseName, List<string> deleteTableNames)
    {
        var errors = new List<string>();
        databaseName = databaseName.Trim().Trim('`');

        if (deleteTableNames.Count == 0) return errors;

        foreach (var tableName in deleteTableNames)
        {
            cmd.CommandText = $@"
SELECT rc.CONSTRAINT_NAME, rc.TABLE_NAME, rc.DELETE_RULE
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
WHERE BINARY rc.UNIQUE_CONSTRAINT_SCHEMA = BINARY '{databaseName.Replace("'", "''")}'
  AND BINARY rc.REFERENCED_TABLE_NAME = BINARY '{tableName.Replace("'", "''")}'
  AND rc.DELETE_RULE = 'CASCADE';
";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var fkName = reader.GetString(0);
                var childTable = reader.GetString(1);
                errors.Add($"Table `{tableName}` uses MergeType=Insert/Update/Delete but is referenced by FK `{fkName}` on `{childTable}` with ON DELETE CASCADE. " +
                           $"Deletes would cascade to `{childTable}`. Change MergeType to Insert/Update or remove the CASCADE rule.");
            }
        }

        return errors;
    }

    private static string GetSchemaOrDb(IDeliverableTable table, string databaseName, string platform)
    {
        if (platform.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
            return databaseName;
        return table.Schema ?? DataDeliveryHelper.GetDefaultSchema(platform);
    }

    internal static string ResolveContentFilePath(string templateRootPath, string contentFile)
    {
        if (string.IsNullOrEmpty(contentFile) || string.IsNullOrEmpty(templateRootPath))
            return null;

        return Path.Combine(templateRootPath, contentFile.Replace('\\', '/'));
    }

    private static readonly string[] ValidMergeTypes =
        { "Insert", "Insert/Update", "Insert/Update/Delete" };

    private static List<string> ValidateMergeTypes(IList<IDeliverableTable> tables)
    {
        var errors = new List<string>();
        foreach (var table in tables)
        {
            var mergeType = table.DataDelivery?.MergeType;
            if (string.IsNullOrEmpty(mergeType)) continue;
            if (ValidMergeTypes.Any(v => v.Equals(mergeType, StringComparison.OrdinalIgnoreCase))) continue;
            errors.Add($"Table {table.Name} has invalid MergeType '{mergeType}'. " +
                       $"Valid values: {string.Join(", ", ValidMergeTypes)}");
        }
        return errors;
    }
}
