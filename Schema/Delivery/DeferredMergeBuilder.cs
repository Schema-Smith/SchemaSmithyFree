// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Schema.Delivery;

/// <summary>
/// Deferred merge script assembly. Builds platform-specific merge scripts
/// that NULL specified FK columns for 2-pass data delivery. Uses IMergeScriptHelper
/// fragments for standard column info and GetColumnMetadata for type details needed
/// to generate proper NULL casts.
/// </summary>
internal static class DeferredMergeBuilder
{
    public static string Build(IMergeScriptHelper helper, IDbCommand cmd, string platform,
        string schemaOrDb, string tableName, string tableData, string keyColumns,
        bool disableTriggers, List<string> deferredColumns,
        bool disableRules = false, bool updateDescendents = false, int pgServerVersionNum = 0)
    {
        if (platform.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            return BuildSqlServer(helper, cmd, schemaOrDb, tableName, tableData, keyColumns, disableTriggers, deferredColumns);
        if (platform.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            return BuildPostgreSql(helper, cmd, schemaOrDb, tableName, tableData, keyColumns, disableTriggers, deferredColumns, disableRules, updateDescendents, pgServerVersionNum);
        if (platform.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
            return BuildMySql(helper, cmd, schemaOrDb, tableName, tableData, keyColumns, deferredColumns);

        throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform));
    }

    #region SQL Server

    private static string BuildSqlServer(IMergeScriptHelper helper, IDbCommand cmd,
        string schemaOrDb, string tableName, string tableData, string keyColumns,
        bool disableTriggers, List<string> deferredColumns)
    {
        var schema = schemaOrDb.Trim().Trim('[', ']');
        var table = tableName.Trim().Trim('[', ']');
        var deferredSet = new HashSet<string>(deferredColumns.Select(c => c.Trim().Trim('[', ']')), StringComparer.InvariantCultureIgnoreCase);

        var matchColumns = helper.GetMatchColumns(keyColumns);
        var jsonColumns = helper.GetJsonColumnDefinitions(cmd, schemaOrDb, tableName);
        var insertColumns = helper.GetInsertColumns(cmd, schemaOrDb, tableName);
        var identityInsert = helper.NeedsIdentityInsert(cmd, schemaOrDb, tableName);

        var columns = helper.GetColumnMetadata(cmd, schemaOrDb, tableName);
        var selectColumns = BuildDeferredSelectColumnsSqlServer(columns, deferredSet);

        var sb = new StringBuilder();
        sb.AppendLine($"DECLARE @v_json NVARCHAR(MAX) = '{tableData?.Replace("'", "''")}';");
        sb.AppendLine();
        if (disableTriggers) sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DISABLE TRIGGER ALL;");
        if (identityInsert) sb.AppendLine($"SET IDENTITY_INSERT [{schema}].[{table}] ON;");
        sb.AppendLine($"MERGE INTO [{schema}].[{table}] AS Target");
        sb.AppendLine("USING (");
        sb.AppendLine($"  SELECT {selectColumns}");
        sb.AppendLine("    FROM OPENJSON(@v_json)");
        sb.AppendLine("    WITH (");
        sb.AppendLine($"{jsonColumns}");
        sb.AppendLine("    )");
        sb.AppendLine(") AS Source");
        sb.AppendLine($"ON {matchColumns}");
        sb.AppendLine();
        sb.AppendLine(" WHEN NOT MATCHED BY TARGET THEN");
        sb.AppendLine("   INSERT (");
        sb.AppendLine($" {insertColumns}");
        sb.AppendLine("   ) VALUES (");
        sb.AppendLine($" {insertColumns?.Replace("[", "Source.[")}");
        sb.AppendLine("   )");
        sb.AppendLine(";");
        if (identityInsert) sb.AppendLine($"SET IDENTITY_INSERT [{schema}].[{table}] OFF;");
        if (disableTriggers) sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ENABLE TRIGGER ALL;");

        return sb.ToString();
    }

    private static string BuildDeferredSelectColumnsSqlServer(List<MergeColumnInfo> columns, HashSet<string> deferredSet)
    {
        if (columns == null || columns.Count == 0) return "*";

        return string.Join(",", columns.Select(c =>
        {
            if (deferredSet.Contains(c.Name))
                return $"CAST(NULL AS {c.JsonParseType}) AS [{c.Name}]";
            if (c.IsGeometry)
                return $"{c.DataType.ToLowerInvariant()}::STGeomFromText([{c.Name}], [{c.Name}.STSrid]) AS [{c.Name}]";
            return $"[{c.Name}]";
        }));
    }

    #endregion

    #region PostgreSQL

    private static string BuildPostgreSql(IMergeScriptHelper helper, IDbCommand cmd,
        string schemaOrDb, string tableName, string tableData, string keyColumns,
        bool disableTriggers, List<string> deferredColumns,
        bool disableRules, bool updateDescendents, int pgServerVersionNum = 0)
    {
        var schema = schemaOrDb.Trim().Trim('"');
        var table = tableName.Trim().Trim('"');
        var deferredSet = new HashSet<string>(deferredColumns.Select(c => c.Trim().Trim('"')), StringComparer.InvariantCultureIgnoreCase);

        var matchColumns = helper.GetMatchColumns(keyColumns);
        var insertColumns = helper.GetInsertColumns(cmd, schemaOrDb, tableName);
        var identAndSeq = helper.GetIdentitySequence(cmd, schemaOrDb, tableName);

        var columns = helper.GetColumnMetadata(cmd, schemaOrDb, tableName);
        var jsonSelectColumns = BuildDeferredJsonColumnsPostgreSql(columns, deferredSet);

        var (disableRuleStmts, enableRuleStmts) = disableRules
            ? helper.GetRuleStatements(cmd, schemaOrDb, tableName, updateDescendents)
            : ((string)null, (string)null);

        var only = updateDescendents ? "" : "ONLY ";
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(disableRuleStmts)) sb.AppendLine(disableRuleStmts);
        sb.AppendLine("DO $$");
        sb.AppendLine("DECLARE");
        sb.AppendLine($"  v_json JSON = '{tableData?.Replace("'", "''")}';");
        sb.AppendLine("  nextval BIGINT;");
        sb.AppendLine("BEGIN");
        if (disableTriggers) sb.AppendLine($"ALTER TABLE \"{schema}\".\"{table}\" {only}DISABLE TRIGGER ALL;");
        sb.AppendLine($"MERGE INTO {only}\"{schema}\".\"{table}\" AS \"Target\"");
        sb.AppendLine("USING (");
        sb.AppendLine("    WITH my_tables(arr) AS (VALUES(v_json::JSON))");
        sb.AppendLine($"    SELECT {jsonSelectColumns}");
        sb.AppendLine("      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem");
        sb.AppendLine(") AS \"Source\"");
        sb.AppendLine($"ON {matchColumns}");
        sb.AppendLine();
        sb.AppendLine(" WHEN NOT MATCHED THEN");
        sb.AppendLine("   INSERT (");
        sb.AppendLine($" {insertColumns}");

        if (!string.IsNullOrEmpty(identAndSeq))
        {
            var parts = identAndSeq.Split('=');
            if (parts.Length >= 3)
                sb.AppendLine($"   ) OVERRIDING {parts[2]} VALUE");
            else
                sb.AppendLine("   )");
        }
        else
        {
            sb.AppendLine("   )");
        }

        sb.AppendLine("  VALUES (");
        if (insertColumns != null)
        {
            sb.AppendLine(string.Join(",\n", insertColumns.Split(',')
                .Select(c => $"        \"Source\".{c.Trim()}")));
        }
        sb.AppendLine("   )");
        sb.AppendLine(" ;");
        if (disableTriggers) sb.AppendLine($"ALTER TABLE \"{schema}\".\"{table}\" {only}ENABLE TRIGGER ALL;");

        if (!string.IsNullOrEmpty(identAndSeq))
        {
            var parts = identAndSeq.Split('=');
            if (parts.Length >= 2)
                sb.AppendLine($"SELECT SETVAL('{parts[1]}', (SELECT MAX(\"{parts[0]}\") FROM \"{schema}\".\"{table}\")) INTO nextval;");
        }

        sb.AppendLine();
        sb.AppendLine("END $$ LANGUAGE plpgsql;");
        if (!string.IsNullOrEmpty(enableRuleStmts)) sb.AppendLine(enableRuleStmts);

        return sb.ToString();
    }

    private static string BuildDeferredJsonColumnsPostgreSql(List<MergeColumnInfo> columns, HashSet<string> deferredSet)
    {
        if (columns == null || columns.Count == 0) return "*";

        return string.Join(",\n           ", columns.Select(c =>
        {
            if (deferredSet.Contains(c.Name))
                return $"CAST(NULL AS {c.JsonParseType}) AS \"{c.Name}\"";
            if (c.IsGeometry)
                return $"ST_GeomFromText(elem ->> '{c.Name}') AS \"{c.Name}\"";
            if (c.IsBinary)
                return $"decode(elem ->> '{c.Name}', 'base64') AS \"{c.Name}\"";
            return $"(elem ->> '{c.Name}')::{c.JsonParseType} AS \"{c.Name}\"";
        }));
    }

    #endregion

    #region MySQL

    private static string BuildMySql(IMergeScriptHelper helper, IDbCommand cmd,
        string schemaOrDb, string tableName, string tableData, string keyColumns,
        List<string> deferredColumns)
    {
        var db = schemaOrDb.Trim().Trim('`');
        var table = tableName.Trim().Trim('`');
        var deferredSet = new HashSet<string>(deferredColumns, StringComparer.OrdinalIgnoreCase);

        var matchColumns = helper.GetMatchColumns(keyColumns);
        var columns = helper.GetColumnMetadata(cmd, schemaOrDb, tableName);
        if (columns.Count == 0)
            throw new InvalidOperationException($"No columns found for table `{db}`.`{table}`.");

        var columnList = string.Join(", ", columns.Select(c => $"`{c.Name}`"));

        var jsonTableColumns = string.Join(",\n    ", columns.Where(c => !c.IsComputed).Select(c =>
            $"`{c.Name}` {c.JsonParseType} PATH '$.{c.Name}'"));

        var selectExpressions = string.Join(", ", columns.Select(c =>
        {
            if (deferredSet.Contains(c.Name)) return "NULL";
            if (c.IsGeometry) return $"ST_GeomFromText(`jt`.`{c.Name}`)";
            if (c.IsBinary) return $"FROM_BASE64(`jt`.`{c.Name}`)";
            return $"`jt`.`{c.Name}`";
        }));

        var sb = new StringBuilder();
        sb.AppendLine($"SET @json_data = '{tableData?.Replace("'", "''")}';");
        sb.AppendLine();
        sb.AppendLine($"INSERT INTO `{db}`.`{table}` ({columnList})");
        sb.AppendLine($"SELECT {selectExpressions}");
        sb.AppendLine("FROM JSON_TABLE(");
        sb.AppendLine("  @json_data,");
        sb.AppendLine("  '$[*]' COLUMNS (");
        sb.AppendLine($"    {jsonTableColumns}");
        sb.AppendLine("  )");
        sb.AppendLine(") AS jt");

        var updateCols = columns.Where(c => !c.IsIdentity && !c.IsComputed).Select(c =>
            deferredSet.Contains(c.Name) ? $"`{c.Name}` = NULL" : $"`{c.Name}` = VALUES(`{c.Name}`)");
        sb.AppendLine($"ON DUPLICATE KEY UPDATE {string.Join(", ", updateCols)};");

        return sb.ToString();
    }

    #endregion
}
