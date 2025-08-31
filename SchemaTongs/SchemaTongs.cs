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
using System.Xml.Linq;

namespace SchemaTongs;

public class SchemaTongs
{
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private string _productPath = "";
    private string _templatePath = "";
    private readonly ScriptingOptions _options = new()
    {
        SchemaQualify = true,
        NoCollation = true,
        WithDependencies = false,
        ExtendedProperties = true,
        AllowSystemObjects = false,
        Permissions = false,
        ScriptForCreateOrAlter = true,
        ScriptForCreateDrop = false,
        IncludeIfNotExists = true
    };
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
    private string[] _objectsToCast = [];

    private IDbConnection GetConnection(string targetDb)
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();

        var connectionString = ConnectionString.Build(config["Source:Server"], targetDb, config["Source:User"], config["Source:Password"]);

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
        _objectsToCast = (config["ShouldCast:ObjectList"]?.ToLower() ?? "").Split(new []{ ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        RepositoryHelper.UpdateOrInitRepository(_productPath, config["Product:Name"], config["Template:Name"], targetDb);
        _templatePath = RepositoryHelper.UpdateOrInitTemplate(_productPath, config["Template:Name"], targetDb);
        CastDatabaseObjects(targetDb);
    }

    private void CastDatabaseObjects(string targetDb)
    {
        using var connection = GetConnection(targetDb);
        using var command = connection.CreateCommand();

        _progressLog.Info("Kindling The Forge");
        ForgeKindler.KindleTheForge(command);

        if (_includeTables) ExtractTableDefinitions(command, targetDb);

        var serverConnection = new ServerConnection((SqlConnection)connection);
        var server = new Server(serverConnection);
        var sourceDb = server.Databases[targetDb];
        if (_includeSchemas) ScriptSchemas(sourceDb);
        if (_includeUserDefinedTypes) ScriptUserDefinedTypes(sourceDb);
        if (_includeUserDefinedFunctions) ScriptUserDefinedFunctions(sourceDb);
        if (_includeViews) ScriptViews(sourceDb);
        if (_includeStoredProcedures) ScriptStoredProcedures(sourceDb);
        if (_includeTableTriggers) ScriptTableTriggers(sourceDb);
        if (_includeFullTextCatalogs) ScriptFullTextCatalogs(sourceDb);
        if (_includeFullTextStopLists) ScriptFullTextStopLists(sourceDb);
        if (_includeDDLTriggers) ScriptDDLTriggers(sourceDb);
        if (_includeXmlSchemaCollections) ScriptXmlSchemaCollections(sourceDb);
        _progressLog.Info("");
        _progressLog.Info("Casting Completed Successfully");
    }

    private void ScriptSchemas(Database sourceDb)
    {
        _progressLog.Info("Casting Schema Scripts");
        sourceDb.PrefetchObjects(typeof(Microsoft.SqlServer.Management.Smo.Schema), _options);
        var castPath = Path.Combine(_templatePath, "Schemas");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (Microsoft.SqlServer.Management.Smo.Schema schema in sourceDb.Schemas)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(schema.Name.ToLower())) continue;
            if (schema.IsSystemObject || schema.Name.Contains(@"\") || schema.Name.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(castPath, $"{schema.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", schema.Script(_options).Cast<string>()));
        }
    }

    private void ScriptUserDefinedTypes(Database sourceDb)
    {
        _progressLog.Info("Casting User Defined Types");
        sourceDb.PrefetchObjects(typeof(UserDefinedDataType), _options);
        sourceDb.PrefetchObjects(typeof(UserDefinedTableType), _options);
        var castPath = Path.Combine(_templatePath, "DataTypes");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (UserDefinedDataType type in sourceDb.UserDefinedDataTypes)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(type.Name.ToLower()) && !_objectsToCast.Contains($"{type.Schema}.{type.Name}".ToLower())) continue;
            
            var fileName = Path.Combine(castPath, $"{type.Schema}.{type.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", type.Script(_options).Cast<string>()));
        }
        foreach (UserDefinedTableType type in sourceDb.UserDefinedTableTypes)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(type.Name.ToLower()) && !_objectsToCast.Contains($"{type.Schema}.{type.Name}".ToLower())) continue;
            
            var fileName = Path.Combine(castPath, $"{type.Schema}.{type.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\n", type.Script(_options).Cast<string>()));
        }
    }

    private void ScriptUserDefinedFunctions(Database sourceDb)
    {
        _progressLog.Info("Casting Function Scripts");
        sourceDb.PrefetchObjects(typeof(UserDefinedFunction), _options);
        var castPath = Path.Combine(_templatePath, "Functions");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (UserDefinedFunction function in sourceDb.UserDefinedFunctions)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(function.Name.ToLower()) && !_objectsToCast.Contains($"{function.Schema}.{function.Name}".ToLower())) continue;
            if (function.IsSystemObject || function.IsEncrypted || function.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(castPath, $"{function.Schema}.{function.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(function.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(function.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{function.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{function.TextBody}
GO
{AddExtendedProperiesScript(function.ExtendedProperties)}";
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private string AddExtendedProperiesScript(ExtendedPropertyCollection properties)
    {
        if (properties.Count == 0) return "";

        return properties.Count == 0 ? "" : $"{string.Join("\r\n", properties.Cast<ExtendedProperty>().SelectMany(p => p.Script(_options).Cast<string>()))}\r\nGO";
    }

    private void ScriptViews(Database sourceDb)
    {
        _progressLog.Info("Casting View Scripts");
        sourceDb.PrefetchObjects(typeof(View), _options);
        var castPath = Path.Combine(_templatePath, "Views");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (View view in sourceDb.Views)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(view.Name.ToLower()) && !_objectsToCast.Contains($"{view.Schema}.{view.Name}".ToLower())) continue;
            if (view.IsSystemObject || view.IsEncrypted || view.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(castPath, $"{view.Schema}.{view.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(view.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(view.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{view.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{view.TextBody}
GO
{AddExtendedProperiesScript(view.ExtendedProperties)}";
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private void ScriptStoredProcedures(Database sourceDb)
    {
        _progressLog.Info("Casting Stored Procedure Scripts");
        sourceDb.PrefetchObjects(typeof(StoredProcedure), _options);
        var castPath = Path.Combine(_templatePath, "Procedures");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (StoredProcedure procedure in sourceDb.StoredProcedures)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(procedure.Name.ToLower()) && !_objectsToCast.Contains($"{procedure.Schema}.{procedure.Name}".ToLower())) continue;
            if (procedure.IsSystemObject || procedure.IsEncrypted || procedure.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            var fileName = Path.Combine(castPath, $"{procedure.Schema}.{procedure.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(procedure.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(procedure.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{procedure.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{procedure.TextBody}
GO
{AddExtendedProperiesScript(procedure.ExtendedProperties)}";

            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private void ScriptTableTriggers(Database sourceDb)
    {
        _progressLog.Info("Casting Table Trigger Scripts");
        sourceDb.PrefetchObjects(typeof(Table), _options);
        var castPath = Path.Combine(_templatePath, "Triggers");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (Table table in sourceDb.Tables)
        {
            if (table.IsSystemObject || table.Schema.EqualsIgnoringCase("SchemaSmith")) continue;

            foreach (Trigger trigger in table.Triggers)
            {
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(trigger.Name.ToLower())) continue;
                if (trigger.IsSystemObject || trigger.IsEncrypted) continue;

                var fileName = Path.Combine(castPath, $"{table.Schema}.{table.Name}.{trigger.Name}.sql");
                var sql = @$"SET ANSI_NULLS {(trigger.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(trigger.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{trigger.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{trigger.TextBody}
GO
{AddExtendedProperiesScript(trigger.ExtendedProperties)}";
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

            var filename = Path.Combine(tableDir, $"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}.json");
            _progressLog.Info($"    Casting {filename}");
            _ = JsonConvert.DeserializeObject<Schema.Domain.Table>(json); // make sure the json is valid
            FileWrapper.GetFromFactory().WriteAllText(filename, json);
        }
    }

    private void ScriptFullTextCatalogs(Database sourceDb)
    {
        _progressLog.Info("Casting FullText Catalog Scripts");
        var castPath = Path.Combine(_templatePath, "FullTextCatalogs");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (FullTextCatalog catalog in sourceDb.FullTextCatalogs)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(catalog.Name.ToLower())) continue;
            
            var fileName = Path.Combine(castPath, $"{catalog.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\nGO\r\n", catalog.Script(_options).Cast<string>()));
        }
    }

    private void ScriptFullTextStopLists(Database sourceDb)
    {
        _progressLog.Info("Casting FullText Stop List Scripts");
        var castPath = Path.Combine(_templatePath, "FullTextStopLists");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (FullTextStopList list in sourceDb.FullTextStopLists)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(list.Name.ToLower())) continue;

            var fileName = Path.Combine(castPath, $"{list.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\nGO\r\n", list.Script(_options).Cast<string>()));
        }
    }

    private void ScriptDDLTriggers(Database sourceDb)
    {
        _progressLog.Info("Casting Database DDL Trigger Scripts");
        var castPath = Path.Combine(_templatePath, "DDLTriggers");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (DatabaseDdlTrigger trigger in sourceDb.Triggers)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(trigger.Name.ToLower())) continue;

            var fileName = Path.Combine(castPath, $"{trigger.Name}.sql");
            var sql = @$"SET ANSI_NULLS {(trigger.AnsiNullsStatus ? "ON" : "OFF")}
SET QUOTED_IDENTIFIER {(trigger.QuotedIdentifierStatus ? "ON" : "OFF")}
GO
{trigger.ScriptHeader(ScriptNameObjectBase.ScriptHeaderType.ScriptHeaderForCreateOrAlter)}
{trigger.TextBody}
GO
{AddExtendedProperiesScript(trigger.ExtendedProperties)}";
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
        }
    }

    private void ScriptXmlSchemaCollections(Database sourceDb)
    {
        _progressLog.Info("Casting XML Schema Collection Scripts");
        var castPath = Path.Combine(_templatePath, "XMLSchemaCollections");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        foreach (XmlSchemaCollection collection in sourceDb.XmlSchemaCollections)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(collection.Name.ToLower()) && !_objectsToCast.Contains($"{collection.Schema}.{collection.Name}".ToLower())) continue;

            var fileName = Path.Combine(castPath, $"{collection.Schema}.{collection.Name}.sql");
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, string.Join("\r\nGO\r\n", collection.Script(_options).Cast<string>().Select(FormatXmlInScript)));
        }
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