// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Delivery;

namespace Schema.Utility;

/// <summary>
/// Generates platform-aware data delivery merge scripts.
/// SQL Server and PostgreSQL use MERGE syntax; MySQL uses INSERT/REPLACE/ON DUPLICATE KEY UPDATE.
/// </summary>
public static class MergeScriptHelper
{
    // SQL Server types that cannot be represented in JSON or compared reliably
    internal const string SqlServerUnsupportedTypeFilter = "AND c.DATA_TYPE NOT IN ('sql_variant', 'rowversion', 'timestamp')";

    // PostgreSQL types that cannot be represented in JSON or compared reliably.
    // Note: 'point' and 'polygon' are NOT excluded — they overlap with PostGIS types.
    internal const string PostgreSqlUnsupportedTypeFilter =
        "AND c.udt_name NOT IN ('tsvector', 'tsquery', 'money', 'box', 'circle', 'line', 'lseg', 'path')" +
        "\n    AND NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace" +
        "\n                    WHERE t.typname = c.udt_name AND n.nspname = c.udt_schema AND t.typtype = 'c')";

    /// <summary>
    /// Extracts the union of all property names from a JSON array string.
    /// Returns null if the data is empty/null (caller should include all columns).
    /// </summary>
    internal static HashSet<string> GetJsonDataKeys(string tableData)
    {
        if (string.IsNullOrWhiteSpace(tableData)) return null;
        try
        {
            var arr = JArray.Parse(tableData);
            if (arr.Count == 0) return null;
            return arr
                .SelectMany(t => ((JObject)t).Properties().Select(p => p.Name))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        }
        catch
        {
            return null; // If parsing fails, include all columns
        }
    }

    /// <summary>
    /// Builds a SQL IN clause fragment for filtering columns by JSON data keys.
    /// Returns empty string if jsonKeys is null (include all columns).
    /// </summary>
    internal static string BuildJsonKeyFilter(HashSet<string> jsonKeys, string columnExpression)
    {
        if (jsonKeys == null || jsonKeys.Count == 0) return "";
        return $" AND LOWER({columnExpression}) IN ({string.Join(",", jsonKeys.Select(k => $"'{k.ToLowerInvariant().Replace("'", "''")}'"))})";
    }

    /// <summary>
    /// Binds named parameters on a shared introspection command. Clears any parameters from a
    /// prior reuse of <paramref name="cmd"/> first — these helper methods reassign CommandText and
    /// re-execute the same command repeatedly, so stale parameters would otherwise throw
    /// "parameter already added". Identifier values are bound as parameters (never interpolated)
    /// so single quotes and other characters in schema/table names can't break or inject into the
    /// introspection query. The <c>@name</c> style is supported by SqlClient, Npgsql, and MySqlConnector.
    /// </summary>
    private static void BindIdentifierParameters(IDbCommand cmd, params (string Name, string Value)[] parameters)
    {
        cmd.Parameters.Clear();
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.DbType = DbType.String;
            p.Value = (object)value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    #region IMergeScriptHelper dispatch methods

    public static string GetMatchColumns(Platform platform, string keyColumns) => platform.GetBasePlatform() switch
    {
        Platform.SqlServer => BuildSqlServerMatchColumns(keyColumns),
        Platform.PostgreSQL => BuildPostgreSqlMatchColumns(keyColumns),
        Platform.MySQL => BuildMySqlMatchColumns(keyColumns),
        _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
    };

    public static string GetJsonColumnDefinitions(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys = null) => platform.GetBasePlatform() switch
    {
        Platform.SqlServer => GetJsonColumnDefinitionsSqlServer(cmd, schemaOrDb, tableName, jsonKeys),
        Platform.PostgreSQL => GetJsonColumnDefinitionsPostgreSql(cmd, schemaOrDb, tableName, jsonKeys),
        Platform.MySQL => GetJsonColumnDefinitionsMySql(cmd, schemaOrDb, tableName, jsonKeys),
        _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
    };

    public static string GetJsonSelectColumns(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys = null) => platform.GetBasePlatform() switch
    {
        Platform.SqlServer => GetJsonSelectColumnsSqlServer(cmd, schemaOrDb, tableName, jsonKeys),
        Platform.PostgreSQL => "", // PostgreSQL uses json_populate_recordset — no separate select columns
        Platform.MySQL => GetJsonSelectColumnsMySql(cmd, schemaOrDb, tableName, jsonKeys),
        _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
    };

    public static string GetInsertColumns(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys = null) => platform.GetBasePlatform() switch
    {
        Platform.SqlServer => GetInsertColumnsSqlServer(cmd, schemaOrDb, tableName, jsonKeys),
        Platform.PostgreSQL => GetInsertColumnsPostgreSql(cmd, schemaOrDb, tableName, jsonKeys),
        Platform.MySQL => GetInsertColumnsMySql(cmd, schemaOrDb, tableName, jsonKeys),
        _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
    };

    public static string GetUpdateColumns(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys = null) => platform.GetBasePlatform() switch
    {
        Platform.SqlServer => GetUpdateColumnsSqlServer(cmd, schemaOrDb, tableName, jsonKeys),
        Platform.PostgreSQL => GetUpdateColumnsPostgreSql(cmd, schemaOrDb, tableName, jsonKeys),
        Platform.MySQL => GetUpdateColumnsMySql(cmd, schemaOrDb, tableName, jsonKeys),
        _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
    };

    public static bool NeedsIdentityInsert(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName) => platform.GetBasePlatform() switch
    {
        Platform.SqlServer => NeedsIdentityInsertSqlServer(cmd, schemaOrDb, tableName),
        _ => false
    };

    public static string GetIdentitySequence(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName) => platform.GetBasePlatform() switch
    {
        Platform.PostgreSQL => GetIdentityColumnAndSequencePostgreSql(cmd, schemaOrDb, tableName),
        _ => null
    };

    public static (string Disable, string Enable) GetRuleStatements(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName, bool updateDescendents = false) => platform.GetBasePlatform() switch
    {
        Platform.PostgreSQL => GetRuleDisableEnableStatements(cmd, schemaOrDb, tableName, updateDescendents),
        _ => (null, null)
    };

    /// <summary>
    /// Returns structured column metadata for Pro's deferred merge assembly.
    /// Excludes computed columns and unsupported types. Includes JSON parse type
    /// information so Pro can build type-safe NULL casts.
    /// </summary>
    public static List<MergeColumnInfo> GetColumnMetadata(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys = null)
    {
        return platform.GetBasePlatform() switch
        {
            Platform.SqlServer => GetColumnMetadataSqlServer(cmd, schemaOrDb, tableName, jsonKeys),
            Platform.PostgreSQL => GetColumnMetadataPostgreSql(cmd, schemaOrDb, tableName, jsonKeys),
            Platform.MySQL => GetColumnMetadataMySql(cmd, schemaOrDb, tableName, jsonKeys),
            _ => new List<MergeColumnInfo>()
        };
    }

    private static List<MergeColumnInfo> GetColumnMetadataSqlServer(IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys)
    {
        var schema = schemaOrDb.Trim().Trim('[', ']');
        var table = tableName.Trim().Trim('[', ']');

        BindIdentifierParameters(cmd, ("@schema", schema), ("@table", table));
        cmd.CommandText = $@"
SELECT c.COLUMN_NAME, c.DATA_TYPE,
       CASE WHEN SCHEMA_NAME(st.[schema_id]) IN ('sys', 'dbo')
            THEN '' ELSE SCHEMA_NAME(st.[schema_id]) + '.' END + st.[name] AS USER_TYPE,
       c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.DATETIME_PRECISION,
       c.IS_NULLABLE, CAST(CASE WHEN ident.[Name] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsIdentity,
       CAST(CASE WHEN cc.[name] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsComputed,
       CAST(CASE WHEN c.DATA_TYPE IN ('geography','geometry') THEN 1 ELSE 0 END AS BIT) AS IsGeometry,
       CAST(CASE WHEN c.DATA_TYPE IN ('binary','varbinary','image') THEN 1 ELSE 0 END AS BIT) AS IsBinary,
       CAST(CASE WHEN c.DATA_TYPE = 'xml' THEN 1 ELSE 0 END AS BIT) AS IsXml
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AND sc.[name] = c.COLUMN_NAME
  JOIN (SELECT CASE WHEN SCHEMA_NAME(st2.[schema_id]) IN ('sys', 'dbo')
                    THEN '' ELSE SCHEMA_NAME(st2.[schema_id]) + '.' END + st2.[name] AS [name], st2.user_type_id, st2.[schema_id]
          FROM sys.types st2 WITH (NOLOCK)) st ON st.user_type_id = sc.user_type_id
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = c.COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
    {SqlServerUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.COLUMN_NAME")}
  ORDER BY c.COLUMN_NAME
";
        var results = new List<MergeColumnInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var userType = reader.GetString(2).ToUpperInvariant();
            var maxLen = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
            var precision = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
            var scale = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));
            var dtPrecision = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6));

            // Build JSON parse type with proper size/precision qualifiers
            var parseType = userType
                .Replace("HIERARCHYID", "NVARCHAR(4000)")
                .Replace("GEOGRAPHY", "NVARCHAR(4000)")
                .Replace("GEOMETRY", "NVARCHAR(4000)")
                .Replace("DATETIMEOFFSET", "NVARCHAR(50)")
                .Replace("NTEXT", "NVARCHAR(MAX)")
                .Replace("TEXT", "VARCHAR(MAX)")
                .Replace("IMAGE", "VARBINARY(MAX)");

            if (userType.Contains("CHAR") || userType.Contains("BINARY"))
                parseType += $"({(maxLen == -1 ? "MAX" : maxLen.ToString())})";
            else if (userType is "NUMERIC" or "DECIMAL")
                parseType += $"({precision}, {scale})";
            else if (userType == "DATETIME2")
                parseType += $"({dtPrecision})";

            results.Add(new MergeColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                JsonParseType = parseType,
                MaxLength = maxLen,
                Precision = precision,
                Scale = scale,
                IsNullable = reader.GetString(7) == "YES",
                IsIdentity = (bool)reader.GetValue(8),
                IsComputed = (bool)reader.GetValue(9),
                IsGeometry = (bool)reader.GetValue(10),
                IsBinary = (bool)reader.GetValue(11),
                IsXml = (bool)reader.GetValue(12)
            });
        }
        return results;
    }

    private static List<MergeColumnInfo> GetColumnMetadataPostgreSql(IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys)
    {
        var schema = schemaOrDb.Trim().Trim('"');
        var table = tableName.Trim().Trim('"');

        BindIdentifierParameters(cmd, ("@schema", schema), ("@table", table));
        cmd.CommandText = $@"
SELECT c.column_name, c.data_type, c.udt_name, c.udt_schema,
       c.character_maximum_length, c.numeric_precision, c.numeric_scale, c.datetime_precision,
       c.is_nullable,
       CASE WHEN a.attidentity != '' OR a.attgenerated != '' THEN true ELSE false END AS is_identity,
       CASE WHEN a.attgenerated != '' THEN true ELSE false END AS is_computed
  FROM information_schema.columns c
  JOIN pg_class cls ON cls.relname = c.table_name
  JOIN pg_namespace ns ON ns.oid = cls.relnamespace AND ns.nspname = c.table_schema
  JOIN pg_attribute a ON a.attrelid = cls.oid AND a.attname = c.column_name AND NOT a.attisdropped AND a.attgenerated = ''
  WHERE c.table_schema = @schema AND c.table_name = @table{BuildJsonKeyFilter(jsonKeys, "c.column_name")}
  ORDER BY c.column_name
";
        var results = new List<MergeColumnInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var udtName = reader.GetString(2);
            var udtSchema = reader.GetString(3);
            var maxLen = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
            var precision = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));
            var scale = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6));
            var dtPrecision = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));

            var fullType = udtSchema is not "pg_catalog" and not "information_schema"
                ? $"{udtSchema}.{udtName}" : udtName;

            var parseType = fullType;
            var dataType = reader.GetString(1);
            if (dataType.Contains("char", StringComparison.OrdinalIgnoreCase) || dataType.Contains("binary", StringComparison.OrdinalIgnoreCase))
                parseType += maxLen > 0 ? $"({maxLen})" : "";
            else if (dataType is "numeric" or "decimal")
                parseType += $"({precision}, {scale})";
            else if (dataType.StartsWith("timestamp") || dataType.StartsWith("time"))
                parseType += dtPrecision > 0 ? $"({dtPrecision})" : "";

            results.Add(new MergeColumnInfo
            {
                Name = reader.GetString(0),
                DataType = udtName,
                JsonParseType = parseType,
                MaxLength = maxLen,
                Precision = precision,
                Scale = scale,
                IsNullable = reader.GetString(8) == "YES",
                IsIdentity = (bool)reader.GetValue(9),
                IsComputed = (bool)reader.GetValue(10),
                IsGeometry = IsGeometryTypePostgreSql(udtName),
                IsBinary = IsByteaTypePostgreSql(udtName),
                IsXml = IsXmlTypePostgreSql(udtName)
            });
        }
        return results;
    }

    private static List<MergeColumnInfo> GetColumnMetadataMySql(IDbCommand cmd, string schemaOrDb, string tableName, HashSet<string> jsonKeys)
    {
        var db = schemaOrDb.Trim().Trim('`');
        var table = tableName.Trim().Trim('`');

        BindIdentifierParameters(cmd, ("@db", db), ("@table", table));
        cmd.CommandText = $@"
SELECT c.COLUMN_NAME, c.DATA_TYPE, c.COLUMN_TYPE,
       c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.DATETIME_PRECISION,
       c.IS_NULLABLE,
       CASE WHEN c.EXTRA LIKE '%auto_increment%' THEN 1 ELSE 0 END AS IsIdentity,
       CASE WHEN c.GENERATION_EXPRESSION != '' THEN 1 ELSE 0 END AS IsComputed,
       c.CHARACTER_SET_NAME
  FROM INFORMATION_SCHEMA.COLUMNS c
  WHERE BINARY c.TABLE_SCHEMA = BINARY @db
    AND BINARY c.TABLE_NAME = BINARY @table
    AND (c.GENERATION_EXPRESSION IS NULL OR c.GENERATION_EXPRESSION = ''){BuildJsonKeyFilter(jsonKeys, "c.COLUMN_NAME")}
  ORDER BY c.ORDINAL_POSITION
";
        var results = new List<MergeColumnInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dataType = reader.GetString(1).ToLowerInvariant();
            var columnType = reader.GetString(2);
            var maxLen = reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader.GetValue(3));
            var precision = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
            var scale = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));

            // MySQL JSON_TABLE COLUMNS clause requires real column types — not CAST aliases
            // like SIGNED/UNSIGNED. Integer types must be spelled out (INT, BIGINT, etc.).
            var jsonType = dataType switch
            {
                "tinyint" or "smallint" or "mediumint" or "int" or "integer" or "bigint" => dataType.ToUpperInvariant(),
                "float" or "double" or "real" => "DOUBLE",
                "decimal" or "numeric" => $"DECIMAL({precision},{scale})",
                "date" => "DATE",
                "datetime" or "timestamp" => "DATETIME",
                "time" => "TIME",
                "year" => "YEAR",
                "json" => "JSON",
                _ => maxLen > 0 ? $"CHAR({maxLen})" : "CHAR(65535)"
            };

            var isGeometry = dataType is "point" or "linestring" or "polygon" or "geometry"
                or "multipoint" or "multilinestring" or "multipolygon" or "geometrycollection";
            var isBinary = dataType is "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob";

            results.Add(new MergeColumnInfo
            {
                Name = reader.GetString(0),
                DataType = dataType,
                JsonParseType = jsonType,
                MaxLength = (int)Math.Min(maxLen, int.MaxValue),
                Precision = precision,
                Scale = scale,
                IsNullable = reader.GetString(7) == "YES",
                IsIdentity = Convert.ToInt32(reader.GetValue(8)) == 1,
                IsComputed = Convert.ToInt32(reader.GetValue(9)) == 1,
                IsGeometry = isGeometry,
                IsBinary = isBinary,
                IsXml = false
            });
        }
        return results;
    }

    #endregion

    #region GetKeyColumns

    /// <summary>
    /// Gets the key columns for a table using platform-specific SQL.
    /// </summary>
    public static string GetKeyColumns(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName)
    {
        return platform.GetBasePlatform() switch
        {
            Platform.SqlServer => GetKeyColumnsSqlServer(cmd, schemaOrDb, tableName),
            Platform.PostgreSQL => GetKeyColumnsPostgreSql(cmd, schemaOrDb, tableName),
            Platform.MySQL => GetKeyColumnsMySql(cmd, schemaOrDb, tableName),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }

    private static string GetKeyColumnsSqlServer(IDbCommand cmd, string tableSchema, string tableName)
    {
        tableSchema = tableSchema.Trim().Trim('[', ']');
        tableName = tableName.Trim().Trim('[', ']');

        BindIdentifierParameters(cmd, ("@objname", $"{tableSchema}.{tableName}"));
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN sc.is_nullable = 1 THEN '*' ELSE '' END + '[' + COL_NAME(ic.[object_id], ic.column_id) + ']', ',')
  FROM sys.indexes si WITH (NOLOCK)
  JOIN sys.index_columns ic WITH (NOLOCK) ON ic.[object_id] = si.[object_id]
                                         AND ic.index_id = si.index_id
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = ic.[object_id]
                                   AND sc.column_id = ic.column_id
  WHERE si.[object_id] = OBJECT_ID(@objname)
    AND si.index_id = (SELECT TOP 1 si2.index_id
                         FROM sys.indexes si2 WITH (NOLOCK)
                         WHERE si2.[object_id] = si.[object_id]
                           AND si2.is_unique = 1
                         ORDER BY CASE WHEN is_primary_key = 1 THEN 0 ELSE 1 END,
                                  (SELECT COUNT(*)
                                    FROM sys.index_columns ic2 WITH (NOLOCK)
                                    JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = ic2.[object_id] AND sc.column_id = ic2.column_id
                                    WHERE ic2.[object_id] = si2.[object_id]
                                      AND sc.is_nullable = 0))
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static string GetKeyColumnsPostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        tableSchema = tableSchema.Trim().Trim('"');
        tableName = tableName.Trim().Trim('"');

        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN c.is_nullable = 'YES' THEN '*' ELSE '' END || '""' || a.attname || '""', ',' ORDER BY ik.ord )
  FROM pg_index idx
  JOIN pg_class tbl ON tbl.oid = idx.indrelid
  JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
  JOIN LATERAL UNNEST(idx.indkey) WITH ORDINALITY AS ik(attnum, ord) ON TRUE
  JOIN pg_attribute a ON a.attrelid = tbl.oid
                     AND a.attnum = ik.attnum
  JOIN information_schema.columns c ON c.table_schema = ns.nspname
                                   AND c.table_name = tbl.relname
                                   AND c.column_name = a.attname
  WHERE ns.nspname = @schema
    AND tbl.relname = @table
    AND idx.indexrelid = (SELECT i2.indexrelid
                            FROM pg_index i2
                            WHERE i2.indrelid = tbl.oid
                              AND i2.indisunique = true
                            ORDER BY CASE WHEN i2.indisprimary THEN 0 ELSE 1 END,
                                     (SELECT COUNT(*)
                                        FROM UNNEST(i2.indkey) AS k(attnum)
                                        JOIN pg_attribute pa ON pa.attrelid = tbl.oid AND pa.attnum = k.attnum
                                        WHERE pa.attnotnull = true)
                            LIMIT 1);
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static string GetKeyColumnsMySql(IDbCommand cmd, string databaseName, string tableName)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');

        BindIdentifierParameters(cmd, ("@db", databaseName), ("@table", tableName));
        cmd.CommandText = $@"
SELECT GROUP_CONCAT(CONCAT('`', kcu.COLUMN_NAME, '`') ORDER BY kcu.ORDINAL_POSITION SEPARATOR ',')
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
  ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
  AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
  AND tc.TABLE_NAME = kcu.TABLE_NAME
WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND BINARY tc.TABLE_SCHEMA = BINARY @db
  AND BINARY tc.TABLE_NAME = BINARY @table;
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    #endregion

    #region BuildMergeScript

    /// <summary>
    /// Builds a platform-aware merge script for data delivery.
    /// </summary>
    /// <param name="platform">Target database platform.</param>
    /// <param name="cmd">Database command for metadata queries.</param>
    /// <param name="schemaOrDb">Schema name (SQL Server/PostgreSQL) or database name (MySQL). Used for
    /// INFORMATION_SCHEMA / catalog probes regardless of <paramref name="destSchemaOverride"/>.</param>
    /// <param name="tableName">Target table name.</param>
    /// <param name="tableData">JSON array of row data.</param>
    /// <param name="keyColumns">Comma-separated key columns for matching.</param>
    /// <param name="mergeUpdate">Whether to update matching rows.</param>
    /// <param name="mergeDelete">Whether to delete rows not in source.</param>
    /// <param name="disableTriggers">Whether to disable triggers during merge.</param>
    /// <param name="tokenizeScripts">Whether to use token placeholders instead of inline data.</param>
    /// <param name="mergeFilter">Optional filter for delete clause.</param>
    /// <param name="disableRules">PostgreSQL only: whether to disable rules during merge.</param>
    /// <param name="updateDescendents">PostgreSQL only: whether to update descendant tables (omits ONLY keyword).</param>
    /// <param name="destSchemaOverride">When non-empty, used in place of <paramref name="schemaOrDb"/> for
    /// destination-side refs in the emitted script body (MERGE INTO / ALTER TABLE / IDENTITY_INSERT / SETVAL)
    /// AND for the content-file token (which becomes unqualified — just <c>{{tableName.tabledata}}</c>).
    /// Source-side catalog probes still use <paramref name="schemaOrDb"/>. Exists for DataTongs schema-template
    /// extraction (design §8) where the caller passes <c>{{SchemaName}}</c> so the engine resolves the destination
    /// schema at quench time. MySQL ignores the override (no schema-inside-database concept).</param>
    public static string BuildMergeScript(Platform platform, IDbCommand cmd,
        string schemaOrDb, string tableName, string tableData, string keyColumns,
        bool mergeUpdate, bool mergeDelete, bool disableTriggers,
        bool tokenizeScripts, string mergeFilter,
        bool disableRules = false, bool updateDescendents = false,
        string destSchemaOverride = null, int pgServerVersionNum = 0)
    {
        // Extract JSON keys to filter columns — only include columns present in the data.
        // For tokenized scripts, data is replaced at runtime so we include all columns.
        var jsonKeys = tokenizeScripts ? null : GetJsonDataKeys(tableData);

        return platform.GetBasePlatform() switch
        {
            Platform.SqlServer => BuildMergeScriptSqlServer(cmd, schemaOrDb, tableName, tableData, keyColumns,
                mergeUpdate, mergeDelete, disableTriggers, tokenizeScripts, mergeFilter, jsonKeys, destSchemaOverride),
            Platform.PostgreSQL => BuildMergeScriptPostgreSql(cmd, schemaOrDb, tableName, updateDescendents, tableData, keyColumns,
                mergeUpdate, mergeDelete, disableTriggers, disableRules, tokenizeScripts, mergeFilter, jsonKeys, destSchemaOverride, pgServerVersionNum),
            Platform.MySQL => BuildMergeScriptMySql(cmd, schemaOrDb, tableName, tableData, keyColumns,
                mergeUpdate, mergeDelete, tokenizeScripts, mergeFilter, jsonKeys),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }

    #endregion

    #region Unsupported Column Comments

    /// <summary>
    /// Queries for columns with unsupported data types and returns SQL comment lines for each.
    /// </summary>
    internal static string GetUnsupportedColumnComments(Platform platform, IDbCommand cmd, string schemaOrDb, string tableName)
    {
        return platform.GetBasePlatform() switch
        {
            Platform.SqlServer => GetUnsupportedColumnCommentsSqlServer(cmd, schemaOrDb, tableName),
            Platform.PostgreSQL => GetUnsupportedColumnCommentsPostgreSql(cmd, schemaOrDb, tableName),
            _ => ""
        };
    }

    private static string GetUnsupportedColumnCommentsSqlServer(IDbCommand cmd, string tableSchema, string tableName)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG('-- Column [' + c.COLUMN_NAME + '] skipped: ' + c.DATA_TYPE + ' is not supported for data delivery', CHAR(13) + CHAR(10))
  FROM INFORMATION_SCHEMA.COLUMNS c
  WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
    AND c.DATA_TYPE IN ('sql_variant', 'rowversion', 'timestamp')
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static string GetUnsupportedColumnCommentsPostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG('-- Column ""' || c.column_name || '"" skipped: ' || c.udt_name || ' is not supported for data delivery', CHR(10) ORDER BY c.column_name)
  FROM information_schema.columns c
  JOIN pg_class cls ON cls.relname = c.table_name
  JOIN pg_namespace ns ON ns.oid = cls.relnamespace AND ns.nspname = c.table_schema
  JOIN pg_attribute a ON a.attrelid = cls.oid AND a.attname = c.column_name AND NOT a.attisdropped AND a.attgenerated = ''
  WHERE c.table_schema = @schema AND c.table_name = @table
    AND (c.udt_name IN ('tsvector', 'tsquery', 'money', 'box', 'circle', 'line', 'lseg', 'path')
         OR EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                    WHERE t.typname = c.udt_name AND n.nspname = c.udt_schema AND t.typtype = 'c'))
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    #endregion

    #region SQL Server Implementation

    private static string BuildMergeScriptSqlServer(IDbCommand cmd, string tableSchema, string tableName,
        string tableData, string keyColumns, bool mergeUpdate, bool mergeDelete,
        bool disableTriggers, bool tokenizeScripts, string mergeFilter, HashSet<string> jsonKeys,
        string destSchemaOverride = null)
    {
        tableSchema = tableSchema.Trim().Trim('[', ']');
        tableName = tableName.Trim().Trim('[', ']');

        // destSchema is the identifier used in EMITTED destination refs; tableSchema is used
        // for catalog probes against the source database. They differ only when DataTongs
        // is extracting in schema-template mode (override = "{{SchemaName}}").
        var destSchema = string.IsNullOrEmpty(destSchemaOverride)
            ? tableSchema
            : destSchemaOverride.Trim().Trim('[', ']');

        var unsupportedComments = GetUnsupportedColumnCommentsSqlServer(cmd, tableSchema, tableName);
        var matchColumns = BuildSqlServerMatchColumns(keyColumns);
        var fromJsonSelectColumns = GetJsonSelectColumnsSqlServer(cmd, tableSchema, tableName, jsonKeys);
        // IDENTITY_INSERT must be ON only when tabledata provides values for an identity column.
        // NeedsIdentityInsertSqlServer returns true whenever ANY identity column exists; filter by jsonKeys.
        var identityInsert = NeedsIdentityInsertSqlServer(cmd, tableSchema, tableName)
                             && IdentityColumnInJsonKeysSqlServer(cmd, tableSchema, tableName, jsonKeys);
        var jsonColumns = GetJsonColumnDefinitionsSqlServer(cmd, tableSchema, tableName, jsonKeys);

        // Content-file token: when destination is overridden, the .tabledata file is unqualified
        // (DataTongs writes Customers.tabledata, not tenant_seed.Customers.tabledata in schema-
        // template mode), so the token must match by name. Otherwise stays schema-qualified.
        var contentToken = string.IsNullOrEmpty(destSchemaOverride)
            ? $"{tableSchema}.{tableName}.tabledata"
            : $"{tableName}.tabledata";
        var jsonValue = tokenizeScripts
            ? $"{{{{{contentToken}}}}}"
            : tableData?.Replace("'", "''");

        var mergeSQL = (string.IsNullOrEmpty(unsupportedComments) ? "" : unsupportedComments + "\r\n") + $@"
DECLARE @v_json NVARCHAR(MAX) = '{jsonValue}';

{(disableTriggers ? $"ALTER TABLE [{destSchema}].[{tableName}] DISABLE TRIGGER ALL;" : "")}
{(identityInsert ? $"SET IDENTITY_INSERT [{destSchema}].[{tableName}] ON;" : "")}
MERGE INTO [{destSchema}].[{tableName}] AS Target
USING (
  SELECT {fromJsonSelectColumns}
    FROM OPENJSON(@v_json)
    WITH (
{jsonColumns}
    )
) AS Source
ON {matchColumns}
";

        if (mergeUpdate)
        {
            var updateColumns = GetUpdateColumnsSqlServer(cmd, tableSchema, tableName, jsonKeys);
            // NULL-safe inequality: fires update when exactly one side is NULL, or when both are non-NULL but differ.
            // The original "NOT (T = S OR (both NULL))" form falls into three-valued-logic NULL for the asymmetric cases.
            static string NullSafeNotEqual(string tExpr, string sExpr) =>
                $"((({tExpr}) IS NULL AND ({sExpr}) IS NOT NULL) OR (({tExpr}) IS NOT NULL AND ({sExpr}) IS NULL) OR ({tExpr}) <> ({sExpr}))";
            var updateCompare = string.Join(" OR ",
                updateColumns!.Split(',').Select(c => c.StartsWith("G[")
                    ? NullSafeNotEqual($"Target.{c.Substring(1)}.ToString()", $"Source.{c.Substring(1)}.ToString()")
                    : c.StartsWith("X[") || c.StartsWith("N[")
                        ? NullSafeNotEqual($"CAST(Target.{c.Substring(1)} AS NVARCHAR(MAX))", $"CAST(Source.{c.Substring(1)} AS NVARCHAR(MAX))")
                        : c.StartsWith("T[")
                            ? NullSafeNotEqual($"CAST(Target.{c.Substring(1)} AS VARCHAR(MAX))", $"CAST(Source.{c.Substring(1)} AS VARCHAR(MAX))")
                            : c.StartsWith("I[")
                                ? NullSafeNotEqual($"CAST(Target.{c.Substring(1)} AS VARBINARY(MAX))", $"CAST(Source.{c.Substring(1)} AS VARBINARY(MAX))")
                                : c.StartsWith("D[")
                                    ? NullSafeNotEqual($"CAST(Target.{c.Substring(1)} AS NVARCHAR(50))", $"CAST(Source.{c.Substring(1)} AS NVARCHAR(50))")
                                    : NullSafeNotEqual($"Target.{c}", $"Source.{c}")));

            mergeSQL += $"""

WHEN MATCHED AND ({updateCompare}) THEN
  UPDATE SET
{string.Join(",\r\n", updateColumns.Split(',').Select(c => $"        {c.Replace("G[", "[").Replace("X[", "[").Replace("N[", "[").Replace("T[", "[").Replace("I[", "[").Replace("D[", "[")} = Source.{c.Replace("G[", "[").Replace("X[", "[").Replace("N[", "[").Replace("T[", "[").Replace("I[", "[").Replace("D[", "[")}"))}
""";
        }

        var insertColumns = GetInsertColumnsSqlServer(cmd, tableSchema, tableName, jsonKeys);
        mergeSQL += $@"

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
 {insertColumns}
   ) VALUES (
 {insertColumns!.Replace("[", "Source.[")}
   )
 ";

        if (mergeDelete)
        {
            mergeSQL += $@"

 WHEN NOT MATCHED BY SOURCE{(string.IsNullOrWhiteSpace(mergeFilter) ? "" : $" AND ({mergeFilter})")} THEN
   DELETE
 ";
        }

        mergeSQL += $";\r\n{(identityInsert ? $"SET IDENTITY_INSERT [{destSchema}].[{tableName}] OFF;\r\n" : "")}{(disableTriggers ? $"ALTER TABLE [{destSchema}].[{tableName}] ENABLE TRIGGER ALL;\r\n" : "")}";
        return mergeSQL;
    }

    private static string BuildSqlServerMatchColumns(string keyColumns)
    {
        return string.Join(" AND ", keyColumns.Split(',')
            .Select(c => c.Trim().StartsWith("*")
                ? $"(Source.[{c.Trim().Substring(1).Trim(']', '[')}] = Target.[{c.Trim().Substring(1).Trim(']', '[')}] OR (Source.[{c.Trim().Substring(1).Trim(']', '[')}] IS NULL AND Target.[{c.Trim().Substring(1).Trim(']', '[')}] IS NULL))"
                : $"Source.[{c.Trim().Trim(']', '[')}] = Target.[{c.Trim().Trim(']', '[')}]"));
    }

    private static string GetJsonSelectColumnsSqlServer(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN c.DATA_TYPE IN ('GEOGRAPHY', 'GEOMETRY')
                       THEN CASE WHEN c.DATA_TYPE = 'GEOGRAPHY' THEN 'geography' ELSE 'geometry' END + '::STGeomFromText([' + c.COLUMN_NAME + '], [' + c.COLUMN_NAME + '.STSrid]) AS [' + c.COLUMN_NAME + ']'
                       ELSE '[' + c.COLUMN_NAME + ']' END, ',') WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
    {SqlServerUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.COLUMN_NAME")}
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static bool NeedsIdentityInsertSqlServer(IDbCommand cmd, string tableSchema, string tableName)
    {
        BindIdentifierParameters(cmd, ("@objname", $"{tableSchema}.{tableName}"));
        cmd.CommandText = $@"
SELECT CAST(CASE WHEN EXISTS (SELECT * FROM sys.identity_columns WITH (NOLOCK) WHERE [object_id] = OBJECT_ID(@objname))
                 THEN 1 ELSE 0 END AS BIT)
";
        return cmd.ExecuteScalar() as bool? ?? false;
    }

    private static bool IdentityColumnInJsonKeysSqlServer(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys)
    {
        if (jsonKeys == null || jsonKeys.Count == 0) return false;
        var names = string.Join(",", jsonKeys.Select(k => $"'{k.Replace("'", "''")}'"));
        BindIdentifierParameters(cmd, ("@objname", $"{tableSchema}.{tableName}"));
        cmd.CommandText = $@"
SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.identity_columns c WITH (NOLOCK)
                              WHERE c.[object_id] = OBJECT_ID(@objname)
                                AND c.[name] IN ({names}))
                 THEN 1 ELSE 0 END AS BIT)
";
        return cmd.ExecuteScalar() as bool? ?? false;
    }

    private static string GetJsonColumnDefinitionsSqlServer(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG('           [' + c.COLUMN_NAME + '] ' +
                  REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(USER_TYPE), 'HIERARCHYID', 'NVARCHAR(4000)'), 'GEOGRAPHY', 'NVARCHAR(4000)'), 'GEOMETRY', 'NVARCHAR(4000)'), 'DATETIMEOFFSET', 'NVARCHAR(50)'), 'NTEXT', 'NVARCHAR(MAX)'), 'TEXT', 'VARCHAR(MAX)'), 'IMAGE', 'VARBINARY(MAX)') +
                      CASE WHEN USER_TYPE LIKE '%CHAR' OR USER_TYPE LIKE '%BINARY'
                           THEN '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'MAX' ELSE CONVERT(NVARCHAR(20), CHARACTER_MAXIMUM_LENGTH) END + ')'
                                           WHEN USER_TYPE IN ('NUMERIC', 'DECIMAL')
                                           THEN '(' + CONVERT(NVARCHAR(20), NUMERIC_PRECISION) + ', ' + CONVERT(NVARCHAR(20), NUMERIC_SCALE) + ')'
                                           WHEN USER_TYPE = 'DATETIME2'
                                           THEN '(' + CONVERT(NVARCHAR(20), DATETIME_PRECISION) + ')'
                                           WHEN USER_TYPE = 'XML' AND sc.xml_collection_id <> 0
                                           THEN '([' + SCHEMA_NAME(xc.[schema_id]) + '].[' + xc.[name] + '])'
                                           ELSE '' END +
                      CASE WHEN USER_TYPE IN ('GEOGRAPHY', 'GEOMETRY') THEN ', [' + c.COLUMN_NAME + '.STSrid] INT' ELSE '' END, ',' + CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  JOIN (SELECT CASE WHEN SCHEMA_NAME(st.[schema_id]) IN ('sys', 'dbo')
                    THEN '' ELSE SCHEMA_NAME(st.[schema_id]) + '.' END + st.[name] AS USER_TYPE, st.user_type_id
          FROM sys.types st WITH (NOLOCK)) st ON st.user_type_id = sc.user_type_id
  LEFT JOIN sys.xml_schema_collections xc WITH (NOLOCK) ON xc.xml_collection_id = sc.xml_collection_id
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
    AND cc.[name] IS NULL
    {SqlServerUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.COLUMN_NAME")}
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string GetInsertColumnsSqlServer(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG('        [' + c.COLUMN_NAME + ']', ',' + CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
    {SqlServerUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.COLUMN_NAME")}
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string GetUpdateColumnsSqlServer(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN c.DATA_TYPE IN ('GEOGRAPHY', 'GEOMETRY') THEN 'G'
                       WHEN c.DATA_TYPE = 'DATETIMEOFFSET' THEN 'D'
                       WHEN c.DATA_TYPE = 'XML' THEN 'X'
                       WHEN c.DATA_TYPE = 'NTEXT' THEN 'N'
                       WHEN c.DATA_TYPE = 'TEXT' THEN 'T'
                       WHEN c.DATA_TYPE = 'IMAGE' THEN 'I'
                       ELSE '' END +
                  '[' + c.COLUMN_NAME + ']', ',') WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                    AND ident.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
    AND ident.[Name] IS NULL
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
    {SqlServerUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.COLUMN_NAME")}
";
        return cmd.ExecuteScalar()?.ToString();
    }

    #endregion

    #region PostgreSQL Implementation

    private static string BuildMergeScriptPostgreSql(IDbCommand cmd, string tableSchema, string tableName,
        bool updateDescendents, string tableData, string keyColumns, bool mergeUpdate, bool mergeDelete,
        bool disableTriggers, bool disableRules, bool tokenizeScripts, string mergeFilter, HashSet<string> jsonKeys,
        string destSchemaOverride = null, int pgServerVersionNum = 0)
    {
        tableSchema = tableSchema.Trim().Trim('"');
        tableName = tableName.Trim().Trim('"');

        // destSchema is the identifier used in EMITTED destination refs; tableSchema is used
        // for catalog probes against the source database. They differ only when DataTongs is
        // extracting in schema-template mode (override = "{{SchemaName}}").
        var destSchema = string.IsNullOrEmpty(destSchemaOverride)
            ? tableSchema
            : destSchemaOverride.Trim().Trim('"');

        var unsupportedComments = GetUnsupportedColumnCommentsPostgreSql(cmd, tableSchema, tableName);
        var matchColumns = BuildPostgreSqlMatchColumns(keyColumns);
        var identAndSeq = GetIdentityColumnAndSequencePostgreSql(cmd, tableSchema, tableName);
        var jsonColumns = GetJsonColumnDefinitionsPostgreSql(cmd, tableSchema, tableName, jsonKeys);

        // PostgreSQL does not support DISABLE/ENABLE RULE ALL — each rule must be named individually.
        // Rules cannot be disabled inside PL/pgSQL blocks, so these statements go outside the DO $$ block.
        // Rule names are looked up in source catalog (pg_rules with tableSchema); the emitted ALTER
        // TABLE statements use destSchema so the rules disable/enable on the destination at quench.
        var (disableRuleStatements, enableRuleStatements) = disableRules
            ? GetRuleDisableEnableStatements(cmd, tableSchema, tableName, updateDescendents, destSchema)
            : ("", "");

        // Content-file token: see SQL Server method — unqualified when destSchemaOverride is set
        // so the token matches the unqualified filename DataTongs writes in schema-template mode.
        var contentToken = string.IsNullOrEmpty(destSchemaOverride)
            ? $"{tableSchema}.{tableName}.tabledata"
            : $"{tableName}.tabledata";
        var jsonValue = tokenizeScripts
            ? $"{{{{{contentToken}}}}}"
            : tableData?.Replace("'", "''");

        var mergeSQL = (string.IsNullOrEmpty(unsupportedComments) ? "" : unsupportedComments + "\n")
            + (string.IsNullOrEmpty(disableRuleStatements) ? "" : disableRuleStatements + "\n") + $@"
DO $$
DECLARE
  v_json JSON = '{jsonValue}';
  nextval BIGINT;
BEGIN
{(disableTriggers ? $"ALTER TABLE \"{destSchema}\".\"{tableName}\" {(updateDescendents ? "" : "ONLY ")}DISABLE TRIGGER ALL;" : "")}
MERGE INTO {(updateDescendents ? "" : "ONLY ")}""{destSchema}"".""{tableName}"" AS ""Target""
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT {jsonColumns}
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS ""Source""
ON {matchColumns}
";

        if (mergeUpdate)
        {
            var updateColumns = GetUpdateColumnsPostgreSql(cmd, tableSchema, tableName, jsonKeys);
            var xmlColumns = GetXmlColumnsPostgreSql(cmd, tableSchema, tableName);
            var jsonCols = GetJsonColumnsPostgreSql(cmd, tableSchema, tableName);
            // IS DISTINCT FROM is NULL-safe — treats (NULL, X) and (X, NULL) as different without falling into three-valued-logic NULL results
            var updateCompare = string.Join(" OR ",
                updateColumns!.Split(',').Select(c =>
                {
                    var colName = c.Trim().Trim('"');
                    if (xmlColumns.Contains(colName))
                        return $"\"Target\".{c}::text IS DISTINCT FROM \"Source\".{c}::text";
                    if (jsonCols.TryGetValue(colName, out var jsonType))
                    {
                        var cast = jsonType == "jsonb" ? "::jsonb" : "::text";
                        return $"\"Target\".{c}{cast} IS DISTINCT FROM \"Source\".{c}{cast}";
                    }
                    return $"\"Target\".{c} IS DISTINCT FROM \"Source\".{c}";
                }));

            mergeSQL += $"""

WHEN MATCHED AND ({updateCompare}) THEN
  UPDATE SET
{string.Join(",\r\n", updateColumns.Split(',').Select(c => $"        {c} = \"Source\".{c}"))}
""";
        }

        var modernDelete = pgServerVersionNum == 0 || pgServerVersionNum >= 17;

        var insertColumns = GetInsertColumnsPostgreSql(cmd, tableSchema, tableName, jsonKeys);
        mergeSQL += $@"

 WHEN NOT MATCHED {(mergeDelete && modernDelete ? "BY TARGET " : "")}THEN -- 'BY TARGET' requires PostgreSQL v17+; add it only when the v17 delete-on-absence clause is also emitted
   INSERT (
 {insertColumns}
   ) {(!string.IsNullOrEmpty(identAndSeq) ? $"OVERRIDING {identAndSeq.Split("=")[2]} VALUE" : "")}
  VALUES (
 {string.Join(",\n", insertColumns!.Split(',').Select(c => $"        \"Source\".{c.Trim()}"))}
   )
 ";

        if (mergeDelete && modernDelete)
        {
            mergeSQL += $@"

 WHEN NOT MATCHED BY SOURCE{(string.IsNullOrWhiteSpace(mergeFilter) ? "" : $" AND ({mergeFilter})")} THEN -- Requires PostgreSQL v17+
   DELETE
 ";
        }

        mergeSQL += $@";
{(disableTriggers ? $"ALTER TABLE \"{destSchema}\".\"{tableName}\" {(updateDescendents ? "" : "ONLY ")}ENABLE TRIGGER ALL;" : "")}
{(!string.IsNullOrEmpty(identAndSeq) ? $"SELECT SETVAL('{identAndSeq.Split("=")[1]}', (SELECT MAX(\"{identAndSeq.Split("=")[0]}\") FROM \"{destSchema}\".\"{tableName}\")) INTO nextval;" : "")}

END $$ LANGUAGE plpgsql;
" + (string.IsNullOrEmpty(enableRuleStatements) ? "" : "\n" + enableRuleStatements);

        if (mergeDelete && !modernDelete)
        {
            // PG < 17 has no MERGE WHEN NOT MATCHED BY SOURCE. Emit a standalone DELETE outside the
            // DO $$ block targeting rows with no matching source row, keyed on the same columns the
            // MERGE matched on, honoring the same mergeFilter. Source is the JSON array reprojected
            // identically to the MERGE USING clause so key correspondence is exact.
            // Per-key correspondence mirrors BuildPostgreSqlMatchColumns' *-prefix NULL-safe
            // handling (source of truth — keep in sync). A leading '*' is a user-config NULL-safe
            // marker: strip it and emit the (l = r OR (l IS NULL AND r IS NULL)) form.
            var deleteKeyPredicate = string.Join(" AND ",
                keyColumns.Split(',').Select(k =>
                {
                    var raw = k.Trim();
                    var nullSafe = raw.StartsWith("*");
                    var col = $"\"{raw.Trim('*').Trim('"')}\"";  // normalized quoted ident, * stripped
                    var l = $"\"DeleteSource\".{col}";
                    var r = $"\"Target\".{col}";
                    return nullSafe
                        ? $"({l} = {r} OR ({l} IS NULL AND {r} IS NULL))"
                        : $"{l} = {r}";
                }));

            mergeSQL += $@"
DELETE FROM {(updateDescendents ? "" : "ONLY ")}""{destSchema}"".""{tableName}"" AS ""Target""
 WHERE {(string.IsNullOrWhiteSpace(mergeFilter) ? "" : $"({mergeFilter}) AND ")}NOT EXISTS (
   SELECT 1
     FROM (WITH my_tables(arr) AS (VALUES('{jsonValue}'::JSON))
           SELECT {jsonColumns}
             FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem) AS ""DeleteSource""
    WHERE {deleteKeyPredicate}
 );";
        }

        return mergeSQL;
    }

    private static (string DisableStatements, string EnableStatements) GetRuleDisableEnableStatements(
        IDbCommand cmd, string tableSchema, string tableName, bool updateDescendents, string destSchema = null)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT rulename FROM pg_rules
WHERE schemaname = @schema
  AND tablename = @table
  AND rulename <> '_RETURN'
ORDER BY rulename;";
        var ruleNames = new List<string>();
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                ruleNames.Add(reader.GetString(0));

        if (ruleNames.Count == 0)
            return ("", "");

        // Catalog probe uses tableSchema (the source); emitted ALTER TABLE statements use the
        // destination schema so the rule manipulation lands on the destination table at quench.
        var emittedSchema = string.IsNullOrEmpty(destSchema) ? tableSchema : destSchema;
        var only = updateDescendents ? "" : "ONLY ";
        var disable = string.Join("\n", ruleNames.Select(r =>
            $"ALTER TABLE {only}\"{emittedSchema}\".\"{tableName}\" DISABLE RULE \"{r}\";"));
        var enable = string.Join("\n", ruleNames.Select(r =>
            $"ALTER TABLE {only}\"{emittedSchema}\".\"{tableName}\" ENABLE RULE \"{r}\";"));
        return (disable, enable);
    }

    private static string BuildPostgreSqlMatchColumns(string keyColumns)
    {
        // Mirrors the PG<17 fallback's deleteKeyPredicate (source of truth — keep in sync).
        // A leading '*' is a user-config NULL-safe marker: strip it from the emitted column and
        // emit the (l = r OR (l IS NULL AND r IS NULL)) form with both aliases properly quoted.
        return string.Join(" AND ", keyColumns.Split(',')
            .Select(k =>
            {
                var raw = k.Trim();
                var nullSafe = raw.StartsWith("*");
                var col = $"\"{raw.TrimStart('*').Trim('"')}\"";  // normalized quoted ident, * stripped
                var l = $"\"Source\".{col}";
                var r = $"\"Target\".{col}";
                return nullSafe
                    ? $"({l} = {r} OR ({l} IS NULL AND {r} IS NULL))"
                    : $"{l} = {r}";
            }));
    }

    private static string GetIdentityColumnAndSequencePostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT c.column_name || '=' || COALESCE(PG_GET_SERIAL_SEQUENCE(c.table_schema || '.' || c.table_name, c.column_name),
                                        CASE WHEN LOWER(c.column_default) LIKE 'nextval(%'
                                             THEN SUBSTRING(c.column_default
                                                            FROM POSITION('''' IN c.column_default) + 1
                                                            FOR POSITION('''' IN SUBSTRING(c.column_default FROM POSITION('''' IN c.column_default) + 1)) - 1)
                                             END) || '=' || CASE WHEN c.is_identity = 'YES' THEN 'SYSTEM' ELSE 'USER' END
  FROM information_schema.columns c
  WHERE c.table_schema = @schema
    AND c.table_name = @table
    AND (LOWER(c.column_default) LIKE 'nextval(%' OR c.is_identity = 'YES')
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string GetJsonColumnDefinitionsPostgreSql(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys = null)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG(
    CASE WHEN c.udt_name IN ('geometry','geography','point','linestring','polygon',
                              'multipoint','multilinestring','multipolygon','geometrycollection')
         THEN 'ST_GeomFromText(elem ->> ''' || c.column_name || ''')'
         WHEN c.udt_name = 'bytea'
         THEN 'decode(elem ->> ''' || c.column_name || ''', ''base64'')'
         WHEN LEFT(c.udt_name, 1) = '_'
         THEN 'STRING_TO_ARRAY((elem ->> ''' || c.column_name || '''), ''*,*'', ''*NULL_VALUE_REPRESENTATION*'')::' ||
              CASE WHEN c.udt_schema NOT IN ('pg_catalog','information_schema')
                   THEN c.udt_schema || '.' || c.udt_name
                   ELSE c.udt_name END
         ELSE '(elem ->> ''' || c.column_name || ''')::' ||
              CASE WHEN c.udt_schema NOT IN ('pg_catalog','information_schema')
                   THEN c.udt_schema || '.' || c.udt_name
                   ELSE c.udt_name END ||
              CASE WHEN c.data_type ILIKE '%char%' OR c.data_type ILIKE '%binary%'
                   THEN CASE WHEN c.character_maximum_length IS NULL
                             THEN '' ELSE '(' || c.character_maximum_length || ')' END
                   WHEN c.data_type IN ('numeric','decimal') AND c.numeric_precision IS NOT NULL
                   THEN '(' || c.numeric_precision::text || ', ' || COALESCE(c.numeric_scale::text,'0') || ')'
                   WHEN c.data_type LIKE 'timestamp%' OR c.data_type LIKE 'time%'
                   THEN CASE WHEN c.datetime_precision IS NULL
                             THEN '' ELSE '(' || c.datetime_precision::text || ')' END
                   ELSE '' END
    END || ' AS ""' || c.column_name || '""', ',' || CHR(10) || '           ' ORDER BY c.column_name)
  FROM information_schema.columns c
  JOIN pg_class cls ON cls.relname = c.table_name
  JOIN pg_namespace ns ON ns.oid = cls.relnamespace AND ns.nspname = c.table_schema
  JOIN pg_attribute a ON a.attrelid = cls.oid
                     AND a.attname = c.column_name
                     AND NOT a.attisdropped
                     AND a.attgenerated = ''
  WHERE c.table_schema = @schema AND c.table_name = @table
    {PostgreSqlUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.column_name")}
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static string GetInsertColumnsPostgreSql(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys = null)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG('        ""' || c.column_name || '""', ',' || CHR(10) ORDER BY c.column_name)
  FROM information_schema.columns c
  JOIN pg_class cls ON cls.relname = c.table_name
  JOIN pg_namespace ns ON ns.oid = cls.relnamespace AND ns.nspname = c.table_schema
  JOIN pg_attribute a ON a.attrelid = cls.oid
                     AND a.attname = c.column_name
                     AND NOT a.attisdropped
                     AND a.attgenerated = ''
  WHERE c.table_schema = @schema
    AND c.table_name = @table
    {PostgreSqlUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.column_name")}
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static string GetUpdateColumnsPostgreSql(IDbCommand cmd, string tableSchema, string tableName, HashSet<string> jsonKeys = null)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG('""' || c.column_name || '""', ',' ORDER BY c.column_name)
  FROM information_schema.columns c
  JOIN pg_class cls ON cls.relname = c.table_name
  JOIN pg_namespace ns ON ns.oid = cls.relnamespace AND ns.nspname = c.table_schema
  JOIN pg_attribute a ON a.attrelid = cls.oid AND a.attname = c.column_name AND NOT a.attisdropped AND a.attgenerated = ''
  WHERE c.table_schema = @schema
    AND c.table_name = @table
    AND NOT (COALESCE(c.column_default, '') LIKE 'nextval(%' OR c.is_identity = 'YES')
    {PostgreSqlUnsupportedTypeFilter}{BuildJsonKeyFilter(jsonKeys, "c.column_name")}
";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    /// <summary>
    /// Returns true if the PostgreSQL udt_name represents a PostGIS geometry/geography type.
    /// </summary>
    internal static bool IsGeometryTypePostgreSql(string udtName) => udtName.ToLowerInvariant() switch
    {
        "geometry" or "geography" or "point" or "linestring" or "polygon"
            or "multipoint" or "multilinestring" or "multipolygon" or "geometrycollection" => true,
        _ => false
    };

    /// <summary>
    /// Returns true if the PostgreSQL udt_name represents a bytea (binary) type.
    /// </summary>
    internal static bool IsByteaTypePostgreSql(string udtName) =>
        udtName.ToLowerInvariant() == "bytea";

    internal static bool IsXmlTypePostgreSql(string udtName) =>
        udtName.ToLowerInvariant() == "xml";

    private static HashSet<string> GetXmlColumnsPostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT STRING_AGG(c.column_name, ',')
  FROM information_schema.columns c
  WHERE c.table_schema = @schema
    AND c.table_name = @table
    AND c.udt_name = 'xml';
";
        var result = cmd.ExecuteScalar()?.ToString();
        if (string.IsNullOrEmpty(result)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        return new HashSet<string>(result.Split(','), StringComparer.InvariantCultureIgnoreCase);
    }

    private static Dictionary<string, string> GetJsonColumnsPostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BindIdentifierParameters(cmd, ("@schema", tableSchema), ("@table", tableName));
        cmd.CommandText = $@"
SELECT c.column_name, c.udt_name
  FROM information_schema.columns c
  WHERE c.table_schema = @schema
    AND c.table_name = @table
    AND c.udt_name IN ('json', 'jsonb')";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    #endregion

    #region MySQL Implementation

    private static string BuildMySqlMatchColumns(string keyColumns)
    {
        return string.Join(" AND ", keyColumns.Split(',')
            .Select(c => c.Trim().StartsWith("*")
                ? $"(`Source`.`{c.Trim().Substring(1).Trim('`')}` = `Target`.`{c.Trim().Substring(1).Trim('`')}` OR (`Source`.`{c.Trim().Substring(1).Trim('`')}` IS NULL AND `Target`.`{c.Trim().Substring(1).Trim('`')}` IS NULL))"
                : $"`Source`.`{c.Trim().Trim('`')}` = `Target`.`{c.Trim().Trim('`')}`"));
    }

    private static string GetJsonColumnDefinitionsMySql(IDbCommand cmd, string databaseName, string tableName, HashSet<string> jsonKeys)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');
        var columns = GetColumnInfoMySql(cmd, databaseName, tableName, excludeAutoIncrement: false, jsonKeys: jsonKeys);
        return BuildJsonTableColumnsMySql(columns);
    }

    private static string GetJsonSelectColumnsMySql(IDbCommand cmd, string databaseName, string tableName, HashSet<string> jsonKeys)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');
        var columns = GetColumnInfoMySql(cmd, databaseName, tableName, excludeAutoIncrement: false, jsonKeys: jsonKeys);
        return BuildSelectExpressionsMySql(columns);
    }

    private static string GetInsertColumnsMySql(IDbCommand cmd, string databaseName, string tableName, HashSet<string> jsonKeys)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');
        var columns = GetColumnInfoMySql(cmd, databaseName, tableName, excludeAutoIncrement: false, jsonKeys: jsonKeys);
        return string.Join(", ", columns.Select(c => $"`{c.Name}`"));
    }

    private static string GetUpdateColumnsMySql(IDbCommand cmd, string databaseName, string tableName, HashSet<string> jsonKeys)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');
        var columns = GetColumnInfoMySql(cmd, databaseName, tableName, excludeAutoIncrement: false, jsonKeys: jsonKeys);
        return string.Join(",", columns.Select(c =>
            IsJsonTypeMySql(c.DataType) ? $"J[`{c.Name}`]" : $"`{c.Name}`"));
    }

    private static string BuildMergeScriptMySql(IDbCommand cmd, string databaseName, string tableName,
        string tableData, string keyColumns, bool mergeUpdate, bool mergeDelete, bool tokenizeScripts, string mergeFilter, HashSet<string> jsonKeys)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');

        // Compose from fragment methods — same pattern as SQL Server and PostgreSQL.
        // MySQL allows explicit inserts into AUTO_INCREMENT columns without any special command
        // (unlike SQL Server's IDENTITY_INSERT or PostgreSQL's OVERRIDING VALUE).
        var insertColumns = GetInsertColumnsMySql(cmd, databaseName, tableName, jsonKeys);
        var selectExpressions = GetJsonSelectColumnsMySql(cmd, databaseName, tableName, jsonKeys);
        var jsonTableColumns = GetJsonColumnDefinitionsMySql(cmd, databaseName, tableName, jsonKeys);

        if (string.IsNullOrEmpty(insertColumns))
            throw new InvalidOperationException($"No columns found for table `{databaseName}`.`{tableName}` (or all columns are auto-increment/generated).");

        // Map standardized MergeType flags to MySQL strategies:
        // Insert only (no update, no delete) -> INSERT IGNORE
        // Insert/Update (update, no delete) -> INSERT ON DUPLICATE KEY UPDATE
        // Insert/Update/Delete (update and/or delete) -> Upsert + DELETE WHERE NOT EXISTS
        //   REPLACE INTO was used previously but it deletes+reinserts every matching row,
        //   breaking ON DELETE RESTRICT foreign keys.
        if (mergeDelete)
        {
            var updateColumns = GetUpdateColumnsMySql(cmd, databaseName, tableName, jsonKeys);
            var upsert = mergeUpdate
                ? BuildUpsertStatementMySql(databaseName, tableName, insertColumns, selectExpressions, jsonTableColumns, updateColumns, tableData, keyColumns, tokenizeScripts)
                : BuildInsertStatementMySql(databaseName, tableName, insertColumns, selectExpressions, jsonTableColumns, tableData, tokenizeScripts);
            var delete = BuildDeleteStatementMySql(databaseName, tableName, jsonTableColumns, keyColumns, mergeFilter);
            return upsert + "\n" + delete;
        }
        if (mergeUpdate)
        {
            var updateColumns = GetUpdateColumnsMySql(cmd, databaseName, tableName, jsonKeys);
            return BuildUpsertStatementMySql(databaseName, tableName, insertColumns, selectExpressions, jsonTableColumns, updateColumns, tableData, keyColumns, tokenizeScripts);
        }
        return BuildInsertStatementMySql(databaseName, tableName, insertColumns, selectExpressions, jsonTableColumns, tableData, tokenizeScripts);
    }

    private static List<MySqlColumnInfo> GetColumnInfoMySql(IDbCommand cmd, string databaseName, string tableName, bool excludeAutoIncrement, HashSet<string> jsonKeys = null)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');

        BindIdentifierParameters(cmd, ("@db", databaseName), ("@table", tableName));
        cmd.CommandText = $@"
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.DATETIME_PRECISION,
    c.COLUMN_TYPE,
    c.EXTRA,
    c.GENERATION_EXPRESSION
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE BINARY c.TABLE_SCHEMA = BINARY @db
  AND BINARY c.TABLE_NAME = BINARY @table
ORDER BY c.ORDINAL_POSITION;
";
        var columns = new List<MySqlColumnInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var extra = reader.GetString(reader.GetOrdinal("EXTRA"));
            var genExpr = reader.IsDBNull(reader.GetOrdinal("GENERATION_EXPRESSION"))
                ? null
                : reader.GetString(reader.GetOrdinal("GENERATION_EXPRESSION"));

            if (!string.IsNullOrWhiteSpace(genExpr)) continue;
            if (excludeAutoIncrement && extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase)) continue;

            var colName = reader.GetString(reader.GetOrdinal("COLUMN_NAME"));
            if (jsonKeys != null && !jsonKeys.Contains(colName)) continue;

            columns.Add(new MySqlColumnInfo
            {
                Name = colName,
                DataType = reader.GetString(reader.GetOrdinal("DATA_TYPE")),
                CharMaxLength = reader.IsDBNull(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH")),
                NumericPrecision = reader.IsDBNull(reader.GetOrdinal("NUMERIC_PRECISION"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("NUMERIC_PRECISION")),
                NumericScale = reader.IsDBNull(reader.GetOrdinal("NUMERIC_SCALE"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("NUMERIC_SCALE")),
                DatetimePrecision = reader.IsDBNull(reader.GetOrdinal("DATETIME_PRECISION"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("DATETIME_PRECISION")),
                ColumnType = reader.GetString(reader.GetOrdinal("COLUMN_TYPE")),
                IsAutoIncrement = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase)
            });
        }
        return columns;
    }

    private static string BuildDeleteStatementMySql(string databaseName, string tableName,
        string jsonTableColumns, string keyColumns, string mergeFilter)
    {
        var keyColNames = ParseKeyColumnsMySql(keyColumns);
        var sb = new StringBuilder();
        sb.AppendLine($"DELETE Target FROM `{databaseName}`.`{tableName}` Target");
        sb.AppendLine("WHERE NOT EXISTS (");
        sb.AppendLine("  SELECT 1 FROM JSON_TABLE(");
        sb.AppendLine("    @json_data,");
        sb.AppendLine("    '$[*]' COLUMNS (");
        sb.AppendLine($"      {jsonTableColumns}");
        sb.AppendLine("    )");
        sb.AppendLine("  ) AS jt");
        sb.AppendLine($"  WHERE {string.Join(" AND ", keyColNames.Select(k => $"Target.`{k}` = jt.`{k}`"))}");
        sb.Append(")");
        if (!string.IsNullOrWhiteSpace(mergeFilter))
            sb.Append($"\nAND ({mergeFilter})");
        sb.AppendLine(";");
        return sb.ToString();
    }

    private static string BuildUpsertStatementMySql(string databaseName, string tableName,
        string insertColumns, string selectExpressions, string jsonTableColumns, string updateColumns,
        string tableData, string keyColumns, bool tokenizeScripts)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"INSERT INTO `{databaseName}`.`{tableName}` ({insertColumns})");
        sb.AppendLine($"SELECT {selectExpressions}");
        sb.AppendLine("FROM JSON_TABLE(");
        sb.AppendLine("  @json_data,");
        sb.AppendLine("  '$[*]' COLUMNS (");
        sb.AppendLine($"    {jsonTableColumns}");
        sb.AppendLine("  )");
        sb.AppendLine(") AS jt");

        var keyColNames = ParseKeyColumnsMySql(keyColumns);
        var nonKeyColumns = updateColumns.Split(',')
            .Where(c =>
            {
                var colName = c.Trim().Trim('`');
                if (colName.StartsWith("J[")) colName = colName.Substring(2).TrimEnd(']').Trim('`');
                return !keyColNames.Contains(colName, StringComparer.OrdinalIgnoreCase);
            })
            .ToList();

        if (nonKeyColumns.Count > 0)
        {
            sb.AppendLine("ON DUPLICATE KEY UPDATE");
            var updateAssignments = nonKeyColumns.Select(c =>
            {
                var trimmed = c.Trim();
                if (trimmed.StartsWith("J["))
                {
                    var colName = trimmed.Substring(2).TrimEnd(']');
                    return $"  {colName} = IF(CAST(VALUES({colName}) AS JSON) = CAST(`{databaseName}`.`{tableName}`.{colName} AS JSON), `{databaseName}`.`{tableName}`.{colName}, VALUES({colName}))";
                }
                return $"  {trimmed} = VALUES({trimmed})";
            });
            sb.AppendLine(string.Join(",\n", updateAssignments) + ";");
        }
        else
        {
            var firstCol = insertColumns.Split(',')[0].Trim();
            sb.AppendLine($"ON DUPLICATE KEY UPDATE {firstCol} = VALUES({firstCol});");
        }

        return BuildWithJsonVariableMySql(tableName, tableData, sb.ToString(), tokenizeScripts);
    }

    private static string BuildInsertStatementMySql(string databaseName, string tableName,
        string insertColumns, string selectExpressions, string jsonTableColumns,
        string tableData, bool tokenizeScripts)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"INSERT IGNORE INTO `{databaseName}`.`{tableName}` ({insertColumns})");
        sb.AppendLine($"SELECT {selectExpressions}");
        sb.AppendLine("FROM JSON_TABLE(");
        sb.AppendLine("  @json_data,");
        sb.AppendLine("  '$[*]' COLUMNS (");
        sb.AppendLine($"    {jsonTableColumns}");
        sb.AppendLine("  )");
        sb.AppendLine(") AS jt;");

        return BuildWithJsonVariableMySql(tableName, tableData, sb.ToString(), tokenizeScripts);
    }

    private static string BuildWithJsonVariableMySql(string tableName, string tableData, string sqlStatement, bool tokenizeScripts)
    {
        var sb = new StringBuilder();
        if (tokenizeScripts)
        {
            sb.AppendLine($"SET @json_data = '{{{{{tableName}.tabledata}}}}';");
        }
        else
        {
            // Escape backslashes first (before single quotes) to prevent MySQL string literal
            // interpretation of \n, \t, \', etc. in JSON data containing XML, file paths,
            // or other content with backslashes.
            var escapedData = (tableData ?? "[]").Replace("\\", "\\\\").Replace("'", "''");
            sb.AppendLine($"SET @json_data = '{escapedData}';");
        }
        sb.AppendLine();
        sb.Append(sqlStatement);
        return sb.ToString();
    }

    private static string BuildJsonTableColumnsMySql(List<MySqlColumnInfo> columns)
    {
        var definitions = new List<string>();
        foreach (var col in columns)
        {
            var mysqlType = GetMySqlTypeForJsonTable(col);
            // Quote JSON path key with double quotes when it contains spaces or special characters
            var jsonPath = col.Name.Contains(' ') || col.Name.Contains('.') || col.Name.Contains('-')
                ? $"$.\"{col.Name}\""
                : $"$.{col.Name}";
            definitions.Add($"`{col.Name}` {mysqlType} PATH '{jsonPath}'");
        }
        return string.Join(",\n    ", definitions);
    }

    private static string GetMySqlTypeForJsonTable(MySqlColumnInfo col)
    {
        var dataType = col.DataType.ToLowerInvariant();

        return dataType switch
        {
            "tinyint" or "smallint" or "mediumint" or "int" or "integer" or "bigint" => dataType.ToUpperInvariant(),
            "decimal" or "numeric" => col.NumericPrecision.HasValue && col.NumericScale.HasValue
                ? $"DECIMAL({col.NumericPrecision},{col.NumericScale})"
                : "DECIMAL(65,30)",
            "float" => "FLOAT",
            "double" => "DOUBLE",
            "bit" => col.NumericPrecision.HasValue ? $"BIT({col.NumericPrecision})" : "BIT(1)",
            "date" => "DATE",
            "datetime" => col.DatetimePrecision.HasValue && col.DatetimePrecision > 0
                ? $"DATETIME({col.DatetimePrecision})"
                : "DATETIME",
            "timestamp" => col.DatetimePrecision.HasValue && col.DatetimePrecision > 0
                ? $"TIMESTAMP({col.DatetimePrecision})"
                : "TIMESTAMP",
            "time" => col.DatetimePrecision.HasValue && col.DatetimePrecision > 0
                ? $"TIME({col.DatetimePrecision})"
                : "TIME",
            "year" => "YEAR",
            "char" => col.CharMaxLength.HasValue
                ? $"CHAR({col.CharMaxLength})"
                : "CHAR(255)",
            "varchar" => col.CharMaxLength.HasValue
                ? $"VARCHAR({col.CharMaxLength})"
                : "VARCHAR(65535)",
            "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob"
                => "TEXT",
            "tinytext" or "text" or "mediumtext" or "longtext" => "TEXT",
            // Read ENUM/SET as text in JSON_TABLE: MySQL accepts the raw `enum(...)`/`set(...)`
            // column type here, but MariaDB rejects it. The extracted string value coerces back
            // into the real ENUM/SET column on INSERT, so VARCHAR is behavior-identical on MySQL
            // and portable to MariaDB. CHARACTER_MAXIMUM_LENGTH holds the longest member (ENUM)
            // or the full members-with-separators length (SET).
            "enum" or "set" => col.CharMaxLength.HasValue
                ? $"VARCHAR({col.CharMaxLength})"
                : "VARCHAR(65535)",
            "json" => "JSON",
            "geometry" or "point" or "linestring" or "polygon" or "multipoint"
                or "multilinestring" or "multipolygon" or "geometrycollection" => "TEXT",
            _ => "TEXT"
        };
    }

    private static bool IsGeometryTypeMySql(string dataType) => dataType.ToLowerInvariant() switch
    {
        "geometry" or "point" or "linestring" or "polygon" or "multipoint"
            or "multilinestring" or "multipolygon" or "geometrycollection" => true,
        _ => false
    };

    private static bool IsBinaryTypeMySql(string dataType) => dataType.ToLowerInvariant() switch
    {
        "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob" => true,
        _ => false
    };

    private static bool IsJsonTypeMySql(string dataType) =>
        dataType.Equals("json", StringComparison.OrdinalIgnoreCase);

    private static string BuildSelectExpressionsMySql(List<MySqlColumnInfo> columns)
    {
        return string.Join(", ", columns.Select(BuildSingleSelectExpressionMySql));
    }

    private static string BuildSingleSelectExpressionMySql(MySqlColumnInfo c)
    {
        return IsGeometryTypeMySql(c.DataType)
            ? $"ST_GeomFromText(`{c.Name}`)"
            : IsBinaryTypeMySql(c.DataType)
                ? $"FROM_BASE64(`{c.Name}`)"
                : $"`{c.Name}`";
    }

    private static List<string> ParseKeyColumnsMySql(string keyColumns)
    {
        if (string.IsNullOrWhiteSpace(keyColumns))
            return new List<string>();

        return keyColumns.Split(',')
            .Select(c => c.Trim().Trim('`'))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();
    }

    internal class MySqlColumnInfo
    {
        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
        public long? CharMaxLength { get; init; }
        public long? NumericPrecision { get; init; }
        public long? NumericScale { get; init; }
        public int? DatetimePrecision { get; init; }
        public string ColumnType { get; init; } = "";
        public bool IsAutoIncrement { get; init; }
    }

    #endregion
}
