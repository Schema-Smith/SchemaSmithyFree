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

        var cascadeErrors = ValidateDeleteCascade(context.Command, platform, context.DatabaseName,
            tablesToDeliver.Where(t => (t.DataDelivery.MergeType ?? "").IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(t => (
                    Schema: GetSchemaOrDb(t, context.DatabaseName, platform, context.SchemaName),
                    TableName: DataDeliveryHelper.TrimIdentifierQuotes(t.Name, platform)
                )).ToList());
        if (cascadeErrors.Count > 0)
        {
            foreach (var error in cascadeErrors)
                logError($"    {error}");
            throw new InvalidOperationException("Data delivery aborted: Delete merge type with CASCADE delete detected.");
        }

        var deliverySet = DataDeliveryHelper.BuildDeliveryTableSet(tablesToDeliver, platform);

        var tableEdges = new Dictionary<IDeliverableTable, (HashSet<string> RequiredDeps, List<string> DeferredColumns)>();
        var tableDataMap = new Dictionary<IDeliverableTable, string>();
        var contentFileErrors = new List<string>();

        foreach (var table in tablesToDeliver.ToList())
        {
            tableEdges[table] = DataDeliveryHelper.ClassifyFKEdges(table, deliverySet, platform);
            var tableKey = DataDeliveryHelper.GetTableKey(table, platform);

            var contentPath = ResolveContentFilePath(context.TemplateRootPath, table.DataDelivery.ContentFile);
            if (contentPath == null)
            {
                contentFileErrors.Add($"{tableKey}: Unable to locate content file: '{table.DataDelivery.ContentFile}'");
                continue;
            }

            if (context.ReadFileContent == null)
            {
                contentFileErrors.Add($"{tableKey}: No content file reader configured for '{contentPath}'");
                continue;
            }

            try
            {
                var content = context.ReadFileContent(contentPath);
                if (content == null)
                {
                    contentFileErrors.Add($"{tableKey}: Content file not found: '{contentPath}'");
                    continue;
                }
                tableDataMap[table] = content;
            }
            catch (Exception ex)
            {
                contentFileErrors.Add($"{tableKey}: Error reading content file: '{contentPath}' - {ex.Message}");
            }
        }

        if (contentFileErrors.Count > 0)
        {
            foreach (var error in contentFileErrors)
                logError($"    {error}");
            throw new InvalidOperationException("Data delivery aborted: Unable to read one or more content files.");
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
                var schemaOrDb = GetSchemaOrDb(table, context.DatabaseName, platform, context.SchemaName);
                var keyColumns = string.IsNullOrWhiteSpace(delivery.MatchColumns)
                    ? helper.GetKeyColumns(context.Command, schemaOrDb, table.Name)
                    : delivery.MatchColumns;
                var tableData = tableDataMap.TryGetValue(table, out var data) ? data : "";
                var update = (delivery.MergeType ?? "").IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0;
                var delete = (delivery.MergeType ?? "").IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0;
                var mergeFilter = ResolveMergeFilter(delivery.MergeFilter, context.SchemaName);

                var mergeScript = helper.BuildMergeScript(context.Command, schemaOrDb, table.Name,
                    tableData, keyColumns, update, delete, delivery.MergeDisableTriggers, false, mergeFilter,
                    delivery.MergeDisableRules, delivery.MergeUpdateDescendents);

                if (!context.WhatIf)
                {
                    try
                    {
                        context.ExecuteScript?.Invoke(table.Name, mergeScript);
                    }
                    catch (Exception ex)
                    {
                        context.WriteResolvedSqlArtifact?.Invoke(tableKey, mergeScript);
                        logError($"    Error in pass 2 for {tableKey}: {ex.Message}");
                    }
                }
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
        var schemaOrDb = GetSchemaOrDb(table, context.DatabaseName, platform, context.SchemaName);

        if (context.WhatIf)
        {
            log($"    Would DELIVER: {tableKey}");
            delivered.Add(tableKey);
            return;
        }

        var keyColumns = string.IsNullOrWhiteSpace(delivery.MatchColumns)
            ? helper.GetKeyColumns(context.Command, schemaOrDb, table.Name)
            : delivery.MatchColumns;
        var tableData = tableDataMap.TryGetValue(table, out var data) ? data : "";

        if (deferredColumns.Count > 0 && !isCircularFallback)
        {
            log($"    Delivering {tableKey} (pass 1 - deferred columns as NULL)");

            var mergeScript = BuildDeferredMergeScript(context, schemaOrDb, table, tableData, keyColumns, deferredColumns);

            if (!context.WhatIf)
            {
                try
                {
                    context.ExecuteScript?.Invoke(table.Name, mergeScript);
                }
                catch
                {
                    context.WriteResolvedSqlArtifact?.Invoke(tableKey, mergeScript);
                    throw;
                }
            }

            pass2Tables.Add(table);
        }
        else
        {
            log($"    Delivering {tableKey}");
            var update = (delivery.MergeType ?? "").IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0;
            var delete = (delivery.MergeType ?? "").IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0;
            var mergeFilter = ResolveMergeFilter(delivery.MergeFilter, context.SchemaName);
            var mergeScript = helper.BuildMergeScript(context.Command, schemaOrDb, table.Name,
                tableData, keyColumns, update, delete, delivery.MergeDisableTriggers, false, mergeFilter,
                delivery.MergeDisableRules, delivery.MergeUpdateDescendents);

            if (!context.WhatIf)
            {
                try
                {
                    context.ExecuteScript?.Invoke(table.Name, mergeScript);
                }
                catch
                {
                    context.WriteResolvedSqlArtifact?.Invoke(tableKey, mergeScript);
                    throw;
                }
            }
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
    /// Validates that tables using Delete merge type don't have referencing FKs with
    /// CASCADE delete. Deletes would cascade to child tables, which is almost never
    /// intended during data delivery. Cross-platform (MySQL, PostgreSQL, SQL Server).
    /// </summary>
    internal static List<string> ValidateDeleteCascade(System.Data.IDbCommand cmd, string platform, string databaseName,
        IList<(string Schema, string TableName)> deleteTables)
    {
        var errors = new List<string>();
        if (deleteTables == null || deleteTables.Count == 0) return errors;

        var isMySql = platform.Equals("MySQL", StringComparison.OrdinalIgnoreCase);
        var dbName = (databaseName ?? string.Empty).Trim().Trim('`');

        foreach (var (schema, tableName) in deleteTables)
        {
            var schemaKey = (schema ?? string.Empty).Trim().Trim('`', '"', '[', ']');
            var tableKey = (tableName ?? string.Empty).Trim().Trim('`', '"', '[', ']');

            cmd.CommandText = isMySql
                ? BuildMySqlCascadeQuery(dbName, tableKey)
                : BuildStandardCascadeQuery(schemaKey, tableKey);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var fkName = reader.GetString(0);
                var childTable = reader.GetString(1);
                errors.Add(FormatCascadeError(platform, schemaKey, tableKey, fkName, childTable));
            }
        }

        return errors;
    }

    private static string BuildMySqlCascadeQuery(string databaseName, string tableName) => $@"
SELECT rc.CONSTRAINT_NAME, rc.TABLE_NAME, rc.DELETE_RULE
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
WHERE BINARY rc.UNIQUE_CONSTRAINT_SCHEMA = BINARY '{databaseName.Replace("'", "''")}'
  AND BINARY rc.REFERENCED_TABLE_NAME = BINARY '{tableName.Replace("'", "''")}'
  AND rc.DELETE_RULE = 'CASCADE';
";

    private static string BuildStandardCascadeQuery(string schema, string tableName) => $@"
SELECT rc.CONSTRAINT_NAME, tc_c.TABLE_NAME, rc.DELETE_RULE
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc_p
    ON tc_p.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME
   AND tc_p.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA
INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc_c
    ON tc_c.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
   AND tc_c.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA
WHERE tc_p.TABLE_SCHEMA = '{schema.Replace("'", "''")}'
  AND tc_p.TABLE_NAME = '{tableName.Replace("'", "''")}'
  AND rc.DELETE_RULE = 'CASCADE';
";

    private static string FormatCascadeError(string platform, string schema, string tableName, string fkName, string childTable)
    {
        var isSqlServer = platform.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
        var isPg = platform.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);
        var (open, close) = isSqlServer ? ("[", "]") : isPg ? ("\"", "\"") : ("`", "`");
        string Q(string s) => $"{open}{s}{close}";

        var parent = isSqlServer || isPg ? $"{Q(schema)}.{Q(tableName)}" : Q(tableName);
        var child = isSqlServer || isPg ? $"{Q(schema)}.{Q(childTable)}" : Q(childTable);

        return $"Table {parent} uses MergeType=Insert/Update/Delete but is referenced by FK {Q(fkName)} on {child} with ON DELETE CASCADE. " +
               $"Deletes would cascade to {child}. Change MergeType to Insert/Update or remove the CASCADE rule.";
    }

    /// <summary>
    /// Resolves the schema (or database, on MySQL) qualifier for a table at delivery time.
    /// For schema-template iterations (<paramref name="iterationSchemaName"/> non-empty),
    /// substitutes the <c>{{SchemaName}}</c> token in <c>table.Schema</c> with the resolved
    /// iteration value before returning. Without this substitution the literal token would
    /// reach the catalog probes in <see cref="Schema.Utility.MergeScriptHelper"/> (returning
    /// empty result sets) and the emitted <c>MERGE INTO [{{SchemaName}}].[Table]</c> clause —
    /// slice-3 audit bug B3.
    /// Regular templates pass an empty <paramref name="iterationSchemaName"/> and the method
    /// behaves identically to its pre-bug-fix form.
    /// </summary>
    internal static string GetSchemaOrDb(IDeliverableTable table, string databaseName, string platform,
        string iterationSchemaName)
    {
        if (platform.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
            return databaseName;
        var raw = table.Schema ?? DataDeliveryHelper.GetDefaultSchema(platform);
        return string.IsNullOrEmpty(iterationSchemaName)
            ? raw
            : raw.Replace("{{SchemaName}}", iterationSchemaName);
    }

    internal static string ResolveContentFilePath(string templateRootPath, string contentFile)
    {
        if (string.IsNullOrEmpty(contentFile) || string.IsNullOrEmpty(templateRootPath))
            return null;

        // Normalize both separator styles to the platform separator so output is native on either OS.
        return Path.Combine(templateRootPath,
            contentFile.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Substitutes the <c>{{SchemaName}}</c> token in a user-authored
    /// <see cref="DataDelivery.MergeFilter"/> with the iteration's resolved schema name.
    /// Mirrors the <see cref="GetSchemaOrDb"/> pattern (slice-3 audit B3) for the parallel
    /// case: MergeFilter is verbatim text embedded into the executed merge body and never
    /// passes through engine-level token resolution between the helper and the database.
    /// Without this substitution, schema-template authors who reference <c>{{SchemaName}}</c>
    /// in MergeFilter would see the literal token reach the server and produce a SQL parse
    /// error. Regular templates pass an empty <paramref name="iterationSchemaName"/> and
    /// MergeFilter is returned unchanged.
    /// </summary>
    internal static string ResolveMergeFilter(string mergeFilter, string iterationSchemaName)
    {
        if (string.IsNullOrEmpty(mergeFilter) || string.IsNullOrEmpty(iterationSchemaName))
            return mergeFilter;
        return mergeFilter.Replace("{{SchemaName}}", iterationSchemaName);
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
