// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Schema.DataAccess;
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
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private string _productPath = "";
    private string _templatePath = "";
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
    private bool _scriptDynamicDependencyRemovalForFunctions;
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
        _scriptDynamicDependencyRemovalForFunctions = config["ShouldCast:ScriptDynamicDependencyRemovalForFunctions"]?.ToLower() == "true";
        _objectsToCast = (config["ShouldCast:ObjectList"]?.ToLower() ?? "").Split(new []{ ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        RepositoryHelper.UpdateOrInitRepository(_productPath, config["Product:Name"], config["Template:Name"], targetDb);
        _templatePath = RepositoryHelper.UpdateOrInitTemplate(_productPath, config["Template:Name"], targetDb);
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
        }
        finally
        {
            connection.Close();
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

            var filename = Path.Combine(tableDir, $"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}.json");
            _progressLog.Info($"    Casting {filename}");
            _ = JsonConvert.DeserializeObject<Schema.Domain.Table>(json); // make sure the json is valid
            FileWrapper.GetFromFactory().WriteAllText(filename, json);
        }
    }

    private void ScriptSqlServerSchemas(IDbCommand command) { }
    private void ScriptSqlServerUserDefinedTypes(IDbCommand command) { }
    private void ScriptSqlServerFunctions(IDbCommand command) { }
    private void ScriptSqlServerViews(IDbCommand command) { }
    private void ScriptSqlServerProcedures(IDbCommand command) { }
    private void ScriptSqlServerTableTriggers(IDbCommand command) { }
    private void ScriptSqlServerFullTextCatalogs(IDbCommand command) { }
    private void ScriptSqlServerFullTextStopLists(IDbCommand command) { }
    private void ScriptSqlServerDDLTriggers(IDbCommand command) { }
    private void ScriptSqlServerXmlSchemaCollections(IDbCommand command) { }

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
