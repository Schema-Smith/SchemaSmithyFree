// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SchemaTongs;

public class SchemaTongs
{
    internal readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private string _productPath = "";
    internal string _templatePath = "";
    private bool _includeTables;
    private bool _includeSchemas;
    private bool _includeUserDefinedTypes;
    private bool _includeUserDefinedFunctions;
    private bool _includeViews;
    private bool _includeStoredProcedures;
    private bool _includeTableTriggers;
    private bool _includeFullTextCatalogs;
    private bool _includeFullTextStopLists;
    private bool _includeDDLTriggers;
    private bool _includeXmlSchemaCollections;
    private bool _includeIndexedViews;
    private bool _scriptDynamicDependencyRemovalForFunctions;
    private string[] _objectsToCast = [];
    private OrphanHandlingMode _orphanHandlingMode = OrphanHandlingMode.Detect;
    private CheckConstraintStyle _checkConstraintStyle;
    internal bool _validateScripts;
    internal bool _saveInvalidScripts = true;
    internal readonly Dictionary<string, ExtractionFileIndex> _folderIndexes = new();
    internal readonly List<(string Folder, string FileName, string Error)> _invalidObjects = new();

    private void BuildFileIndexes()
    {
        var folderNames = new[] { "Tables", "Schemas", "DataTypes", "Functions", "Views",
            "Procedures", "Triggers", "FullTextCatalogs", "FullTextStopLists",
            "DDLTriggers", "XMLSchemaCollections", "Indexed Views", "Table Data" };

        foreach (var folder in folderNames)
        {
            var folderPath = Path.Combine(_templatePath, folder);
            _folderIndexes[folder] = ExtractionFileIndex.Build(folderPath);
        }
    }

    private string ResolveAndWrite(string folderName, string fileName, string content)
    {
        var basePath = Path.Combine(_templatePath, folderName);
        var index = _folderIndexes.ContainsKey(folderName) ? _folderIndexes[folderName] : null;
        var outputPath = index?.ResolvePath(fileName, basePath) ?? Path.Combine(basePath, fileName);

        // If writing .sql, remove any existing .sqlerror for the same object
        if (Path.GetExtension(fileName).Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            var errorPath = Path.ChangeExtension(outputPath, ".sqlerror");
            var file = FileWrapper.GetFromFactory();
            if (file.Exists(errorPath)) file.Delete(errorPath);
        }

        FileWrapper.GetFromFactory().WriteAllText(outputPath, content);
        index?.MarkWritten(outputPath);
        return outputPath;
    }

    internal void ValidateAndHandleScript(IDbConnection connection, string folderName, string fileName, string script, string objectType)
    {
        if (!_validateScripts) return;

        var result = ScriptValidator.ValidateScript(connection, script, objectType);
        if (result.IsValid) return;

        _progressLog.Warn($"    Invalid script: {fileName} — {result.ErrorMessage}");
        _invalidObjects.Add((folderName, fileName, result.ErrorMessage));

        var file = FileWrapper.GetFromFactory();
        var basePath = Path.Combine(_templatePath, folderName);
        var index = _folderIndexes.ContainsKey(folderName) ? _folderIndexes[folderName] : null;
        var sqlPath = index?.ResolvePath(fileName, basePath) ?? Path.Combine(basePath, fileName);
        var errorPath = Path.ChangeExtension(sqlPath, ".sqlerror");

        if (_saveInvalidScripts)
        {
            file.WriteAllText(errorPath, script);
            if (!sqlPath.Equals(errorPath, StringComparison.OrdinalIgnoreCase))
                file.Delete(sqlPath);
            index?.MarkWritten(errorPath);
        }
        else
        {
            file.Delete(sqlPath);
            if (file.Exists(errorPath)) file.Delete(errorPath);
            index?.MarkWritten(sqlPath); // Mark as processed to prevent orphan detection
        }
    }

    private IDbConnection GetConnection(string targetDb)
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var connectionStringOverride = CommandLineParser.ValueOfSwitch("ConnectionString", null);
        if (!string.IsNullOrEmpty(connectionStringOverride))
        {
            var overrideConnection = SqlConnectionFactory.GetFromFactory().GetSqlConnection(connectionStringOverride);
            overrideConnection.Open();
            return overrideConnection;
        }

        var connectionProperties = ConnectionString.ReadProperties(config, "Source:ConnectionProperties");
        if (connectionProperties.Count == 0)
            connectionProperties = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");

        var connectionString = ConnectionString.Build(config["Source:Server"], targetDb, config["Source:User"], config["Source:Password"], config["Source:Port"], connectionProperties);

        var connection = SqlConnectionFactory.GetFromFactory().GetSqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    public void CastTemplate()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var targetDb = config["Source:Database"]!;
        if (string.IsNullOrEmpty(targetDb)) throw new Exception("Source database is required");
        _productPath = Path.Combine(config["Product:Path"] ?? ".");

        _includeTables = config["ShouldCast:Tables"]?.ToLower() != "false";
        _includeSchemas = config["ShouldCast:Schemas"]?.ToLower() != "false";
        _includeUserDefinedTypes = config["ShouldCast:UserDefinedTypes"]?.ToLower() != "false";
        _includeUserDefinedFunctions = config["ShouldCast:UserDefinedFunctions"]?.ToLower() != "false";
        _includeViews = config["ShouldCast:Views"]?.ToLower() != "false";
        _includeStoredProcedures = config["ShouldCast:StoredProcedures"]?.ToLower() != "false";
        _includeTableTriggers = config["ShouldCast:TableTriggers"]?.ToLower() != "false";
        _includeFullTextCatalogs = config["ShouldCast:Catalogs"]?.ToLower() != "false";
        _includeFullTextStopLists = config["ShouldCast:StopLists"]?.ToLower() != "false";
        _includeDDLTriggers = config["ShouldCast:DDLTriggers"]?.ToLower() != "false";
        _includeXmlSchemaCollections = config["ShouldCast:XMLSchemaCollections"]?.ToLower() != "false";
        _includeIndexedViews = config["ShouldCast:IndexedViews"]?.ToLower() != "false";
        _scriptDynamicDependencyRemovalForFunctions = config["ShouldCast:ScriptDynamicDependencyRemovalForFunctions"]?.ToLower() == "true";
        _objectsToCast = (config["ShouldCast:ObjectList"]?.ToLower() ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        _orphanHandlingMode = Enum.TryParse<OrphanHandlingMode>(config["OrphanHandling:Mode"], true, out var mode)
            ? mode : OrphanHandlingMode.Detect;
        _validateScripts = config["ShouldCast:ValidateScripts"]?.ToLower() == "true";
        _saveInvalidScripts = config["ShouldCast:SaveInvalidScripts"]?.ToLower() != "false";

        var productFile = Path.Combine(_productPath, "Product.json");
        var productFileExistedBeforeInit = FileWrapper.GetFromFactory().Exists(productFile);
        if (productFileExistedBeforeInit)
        {
            var product = JsonHelper.Load<Product>(productFile);
            _checkConstraintStyle = product?.CheckConstraintStyle ?? CheckConstraintStyle.ColumnLevel;

            var configStyle = config["Product:CheckConstraintStyle"];
            if (!string.IsNullOrEmpty(configStyle) &&
                Enum.TryParse<CheckConstraintStyle>(configStyle, true, out var cfgStyle) &&
                cfgStyle != _checkConstraintStyle)
            {
                _progressLog.Warn($"SchemaTongs config specifies CheckConstraintStyle '{cfgStyle}' but Product.json is set to '{_checkConstraintStyle}'. Extracting as '{_checkConstraintStyle}' per the product definition. Update Product.json to change this.");
            }
        }
        else
        {
            var configStyle = config["Product:CheckConstraintStyle"];
            if (!string.IsNullOrEmpty(configStyle) && Enum.TryParse<CheckConstraintStyle>(configStyle, true, out var style))
                _checkConstraintStyle = style;
        }

        RepositoryHelper.UpdateOrInitRepository(_productPath, config["Product:Name"], config["Template:Name"], targetDb);

        if (!productFileExistedBeforeInit && _checkConstraintStyle != CheckConstraintStyle.ColumnLevel)
        {
            var newProduct = JsonHelper.Load<Product>(productFile);
            if (newProduct != null)
            {
                newProduct.CheckConstraintStyle = _checkConstraintStyle;
                JsonHelper.Write(productFile, newProduct);
            }
        }
        _templatePath = RepositoryHelper.UpdateOrInitTemplate(_productPath, config["Template:Name"], targetDb);
        RenameLegacyTableDataFolder();
        BuildFileIndexes();
        CastDatabaseObjects(targetDb);
    }

    private void CastDatabaseObjects(string targetDb)
    {
        using var connection = GetConnection(targetDb);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandTimeout = 0;

            _progressLog.Info("Kindling The Forge");
            ForgeKindler.KindleTheForge(command);

            if (_includeTables) ExtractTableDefinitions(command, targetDb);
            if (_includeSchemas) ScriptSqlServerSchemas(command);
            if (_includeUserDefinedTypes) ScriptSqlServerUserDefinedTypes(command);
            if (_includeUserDefinedFunctions) ScriptSqlServerFunctions(command);
            if (_includeViews) ScriptSqlServerViews(command);
            if (_includeStoredProcedures) ScriptSqlServerProcedures(command);
            if (_includeTableTriggers) ScriptSqlServerTableTriggers(command);
            if (_includeFullTextCatalogs) ScriptSqlServerFullTextCatalogs(command);
            if (_includeFullTextStopLists) ScriptSqlServerFullTextStopLists(command);
            if (_includeDDLTriggers) ScriptSqlServerDDLTriggers(command);
            if (_includeXmlSchemaCollections) ScriptSqlServerXmlSchemaCollections(command);
            if (_includeIndexedViews) CastSqlServerIndexedViews(command);
        }
        finally
        {
            connection.Close();
        }

        var logDirectory = CommandLineParser.ValueOfSwitch("LogPath", null)
            ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        OrphanHandler.ArchiveExistingCleanupScripts(logDirectory);
        ProcessOrphanDetection(logDirectory);

        if (_invalidObjects.Count > 0)
        {
            var invalidScript = CleanupScriptGenerator.GenerateInvalidObjectCleanupScript(_invalidObjects);
            var scriptPath = Path.Combine(logDirectory, "_InvalidObjectCleanup.sql");
            FileWrapper.GetFromFactory().WriteAllText(scriptPath, invalidScript);
            _progressLog.Info($"Generated _InvalidObjectCleanup.sql with {_invalidObjects.Count} invalid object(s)");
        }

        _progressLog.Info("");
        _progressLog.Info("Casting Completed Successfully");
    }

    private void ExtractTableDefinitions(IDbCommand command, string targetDb)
    {
        using var connectionJson = GetConnection(targetDb);
        using var commandJson = connectionJson.CreateCommand();

        command.CommandText = @"
SELECT TABLE_SCHEMA, TABLE_NAME
  FROM INFORMATION_SCHEMA.TABLES t
  JOIN sys.objects so ON so.[object_id] = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME)
                     AND so.is_ms_shipped = 0
  WHERE TABLE_TYPE = 'BASE TABLE'
    AND TABLE_NAME NOT LIKE 'MSPeer[_]%'
    AND TABLE_NAME NOT LIKE 'MSPub[_]%'
    AND TABLE_NAME NOT IN ('dtproperties', 'sysdiagrams')
    AND TABLE_SCHEMA <> 'SchemaSmith'
  ORDER BY 1, 2
";

        _progressLog.Info("Casting Table Structures");
        var tableDir = Path.Combine(_templatePath, "Tables");
        DirectoryWrapper.GetFromFactory().CreateDirectory(tableDir);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains($"{reader["TABLE_NAME"]}".ToLower()) && !_objectsToCast.Contains($"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}".ToLower())) continue;

            _progressLog.Info($"  Cast Json for {reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
            commandJson.CommandText = $"EXEC SchemaSmith.GenerateTableJSON @p_Schema = '{reader["TABLE_SCHEMA"]}', @p_Table = '{reader["TABLE_NAME"]}'";

            using var jsonReader = commandJson.ExecuteReader();
            var json = "";
            while (jsonReader.Read())
                json += $"{jsonReader[0]}\r\n";
            if (string.IsNullOrWhiteSpace(json) || json.Trim().Equals("{}"))
            {
                _progressLog.Error($"    No json returned for {reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
                continue;
            }

            var table = JsonConvert.DeserializeObject<Table>(json); // make sure the json is valid
            if (_checkConstraintStyle == CheckConstraintStyle.TableLevel && table != null)
            {
                PromoteCheckConstraintsToTableLevel(commandJson, table, $"{reader["TABLE_SCHEMA"]}", $"{reader["TABLE_NAME"]}");
                json = JsonConvert.SerializeObject(table, Formatting.Indented);
            }
            var outputPath = ResolveAndWrite("Tables", SchemaTongsEncoder.EncodeFileName((string)reader["TABLE_SCHEMA"], (string)reader["TABLE_NAME"], ".json"), json);
            _progressLog.Info($"    Casting {outputPath}");
        }
    }

    internal static void PromoteCheckConstraintsToTableLevel(IDbCommand command, Table table, string schema, string tableName)
    {
        command.CommandText = $@"
SELECT cc.name AS [Name], SchemaSmith.fn_StripParenWrapping(cc.definition) AS [Expression]
  FROM sys.check_constraints cc WITH (NOLOCK)
 WHERE cc.parent_object_id = OBJECT_ID('{EscapeSql(schema)}.{EscapeSql(tableName)}')
 ORDER BY cc.name";

        var allConstraints = new List<CheckConstraint>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                allConstraints.Add(new CheckConstraint
                {
                    Name = $"[{reader.GetString(0)}]",
                    Expression = reader.GetString(1)
                });
        }

        foreach (var col in table.Columns)
            col.CheckExpression = null;

        table.CheckConstraints = allConstraints;
    }

    private void ScriptSqlServerSchemas(IDbCommand command)
    {
        _progressLog.Info("Casting Schema Scripts");
        var castPath = Path.Combine(_templatePath, "Schemas");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name
  FROM sys.schemas s
 WHERE s.schema_id > 4
   AND s.name NOT LIKE 'db[_]%'
   AND s.name NOT LIKE '%\%'
   AND s.name <> 'SchemaSmith'
   AND s.principal_id IS NOT NULL
 ORDER BY s.name";

        var schemas = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower())) continue;
                schemas.Add(name);
            }
        }

        foreach (var name in schemas)
        {
            var script = $"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'{EscapeSql(name)}')\r\n" +
                         $"EXEC sys.sp_executesql N'CREATE SCHEMA [{name}]'\r\n";

            var outputPath = ResolveAndWrite("Schemas", SchemaTongsEncoder.EncodeFileName(name, ".sql"), script);
            _progressLog.Info($"  Casting {outputPath}");
        }
    }

    private void ScriptSqlServerUserDefinedTypes(IDbCommand command)
    {
        _progressLog.Info("Casting User Defined Types");
        var dataTypesPath = Path.Combine(_templatePath, "DataTypes");
        DirectoryWrapper.GetFromFactory().CreateDirectory(dataTypesPath);
        ScriptSqlServerAliasTypes(command);
        ScriptSqlServerTableTypes(command);
    }

    private void ScriptSqlServerAliasTypes(IDbCommand command)
    {
        command.CommandText = @"
SELECT s.name AS SchemaName, t.name AS TypeName,
       TYPE_NAME(t.system_type_id) AS BaseTypeName,
       t.max_length, t.precision, t.scale, t.is_nullable
  FROM sys.types t
  JOIN sys.schemas s ON t.schema_id = s.schema_id
 WHERE t.is_user_defined = 1
   AND t.is_table_type = 0
   AND t.is_assembly_type = 0
 ORDER BY s.name, t.name";

        var types = new List<(string Schema, string Name, string BaseType, short MaxLength, byte Precision, byte Scale, bool IsNullable)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower())) continue;
                types.Add((schema, name, reader.GetString(2), reader.GetInt16(3), reader.GetByte(4), reader.GetByte(5), reader.GetBoolean(6)));
            }
        }

        foreach (var (schema, name, baseType, maxLength, precision, scale, isNullable) in types)
        {
            var typeSpec = FormatBaseType(baseType, maxLength, precision, scale);
            var nullSpec = isNullable ? "NULL" : "NOT NULL";
            var script = $"IF NOT EXISTS (SELECT * FROM sys.types st JOIN sys.schemas ss ON st.schema_id = ss.schema_id WHERE st.name = N'{EscapeSql(name)}' AND ss.name = N'{EscapeSql(schema)}')\r\n" +
                         $"CREATE TYPE [{schema}].[{name}] FROM {typeSpec} {nullSpec}";

            var outputPath = ResolveAndWrite("DataTypes", SchemaTongsEncoder.EncodeFileName(schema, name, ".sql"), script);
            _progressLog.Info($"  Casting {outputPath}");
        }
    }

    private void ScriptSqlServerTableTypes(IDbCommand command)
    {
        command.CommandText = @"
SELECT s.name AS SchemaName, tt.name AS TypeName, tt.type_table_object_id
  FROM sys.table_types tt
  JOIN sys.schemas s ON tt.schema_id = s.schema_id
 WHERE tt.is_user_defined = 1
 ORDER BY s.name, tt.name";

        var tableTypes = new List<(string Schema, string Name, int ObjectId)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower())) continue;
                tableTypes.Add((schema, name, reader.GetInt32(2)));
            }
        }

        foreach (var (schema, name, objectId) in tableTypes)
        {
            command.CommandText = $@"
SELECT c.name, TYPE_NAME(c.user_type_id) AS TypeName,
       c.max_length, c.precision, c.scale, c.is_nullable,
       c.is_identity, c.is_computed,
       ts.name AS UserTypeName, tss.name AS UserTypeSchema,
       c.column_id
  FROM sys.columns c
  LEFT JOIN sys.types ts ON c.user_type_id = ts.user_type_id AND ts.is_user_defined = 1
  LEFT JOIN sys.schemas tss ON ts.schema_id = tss.schema_id
 WHERE c.object_id = {objectId}
 ORDER BY c.column_id";

            var columns = new List<(string Name, string TypeName, short MaxLength, byte Precision, byte Scale, bool IsNullable, bool IsIdentity, bool IsComputed, string UserTypeName, string UserTypeSchema)>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add((
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetInt16(2),
                        reader.GetByte(3),
                        reader.GetByte(4),
                        reader.GetBoolean(5),
                        reader.GetBoolean(6),
                        reader.GetBoolean(7),
                        reader.IsDBNull(8) ? null : reader.GetString(8),
                        reader.IsDBNull(9) ? null : reader.GetString(9)
                    ));
                }
            }

            command.CommandText = $@"
SELECT i.name, i.type_desc, i.is_unique, i.is_primary_key,
       ic.column_id, c.name AS ColumnName, ic.is_descending_key
  FROM sys.indexes i
  JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
  JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
 WHERE i.object_id = {objectId}
   AND i.type > 0
 ORDER BY i.index_id, ic.key_ordinal";

            var indexes = new Dictionary<string, (string TypeDesc, bool IsUnique, bool IsPrimaryKey, List<(string ColumnName, bool IsDescending)> Columns)>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var indexName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var typeDesc = reader.GetString(1);
                    var isUnique = reader.GetBoolean(2);
                    var isPrimaryKey = reader.GetBoolean(3);
                    var columnName = reader.GetString(5);
                    var isDescending = reader.GetBoolean(6);

                    if (!indexes.ContainsKey(indexName))
                        indexes[indexName] = (typeDesc, isUnique, isPrimaryKey, new List<(string, bool)>());
                    indexes[indexName].Columns.Add((columnName, isDescending));
                }
            }

            command.CommandText = $@"
SELECT cc.name, cc.definition
  FROM sys.check_constraints cc
 WHERE cc.parent_object_id = {objectId}
 ORDER BY cc.name";

            var checkConstraints = new List<(string Name, string Definition)>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    checkConstraints.Add((reader.GetString(0), reader.GetString(1)));
            }

            var lines = new List<string>();
            lines.Add($"CREATE TYPE [{schema}].[{name}] AS TABLE(");

            for (var i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                string typeSpec;
                if (col.UserTypeName != null)
                    typeSpec = $"[{col.UserTypeSchema}].[{col.UserTypeName}]";
                else
                    typeSpec = FormatBaseType(col.TypeName, col.MaxLength, col.Precision, col.Scale);

                var nullSpec = col.IsNullable ? "NULL" : "NOT NULL";
                var comma = (i < columns.Count - 1 || indexes.Count > 0 || checkConstraints.Count > 0) ? "," : "";
                lines.Add($"\t[{col.Name}] {typeSpec} {nullSpec}{comma}");
            }

            var constraintEntries = new List<string>();
            foreach (var kvp in indexes)
            {
                var idx = kvp.Value;
                var colList = string.Join(",\r\n", idx.Columns.Select(c => $"\t[{c.ColumnName}] " + (c.IsDescending ? "DESC" : "ASC")));

                if (idx.IsPrimaryKey)
                    constraintEntries.Add($"\tPRIMARY KEY {idx.TypeDesc} \r\n(\r\n{colList}\r\n)");
                else
                {
                    var unique = idx.IsUnique ? "UNIQUE " : "";
                    constraintEntries.Add($"\t{unique}{idx.TypeDesc} \r\n(\r\n{colList}\r\n)");
                }
            }

            foreach (var cc in checkConstraints)
                constraintEntries.Add($"\tCHECK {cc.Definition}");

            for (var i = 0; i < constraintEntries.Count; i++)
            {
                var suffix = i < constraintEntries.Count - 1 ? "," : "";
                lines.Add(constraintEntries[i] + suffix);
            }

            lines.Add(")");

            var createScript = string.Join("\r\n", lines);

            var script = $"IF NOT EXISTS (SELECT * FROM sys.types st JOIN sys.schemas ss ON st.schema_id = ss.schema_id WHERE st.name = N'{EscapeSql(name)}' AND ss.name = N'{EscapeSql(schema)}')\r\n" +
                         createScript;

            var outputPath = ResolveAndWrite("DataTypes", SchemaTongsEncoder.EncodeFileName(schema, name, ".sql"), script);
            _progressLog.Info($"  Casting {outputPath}");
        }
    }

    private void ScriptSqlServerFunctions(IDbCommand command)
    {
        _progressLog.Info("Casting Function Scripts");
        var castPath = Path.Combine(_templatePath, "Functions");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name AS SchemaName, o.name AS ObjectName
  FROM sys.objects o
  JOIN sys.schemas s ON o.schema_id = s.schema_id
 WHERE o.type IN ('FN', 'IF', 'TF')
   AND o.is_ms_shipped = 0
   AND s.name <> 'SchemaSmith'
 ORDER BY s.name, o.name";

        var functions = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower())) continue;
                functions.Add((schema, name));
            }
        }

        foreach (var (schema, name) in functions)
        {
            var sql = ScriptSqlServerProgrammableObject(command, schema, name, "FUNCTION");
            if (sql == null)
            {
                if (_folderIndexes.ContainsKey("Functions"))
                    _folderIndexes["Functions"].ExcludeFromOrphans($"{schema}.{name}.sql");
                continue;
            }

            if (_scriptDynamicDependencyRemovalForFunctions)
            {
                var dependencyBlock =
                    $"\r\nDECLARE @v_SearchTerm VARCHAR(2000) = '%{name}%'\r\n" +
                    "DECLARE @v_SQL VARCHAR(MAX) = (SELECT STRING_AGG(Task, ';' + CHAR(13) + CHAR(10)) \r\n" +
                    "                                 FROM (SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(cc.parent_object_id) + '].[' + OBJECT_NAME(cc.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + OBJECT_NAME(cc.[name]) + ']' AS Task\r\n" +
                    "                                         FROM sys.check_constraints cc\r\n" +
                    "                                         WHERE cc.[definition] LIKE @v_SearchTerm\r\n" +
                    "                                            OR EXISTS (SELECT *\r\n" +
                    "                                                         FROM sys.computed_columns cc2\r\n" +
                    "                                                         WHERE cc2.[definition] LIKE @v_SearchTerm\r\n" +
                    "                                                           AND cc2.[object_id] = cc.parent_object_id\r\n" +
                    "                                                           AND cc2.column_id = cc.parent_column_id)\r\n" +
                    "                                       UNION ALL\r\n" +
                    "                                       SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(dc.parent_object_id) + '].[' + OBJECT_NAME(dc.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + OBJECT_NAME(dc.[name]) + ']'\r\n" +
                    "                                         FROM sys.default_constraints dc\r\n" +
                    "                                         WHERE dc.[definition] LIKE @v_SearchTerm\r\n" +
                    "                                            OR EXISTS (SELECT *\r\n" +
                    "                                                         FROM sys.computed_columns cc\r\n" +
                    "                                                         WHERE cc.[definition] LIKE @v_SearchTerm\r\n" +
                    "                                                           AND cc.[object_id] = dc.parent_object_id\r\n" +
                    "                                                           AND cc.column_id = dc.parent_column_id)\r\n" +
                    "                                       UNION ALL\r\n" +
                    "                                       SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '].[' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + OBJECT_NAME(fk.[name]) + ']'\r\n" +
                    "                                         FROM sys.foreign_keys fk\r\n" +
                    "                                         WHERE EXISTS (SELECT *\r\n" +
                    "                                                         FROM sys.computed_columns cc\r\n" +
                    "                                                         JOIN sys.foreign_key_columns fc ON fk.[object_id] = fk.[object_id]\r\n" +
                    "                                                                                        AND ((fc.parent_object_id = cc.[object_id] AND fc.parent_column_id = cc.column_id)\r\n" +
                    "                                                                                          OR (fc.referenced_object_id = cc.[object_id] AND fc.referenced_column_id = cc.column_id))\r\n" +
                    "                                                         WHERE cc.[definition] LIKE @v_SearchTerm)\r\n" +
                    "                                       UNION ALL\r\n" +
                    "                                       SELECT 'DROP INDEX IF EXISTS [' + si.[name] + '] ON [' + OBJECT_SCHEMA_NAME(si.[object_id]) + '].[' + OBJECT_NAME(si.[object_id]) + ']'\r\n" +
                    "                                         FROM sys.indexes si\r\n" +
                    "                                         WHERE si.filter_definition LIKE @v_SearchTerm\r\n" +
                    "                                            OR EXISTS (SELECT *\r\n" +
                    "                                                         FROM sys.computed_columns cc\r\n" +
                    "                                                         JOIN sys.index_columns ic ON ic.[object_id] = si.[object_id]\r\n" +
                    "                                                                                  AND ic.index_id = si.index_id\r\n" +
                    "                                                                                  AND ic.column_id = cc.column_id\r\n" +
                    "                                                         WHERE cc.[definition] LIKE @v_SearchTerm\r\n" +
                    "                                                           AND cc.[object_id] = si.[object_id])\r\n" +
                    "                                       UNION ALL\r\n" +
                    "                                       SELECT 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(cc.[object_id]) + '].[' + OBJECT_NAME(cc.[object_id]) + '] DROP COLUMN IF EXISTS [' + cc.[name] + ']'\r\n" +
                    "                                         FROM sys.computed_columns cc\r\n" +
                    "                                         WHERE cc.[definition] LIKE @v_SearchTerm) x) + ';'\r\n" +
                    "EXEC(@v_SQL) -- Remove any dependencies before updating the function\r\n" +
                    "GO\r\n";

                var firstGoEnd = sql.IndexOf("GO\r\n\r\n") + 4;
                if (firstGoEnd > 3)
                    sql = sql.Substring(0, firstGoEnd) + dependencyBlock + sql.Substring(firstGoEnd);
            }

            var outputPath = ResolveAndWrite("Functions", SchemaTongsEncoder.EncodeFileName(schema, name, ".sql"), sql);
            _progressLog.Info($"  Casting {outputPath}");
            ValidateAndHandleScript(command.Connection, "Functions", $"{schema}.{name}.sql", sql, "FUNCTION");
        }
    }

    private void ScriptSqlServerViews(IDbCommand command)
    {
        _progressLog.Info("Casting View Scripts");
        var castPath = Path.Combine(_templatePath, "Views");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name AS SchemaName, o.name AS ObjectName
  FROM sys.objects o
  JOIN sys.schemas s ON o.schema_id = s.schema_id
 WHERE o.type = 'V'
   AND o.is_ms_shipped = 0
   AND s.name <> 'SchemaSmith'
 ORDER BY s.name, o.name";

        var views = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower())) continue;
                views.Add((schema, name));
            }
        }

        foreach (var (schema, name) in views)
        {
            var sql = ScriptSqlServerProgrammableObject(command, schema, name, "VIEW");
            if (sql == null)
            {
                if (_folderIndexes.ContainsKey("Views"))
                    _folderIndexes["Views"].ExcludeFromOrphans($"{schema}.{name}.sql");
                continue;
            }

            var outputPath = ResolveAndWrite("Views", SchemaTongsEncoder.EncodeFileName(schema, name, ".sql"), sql);
            _progressLog.Info($"  Casting {outputPath}");
            ValidateAndHandleScript(command.Connection, "Views", $"{schema}.{name}.sql", sql, "VIEW");
        }
    }

    private void ScriptSqlServerProcedures(IDbCommand command)
    {
        _progressLog.Info("Casting Stored Procedure Scripts");
        var castPath = Path.Combine(_templatePath, "Procedures");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name AS SchemaName, o.name AS ObjectName
  FROM sys.objects o
  JOIN sys.schemas s ON o.schema_id = s.schema_id
 WHERE o.type = 'P'
   AND o.is_ms_shipped = 0
   AND s.name <> 'SchemaSmith'
 ORDER BY s.name, o.name";

        var procedures = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower())) continue;
                procedures.Add((schema, name));
            }
        }

        foreach (var (schema, name) in procedures)
        {
            var sql = ScriptSqlServerProgrammableObject(command, schema, name, "PROCEDURE");
            if (sql == null)
            {
                if (_folderIndexes.ContainsKey("Procedures"))
                    _folderIndexes["Procedures"].ExcludeFromOrphans($"{schema}.{name}.sql");
                continue;
            }

            var outputPath = ResolveAndWrite("Procedures", SchemaTongsEncoder.EncodeFileName(schema, name, ".sql"), sql);
            _progressLog.Info($"  Casting {outputPath}");
            ValidateAndHandleScript(command.Connection, "Procedures", $"{schema}.{name}.sql", sql, "PROCEDURE");
        }
    }
    private void ScriptSqlServerTableTriggers(IDbCommand command)
    {
        _progressLog.Info("Casting Table Trigger Scripts");
        var castPath = Path.Combine(_templatePath, "Triggers");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name AS TableSchema, pt.name AS TableName, tr.name AS TriggerName
  FROM sys.triggers tr
  JOIN sys.objects pt ON tr.parent_id = pt.object_id
  JOIN sys.schemas s ON pt.schema_id = s.schema_id
 WHERE tr.parent_class = 1
   AND pt.is_ms_shipped = 0
   AND s.name <> 'SchemaSmith'
 ORDER BY s.name, pt.name, tr.name";

        var triggers = new List<(string TableSchema, string TableName, string TriggerName)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var triggerName = reader.GetString(2);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(triggerName.ToLower())) continue;
                triggers.Add((reader.GetString(0), reader.GetString(1), triggerName));
            }
        }

        foreach (var (tableSchema, tableName, triggerName) in triggers)
        {
            var sql = ScriptSqlServerProgrammableObject(command, tableSchema, triggerName, "TRIGGER", "TRIGGER", tableName);
            if (sql == null)
            {
                if (_folderIndexes.ContainsKey("Triggers"))
                    _folderIndexes["Triggers"].ExcludeFromOrphans($"{tableSchema}.{tableName}.{triggerName}.sql");
                continue;
            }

            var escapedSchema = Regex.Escape(tableSchema);
            var escapedTable = Regex.Escape(tableName);
            var tablePattern = $@"(?<=\bON\s+)\[?{escapedSchema}\]?\.\[?{escapedTable}\]?";
            sql = Regex.Replace(sql, tablePattern, $"[{tableSchema}].[{tableName}]", RegexOptions.IgnoreCase);

            var outputPath = ResolveAndWrite("Triggers", SchemaTongsEncoder.EncodeTriggerFileName(tableSchema, tableName, triggerName, ".sql"), sql);
            _progressLog.Info($"  Casting {outputPath}");
            ValidateAndHandleScript(command.Connection, "Triggers", $"{tableSchema}.{tableName}.{triggerName}.sql", sql, "TRIGGER");
        }
    }
    private void ScriptSqlServerFullTextCatalogs(IDbCommand command)
    {
        _progressLog.Info("Casting FullText Catalog Scripts");
        var castPath = Path.Combine(_templatePath, "FullTextCatalogs");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"SELECT name FROM sys.fulltext_catalogs ORDER BY name";

        var catalogs = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower())) continue;
                catalogs.Add(name);
            }
        }

        foreach (var name in catalogs)
        {
            var script = $"IF NOT EXISTS (SELECT * FROM sysfulltextcatalogs ftc WHERE ftc.name = N'{EscapeSql(name)}')\r\n" +
                         $"CREATE FULLTEXT CATALOG [{name}] ";

            var outputPath = ResolveAndWrite("FullTextCatalogs", SchemaTongsEncoder.EncodeFileName(name, ".sql"), script);
            _progressLog.Info($"  Casting {outputPath}");
        }
    }

    private void ScriptSqlServerFullTextStopLists(IDbCommand command)
    {
        _progressLog.Info("Casting FullText Stop List Scripts");
        var castPath = Path.Combine(_templatePath, "FullTextStopLists");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT stoplist_id, name
  FROM sys.fulltext_stoplists
 ORDER BY name";

        var stopLists = new List<(int Id, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(1);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower())) continue;
                stopLists.Add((reader.GetInt32(0), name));
            }
        }

        foreach (var (id, name) in stopLists)
        {
            command.CommandText = $@"
SELECT stopword, language
  FROM sys.fulltext_stopwords
 WHERE stoplist_id = {id}
 ORDER BY stopword, language";

            var stopWords = new List<(string Word, string Language)>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    stopWords.Add((reader.GetString(0), reader.GetString(1)));
            }

            var script = $"IF NOT EXISTS (SELECT * FROM sys.fulltext_stoplists ftsl WHERE ftsl.name = N'{EscapeSql(name)}')\r\n" +
                         $"BEGIN\r\n" +
                         $"CREATE FULLTEXT STOPLIST [{name}]\r\n" +
                         $";\r\n";

            foreach (var (word, language) in stopWords)
                script += $"ALTER FULLTEXT STOPLIST [{name}] ADD '{EscapeSql(word)}' LANGUAGE '{EscapeSql(language)}';\r\n";

            script += "END\r\n";

            var outputPath = ResolveAndWrite("FullTextStopLists", SchemaTongsEncoder.EncodeFileName(name, ".sql"), script);
            _progressLog.Info($"  Casting {outputPath}");
        }
    }
    private void ScriptSqlServerDDLTriggers(IDbCommand command)
    {
        _progressLog.Info("Casting Database DDL Trigger Scripts");
        var castPath = Path.Combine(_templatePath, "DDLTriggers");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT tr.name AS TriggerName
  FROM sys.triggers tr
 WHERE tr.parent_class = 0
 ORDER BY tr.name";

        var triggers = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower())) continue;
                triggers.Add(name);
            }
        }

        foreach (var triggerName in triggers)
        {
            command.CommandText = $@"
SELECT sm.definition, sm.uses_ansi_nulls, sm.uses_quoted_identifier
  FROM sys.sql_modules sm
  JOIN sys.triggers tr ON sm.object_id = tr.object_id
 WHERE tr.parent_class = 0 AND tr.name = '{EscapeSql(triggerName)}'";

            string definition = null;
            bool usesAnsiNulls = true;
            bool usesQuotedIdentifier = true;

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    if (reader.IsDBNull(0))
                    {
                        _progressLog.Warn($"  WARNING: {triggerName} is encrypted, skipping");
                        continue;
                    }
                    definition = reader.GetString(0);
                    usesAnsiNulls = reader.GetBoolean(1);
                    usesQuotedIdentifier = reader.GetBoolean(2);
                }
            }

            if (definition == null) continue;

            definition = definition.Trim();

            if (!definition.Contains("\r\n"))
                definition = definition.Replace("\n", "\r\n");

            var createMatch = Regex.Match(definition, @"(?<!\w)CREATE(\s+)TRIGGER\b", RegexOptions.IgnoreCase);
            if (createMatch.Success)
                definition = definition.Substring(0, createMatch.Index) + "CREATE OR ALTER" + createMatch.Value.Substring("CREATE".Length) + definition.Substring(createMatch.Index + createMatch.Length);

            var escapedName = Regex.Escape(triggerName);
            var namePattern = $@"(?<=TRIGGER\s+)\[?{escapedName}\]?";
            definition = Regex.Replace(definition, namePattern, $"[{triggerName}]", RegexOptions.IgnoreCase);

            definition = Regex.Replace(definition, @"(?<=\bAS[ \t]*\r\n)([ \t]*)(?=\S)", "$1\r\n");

            var ansiNulls = usesAnsiNulls ? "ON" : "OFF";
            var quotedIdentifier = usesQuotedIdentifier ? "ON" : "OFF";

            var sql = $"SET ANSI_NULLS {ansiNulls}\r\n" +
                      $"SET QUOTED_IDENTIFIER {quotedIdentifier}\r\n" +
                      $"GO\r\n\r\n" +
                      $"{definition}\r\n\r\n" +
                      $"GO\r\n";

            var outputPath = ResolveAndWrite("DDLTriggers", SchemaTongsEncoder.EncodeFileName(triggerName, ".sql"), sql);
            _progressLog.Info($"  Casting {outputPath}");
            ValidateAndHandleScript(command.Connection, "DDLTriggers", $"{triggerName}.sql", sql, "TRIGGER");
        }
    }
    private void ScriptSqlServerXmlSchemaCollections(IDbCommand command)
    {
        _progressLog.Info("Casting XML Schema Collection Scripts");
        var castPath = Path.Combine(_templatePath, "XMLSchemaCollections");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name AS SchemaName, xsc.name AS CollectionName
  FROM sys.xml_schema_collections xsc
  JOIN sys.schemas s ON xsc.schema_id = s.schema_id
 WHERE xsc.xml_collection_id > 1
 ORDER BY s.name, xsc.name";

        var collections = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                collections.Add((reader.GetString(0), reader.GetString(1)));
        }

        foreach (var (schema, name) in collections)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower())) continue;

            command.CommandText = $"SELECT CAST(XML_SCHEMA_NAMESPACE(N'{EscapeSql(schema)}', N'{EscapeSql(name)}') AS NVARCHAR(MAX))";
            var xmlContent = (string)command.ExecuteScalar();

            var script =
                $"IF NOT EXISTS (SELECT * FROM sys.xml_schema_collections c, sys.schemas s WHERE c.schema_id = s.schema_id AND (quotename(s.name) + '.' + quotename(c.name)) = N'[{schema}].[{name}]')\r\n" +
                $"CREATE XML SCHEMA COLLECTION [{schema}].[{name}] AS N'{xmlContent}'";
            script = FormatXmlInScript(script);

            var outputPath = ResolveAndWrite("XMLSchemaCollections", SchemaTongsEncoder.EncodeFileName(schema, name, ".sql"), script);
            _progressLog.Info($"  Casting {outputPath}");
        }
    }

    private void CastSqlServerIndexedViews(IDbCommand command)
    {
        _progressLog.Info("Casting Indexed Views");

        command.CommandText = @"
            SELECT s.name AS SchemaName, v.name AS ViewName
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            WHERE OBJECTPROPERTY(v.object_id, 'IsIndexed') = 1
            ORDER BY s.name, v.name";

        var views = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                views.Add((reader.GetString(0), reader.GetString(1)));

        if (views.Count == 0)
        {
            _progressLog.Info("  No indexed views found");
            return;
        }

        var indexedViewsPath = Path.Combine(_templatePath, "Indexed Views");
        DirectoryWrapper.GetFromFactory().CreateDirectory(indexedViewsPath);

        foreach (var (schema, name) in views)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower()))
                continue;

            command.CommandText = $"SELECT SchemaSmith.GenerateIndexedViewJson('{schema}', '{name}')";
            var json = command.ExecuteScalar()?.ToString();
            if (string.IsNullOrWhiteSpace(json))
            {
                _progressLog.Info($"  Skipping {schema}.{name} (no JSON returned)");
                continue;
            }

            ResolveAndWrite("Indexed Views", SchemaTongsEncoder.EncodeFileName(schema, name, ".json"), json);
            _progressLog.Info($"  {schema}.{name}");
        }
    }

    internal void RenameLegacyTableDataFolder()
    {
        var dir = DirectoryWrapper.GetFromFactory();
        var file = FileWrapper.GetFromFactory();
        var legacyPath = Path.Combine(_templatePath, "TableData");
        var newPath = Path.Combine(_templatePath, "Table Data");

        if (dir.Exists(legacyPath) && !dir.Exists(newPath))
        {
            dir.CreateDirectory(newPath);
            foreach (var filePath in dir.GetFiles(legacyPath, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = filePath.Substring(legacyPath.Length + 1);
                var destPath = Path.Combine(newPath, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir)) dir.CreateDirectory(destDir);
                file.Copy(filePath, destPath);
            }
            dir.Delete(legacyPath, true);
            _progressLog.Info("Renamed legacy 'TableData' folder to 'Table Data' for consistency.");
        }
        else if (dir.Exists(legacyPath) && dir.Exists(newPath))
        {
            _progressLog.Warn("Both 'TableData' and 'Table Data' folders exist. Please resolve manually.");
        }
    }

    private void ProcessOrphanDetection(string logDirectory)
    {
        var fullyExtractedFolders = new Dictionary<string, ExtractionFileIndex>();
        var hasObjectList = _objectsToCast.Length > 0;

        void AddIfFullyExtracted(bool included, string folderName)
        {
            if (!included) return;
            if (hasObjectList) return; // Skip orphan detection when ObjectList is active
            if (_folderIndexes.ContainsKey(folderName))
                fullyExtractedFolders[folderName] = _folderIndexes[folderName];
        }

        AddIfFullyExtracted(_includeTables, "Tables");
        AddIfFullyExtracted(_includeSchemas, "Schemas");
        AddIfFullyExtracted(_includeUserDefinedTypes, "DataTypes");
        AddIfFullyExtracted(_includeUserDefinedFunctions, "Functions");
        AddIfFullyExtracted(_includeViews, "Views");
        AddIfFullyExtracted(_includeStoredProcedures, "Procedures");
        AddIfFullyExtracted(_includeTableTriggers, "Triggers");
        AddIfFullyExtracted(_includeFullTextCatalogs, "FullTextCatalogs");
        AddIfFullyExtracted(_includeFullTextStopLists, "FullTextStopLists");
        AddIfFullyExtracted(_includeDDLTriggers, "DDLTriggers");
        AddIfFullyExtracted(_includeXmlSchemaCollections, "XMLSchemaCollections");
        AddIfFullyExtracted(_includeIndexedViews, "Indexed Views");

        if (fullyExtractedFolders.Count > 0)
        {
            _progressLog.Info("Processing orphan detection");
            OrphanHandler.ProcessOrphans(fullyExtractedFolders, _orphanHandlingMode, logDirectory);
        }
    }

    internal static string EscapeSql(string value) => value.Replace("'", "''");

    internal static string ConvertToCreateOrAlter(string definition, string schemaName, string objectName)
    {
        var createMatch = Regex.Match(definition,
            @"(?<!\w)CREATE(\s+)(PROCEDURE|FUNCTION|VIEW|TRIGGER)\b",
            RegexOptions.IgnoreCase);
        var result = createMatch.Success
            ? definition.Substring(0, createMatch.Index) + "CREATE OR ALTER" + createMatch.Value.Substring("CREATE".Length) + definition.Substring(createMatch.Index + createMatch.Length)
            : definition;

        var escapedSchema = Regex.Escape(schemaName);
        var escapedName = Regex.Escape(objectName);
        var namePattern = $@"\[?{escapedSchema}\]?\.\[?{escapedName}\]?";
        var match = Regex.Match(result, namePattern, RegexOptions.IgnoreCase);
        if (match.Success)
            result = result.Substring(0, match.Index) + $"[{schemaName}].[{objectName}]" + result.Substring(match.Index + match.Length);

        return result;
    }

    internal static string FormatBaseType(string baseType, short maxLength, byte precision, byte scale)
    {
        var lower = baseType.ToLower();
        return lower switch
        {
            "nvarchar" or "nchar" => maxLength == -1 ? $"[{baseType}](max)" : $"[{baseType}]({maxLength / 2})",
            "varchar" or "char" or "varbinary" or "binary" => maxLength == -1 ? $"[{baseType}](max)" : $"[{baseType}]({maxLength})",
            "decimal" or "numeric" => $"[{baseType}]({precision}, {scale})",
            "datetime2" or "datetimeoffset" or "time" => scale != 7 ? $"[{baseType}]({scale})" : $"[{baseType}]",
            _ => $"[{baseType}]"
        };
    }

    private string ScriptSqlServerProgrammableObject(IDbCommand command, string schemaName, string objectName, string objectType, string level2Type = null, string level2ParentName = null)
    {
        command.CommandText = $@"
SELECT sm.definition, sm.uses_ansi_nulls, sm.uses_quoted_identifier
  FROM sys.sql_modules sm
  JOIN sys.objects o ON sm.object_id = o.object_id
  JOIN sys.schemas s ON o.schema_id = s.schema_id
 WHERE s.name = '{EscapeSql(schemaName)}' AND o.name = '{EscapeSql(objectName)}'";

        string definition = null;
        bool usesAnsiNulls = true;
        bool usesQuotedIdentifier = true;

        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                if (reader.IsDBNull(0))
                {
                    _progressLog.Warn($"  WARNING: {schemaName}.{objectName} is encrypted, skipping");
                    return null;
                }
                definition = reader.GetString(0);
                usesAnsiNulls = reader.GetBoolean(1);
                usesQuotedIdentifier = reader.GetBoolean(2);
            }
        }

        if (definition == null) return null;

        definition = definition.Trim();
        if (!definition.Contains("\r\n"))
            definition = definition.Replace("\n", "\r\n");

        definition = ConvertToCreateOrAlter(definition, schemaName, objectName);
        definition = Regex.Replace(definition, @"(?<=\bAS[ \t]*\r\n)([ \t]*)(?=\S)", "$1\r\n");

        var ansiNulls = usesAnsiNulls ? "ON" : "OFF";
        var quotedIdentifier = usesQuotedIdentifier ? "ON" : "OFF";

        return $"SET ANSI_NULLS {ansiNulls}\r\nSET QUOTED_IDENTIFIER {quotedIdentifier}\r\nGO\r\n\r\n{definition}\r\n\r\nGO\r\n";
    }

    private static string FormatXmlInScript(string script)
    {
        if (!script.Contains(" AS N'")) return script;

        var xmlStart = script.IndexOfIgnoringCase(" AS N'") + 6;
        var xml = script.Substring(xmlStart, script.Length - (xmlStart + 1));
        var formattedXml = "\r\n" + string.Join("\r\n", xml.Replace("</xsd:schema>", "</xsd:schema>\r").Split('\r').Select(FormatXml));
        return script.Replace(xml, formattedXml);
    }

    private static string FormatXml(string xml)
    {
        try
        {
            return string.IsNullOrWhiteSpace(xml) ? xml : XDocument.Parse(xml).ToString();
        }
        catch
        {
            return xml; // if parsing fails then send it back unformatted
        }
    }
}
