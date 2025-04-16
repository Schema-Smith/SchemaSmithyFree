using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Newtonsoft.Json;
using Schema.DataAccess;
using Schema.Isolators;
using Schema.Utility;
using System;
using System.Data;
using System.IO;
using System.Linq;

namespace SchemaTongs;

public class SchemaTongs
{
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private string _targetDir = "";
    private readonly ScriptingOptions _options = new()
    {
        SchemaQualify = true,
        NoCollation = true,
        WithDependencies = false,
        ExtendedProperties = false,
        AllowSystemObjects = false,
        Permissions = false,
        ScriptForCreateOrAlter = true,
        ScriptForCreateDrop = false,
        IncludeIfNotExists = true
    };

    private IDbConnection GetConnection(string targetDb)
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();

        var connectionString = ConnectionString.Build(config["Target:Server"], targetDb, config["Target:User"], config["Target:Password"]);

        var connection = SqlConnectionFactory.GetFromFactory().GetSqlConnection(connectionString);

        connection.Open();
        return connection;
    }

    public void ExctractTemplate()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var targetDb = config["Target:Database"]!;
        if (string.IsNullOrEmpty(targetDb)) throw new Exception("Target database is required");
        _targetDir = Path.Combine(config["Target:Directory"] ?? ".", targetDb);

        DirectoryWrapper.GetFromFactory().CreateDirectory(_targetDir);
        ExportDatabase(targetDb);
    }

    private void ExportDatabase(string targetDb)
    {
        using var connection = GetConnection(targetDb);
        using var command = connection.CreateCommand();

        _progressLog.Info("Kindling The Forge");
        ForgeKindler.KindleTheForge(command);

        ExtractTableDefinitions(command, targetDb);

        var serverConnection = new ServerConnection((SqlConnection)connection);
        var server = new Server(serverConnection);
        var sourceDb = server.Databases[targetDb];
        ScriptSchemas(sourceDb);
        ScriptUserDefinedTypes(sourceDb);
        ScriptUserDefinedFunctions(sourceDb);
        ScriptViews(sourceDb);
        ScriptStoredProcedures(sourceDb);
        ScriptTriggers(sourceDb);
        ScriptFullTextCatalogs(sourceDb);
        ScriptFullTextStopLists(sourceDb);
        ScriptDDLTriggers(sourceDb);
    }

    private void ScriptSchemas(Database sourceDb)
    {
        _progressLog.Info("Casting Schema Scripts");
        sourceDb.PrefetchObjects(typeof(Microsoft.SqlServer.Management.Smo.Schema), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Schemas"));
        foreach (Microsoft.SqlServer.Management.Smo.Schema schema in sourceDb.Schemas)
        {
            if (schema.IsSystemObject || schema.Name.Contains("\\") || schema.Name.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(_targetDir, "Schemas", $"{schema.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", schema.Script(_options).Cast<string>()));
        }
    }

    private void ScriptUserDefinedTypes(Database sourceDb)
    {
        _progressLog.Info("Casting User Defined Types");
        sourceDb.PrefetchObjects(typeof(UserDefinedDataType), _options);
        sourceDb.PrefetchObjects(typeof(UserDefinedTableType), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "DataTypes"));
        foreach (UserDefinedDataType type in sourceDb.UserDefinedDataTypes)
        {
            var fileName = Path.Combine(_targetDir, "DataTypes", $"{type.Schema}.{type.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", type.Script(_options).Cast<string>()));
        }
        foreach (UserDefinedTableType type in sourceDb.UserDefinedTableTypes)
        {
            var fileName = Path.Combine(_targetDir, "DataTypes", $"{type.Schema}.{type.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", type.Script(_options).Cast<string>()));
        }
    }

    private void ScriptUserDefinedFunctions(Database sourceDb)
    {
        _progressLog.Info("Casting Function Scripts");
        sourceDb.PrefetchObjects(typeof(UserDefinedFunction), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Functions"));
        foreach (UserDefinedFunction function in sourceDb.UserDefinedFunctions)
        {
            if (function.IsSystemObject || function.IsEncrypted || function.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(_targetDir, "Functions", $"{function.Schema}.{function.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(function.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(function.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{function.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{function.TextBody}
";
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private void ScriptViews(Database sourceDb)
    {
        _progressLog.Info("Casting View Scripts");
        sourceDb.PrefetchObjects(typeof(View), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Views"));
        foreach (View view in sourceDb.Views)
        {
            if (view.IsSystemObject || view.IsEncrypted || view.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(_targetDir, "Views", $"{view.Schema}.{view.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(view.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(view.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{view.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{view.TextBody}
";
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private void ScriptStoredProcedures(Database sourceDb)
    {
        _progressLog.Info("Casting Stored Procedure Scripts");
        sourceDb.PrefetchObjects(typeof(StoredProcedure), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Procedures"));
        foreach (StoredProcedure procedure in sourceDb.StoredProcedures)
        {
            if (procedure.IsSystemObject || procedure.IsEncrypted || procedure.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(_targetDir, "Procedures", $"{procedure.Schema}.{procedure.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(procedure.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(procedure.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{procedure.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{procedure.TextBody}
";
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private void ScriptTriggers(Database sourceDb)
    {
        _progressLog.Info("Casting Trigger Scripts");
        sourceDb.PrefetchObjects(typeof(Table), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Triggers"));
        foreach (Table table in sourceDb.Tables)
        {
            if (table.IsSystemObject || table.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            foreach (Trigger trigger in table.Triggers)
            {
                if (trigger.IsSystemObject || trigger.IsEncrypted) continue;
                var fileName = Path.Combine(_targetDir, "Triggers", $"{table.Schema}.{table.Name}.{trigger.Name}.sql");
                var sql = @$"SET ANSI_NULLS {(trigger.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(trigger.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{trigger.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{trigger.TextBody}
";
                _progressLog.Info($"  Casting {fileName}");
                FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
            }
        }
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
    AND TABLE_NAME NOT LIKE 'sys%'
    AND TABLE_SCHEMA <> 'SchemaSmith'
  ORDER BY 1, 2
";

        _progressLog.Info("Casting Table Structures");
        var tableDir = Path.Combine(_targetDir, "Tables");
        DirectoryWrapper.GetFromFactory().CreateDirectory(tableDir);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            _progressLog.Info($"  Cast Json for {reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
            commandJson.CommandText = $"EXEC SchemaSmith.GenerateTableJSON @p_Schema = '{reader["TABLE_SCHEMA"]}', @p_Table = '{reader["TABLE_NAME"]}'";

            using var jsonReader = commandJson.ExecuteReader();
            var json = "";
            while (jsonReader.Read())
                json += $"{jsonReader[0].ToString()}\r\n";
            if (string.IsNullOrWhiteSpace(json) || json.Trim().Equals("{}"))
            {
                _progressLog.Error($"    No json returned for {reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
                continue;
            }

            var filename = Path.Combine(tableDir, $"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}.json");
            _progressLog.Info($"    Casting {filename}");
            _ = JsonConvert.DeserializeObject<Schema.Domain.Table>(json); // make sure the json is valid
            FileWrapper.GetFromFactory().WriteAllText(filename, json);
        }
    }

    private void ScriptFullTextCatalogs(Database sourceDb)
    {
        _progressLog.Info("Casting FullText Catalog Scripts");
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "FullTextCatalogs"));
        foreach (FullTextCatalog catalog in sourceDb.FullTextCatalogs)
        {
            var fileName = Path.Combine(_targetDir, "FullTextCatalogs", $"{catalog.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\nGO\r\n", catalog.Script(_options).Cast<string>()));
        }
    }

    private void ScriptFullTextStopLists(Database sourceDb)
    {
        _progressLog.Info("Casting FullText Stop List Scripts");
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "FullTextStopLists"));
        foreach (FullTextStopList list in sourceDb.FullTextStopLists)
        {
            var fileName = Path.Combine(_targetDir, "FullTextStopLists", $"{list.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\nGO\r\n", list.Script(_options).Cast<string>()));
        }
    }

    private void ScriptDDLTriggers(Database sourceDb)
    {
        _progressLog.Info("Casting DDL Trigger Scripts");
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "DDLTriggers"));
        foreach (DatabaseDdlTrigger trigger in sourceDb.Triggers)
        {
            var fileName = Path.Combine(_targetDir, "DDLTriggers", $"{trigger.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\nGO\r\n", trigger.Script(_options).Cast<string>()));
        }
    }
}