// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Schema.DataAccess;
using Schema.Delivery;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace DataTongs;

public class DataTongs
{
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private readonly Platform _platform;

    public DataTongs(Platform platform)
    {
        _platform = platform;
    }

    private IDbConnection GetConnection(string targetDb)
    {
        var connectionStringOverride = CommandLineParser.ValueOfSwitch("ConnectionString", null);
        if (!string.IsNullOrEmpty(connectionStringOverride))
        {
            if (_platform.GetBasePlatform() == Platform.MySQL &&
                !connectionStringOverride.Contains("AllowUserVariables", StringComparison.OrdinalIgnoreCase))
            {
                _progressLog.Warn("Connection string override for MySQL does not contain AllowUserVariables=true. " +
                                  "This is required for SchemaSmith stored procedures that use PREPARE/EXECUTE.");
            }
            var overrideFactory = GetConnectionFactory();
            var overrideConnection = overrideFactory.GetDbConnection(connectionStringOverride);
            overrideConnection.Open();
            return overrideConnection;
        }

        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var server = config["Source:Server"] ?? config["Target:Server"];
        var user = config["Source:User"] ?? config["Target:User"];
        var password = config["Source:Password"] ?? config["Target:Password"];
        var port = config["Source:Port"] ?? config["Target:Port"];
        var connectionProperties = ConnectionString.ReadProperties(config, "Source:ConnectionProperties");
        if (connectionProperties.Count == 0)
            connectionProperties = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        CommandLineParser.ApplyTransportSecuritySwitch(_platform, connectionProperties);
        var integratedSecurity = string.Equals(config["Source:IntegratedSecurity"] ?? config["Target:IntegratedSecurity"], "true", StringComparison.OrdinalIgnoreCase);

        var connectionString = ConnectionString.Build(_platform, server, targetDb, user, password, port, connectionProperties, integratedSecurity: integratedSecurity);
        var connectionFactory = GetConnectionFactory();
        var connection = connectionFactory.GetDbConnection(connectionString);
        connection.Open();
        return connection;
    }

    private IDbConnectionFactory GetConnectionFactory()
    {
        return _platform.GetBasePlatform() switch
        {
            Platform.SqlServer => SqlServerConnectionFactory.GetFromFactory(),
            Platform.PostgreSQL => PostgreSqlConnectionFactory.GetFromFactory(),
            Platform.MySQL => MySqlConnectionFactory.GetFromFactory(),
            _ => throw new Exception($"Unsupported platform: {_platform}")
        };
    }

    public void CastData()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();

        var disableTriggers = config["ShouldCast:DisableTriggers"]?.ToLower() == "true";
        var tokenizeScripts = config["ShouldCast:TokenizeScripts"]?.ToLower() != "false";
        var mergeUpdate = config["ShouldCast:MergeUpdate"]?.ToLower() != "false";
        var mergeDelete = config["ShouldCast:MergeDelete"]?.ToLower() != "false";

        // PostgreSQL-specific options
        var disableRules = config["ShouldCast:DisableRules"]?.ToLower() == "true";
        var updateDescendents = config["ShouldCast:UpdateDescendents"]?.ToLower() != "false";

        var outputContents = config["ShouldCast:OutputContentFiles"]?.ToLower() != "false";
        var outputScripts = config["ShouldCast:OutputScripts"]?.ToLower() != "false";
        var contentsPath = config["ContentPath"] ?? ".";
        var scriptPath = config["ScriptPath"] ?? ".";
        var configureDataDelivery = CommandLineParser.ContainsSwitch("ConfigureDataDelivery")
            || config["ShouldCast:ConfigureDataDelivery"]?.ToLower() == "true";
        var templatePath = CommandLineParser.ValueOfSwitch("TemplatePath", null)
            ?? config["TemplatePath"];
        var sourceSchemaSetting = config["Source:Schema"] ?? "";

        // Always resolve template root — schema-template mode detection (§8.1) must run even
        // when Source.Schema is unset, so we can error out cleanly if the user pointed at a
        // schema template without setting Source.Schema. Walking up is a single Exists check
        // per ancestor and runs once.
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            templatePath = FindTemplateRootPath(contentsPath);
            if (string.IsNullOrWhiteSpace(templatePath) && configureDataDelivery)
            {
                _progressLog.Warn($"  ContentPath '{contentsPath}' is not within a template (no Template.json found walking up). Disabling ConfigureDataDelivery.");
                configureDataDelivery = false;
            }
        }

        // Two-signal schema-template mode detection (design §8.1):
        //   signal 1 = target Template.json has SchemaIdentificationScript
        //   signal 2 = Source.Schema set in DataTongs.settings.json
        var targetTemplateIsSchemaTemplate = !string.IsNullOrWhiteSpace(templatePath)
            && TargetTemplateHasSchemaIdentificationScript(templatePath);
        var sourceSchemaSet = !string.IsNullOrWhiteSpace(sourceSchemaSetting);
        var schemaTemplateMode = targetTemplateIsSchemaTemplate && sourceSchemaSet;

        if (targetTemplateIsSchemaTemplate && !sourceSchemaSet)
        {
            throw new InvalidOperationException(
                "Target template is a schema template (has SchemaIdentificationScript), but Source.Schema " +
                "is not set in DataTongs.settings.json. Set Source.Schema to the name of the source schema " +
                "you are extracting data from (typically a seed-tenant schema), or point DataTongs at a " +
                "regular template instead.");
        }

        if (!targetTemplateIsSchemaTemplate && sourceSchemaSet)
        {
            _progressLog.Warn($"  Source.Schema='{sourceSchemaSetting}' is set, but the target template is not a schema template " +
                              $"(no SchemaIdentificationScript). Source.Schema is ignored in regular extraction mode.");
        }

        if (outputContents) DirectoryWrapper.GetFromFactory().CreateDirectory(contentsPath);
        if (outputScripts) DirectoryWrapper.GetFromFactory().CreateDirectory(scriptPath);

        var sourceDb = config["Source:Database"];
        if (string.IsNullOrEmpty(sourceDb)) throw new Exception("Source database is required");

        var tables = config.GetSection("Tables")
            .GetChildren()
            .Select(t => new TableConfig
            {
                TableName = t["Name"] ?? "",
                KeyColumns = t["KeyColumns"] ?? "",
                SelectColumns = t["SelectColumns"] ?? "",
                Filter = t["Filter"] ?? "",
                MergeType = t["MergeType"] ?? "",
                VariantName = t["VariantName"] ?? ""
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.TableName))
            .ToList();

        // Schema-template mode: Tables[N].Name must be unqualified — Source.Schema is the
        // single source schema for every entry. Reject qualified names with a directive
        // error per §8.3.
        if (schemaTemplateMode)
        {
            for (var i = 0; i < tables.Count; i++)
            {
                if (tables[i].TableName.Contains('.'))
                {
                    throw new InvalidOperationException(
                        $"Table names in schema-template mode must be unqualified. " +
                        $"Tables[{i}].Name = \"{tables[i].TableName}\". " +
                        $"Use \"{tables[i].TableName.Split('.').Last()}\" and let Source.Schema specify the source. " +
                        $"Cross-schema data extraction (e.g., from dbo) goes in a separate DataTongs run targeting a regular template.");
                }
            }
        }

        _progressLog.Info("Starting DataTongs...");
        _progressLog.Info($"  Platform: {_platform}");
        _progressLog.Info($"  Source Database: {sourceDb}");
        if (schemaTemplateMode)
            _progressLog.Info($"  Schema-template extraction mode: source schema = '{sourceSchemaSetting}', destination refs use {{{{SchemaName}}}}.");

        if (tables.Count == 0)
        {
            _progressLog.Warn("No tables configured for data extraction.");
            _progressLog.Info("DataTongs completed (no tables to process).");
            return;
        }

        using var sourceConnection = GetConnection(sourceDb);
        var cmd = sourceConnection.CreateCommand();

        var tablesProcessed = 0;
        var errors = 0;

        foreach (var table in tables)
        {
            try
            {
                _progressLog.Info($"  Casting data for: {table.TableName}");

                var parts = ParseTableName(table.TableName, sourceDb);
                // In schema-template mode the user supplies unqualified Tables[N].Name and
                // Source.Schema names the source schema. Override the parsed schema with the
                // settings value so catalog queries hit the right source rows.
                var tableSchema = schemaTemplateMode ? sourceSchemaSetting : parts.Schema;
                var tableName = parts.Name;

                // MySQL uses the database name for INFORMATION_SCHEMA queries, not a schema prefix
                var querySchema = _platform.GetBasePlatform() == Platform.MySQL ? sourceDb : tableSchema;
                var displayName = FormatTableName(tableSchema, tableName);
                // Schema-template mode emits unqualified filenames: Customers.tabledata,
                // Populate Customers.sql. The merge script's content-file token must match.
                var encodedDisplayName = schemaTemplateMode || string.IsNullOrEmpty(tableSchema)
                    ? FileNameEncoder.Encode(tableName)
                    : $"{FileNameEncoder.Encode(tableSchema)}.{FileNameEncoder.Encode(tableName)}";

                if (!TableExists(cmd, querySchema, tableName))
                {
                    _progressLog.Error($"  Table {displayName} does not exist in source database. Skipping table.");
                    continue;
                }

                var keyColumns = string.IsNullOrWhiteSpace(table.KeyColumns)
                    ? MergeScriptHelper.GetKeyColumns(_platform, cmd, querySchema, tableName)
                    : table.KeyColumns;

                if (string.IsNullOrWhiteSpace(keyColumns))
                {
                    _progressLog.Error($"  No match columns found for {displayName}. Skipping table.");
                    continue;
                }

                if (!IsValidKeyColumns(keyColumns))
                {
                    _progressLog.Error($"  Invalid KeyColumns '{keyColumns}' for {displayName}. Expected comma-separated column names (e.g., 'Col1,Col2'). Skipping table.");
                    continue;
                }

                var orderColumns = FormatOrderColumns(keyColumns);

                LogUnsupportedColumns(cmd, _platform, querySchema, tableName);

                string tableData;
                string selectColumns = null;
                if (_platform.GetBasePlatform() == Platform.MySQL)
                {
                    tableData = GetTableDataJsonMySql(cmd, querySchema, tableName, orderColumns, table.Filter, table.SelectColumns);
                }
                else
                {
                    selectColumns = string.IsNullOrWhiteSpace(table.SelectColumns)
                        ? GetSelectColumns(cmd, querySchema, tableName)
                        : table.SelectColumns;
                    tableData = GetTableDataJson(cmd, selectColumns, tableSchema, tableName, orderColumns, table.Filter);
                }

                if (string.IsNullOrEmpty(tableData) || tableData == "null" || tableData == "[]")
                {
                    _progressLog.Info($"    No rows found for {table.TableName}. Skipping merge script.");

                    if (outputContents)
                    {
                        var emptyContentFilePath = Path.Combine(contentsPath, $"{encodedDisplayName}.tabledata");
                        _progressLog.Info($"    Writing contents to : {emptyContentFilePath}");
                        FileWrapper.GetFromFactory().WriteAllText(emptyContentFilePath, "[]");
                    }

                    tablesProcessed++;
                    continue;
                }
                else
                {
                    var rowCount = CountRows(tableData);
                    _progressLog.Info($"    Extracted {rowCount} row(s) from {table.TableName}.");
                }

                string contentFilePath = null;
                if (outputContents)
                {
                    contentFilePath = Path.Combine(contentsPath, $"{encodedDisplayName}.tabledata");
                    _progressLog.Info($"    Writing contents to : {contentFilePath}");
                    FileWrapper.GetFromFactory().WriteAllText(contentFilePath, tableData);
                }

                if (configureDataDelivery && !string.IsNullOrWhiteSpace(templatePath) && !string.IsNullOrEmpty(contentFilePath))
                {
                    DataDeliveryConfiguratorImpl.GetFromFactory().Configure(new DataDeliveryConfiguratorContext
                    {
                        TemplateRootPath = templatePath,
                        Platform = _platform.ToString(),
                        TableSchema = tableSchema,
                        TableName = tableName,
                        ContentFilePath = contentFilePath,
                        KeyColumns = keyColumns,
                        DefaultMergeType = mergeDelete ? "Insert/Update/Delete" : mergeUpdate ? "Insert/Update" : "Insert",
                        DisableTriggers = disableTriggers,
                        DisableRules = disableRules,
                        UpdateDescendents = updateDescendents,
                        MergeTypeOverride = table.MergeType,
                        KeyColumnsOverride = table.KeyColumns,
                        MergeFilterOverride = table.Filter,
                        VariantName = table.VariantName,
                        ProgressLog = _progressLog.Info,
                        WarningLog = _progressLog.Warn
                    });
                }

                if (!outputScripts) { tablesProcessed++; continue; }

                var destSchemaOverride = schemaTemplateMode ? "{{SchemaName}}" : null;
                var mergeSQL = MergeScriptHelper.BuildMergeScript(_platform, cmd, querySchema, tableName, tableData,
                    keyColumns, mergeUpdate, mergeDelete, disableTriggers, tokenizeScripts, table.Filter,
                    disableRules, updateDescendents, destSchemaOverride);

                var scriptFilePath = Path.Combine(scriptPath, $"Populate {encodedDisplayName}.sql");
                _progressLog.Info($"    Writing merge script to : {scriptFilePath}");
                FileWrapper.GetFromFactory().WriteAllText(scriptFilePath, mergeSQL);
                tablesProcessed++;
            }
            catch (Exception ex)
            {
                errors++;
                _progressLog.Error($"  Error processing table {table.TableName}: {ex.Message}");
            }
        }

        sourceConnection.Close();
        _progressLog.Info("=== DataTongs Summary ===");
        _progressLog.Info($"  Tables processed: {tablesProcessed}");
        if (errors > 0) _progressLog.Info($"  Errors: {errors}");
        _progressLog.Info("DataTongs completed.");
    }

    /// <summary>
    /// Returns true when the <c>Template.json</c> at <paramref name="templateRoot"/> has a
    /// non-empty <c>SchemaIdentificationScript</c> field — i.e. the target template is a
    /// schema template (design §8.1, signal 1). Reads the file directly via JObject rather
    /// than going through <c>Template.Load</c> because we only need one scalar field and
    /// the full load path requires the Product to be set, which DataTongs does not have
    /// in its calling context.
    /// </summary>
    internal static bool TargetTemplateHasSchemaIdentificationScript(string templateRoot)
    {
        if (string.IsNullOrWhiteSpace(templateRoot)) return false;

        var templateJsonPath = Path.Combine(templateRoot, "Template.json");
        var fileWrapper = FileWrapper.GetFromFactory();
        if (!fileWrapper.Exists(templateJsonPath)) return false;

        try
        {
            var json = fileWrapper.ReadAllText(templateJsonPath);
            if (string.IsNullOrWhiteSpace(json)) return false;
            var obj = JObject.Parse(json);
            var scriptValue = obj["SchemaIdentificationScript"]?.ToString();
            return !string.IsNullOrWhiteSpace(scriptValue);
        }
        catch
        {
            // Malformed Template.json is not our problem to diagnose here — the engine
            // surfaces a richer error at quench time. Return false so DataTongs falls
            // back to regular mode.
            return false;
        }
    }

    /// <summary>
    /// Walks up from <paramref name="contentPath"/> (a Tables-sibling directory such as
    /// a template's Content/ folder) looking for the nearest ancestor containing a
    /// Template.json file, and returns that ancestor. Returns null when no Template.json
    /// is found — typically because the user is running DataTongs outside of a template.
    /// </summary>
    internal static string FindTemplateRootPath(string contentPath)
    {
        if (string.IsNullOrWhiteSpace(contentPath)) return null;

        var dir = Path.GetFullPath(contentPath);
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "Template.json");
            if (FileWrapper.GetFromFactory().Exists(candidate))
                return dir;

            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) return null;
            dir = parent;
        }

        return null;
    }

    #region Table Name Parsing

    internal (string Schema, string Name) ParseTableName(string fullName, string databaseName = null)
    {
        var parts = fullName.Split('.').Select(p => p.Trim()).ToArray();

        // MySQL has no schema concept — database.table is the only dotted form.
        // Strip the database prefix if present; we use the connection's database context instead.
        if (_platform.GetBasePlatform() == Platform.MySQL)
            return ("", parts.Length == 2 ? parts[1] : parts[0]);

        if (parts.Length == 2)
            return (parts[0], parts[1]);

        var defaultSchema = _platform.GetBasePlatform() switch
        {
            Platform.SqlServer => "dbo",
            Platform.PostgreSQL => "public",
            _ => ""
        };

        return (defaultSchema, parts[0]);
    }

    /// <summary>
    /// Formats schema.table for filenames and display. MySQL tables have no schema prefix.
    /// </summary>
    internal string FormatTableName(string tableSchema, string tableName) =>
        string.IsNullOrEmpty(tableSchema) ? tableName : $"{tableSchema}.{tableName}";

    #endregion

    #region Order Columns Formatting

    internal string FormatOrderColumns(string keyColumns)
    {
        return _platform.GetBasePlatform() switch
        {
            Platform.SqlServer => string.Join(",", keyColumns.Split(',')
                .Select(c => $"[{c.Trim().Trim(']', '[', '*')}]")),
            Platform.PostgreSQL => string.Join(",", keyColumns.Split(',')
                .Select(c => $"\"{c.Trim().Trim('\"', '*')}\"")),
            Platform.MySQL => string.Join(",", keyColumns.Split(',')
                .Select(c => $"`{c.Trim().Trim('`', '*')}`")),
            _ => keyColumns
        };
    }

    internal static bool IsValidKeyColumns(string keyColumns)
    {
        if (string.IsNullOrWhiteSpace(keyColumns)) return false;

        // Reject JSON array syntax: starts with ["
        var trimmed = keyColumns.TrimStart();
        if (trimmed.StartsWith("[\"") || trimmed == "[]") return false;

        // Split on comma and verify each segment is non-empty after trimming
        var segments = keyColumns.Split(',');
        return segments.All(s => !string.IsNullOrWhiteSpace(s));
    }

    #endregion

    #region Table Existence Checks

    internal bool TableExists(IDbCommand cmd, string schemaOrDb, string tableName)
    {
        return _platform.GetBasePlatform() switch
        {
            Platform.SqlServer => TableExistsSqlServer(cmd, schemaOrDb, tableName),
            Platform.PostgreSQL => TableExistsPostgreSql(cmd, schemaOrDb, tableName),
            Platform.MySQL => TableExistsMySql(cmd, schemaOrDb, tableName),
            _ => throw new Exception($"Unsupported platform: {_platform}")
        };
    }

    private static bool TableExistsSqlServer(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $"SELECT CAST(CASE WHEN OBJECT_ID('{tableSchema.Replace("'", "''")}.{tableName.Replace("'", "''")}') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        return cmd.ExecuteScalar() as bool? ?? false;
    }

    private static bool TableExistsPostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $"SELECT EXISTS (SELECT * FROM pg_class tbl JOIN pg_namespace ns ON ns.oid = tbl.relnamespace WHERE ns.nspname = '{tableSchema.Replace("'", "''")}' AND tbl.relname = '{tableName.Replace("'", "''")}');";
        return cmd.ExecuteScalar() as bool? ?? false;
    }

    private static bool TableExistsMySql(IDbCommand cmd, string databaseName, string tableName)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');
        cmd.CommandText = $@"
SELECT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE BINARY TABLE_SCHEMA = BINARY '{databaseName.Replace("'", "''")}'
      AND BINARY TABLE_NAME = BINARY '{tableName.Replace("'", "''")}'
      AND TABLE_TYPE = 'BASE TABLE'
);";
        var result = cmd.ExecuteScalar();
        return Convert.ToInt64(result) == 1;
    }

    #endregion

    #region Select Columns

    internal string GetSelectColumns(IDbCommand cmd, string schemaOrDb, string tableName)
    {
        return _platform.GetBasePlatform() switch
        {
            Platform.SqlServer => GetSelectColumnsSqlServer(cmd, schemaOrDb, tableName),
            Platform.PostgreSQL => GetSelectColumnsPostgreSql(cmd, schemaOrDb, tableName),
            Platform.MySQL => GetSelectColumnsMySql(cmd, schemaOrDb, tableName),
            _ => throw new Exception($"Unsupported platform: {_platform}")
        };
    }

    private static string GetSelectColumnsSqlServer(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT STRING_AGG(CASE WHEN c.DATA_TYPE IN ('GEOGRAPHY', 'GEOMETRY')
                       THEN '[' + c.COLUMN_NAME + '].ToString() AS [' + c.COLUMN_NAME + '], [' + c.COLUMN_NAME + '].STSrid AS [' + c.COLUMN_NAME + '.STSrid]'
                       WHEN c.DATA_TYPE = 'HIERARCHYID'
                       THEN '[' + c.COLUMN_NAME + '].ToString() AS [' + c.COLUMN_NAME + ']'
                       ELSE '[' + c.COLUMN_NAME + ']' END, ',') WITHIN GROUP (ORDER BY c.COLUMN_NAME)
  FROM INFORMATION_SCHEMA.COLUMNS c
  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME) AND sc.[name] = C.COLUMN_NAME
  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                 AND cc.[object_id] = OBJECT_ID(C.TABLE_SCHEMA + '.' + C.TABLE_NAME)
  WHERE c.TABLE_SCHEMA = '{tableSchema.Replace("'", "''")}' AND c.TABLE_NAME = '{tableName.Replace("'", "''")}'
    AND cc.[name] IS NULL
    AND sc.is_rowguidcol = 0
    AND c.DATA_TYPE NOT IN ('sql_variant', 'rowversion', 'timestamp')
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string GetSelectColumnsPostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT STRING_AGG(
    CASE WHEN c.udt_name IN ('geometry','geography','point','linestring','polygon',
                              'multipoint','multilinestring','multipolygon','geometrycollection')
         THEN 'ST_AsText(""' || c.column_name || '"") AS ""' || c.column_name || '""'
         WHEN c.udt_name = 'bytea'
         THEN 'encode(""' || c.column_name || '"", ''base64'') AS ""' || c.column_name || '""'
         WHEN LEFT(c.udt_name, 1) = '_'
         THEN 'ARRAY_TO_STRING(""' || c.column_name || '"", ''*,*'', ''*NULL_VALUE_REPRESENTATION*'') AS ""' || c.column_name || '""'
         ELSE '""' || c.column_name || '""' END, ',' ORDER BY c.column_name)
  FROM information_schema.columns c
  JOIN pg_class cls ON cls.relname = c.table_name
  JOIN pg_namespace ns ON ns.oid = cls.relnamespace AND ns.nspname = c.table_schema
  JOIN pg_attribute a ON a.attrelid = cls.oid AND a.attname = c.column_name AND NOT a.attisdropped AND a.attgenerated = ''
  WHERE c.table_schema = '{tableSchema.Replace("'", "''")}'
    AND c.table_name = '{tableName.Replace("'", "''")}'
    AND c.udt_name NOT IN ('tsvector', 'tsquery', 'money', 'box', 'circle', 'line', 'lseg', 'path')
    AND NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                    WHERE t.typname = c.udt_name AND n.nspname = c.udt_schema AND t.typtype = 'c');
";
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string GetSelectColumnsMySql(IDbCommand cmd, string databaseName, string tableName)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');
        cmd.CommandText = $@"
SELECT GROUP_CONCAT(
    CASE
        WHEN c.DATA_TYPE IN ('binary','varbinary','tinyblob','blob','mediumblob','longblob')
            THEN CONCAT('REPLACE(REPLACE(TO_BASE64(`', c.COLUMN_NAME, '`), ''\n'', ''''), ''\r'', '''') AS `', c.COLUMN_NAME, '`')
        WHEN c.DATA_TYPE IN ('geometry','point','linestring','polygon','multipoint','multilinestring','multipolygon','geometrycollection')
            THEN CONCAT('ST_AsText(`', c.COLUMN_NAME, '`) AS `', c.COLUMN_NAME, '`')
        WHEN c.DATA_TYPE = 'bit'
            THEN CONCAT('CAST(`', c.COLUMN_NAME, '` AS UNSIGNED) AS `', c.COLUMN_NAME, '`')
        WHEN c.DATA_TYPE IN ('date')
            THEN CONCAT('DATE_FORMAT(`', c.COLUMN_NAME, '`, ''%Y-%m-%d'') AS `', c.COLUMN_NAME, '`')
        WHEN c.DATA_TYPE IN ('datetime','timestamp')
            THEN CONCAT('DATE_FORMAT(`', c.COLUMN_NAME, '`, ''%Y-%m-%dT%H:%i:%s'') AS `', c.COLUMN_NAME, '`')
        WHEN c.DATA_TYPE = 'time'
            THEN CONCAT('TIME_FORMAT(`', c.COLUMN_NAME, '`, ''%H:%i:%s'') AS `', c.COLUMN_NAME, '`')
        ELSE CONCAT('`', c.COLUMN_NAME, '`')
    END
    ORDER BY c.ORDINAL_POSITION SEPARATOR ',')
  FROM INFORMATION_SCHEMA.COLUMNS c
  WHERE BINARY c.TABLE_SCHEMA = BINARY '{databaseName.Replace("'", "''")}'
    AND BINARY c.TABLE_NAME = BINARY '{tableName.Replace("'", "''")}'
    AND (c.GENERATION_EXPRESSION IS NULL OR c.GENERATION_EXPRESSION = '')
";
        return cmd.ExecuteScalar()?.ToString();
    }

    #endregion

    #region Table Data JSON Extraction

    internal string GetTableDataJson(IDbCommand cmd, string selectColumns, string schemaOrDb,
        string tableName, string orderColumns, string filter)
    {
        return _platform.GetBasePlatform() switch
        {
            Platform.SqlServer => GetTableDataJsonSqlServer(cmd, selectColumns, schemaOrDb, tableName, orderColumns, filter),
            Platform.PostgreSQL => GetTableDataJsonPostgreSql(cmd, selectColumns, schemaOrDb, tableName, orderColumns, filter),
            Platform.MySQL => GetTableDataJsonMySql(cmd, schemaOrDb, tableName, orderColumns, filter, null),
            _ => throw new Exception($"Unsupported platform: {_platform}")
        };
    }

    private static string GetTableDataJsonSqlServer(IDbCommand cmd, string selectColumns, string tableSchema,
        string tableName, string orderColumns, string filter)
    {
        cmd.CommandText = $@"
SELECT CAST((
SELECT {selectColumns}
  FROM [{Identifier.EscapeDelimited(tableSchema, Platform.SqlServer)}].[{Identifier.EscapeDelimited(tableName, Platform.SqlServer)}] WITH (NOLOCK)
  {(string.IsNullOrWhiteSpace(filter) ? "" : $"WHERE {filter}")}
  ORDER BY {orderColumns}
  FOR JSON AUTO) AS NVARCHAR(MAX))
";
        return FormatJsonResult(cmd.ExecuteScalar()?.ToString() ?? "");
    }

    private static string GetTableDataJsonPostgreSql(IDbCommand cmd, string selectColumns, string tableSchema,
        string tableName, string orderColumns, string filter)
    {
        cmd.CommandText = $@"
SELECT JSON_AGG(ROW_TO_JSON(tbl))
  FROM(SELECT {selectColumns}
         FROM ""{Identifier.EscapeDelimited(tableSchema, Platform.PostgreSQL)}"".""{Identifier.EscapeDelimited(tableName, Platform.PostgreSQL)}""
{(string.IsNullOrWhiteSpace(filter) ? "" : $"         WHERE {filter}")}
         ORDER BY {orderColumns}) tbl
";
        return FormatJsonResult(cmd.ExecuteScalar()?.ToString() ?? "");
    }

    private string GetTableDataJsonMySql(IDbCommand cmd, string databaseName,
        string tableName, string orderColumns, string filter, string configSelectColumns)
    {
        databaseName = databaseName.Trim().Trim('`');
        tableName = tableName.Trim().Trim('`');

        // Get structured column info for type-aware JSON generation
        List<ColumnInfo> columns;
        if (!string.IsNullOrWhiteSpace(configSelectColumns))
        {
            columns = configSelectColumns.Split(',')
                .Select(c => c.Trim().Trim('`'))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => new ColumnInfo { Name = c, DataType = "varchar" })
                .ToList();
        }
        else
        {
            columns = GetMySqlColumnInfo(cmd, databaseName, tableName);
        }

        var jsonObjectArgs = columns.Select(c => FormatColumnForJsonObject(c)).ToList();
        var jsonObjectClause = string.Join(",\n            ", jsonObjectArgs);
        var whereClause = string.IsNullOrWhiteSpace(filter) ? "" : $"WHERE {filter}";

        cmd.CommandText = $@"
SELECT JSON_ARRAYAGG(
        JSON_OBJECT(
            {jsonObjectClause}
        )
    ) AS json_data
FROM `{Identifier.EscapeDelimited(databaseName, Platform.MySQL)}`.`{Identifier.EscapeDelimited(tableName, Platform.MySQL)}`
{whereClause}
ORDER BY {orderColumns};";

        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? "[]";
    }

    internal static List<ColumnInfo> GetMySqlColumnInfo(IDbCommand cmd, string databaseName, string tableName)
    {
        cmd.CommandText = $@"
SELECT c.COLUMN_NAME, c.DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE BINARY c.TABLE_SCHEMA = BINARY '{databaseName.Replace("'", "''")}'
  AND BINARY c.TABLE_NAME = BINARY '{tableName.Replace("'", "''")}'
  AND (c.GENERATION_EXPRESSION IS NULL OR c.GENERATION_EXPRESSION = '')
ORDER BY c.ORDINAL_POSITION;";

        var columns = new List<ColumnInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1)
            });
        }
        return columns;
    }

    internal static string FormatColumnForJsonObject(ColumnInfo column)
    {
        var quotedName = $"'{column.Name.Replace("'", "''")}'";
        var columnRef = $"`{Identifier.EscapeDelimited(column.Name, Platform.MySQL)}`";
        var dataType = column.DataType.ToLowerInvariant();

        return dataType switch
        {
            "date" => $"{quotedName}, DATE_FORMAT({columnRef}, '%Y-%m-%d')",
            "datetime" or "timestamp" => $"{quotedName}, DATE_FORMAT({columnRef}, '%Y-%m-%dT%H:%i:%s')",
            "time" => $"{quotedName}, TIME_FORMAT({columnRef}, '%H:%i:%s')",
            "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob"
                => $"{quotedName}, REPLACE(REPLACE(TO_BASE64({columnRef}), '\n', ''), '\r', '')",
            "geometry" or "point" or "linestring" or "polygon" or "multipoint"
                or "multilinestring" or "multipolygon" or "geometrycollection"
                => $"{quotedName}, ST_AsText({columnRef})",
            "bit" => $"{quotedName}, CAST({columnRef} AS UNSIGNED)",
            _ => $"{quotedName}, {columnRef}"
        };
    }

    internal static string FormatJsonResult(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return "";
        return rawJson.Replace("}, {", "},\r\n{").Replace("},{", "},\r\n{").Replace("[{", "[\r\n{").Replace("}]", "}\r\n]");
    }

    internal static int CountRows(string tableDataJson) => JArray.Parse(tableDataJson).Count;

    #endregion

    #region Unsupported Column Warnings

    internal void LogUnsupportedColumns(IDbCommand cmd, Platform platform, string schema, string table)
    {
        switch (platform.GetBasePlatform())
        {
            case Platform.SqlServer:
                LogUnsupportedColumnsSqlServer(cmd, schema, table);
                break;
            case Platform.PostgreSQL:
                LogUnsupportedColumnsPostgreSql(cmd, schema, table);
                break;
        }
    }

    private void LogUnsupportedColumnsSqlServer(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT c.COLUMN_NAME, c.DATA_TYPE
  FROM INFORMATION_SCHEMA.COLUMNS c
  WHERE c.TABLE_SCHEMA = '{tableSchema.Replace("'", "''")}' AND c.TABLE_NAME = '{tableName.Replace("'", "''")}'
    AND c.DATA_TYPE IN ('sql_variant', 'rowversion', 'timestamp')
";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var colName = reader.GetString(0);
            var typeName = reader.GetString(1);
            _progressLog.Warn($"Column {tableSchema}.{tableName}.{colName} has type {typeName} which is not supported for data delivery — skipping");
        }
    }

    private void LogUnsupportedColumnsPostgreSql(IDbCommand cmd, string tableSchema, string tableName)
    {
        cmd.CommandText = $@"
SELECT c.column_name, c.udt_name
  FROM information_schema.columns c
  JOIN pg_class cls ON cls.relname = c.table_name
  JOIN pg_namespace ns ON ns.oid = cls.relnamespace AND ns.nspname = c.table_schema
  JOIN pg_attribute a ON a.attrelid = cls.oid AND a.attname = c.column_name AND NOT a.attisdropped AND a.attgenerated = ''
  WHERE c.table_schema = '{tableSchema.Replace("'", "''")}' AND c.table_name = '{tableName.Replace("'", "''")}'
    AND (c.udt_name IN ('tsvector', 'tsquery', 'money', 'box', 'circle', 'line', 'lseg', 'path')
         OR EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                    WHERE t.typname = c.udt_name AND n.nspname = c.udt_schema AND t.typtype = 'c'))
";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var colName = reader.GetString(0);
            var typeName = reader.GetString(1);
            _progressLog.Warn($"Column {tableSchema}.{tableName}.{colName} has type {typeName} which is not supported for data delivery — skipping");
        }
    }

    #endregion

    internal class TableConfig
    {
        public string TableName { get; set; } = "";
        public string KeyColumns { get; set; } = "";
        public string SelectColumns { get; set; } = "";
        public string Filter { get; set; } = "";
        public string MergeType { get; set; } = "";
        public string VariantName { get; set; } = "";
    }

    internal class ColumnInfo
    {
        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
    }
}
