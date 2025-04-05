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

        KindleTheForge(command);

        ExtractTableDefinitions(command, targetDb);

        var serverConnection = new ServerConnection((SqlConnection)connection);
        var server = new Server(serverConnection);
        var sourceDb = server.Databases[targetDb];
        ScriptSchemas(sourceDb);
        ScriptUDTs(sourceDb);
        ScriptUserDefinedFunctions(sourceDb);
        ScriptViews(sourceDb);
        ScriptStoredProcedures(sourceDb);
        ScriptTriggers(sourceDb);
        ScriptFullTextCatalogs(sourceDb);
        ScriptFullTextStopLists(sourceDb);
    }

    private void ScriptSchemas(Database sourceDb)
    {
        _progressLog.Info("Extracting Schema Scripts");
        sourceDb.PrefetchObjects(typeof(Microsoft.SqlServer.Management.Smo.Schema), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Schemas"));
        foreach (Microsoft.SqlServer.Management.Smo.Schema schema in sourceDb.Schemas)
        {
            if (schema.IsSystemObject || schema.Name.Contains("\\")) continue;

            var fileName = Path.Combine(_targetDir, "Schemas", $"{schema.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", schema.Script(_options)));
        }
    }

    private void ScriptUDTs(Database sourceDb)
    {
        _progressLog.Info("Extracting User Defined Types");
        sourceDb.PrefetchObjects(typeof(UserDefinedDataType), _options);
        sourceDb.PrefetchObjects(typeof(UserDefinedTableType), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "DataTypes"));
        foreach (UserDefinedDataType type in sourceDb.UserDefinedDataTypes)
        {
            var fileName = Path.Combine(_targetDir, "DataTypes", $"{type.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", type.Script(_options)));
        }
        foreach (UserDefinedTableType type in sourceDb.UserDefinedTableTypes)
        {
            var fileName = Path.Combine(_targetDir, "DataTypes", $"{type.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", type.Script(_options)));
        }
    }

    private void ScriptUserDefinedFunctions(Database sourceDb)
    {
        _progressLog.Info("Extracting Function Scripts");
        sourceDb.PrefetchObjects(typeof(UserDefinedFunction), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Functions"));
        foreach (UserDefinedFunction function in sourceDb.UserDefinedFunctions)
        {
            if (function.IsSystemObject || function.IsEncrypted) continue;

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
        _progressLog.Info("Extracting View Scripts");
        sourceDb.PrefetchObjects(typeof(View), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Views"));
        foreach (View view in sourceDb.Views)
        {
            if (view.IsSystemObject || view.IsEncrypted) continue;
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
        _progressLog.Info("Extracting Stored Procedure Scripts");
        sourceDb.PrefetchObjects(typeof(StoredProcedure), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Procedures"));
        foreach (StoredProcedure proc in sourceDb.StoredProcedures)
        {
            if (proc.IsSystemObject || proc.IsEncrypted) continue;
            var fileName = Path.Combine(_targetDir, "Procedures", $"{proc.Schema}.{proc.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(proc.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(proc.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{proc.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{proc.TextBody}
";
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private void ScriptTriggers(Database sourceDb)
    {
        _progressLog.Info("Extracting Trigger Scripts");
        sourceDb.PrefetchObjects(typeof(Table), _options);
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "Triggers"));
        foreach (Table table in sourceDb.Tables)
        {
            if (table.IsSystemObject) continue;

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
  ORDER BY 1, 2
";

        var tableDir = Path.Combine(_targetDir, "Tables");
        DirectoryWrapper.GetFromFactory().CreateDirectory(tableDir);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            _progressLog.Info($"Generate Json for {reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
            commandJson.CommandText = $"EXEC SchemaSmith.GenerateTableJSON @p_Schema = '{reader["TABLE_SCHEMA"]}', @p_Table = '{reader["TABLE_NAME"]}'";

            using var jsonReader = commandJson.ExecuteReader();
            var json = "";
            while (jsonReader.Read())
                json += jsonReader[0].ToString();
            if (string.IsNullOrWhiteSpace(json))
            {
                _progressLog.Error($"No json returned for {reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
                continue;
            }

            var filename = Path.Combine(tableDir, $"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}.json");
            _progressLog.Info($"  Casting {filename}");
            _ = JsonConvert.DeserializeObject<Schema.Domain.Table>(json); // make sure the json is valid
            FileWrapper.GetFromFactory().WriteAllText(filename, json);
        }
    }

    private void ScriptFullTextCatalogs(Database sourceDb)
    {
        _progressLog.Info("Extracting FullText Catalogs");
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "FullTextCatalogs"));
        foreach (FullTextCatalog catalog in sourceDb.FullTextCatalogs)
        {
            var fileName = Path.Combine(_targetDir, "FullTextCatalogs", $"{catalog.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", catalog.Script(_options)));
        }
    }

    private void ScriptFullTextStopLists(Database sourceDb)
    {
        _progressLog.Info("Extracting FullText Stop Lists");
        DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_targetDir, "FullTextStopLists"));
        foreach (FullTextStopList list in sourceDb.FullTextStopLists)
        {
            var fileName = Path.Combine(_targetDir, "FullTextStopLists", $"{list.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", list.Script(_options)));
        }
    }


    private void KindleTheForge(IDbCommand command)
    {
        _progressLog.Info("  Kindling The Forge");

        command.CommandText = ResourceLoader.Load("Kindling_SchemaSmith_Schema.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_StripParenWrapping.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_FormatJson.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.GenerateTableJson.sql");
        command.ExecuteNonQuery();
    }
}