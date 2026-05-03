// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using log4net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using MySqlConnector;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;
using Schema.Utility;

namespace SchemaTongs;

public class SchemaTongs
{
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private readonly Platform _platform;
    private readonly Stopwatch _stopwatch = new();
    private string _productPath = "";
    private string _templatePath = "";
    private string[] _objectsToCast = [];

    private FolderMappingConfig _folderMappingConfig;
    internal Dictionary<ScriptObjectType, string> ResolvedFolders { get; } = new();

    // Platform-specific ShouldCast flags
    // Common
    private bool _includeTables;
    private bool _includeViews;

    // SQL Server specific
    private bool _includeSchemas;
    private bool _includeUserDefinedTypes;
    private bool _includeUserDefinedFunctions;
    private bool _includeStoredProcedures;
    private bool _includeTableTriggers;
    private bool _includeFullTextCatalogs;
    private bool _includeFullTextStopLists;
    private bool _includeDDLTriggers;
    private bool _includeXmlSchemaCollections;
    private bool _scriptDynamicDependencyRemovalForFunctions;
    private bool _includeIndexedViews;

    // PostgreSQL specific
    private bool _includeDomainTypes;
    private bool _includeEnumTypes;
    private bool _includeCompositeTypes;
    private bool _includeFunctions; // PostgreSQL: functions, trigger functions, and window functions
    private bool _includeAggregates;
    private bool _includeProcedures;
    private bool _includeSequences;
    private bool _includeRules;
    private bool _includeTriggers;
    private bool _includeMaterializedViews;

    // MySQL specific
    private bool _includeEvents;

    // Orphan handling
    private OrphanHandlingMode _orphanHandlingMode;

    // Script validation
    internal bool _validateScripts;
    internal bool _saveInvalidScripts = true;
    private readonly ScriptValidator _scriptValidator = new();
    internal readonly List<(string FileName, string ErrorMessage, ScriptObjectType ObjectType)> _invalidScripts = new();

    internal CheckConstraintStyle CheckConstraintStyle => _checkConstraintStyle;
    private CheckConstraintStyle _checkConstraintStyle;

    public SchemaTongs(Platform platform)
    {
        _platform = platform;
    }

    internal void SetTemplatePath(string path) => _templatePath = path;

    internal void ResolveFolderMappings()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        _folderMappingConfig = new FolderMappingConfig(config, _platform);
        _folderMappingConfig.Validate();

        var templateFile = Path.Combine(_templatePath, "Template.json");
        if (!FileWrapper.GetFromFactory().Exists(templateFile)) return;

        var template = JsonHelper.Load<Template>(templateFile);

        var defaultSlots = Template.GetDefaultTemplateFolders(_platform)
            .Where(f => f.ObjectType != ScriptObjectType.None)
            .ToDictionary(f => f.ObjectType, f => f.QuenchSlot);

        var modified = false;

        foreach (ScriptObjectType type in Enum.GetValues<ScriptObjectType>())
        {
            if (type == ScriptObjectType.None) continue;
            var configFolder = _folderMappingConfig.GetFolderName(type);
            if (configFolder == null) continue;

            var templateFolder = template.ScriptFolders.FirstOrDefault(f => f.ObjectType == type);

            if (templateFolder != null)
            {
                ResolvedFolders[type] = templateFolder.FolderPath;
                if (!string.Equals(templateFolder.FolderPath, configFolder, StringComparison.OrdinalIgnoreCase))
                {
                    _progressLog.Warn($"SchemaTongs config maps '{type}' to '{configFolder}' but template uses '{templateFolder.FolderPath}'. " +
                                      $"Extracting to '{templateFolder.FolderPath}' per template definition.");
                }
            }
            else
            {
                if (defaultSlots.TryGetValue(type, out var slot))
                {
                    template.ScriptFolders.Add(new TemplateFolder
                    {
                        FolderPath = configFolder,
                        QuenchSlot = slot,
                        ObjectType = type
                    });
                    ResolvedFolders[type] = configFolder;
                    modified = true;

                    DirectoryWrapper.GetFromFactory().CreateDirectory(Path.Combine(_templatePath, configFolder));
                }
            }
        }

        if (modified)
            JsonHelper.Write(templateFile, template);
    }


    internal string GetCastPath(ScriptObjectType type, string defaultFolderName)
    {
        if (ResolvedFolders.TryGetValue(type, out var folder))
            return Path.Combine(_templatePath, folder);
        return Path.Combine(_templatePath, defaultFolderName);
    }

    internal string ResolveFolderName(string defaultFolderName, ScriptObjectType type)
    {
        if (ResolvedFolders.TryGetValue(type, out var folder))
            return folder;
        return defaultFolderName;
    }

    private IDbConnection GetConnection(string targetDb)
    {
        var connectionStringOverride = CommandLineParser.ValueOfSwitch("ConnectionString", null);
        if (!string.IsNullOrEmpty(connectionStringOverride))
        {
            if (_platform == Platform.MySQL &&
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

        var connectionString = ConnectionString.Build(_platform, server, targetDb, user, password, port, connectionProperties);
        var connectionFactory = GetConnectionFactory();
        var connection = connectionFactory.GetDbConnection(connectionString);
        connection.Open();
        return connection;
    }

    private IDbConnectionFactory GetConnectionFactory()
    {
        return _platform switch
        {
            Platform.SqlServer => SqlServerConnectionFactory.GetFromFactory(),
            Platform.PostgreSQL => PostgreSqlConnectionFactory.GetFromFactory(),
            Platform.MySQL => MySqlConnectionFactory.GetFromFactory(),
            _ => throw new Exception($"Unsupported platform: {_platform}")
        };
    }

    public void CastTemplate()
    {
        _stopwatch.Start();
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var targetDb = config["Source:Database"] ?? config["Source:Schema"];
        if (string.IsNullOrEmpty(targetDb)) throw new Exception("Source database is required. Set 'Source:Database' in appsettings.json.");
        _productPath = Path.Combine(config["Product:Path"] ?? ".");

        LoadShouldCastSettings(config);

        _objectsToCast = (config["ShouldCast:ObjectList"]?.ToLower() ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        var configStyle = Enum.TryParse<CheckConstraintStyle>(config["Product:CheckConstraintStyle"], true, out var style)
            ? style : (CheckConstraintStyle?)null;

        var productFile = Path.Combine(_productPath, "Product.json");
        var productIsNew = !FileWrapper.GetFromFactory().Exists(productFile);

        RepositoryHelper.UpdateOrInitRepository(_productPath, config["Product:Name"], config["Template:Name"], targetDb, _platform);

        ApplyCheckConstraintStyle(productFile, productIsNew, configStyle);

        _templatePath = RepositoryHelper.UpdateOrInitTemplate(_productPath, config["Template:Name"], targetDb, _platform);

        ResolveFolderMappings();

        BuildFileIndexes();

        RepositoryHelper.WriteSchemaFiles(_productPath, _platform);

        CastDatabaseObjects(targetDb);
        CleanupResolvedSqulerrorFiles();
        ProcessOrphanedFiles();
        GenerateInvalidObjectCleanupScript();
        _stopwatch.Stop();
        LogSummary();
    }

    private void LoadShouldCastSettings(IConfigurationRoot config)
    {
        _orphanHandlingMode = Enum.TryParse<OrphanHandlingMode>(config["OrphanHandling:Mode"], true, out var mode)
            ? mode : OrphanHandlingMode.Detect;

        _validateScripts = config["ShouldCast:ValidateScripts"]?.ToLower() == "true";
        _saveInvalidScripts = config["ShouldCast:SaveInvalidScripts"]?.ToLower() != "false";

        _includeTables = config["ShouldCast:Tables"]?.ToLower() != "false";
        _includeViews = config["ShouldCast:Views"]?.ToLower() != "false";

        switch (_platform)
        {
            case Platform.SqlServer:
                _includeSchemas = config["ShouldCast:Schemas"]?.ToLower() != "false";
                _includeUserDefinedTypes = config["ShouldCast:UserDefinedTypes"]?.ToLower() != "false";
                _includeUserDefinedFunctions = config["ShouldCast:Functions"]?.ToLower() != "false";
                _includeStoredProcedures = config["ShouldCast:Procedures"]?.ToLower() != "false";
                _includeTableTriggers = config["ShouldCast:TableTriggers"]?.ToLower() != "false";
                _includeFullTextCatalogs = config["ShouldCast:Catalogs"]?.ToLower() != "false";
                _includeFullTextStopLists = config["ShouldCast:StopLists"]?.ToLower() != "false";
                _includeDDLTriggers = config["ShouldCast:DDLTriggers"]?.ToLower() != "false";
                _includeXmlSchemaCollections = config["ShouldCast:XMLSchemaCollections"]?.ToLower() != "false";
                _scriptDynamicDependencyRemovalForFunctions = config["ShouldCast:ScriptDynamicDependencyRemovalForFunctions"]?.ToLower() == "true";
                _includeIndexedViews = config["ShouldCast:IndexedViews"]?.ToLower() != "false";
                break;

            case Platform.PostgreSQL:
                _includeSchemas = config["ShouldCast:Schemas"]?.ToLower() != "false";
                _includeDomainTypes = config["ShouldCast:DomainTypes"]?.ToLower() != "false";
                _includeEnumTypes = config["ShouldCast:EnumTypes"]?.ToLower() != "false";
                _includeCompositeTypes = config["ShouldCast:CompositeTypes"]?.ToLower() != "false";
                _includeFunctions = config["ShouldCast:Functions"]?.ToLower() != "false";
                _includeAggregates = config["ShouldCast:Aggregates"]?.ToLower() != "false";
                _includeProcedures = config["ShouldCast:Procedures"]?.ToLower() != "false";
                _includeSequences = config["ShouldCast:Sequences"]?.ToLower() != "false";
                _includeRules = config["ShouldCast:Rules"]?.ToLower() != "false";
                _includeTriggers = config["ShouldCast:TableTriggers"]?.ToLower() != "false";
                _includeMaterializedViews = config["ShouldCast:MaterializedViews"]?.ToLower() != "false";
                break;

            case Platform.MySQL:
                _includeUserDefinedFunctions = config["ShouldCast:Functions"]?.ToLower() != "false";
                _includeStoredProcedures = config["ShouldCast:Procedures"]?.ToLower() != "false";
                _includeTableTriggers = config["ShouldCast:TableTriggers"]?.ToLower() != "false";
                _includeEvents = config["ShouldCast:Events"]?.ToLower() != "false";
                break;
        }
    }

    private void ApplyCheckConstraintStyle(string productFile, bool productIsNew, CheckConstraintStyle? configStyle)
    {
        var productJson = FileWrapper.GetFromFactory().ReadAllText(productFile);
        var product = productJson != null
            ? (JsonConvert.DeserializeObject<Product>(productJson) ?? new Product())
            : new Product();
        var productStyle = product.CheckConstraintStyle;

        if (productIsNew && configStyle.HasValue)
        {
            product.CheckConstraintStyle = configStyle.Value;
            Product.Save(productFile, product);
            _checkConstraintStyle = configStyle.Value;
        }
        else if (!productIsNew && configStyle.HasValue && configStyle.Value != productStyle)
        {
            _progressLog.Warn($"SchemaTongs config specifies CheckConstraintStyle '{configStyle.Value}' but Product.json is set to '{productStyle}'. " +
                              $"Extracting as '{productStyle}' per the product definition. Update Product.json to change this.");
            _checkConstraintStyle = productStyle;
        }
        else
        {
            _checkConstraintStyle = productStyle;
        }
    }

    private readonly ExtractionStats _stats = new();
    private readonly Dictionary<string, ExtractionFileIndex> _folderIndexes = new();
    private readonly List<string> _pendingSqulerrorCleanup = new();

    private void BuildFileIndexes()
    {
        var templateFile = Path.Combine(_templatePath, "Template.json");
        if (!FileWrapper.GetFromFactory().Exists(templateFile)) return;

        var template = JsonHelper.Load<Template>(templateFile);
        if (template == null) return;

        foreach (var folder in template.ScriptFolders)
        {
            var fullPath = Path.Combine(_templatePath, folder.FolderPath);
            var index = new ExtractionFileIndex();
            index.BuildIndex(fullPath);
            _folderIndexes[folder.FolderPath] = index;
        }

        foreach (var jsonFolder in new[] { "Tables", "Indexed Views", "Materialized Views" })
        {
            var fullPath = Path.Combine(_templatePath, jsonFolder);
            var index = new ExtractionFileIndex();
            index.BuildIndex(fullPath);
            _folderIndexes[jsonFolder] = index;
        }
    }

    private string ResolveOutputPath(string baseFolderPath, string fileName)
    {
        var folderName = GetRelativeFolderName(baseFolderPath);
        if (_folderIndexes.TryGetValue(folderName, out var index))
        {
            var existingPath = index.FindExistingPath(fileName);
            if (existingPath != null)
            {
                // If the existing file is .sqlerror but we're writing .sql, use the .sql path
                // and clean up the old .sqlerror file
                if (existingPath.EndsWith(".sqlerror", StringComparison.OrdinalIgnoreCase)
                    && fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                {
                    var sqlPath = Path.ChangeExtension(existingPath, ".sql");
                    index.MarkWritten(existingPath);
                    index.MarkWritten(sqlPath);
                    _pendingSqulerrorCleanup.Add(existingPath);
                    return sqlPath;
                }

                index.MarkWritten(existingPath);
                return existingPath;
            }
        }

        var defaultPath = Path.Combine(baseFolderPath, fileName);
        _folderIndexes.GetValueOrDefault(folderName)?.MarkWritten(defaultPath);
        return defaultPath;
    }

    private static string EncodeFileName(string schema, string name, string extension)
    {
        return $"{FileNameEncoder.Encode(schema)}.{FileNameEncoder.Encode(name)}{extension}";
    }

    private static string EncodeFileName(string name, string extension)
    {
        return $"{FileNameEncoder.Encode(name)}{extension}";
    }

    private static string EncodeFullName(string fullName, string extension)
    {
        var parts = fullName.Split('.');
        return string.Join(".", parts.Select(FileNameEncoder.Encode)) + extension;
    }

    private string GetRelativeFolderName(string fullPath)
    {
        if (fullPath.StartsWith(_templatePath))
        {
            var relative = fullPath.Substring(_templatePath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative;
        }
        return fullPath;
    }

    /// <summary>
    /// Checks if a script should be skipped because it has a known .sqlerror file and validation is off.
    /// When validation is disabled, existing .sqlerror files are preserved as known-bad markers.
    /// Call this BEFORE writing the .sql file — returns true if the write should be skipped.
    /// </summary>
    internal bool ShouldSkipKnownBadScript(string filePath)
    {
        if (_validateScripts) return false; // When validating, always extract (validation handles the rest)

        var errorPath = Path.ChangeExtension(filePath, ".sqlerror");
        var file = FileWrapper.GetFromFactory();
        if (!file.Exists(errorPath)) return false;

        _progressLog.Info($"  Skipping export for known invalid script: {Path.GetFileName(filePath)} (.sqlerror exists, validation off)");
        return true;
    }

    internal void ValidateAndHandleScript(IDbConnection connection, string filePath, string script,
        ScriptObjectType objectType)
    {
        if (!_validateScripts) return;

        // Skip validation for unchanged known-bad scripts — if an existing .sqlerror file
        // has identical content, the script hasn't changed on the server and will fail the same way
        var existingErrorPath = Path.ChangeExtension(filePath, ".sqlerror");
        var file = FileWrapper.GetFromFactory();
        if (file.Exists(existingErrorPath))
        {
            var existingContent = file.ReadAllText(existingErrorPath);
            if (string.Equals(existingContent, script, StringComparison.Ordinal))
            {
                _progressLog.Info($"  Skipping validation for unchanged invalid script: {Path.GetFileName(filePath)}");
                _invalidScripts.Add((Path.GetFileName(filePath), "Previously invalid — unchanged", objectType));
                // Remove the .sql file since we know it's still bad
                if (!filePath.Equals(existingErrorPath, StringComparison.OrdinalIgnoreCase))
                    file.Delete(filePath);
                return;
            }
            // Script changed on server — re-validate the new version
            _progressLog.Info($"  Re-validating changed script: {Path.GetFileName(filePath)}");
        }

        var result = _scriptValidator.ValidateScript(connection, script, objectType, _platform);
        if (result.IsValid) return;

        _progressLog.Warn($"  Invalid script detected: {Path.GetFileName(filePath)} — {result.ErrorMessage}");
        _invalidScripts.Add((Path.GetFileName(filePath), result.ErrorMessage, objectType));

        if (_saveInvalidScripts)
        {
            var errorPath = Path.ChangeExtension(filePath, ".sqlerror");
            file.WriteAllText(errorPath, script);
            if (!filePath.Equals(errorPath, StringComparison.OrdinalIgnoreCase))
                file.Delete(filePath);
        }
        else
        {
            file.Delete(filePath);
        }
    }

    private void CleanupResolvedSqulerrorFiles()
    {
        if (_pendingSqulerrorCleanup.Count == 0) return;

        var file = FileWrapper.GetFromFactory();
        foreach (var sqlerrorPath in _pendingSqulerrorCleanup)
        {
            if (file.Exists(sqlerrorPath))
            {
                file.Delete(sqlerrorPath);
                _progressLog.Info($"  Removed previously invalid script: {Path.GetFileName(sqlerrorPath)}");
            }
        }
    }

    internal void GenerateInvalidObjectCleanupScript()
    {
        if (_invalidScripts.Count == 0) return;

        var logsDir = Path.Combine(_templatePath, "Logs");
        var file = FileWrapper.GetFromFactory();
        var directory = DirectoryWrapper.GetFromFactory();

        // Archive existing cleanup scripts
        if (directory.Exists(logsDir))
        {
            var existingScripts = directory.GetFiles(logsDir, "_InvalidObjectCleanup.sql", SearchOption.TopDirectoryOnly);
            if (existingScripts.Length > 0)
            {
                var archiveDir = Path.Combine(logsDir, DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
                directory.CreateDirectory(archiveDir);
                foreach (var script in existingScripts)
                {
                    var destPath = Path.Combine(archiveDir, Path.GetFileName(script));
                    file.Move(script, destPath);
                }
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"-- {_invalidScripts.Count} invalid objects detected during extraction on {DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine();

        foreach (var (fileName, _, objectType) in _invalidScripts)
        {
            var drop = CleanupScriptGenerator.GenerateDropStatement(fileName, objectType, _platform);
            if (drop != null)
                sb.AppendLine(drop);
        }

        directory.CreateDirectory(logsDir);
        var scriptPath = Path.Combine(logsDir, "_InvalidObjectCleanup.sql");
        file.WriteAllText(scriptPath, sb.ToString());

        _progressLog.Warn($"{_invalidScripts.Count} invalid script(s) detected. Cleanup script written to {scriptPath}");
    }

    private void ProcessOrphanedFiles()
    {
        var fullyExtracted = GetFullyExtractedFolders();
        if (fullyExtracted.Count == 0) return;

        var folderObjectTypes = GetFolderObjectTypes();
        var logsDir = Path.Combine(_templatePath, "Logs");

        var handler = new OrphanHandler();
        handler.ProcessOrphans(_folderIndexes, _platform, _orphanHandlingMode, fullyExtracted, logsDir, folderObjectTypes);
    }

    private HashSet<string> GetFullyExtractedFolders()
    {
        if (_objectsToCast.Length > 0) return [];

        var folders = new HashSet<string>();

        // Common
        if (_includeTables) folders.Add("Tables");
        if (_includeViews) folders.Add(ResolveFolderName("Views", ScriptObjectType.Views));

        switch (_platform)
        {
            case Platform.SqlServer:
                if (_includeSchemas) folders.Add(ResolveFolderName("Schemas", ScriptObjectType.Schemas));
                if (_includeUserDefinedTypes) folders.Add(ResolveFolderName("DataTypes", ScriptObjectType.DataTypes));
                if (_includeUserDefinedFunctions) folders.Add(ResolveFolderName("Functions", ScriptObjectType.Functions));
                if (_includeStoredProcedures) folders.Add(ResolveFolderName("Procedures", ScriptObjectType.Procedures));
                if (_includeTableTriggers) folders.Add(ResolveFolderName("Triggers", ScriptObjectType.Triggers));
                if (_includeFullTextCatalogs) folders.Add(ResolveFolderName("FullTextCatalogs", ScriptObjectType.FullTextCatalogs));
                if (_includeFullTextStopLists) folders.Add(ResolveFolderName("FullTextStopLists", ScriptObjectType.FullTextStopLists));
                if (_includeDDLTriggers) folders.Add(ResolveFolderName("DDLTriggers", ScriptObjectType.DDLTriggers));
                if (_includeXmlSchemaCollections) folders.Add(ResolveFolderName("XMLSchemaCollections", ScriptObjectType.XMLSchemaCollections));
                if (_includeIndexedViews) folders.Add("Indexed Views");
                break;

            case Platform.PostgreSQL:
                if (_includeSchemas) folders.Add(ResolveFolderName("Schemas", ScriptObjectType.Schemas));
                if (_includeDomainTypes) folders.Add(ResolveFolderName("Domain Types", ScriptObjectType.DomainTypes));
                if (_includeEnumTypes) folders.Add(ResolveFolderName("Enum Types", ScriptObjectType.EnumTypes));
                if (_includeCompositeTypes) folders.Add(ResolveFolderName("Composite Types", ScriptObjectType.CompositeTypes));
                if (_includeFunctions)
                {
                    folders.Add(ResolveFolderName("Functions", ScriptObjectType.Functions));
                    folders.Add(ResolveFolderName("Trigger Functions", ScriptObjectType.TriggerFunctions));
                    folders.Add(ResolveFolderName("Window Functions", ScriptObjectType.WindowFunctions));
                }
                if (_includeAggregates) folders.Add(ResolveFolderName("Aggregates", ScriptObjectType.Aggregates));
                if (_includeProcedures) folders.Add(ResolveFolderName("Procedures", ScriptObjectType.Procedures));
                if (_includeSequences) folders.Add(ResolveFolderName("Sequences", ScriptObjectType.Sequences));
                if (_includeRules) folders.Add(ResolveFolderName("Rules", ScriptObjectType.Rules));
                if (_includeTriggers) folders.Add(ResolveFolderName("Triggers", ScriptObjectType.Triggers));
                if (_includeMaterializedViews) folders.Add("Materialized Views");
                break;

            case Platform.MySQL:
                if (_includeUserDefinedFunctions) folders.Add(ResolveFolderName("Functions", ScriptObjectType.Functions));
                if (_includeStoredProcedures) folders.Add(ResolveFolderName("Procedures", ScriptObjectType.Procedures));
                if (_includeTableTriggers) folders.Add(ResolveFolderName("Triggers", ScriptObjectType.Triggers));
                if (_includeEvents) folders.Add(ResolveFolderName("Events", ScriptObjectType.Events));
                break;
        }

        return folders;
    }

    private Dictionary<string, ScriptObjectType> GetFolderObjectTypes()
    {
        var map = new Dictionary<string, ScriptObjectType>();
        foreach (var (folderName, _) in _folderIndexes)
        {
            var objectType = folderName switch
            {
                "Tables" => ScriptObjectType.None,
                "Indexed Views" => ScriptObjectType.None,
                "Materialized Views" => ScriptObjectType.None,
                _ => ScriptFolderTypeInference.InferFromFolderName(folderName)
            };
            map[folderName] = objectType;
        }
        return map;
    }

    private void CastDatabaseObjects(string targetDb)
    {
        switch (_platform)
        {
            case Platform.SqlServer:
                CastSqlServerObjects(targetDb);
                break;
            case Platform.PostgreSQL:
                CastPostgreSqlObjects(targetDb);
                break;
            case Platform.MySQL:
                CastMySqlObjects(targetDb);
                break;
            default:
                throw new Exception($"Unsupported platform: {_platform}");
        }
    }

    #region SQL Server Extraction

    private void CastSqlServerObjects(string targetDb)
    {
        using var connection = GetConnection(targetDb);
        try
        {
            using var command = connection.CreateCommand();

            _progressLog.Info("Kindling The Forge");
            ForgeKindler.KindleTheForge(command, _platform);

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
    }

    private void ScriptSqlServerSchemas(IDbCommand command)
    {
        _progressLog.Info("Casting Schema Scripts");
        var castPath = GetCastPath(ScriptObjectType.Schemas, "Schemas");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name, s.schema_id
  FROM sys.schemas s
 WHERE s.schema_id > 4
   AND s.name NOT LIKE 'db[_]%'
   AND s.name NOT LIKE '%\%'
   AND s.name <> 'SchemaSmith'
   AND s.principal_id IS NOT NULL
 ORDER BY s.name";

        var schemas = new List<(string Name, int SchemaId)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var schemaId = reader.GetInt32(1);
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower())) continue;
                schemas.Add((name, schemaId));
            }
        }

        foreach (var (name, schemaId) in schemas)
        {
            var escapedName = EscapeSql(name);
            var script = $"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'{escapedName}')\r\n" +
                         $"EXEC sys.sp_executesql N'CREATE SCHEMA [{name}]'\r\n";

            var extProps = GetExtendedProperties(command, "SCHEMA", name);
            if (extProps.Length > 0)
                script += "\r\n" + extProps;

            var fileName = ResolveOutputPath(castPath, EncodeFileName(name, ".sql"));
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            _stats.Schemas++;
        }
    }

    internal string GetExtendedProperties(IDbCommand command, string level0Type, string level0Name,
        string level1Type = null, string level1Name = null,
        string level2Type = null, string level2Name = null)
    {
        var fnLevel1Type = level1Type != null ? $"N'{level1Type}'" : "NULL";
        var fnLevel1Name = level1Type != null ? $"N'{EscapeSql(level1Name)}'" : "NULL";
        var fnLevel2Type = level2Type != null ? $"N'{level2Type}'" : "NULL";
        var fnLevel2Name = level2Type != null ? $"N'{EscapeSql(level2Name)}'" : "NULL";

        command.CommandText = $@"
SELECT name, CAST(value AS NVARCHAR(MAX)) AS value
  FROM sys.fn_listextendedproperty(NULL, N'{level0Type}', N'{EscapeSql(level0Name)}', {fnLevel1Type}, {fnLevel1Name}, {fnLevel2Type}, {fnLevel2Name})
 ORDER BY name";

        var properties = new List<(string Name, string Value)>();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (!InternalExtendedProperties.IsInternal(name))
                    properties.Add((name, reader.GetString(1)));
            }
        }
        catch (Exception)
        {
            // fn_listextendedproperty raises an error when the referenced object doesn't exist
        }

        if (properties.Count == 0) return "";

        var spLevel0 = $" , @level0type=N'{level0Type}',@level0name=N'{EscapeSql(level0Name)}'";
        var spLevel1 = level1Type != null ? $" , @level1type=N'{level1Type}',@level1name=N'{EscapeSql(level1Name)}'" : "";
        var spLevel2 = level2Type != null ? $" , @level2type=N'{level2Type}',@level2name=N'{EscapeSql(level2Name)}'" : "";

        var lines = new List<string>();
        foreach (var (propName, propValue) in properties)
        {
            var escapedValue = propValue.Replace("'", "''");
            var escapedPropName = EscapeSql(propName);
            var fnArgs = $"N'{escapedPropName}' , N'{level0Type}',N'{EscapeSql(level0Name)}', {fnLevel1Type},{fnLevel1Name}, {fnLevel2Type},{fnLevel2Name}";
            var addArgs = $"@name=N'{escapedPropName}', @value=N'{escapedValue}'{spLevel0}{spLevel1}{spLevel2}";
            var updateArgs = $"@name=N'{escapedPropName}', @value=N'{escapedValue}'{spLevel0}{spLevel1}{spLevel2}";

            lines.Add(
                $"IF NOT EXISTS (SELECT * FROM sys.fn_listextendedproperty({fnArgs}))\r\n" +
                $"\tEXEC sys.sp_addextendedproperty {addArgs}\r\n" +
                $"ELSE\r\n" +
                $"BEGIN\r\n" +
                $"\tEXEC sys.sp_updateextendedproperty {updateArgs}\r\n" +
                $"END\r\n");
        }

        return string.Join("", lines);
    }

    private void ScriptSqlServerUserDefinedTypes(IDbCommand command)
    {
        _progressLog.Info("Casting User Defined Types");
        var castPath = GetCastPath(ScriptObjectType.DataTypes, "DataTypes");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
        ScriptSqlServerAliasTypes(command, castPath);
        ScriptSqlServerTableTypes(command, castPath);
    }

    private void ScriptSqlServerAliasTypes(IDbCommand command, string castPath)
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

            var fileName = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".sql"));
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            _stats.DataTypes++;
        }
    }

    private void ScriptSqlServerTableTypes(IDbCommand command, string castPath)
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

            var fileName = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".sql"));
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            _stats.DataTypes++;
        }
    }

    private void ScriptSqlServerFunctions(IDbCommand command)
    {
        _progressLog.Info("Casting Function Scripts");
        var castPath = GetCastPath(ScriptObjectType.Functions, "Functions");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name AS SchemaName, o.name AS ObjectName
  FROM sys.objects o
  JOIN sys.schemas s ON o.schema_id = s.schema_id
  LEFT JOIN sys.sql_modules sm ON o.object_id = sm.object_id
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
            if (sql == null) continue;

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
                sql = sql.Substring(0, firstGoEnd) + dependencyBlock + sql.Substring(firstGoEnd);
            }

            var fileName = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".sql"));
            if (ShouldSkipKnownBadScript(fileName)) { _stats.Functions++; continue; }
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
            ValidateAndHandleScript(command.Connection, fileName, sql, ScriptObjectType.Functions);
            _stats.Functions++;
        }
    }

    private void ScriptSqlServerViews(IDbCommand command)
    {
        _progressLog.Info("Casting View Scripts");
        var castPath = GetCastPath(ScriptObjectType.Views, "Views");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT s.name AS SchemaName, o.name AS ObjectName
  FROM sys.objects o
  JOIN sys.schemas s ON o.schema_id = s.schema_id
  LEFT JOIN sys.sql_modules sm ON o.object_id = sm.object_id
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
            if (sql == null) continue;

            var fileName = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".sql"));
            if (ShouldSkipKnownBadScript(fileName)) { _stats.Views++; continue; }
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
            ValidateAndHandleScript(command.Connection, fileName, sql, ScriptObjectType.Views);
            _stats.Views++;
        }
    }

    private void ScriptSqlServerProcedures(IDbCommand command)
    {
        _progressLog.Info("Casting Stored Procedure Scripts");
        var castPath = GetCastPath(ScriptObjectType.Procedures, "Procedures");
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
            if (sql == null) continue;

            var fileName = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".sql"));
            if (ShouldSkipKnownBadScript(fileName)) { _stats.Procedures++; continue; }
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
            ValidateAndHandleScript(command.Connection, fileName, sql, ScriptObjectType.Procedures);
            _stats.Procedures++;
        }
    }

    private void ScriptSqlServerTableTriggers(IDbCommand command)
    {
        _progressLog.Info("Casting Table Trigger Scripts");
        var castPath = GetCastPath(ScriptObjectType.Triggers, "Triggers");
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
            if (sql == null) continue;

            var escapedSchema = Regex.Escape(tableSchema);
            var escapedTable = Regex.Escape(tableName);
            var tablePattern = $@"(?<=\bON\s+)\[?{escapedSchema}\]?\.\[?{escapedTable}\]?";
            sql = Regex.Replace(sql, tablePattern, $"[{tableSchema}].[{tableName}]", RegexOptions.IgnoreCase);

            var fileName = ResolveOutputPath(castPath, $"{FileNameEncoder.Encode(tableSchema)}.{FileNameEncoder.Encode(tableName)}.{FileNameEncoder.Encode(triggerName)}.sql");
            if (ShouldSkipKnownBadScript(fileName)) { _stats.Triggers++; continue; }
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
            ValidateAndHandleScript(command.Connection, fileName, sql, ScriptObjectType.Triggers);
            _stats.Triggers++;
        }
    }

    private void ScriptSqlServerFullTextCatalogs(IDbCommand command)
    {
        _progressLog.Info("Casting FullText Catalog Scripts");
        var castPath = GetCastPath(ScriptObjectType.FullTextCatalogs, "FullTextCatalogs");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
SELECT name
  FROM sys.fulltext_catalogs
 ORDER BY name";

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

            var fileName = ResolveOutputPath(castPath, EncodeFileName(name, ".sql"));
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            _stats.FullTextCatalogs++;
        }
    }

    private void ScriptSqlServerFullTextStopLists(IDbCommand command)
    {
        _progressLog.Info("Casting FullText Stop List Scripts");
        var castPath = GetCastPath(ScriptObjectType.FullTextStopLists, "FullTextStopLists");
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

            var fileName = ResolveOutputPath(castPath, EncodeFileName(name, ".sql"));
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            _stats.FullTextStopLists++;
        }
    }

    private void ScriptSqlServerDDLTriggers(IDbCommand command)
    {
        _progressLog.Info("Casting Database DDL Trigger Scripts");
        var castPath = GetCastPath(ScriptObjectType.DDLTriggers, "DDLTriggers");
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

            var extProps = GetExtendedProperties(command, "TRIGGER", triggerName);

            var ansiNulls = usesAnsiNulls ? "ON" : "OFF";
            var quotedIdentifier = usesQuotedIdentifier ? "ON" : "OFF";

            var sql = $"SET ANSI_NULLS {ansiNulls}\r\n" +
                      $"SET QUOTED_IDENTIFIER {quotedIdentifier}\r\n" +
                      $"GO\r\n\r\n" +
                      $"{definition}\r\n\r\n" +
                      $"GO\r\n";

            if (extProps.Length > 0)
                sql += extProps + "\r\nGO\r\n";

            var fileName = ResolveOutputPath(castPath, EncodeFileName(triggerName, ".sql"));
            if (ShouldSkipKnownBadScript(fileName)) { _stats.DDLTriggers++; continue; }
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, sql);
            ValidateAndHandleScript(command.Connection, fileName, sql, ScriptObjectType.DDLTriggers);
            _stats.DDLTriggers++;
        }
    }

    private void ScriptSqlServerXmlSchemaCollections(IDbCommand command)
    {
        _progressLog.Info("Casting XML Schema Collection Scripts");
        var castPath = GetCastPath(ScriptObjectType.XMLSchemaCollections, "XMLSchemaCollections");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        command.CommandText = @"
            SELECT s.name AS SchemaName, xsc.name AS CollectionName
              FROM sys.xml_schema_collections xsc
              JOIN sys.schemas s ON xsc.schema_id = s.schema_id
             WHERE xsc.xml_collection_id > 1
             ORDER BY s.name, xsc.name";
        var collections = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                collections.Add((reader.GetString(0), reader.GetString(1)));

        foreach (var (schema, name) in collections)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(name.ToLower()) && !_objectsToCast.Contains($"{schema}.{name}".ToLower())) continue;

            command.CommandText = $"SELECT CAST(XML_SCHEMA_NAMESPACE(N'{EscapeSql(schema)}', N'{EscapeSql(name)}') AS NVARCHAR(MAX))";
            var xmlContent = (string)command.ExecuteScalar();

            var script =
                $"IF NOT EXISTS (SELECT * FROM sys.xml_schema_collections c, sys.schemas s WHERE c.schema_id = s.schema_id AND (quotename(s.name) + '.' + quotename(c.name)) = N'[{schema}].[{name}]')\r\n" +
                $"CREATE XML SCHEMA COLLECTION [{schema}].[{name}] AS N'{xmlContent}'";
            script = FormatXmlInScript(script);

            var extProps = GetExtendedProperties(command, "SCHEMA", schema, "XML SCHEMA COLLECTION", name);
            if (extProps.Length > 0)
                script += "\r\nGO\r\n" + extProps;

            var fileName = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".sql"));
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            _stats.XmlSchemaCollections++;
        }
    }

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

    private string ScriptSqlServerProgrammableObject(IDbCommand command, string schemaName, string objectName,
        string level1Type, string level2Type = null, string level2ParentName = null)
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

        var level1Name = level2Type != null ? level2ParentName : objectName;
        var extPropsLevel1Type = level2Type != null ? "TABLE" : level1Type;
        var extPropsLevel2Type = level2Type;
        var extPropsLevel2Name = level2Type != null ? objectName : null;
        var extProps = GetExtendedProperties(command, "SCHEMA", schemaName,
            extPropsLevel1Type, level1Name, extPropsLevel2Type, extPropsLevel2Name);

        var ansiNulls = usesAnsiNulls ? "ON" : "OFF";
        var quotedIdentifier = usesQuotedIdentifier ? "ON" : "OFF";

        var script = $"SET ANSI_NULLS {ansiNulls}\r\n" +
                     $"SET QUOTED_IDENTIFIER {quotedIdentifier}\r\n" +
                     $"GO\r\n\r\n" +
                     $"{definition}\r\n\r\n" +
                     $"GO\r\n";

        if (extProps.Length > 0)
            script += extProps + "\r\nGO\r\n";

        return script;
    }

    internal static string FormatXmlInScript(string script)
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
            if (string.IsNullOrWhiteSpace(xml)) return xml;
            var formatted = XDocument.Parse(xml).ToString();
            if (!formatted.Contains("\r\n"))
                formatted = formatted.Replace("\n", "\r\n");
            return formatted;
        }
        catch
        {
            return xml;
        }
    }

    private void CastSqlServerIndexedViews(IDbCommand command)
    {
        command.CommandText = @"
SELECT s.name AS SchemaName, v.name AS ViewName
  FROM sys.views v
 INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
 WHERE OBJECTPROPERTY(v.object_id, 'IsIndexed') = 1
   AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'SchemaSmith')
   AND v.is_ms_shipped = 0
 ORDER BY s.name, v.name";

        _progressLog.Info("Casting Indexed View Structures");
        var indexedViews = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read()) indexedViews.Add((reader["SchemaName"].ToString(), reader["ViewName"].ToString()));
        }

        if (indexedViews.Count == 0) return;

        // Install the extraction function
        command.CommandText = ResourceLoader.Load("SchemaSmith.GenerateIndexedViewJson.sql", _platform);
        command.ExecuteNonQuery();

        var castPath = Path.Combine(_templatePath, "Indexed Views");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        foreach (var (schema, name) in indexedViews)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains($"{schema}.{name}".ToLower()) && !_objectsToCast.Contains(name.ToLower())) continue;

            _progressLog.Info($"  Cast Json for {schema}.{name}");
            command.CommandText = $"SELECT [SchemaSmith].[GenerateIndexedViewJson]('{schema}', '{name}')";
            var viewJson = command.ExecuteScalar()?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(viewJson) || viewJson.Trim().Equals("{}"))
            {
                _progressLog.Error($"    No json returned for {schema}.{name}");
                continue;
            }
            var viewObj = JsonConvert.DeserializeObject<SqlServerIndexedView>(viewJson);
            var viewFile = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".json"));
            _progressLog.Info($"    Casting {viewFile}");
            JsonHelper.Write(viewFile, viewObj);
            _stats.IndexedViews++;
        }
    }

    #endregion

    #region PostgreSQL Extraction

    private void CastPostgreSqlObjects(string targetDb)
    {
        using var connection = GetConnection(targetDb);
        try
        {
            using var command = connection.CreateCommand();

            _progressLog.Info("Kindling The Forge");
            ForgeKindler.KindleTheForge(command, _platform);

            if (_includeTables) CastPostgreSqlTableDefinitions(command);
            if (_includeSchemas) CastPostgreSqlSchemas(command);
            if (_includeDomainTypes) CastPostgreSqlDomainTypes(command);
            if (_includeEnumTypes) CastPostgreSqlEnumTypes(command);
            if (_includeCompositeTypes) CastPostgreSqlCompositeTypes(command);
            if (_includeFunctions) CastPostgreSqlFunctions(command);
            if (_includeAggregates) CastPostgreSqlAggregates(command);
            if (_includeProcedures) CastPostgreSqlProcedures(command);
            if (_includeSequences) CastPostgreSqlSequences(command);
            if (_includeRules) CastPostgreSqlRules(command);
            if (_includeTriggers) CastPostgreSqlTriggers(command);
            if (_includeViews) CastPostgreSqlViews(command);
            if (_includeMaterializedViews) CastPostgreSqlMaterializedViews(command);
        }
        finally
        {
            connection.Close();
        }
    }

    private void CastPostgreSqlTableDefinitions(IDbCommand command)
    {
        command.CommandText = @"
SELECT t.schemaname, t.tablename
  FROM pg_tables t
  JOIN pg_class c ON c.relname = t.tablename
                 AND c.relnamespace = (SELECT n.oid FROM pg_namespace n WHERE n.nspname = t.schemaname)
                 AND c.relpersistence = 'p'
  WHERE t.schemaname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
  ORDER BY t.schemaname, t.tablename;
";

        _progressLog.Info("Casting Table Structures");
        var tables = new List<(string Schema, string Table)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read()) tables.Add((reader["schemaname"].ToString(), reader["tablename"].ToString()));
        }

        var castPath = Path.Combine(_templatePath, "Tables");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        foreach (var (schema, table) in tables)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains($"{schema}.{table}".ToLower()) && !_objectsToCast.Contains(table.ToLower())) continue;

            _progressLog.Info($"  Cast Json for {schema}.{table}");
            command.CommandText = $"SELECT \"SchemaSmith\".\"GenerateTableJSON\"('{schema}', '{table}')";
            var tableJson = command.ExecuteScalar()?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(tableJson) || tableJson.Trim().Equals("{}"))
            {
                _progressLog.Error($"    No json returned for {schema}.{table}");
                _stats.TableErrors++;
                continue;
            }
            var tableObj = JsonConvert.DeserializeObject<Table>(tableJson);
            var tableFile = ResolveOutputPath(castPath, EncodeFileName(schema, table, ".json"));
            var oldTableFile = ResolveOutputPath(castPath, EncodeFileName(schema, tableObj.OldName.Trim('"'), ".json"));
            _progressLog.Info($"    Casting {tableFile}");
            if (FileWrapper.GetFromFactory().Exists(tableFile) || FileWrapper.GetFromFactory().Exists(oldTableFile))
            {
                var original = JsonHelper.Load<Table>(FileWrapper.GetFromFactory().Exists(tableFile) ? tableFile : oldTableFile);
                ImportTableHelper.PreserveDataDeliveryAndCustomProperties(tableObj, original);
            }
            JsonHelper.Write(tableFile, tableObj);
            _stats.Tables++;
        }
    }

    private void CastPostgreSqlSchemas(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Schemas' AS Folder,
       n.nspname AS FullName,
       'CREATE SCHEMA IF NOT EXISTS ' || QUOTE_IDENT(n.nspname) ||
       ';' AS Code
  FROM pg_namespace n
  WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'public', 'SchemaSmith')
    AND n.nspname NOT LIKE 'pg_temp_%'
    AND n.nspname NOT LIKE 'pg_toast_temp_%';
";
        PerformPostgreSqlCasting(command, "Schemas");
    }

    private void CastPostgreSqlDomainTypes(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Domain Types' AS Folder,
       n.nspname || '.' || t.typname AS FullName,
       '
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = ''' || n.nspname || ''' AND t.typname = ''' || t.typname || ''') THEN
        CREATE DOMAIN ""' || n.nspname || '"".""' || t.typname || '"" AS ' || FORMAT_TYPE(t.typbasetype, t.typtypmod) ||
               CASE WHEN t.typnotnull THEN ' NOT NULL' ELSE '' END ||
               COALESCE((SELECT ' DEFAULT ' || PG_GET_EXPR(ad.adbin, ad.adrelid) FROM pg_attrdef ad WHERE ad.adrelid = 0 AND ad.oid = t.oid), '') ||
               COALESCE((SELECT STRING_AGG(' CONSTRAINT ' || QUOTE_IDENT(conname) || ' ' || PG_GET_CONSTRAINTDEF(c.oid, true), ' ') FROM pg_constraint c WHERE c.contypid = t.oid AND c.contype = 'c'), '') || ';
    END IF;
END
$$;' AS Code
  FROM pg_type t
  JOIN pg_namespace n ON n.oid = t.typnamespace
  WHERE t.typtype = 'd'
    AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND n.nspname NOT LIKE 'pg_temp_%'
    AND n.nspname NOT LIKE 'pg_toast_temp_%';
";
        PerformPostgreSqlCasting(command, "Domain Types");
    }

    private void CastPostgreSqlEnumTypes(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Enum Types' AS Folder,
       n.nspname || '.' || t.typname AS FullName,
       '
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = ''' || n.nspname || ''' AND t.typname = ''' || t.typname || ''') THEN
        CREATE TYPE ""' || n.nspname || '"".""' || t.typname || '"" AS ENUM (' || STRING_AGG(QUOTE_LITERAL(e.enumlabel), ', ' ORDER BY e.enumsortorder) || ');
    END IF;
END
$$;' AS Code
  FROM pg_type t
  JOIN pg_namespace n ON n.oid = t.typnamespace
  JOIN pg_enum e ON t.oid = e.enumtypid
  WHERE t.typtype = 'e'
    AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND n.nspname NOT LIKE 'pg_temp_%'
    AND n.nspname NOT LIKE 'pg_toast_temp_%'
  GROUP BY n.nspname, t.typname;
";
        PerformPostgreSqlCasting(command, "Enum Types");
    }

    private void CastPostgreSqlCompositeTypes(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Composite Types' AS Folder,
       ns.nspname || '.' || t.typname AS FullName,
       '
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = ''' || ns.nspname || ''' AND t.typname = ''' || t.typname || ''') THEN
        CREATE TYPE ""' || ns.nspname || '"".""' || t.typname || '""  AS (' || STRING_AGG(a.attname || ' ' || FORMAT_TYPE(a.atttypid, a.atttypmod), ', ' ORDER BY a.attnum) || ');
    END IF;
END
$$;' AS Code
  FROM pg_type t
  JOIN pg_class c ON t.typrelid = c.oid
  JOIN pg_attribute a ON c.oid = a.attrelid
  JOIN pg_namespace ns ON t.typnamespace = ns.oid
  WHERE t.typtype = 'c'
    AND a.attnum > 0
    AND pg_type_is_visible(t.oid)
    AND ns.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND ns.nspname NOT LIKE 'pg_temp_%'
    AND ns.nspname NOT LIKE 'pg_toast_temp_%'
    AND NOT EXISTS (SELECT * FROM pg_Tables tbl WHERE tbl.schemaname = ns.nspname AND tbl.tablename = t.typname)
    AND NOT EXISTS (SELECT * FROM pg_views v WHERE v.schemaname = ns.nspname AND v.viewname = t.typname)
  GROUP BY ns.nspname, t.typname;
";
        PerformPostgreSqlCasting(command, "Composite Types");
    }

    private void CastPostgreSqlFunctions(IDbCommand command)
    {
        command.CommandText = @"
SELECT CASE p.prokind
            WHEN 'f' THEN CASE WHEN prorettype = (SELECT oid FROM pg_type WHERE typname = 'trigger') THEN 'Trigger ' ELSE '' END || 'Functions'
            WHEN 'w' THEN 'Window Functions'
            END AS Folder,
       n.nspname || '.' || p.proname AS FullName,
       PG_GET_FUNCTIONDEF(p.oid) AS Code
  FROM pg_proc p
  JOIN pg_namespace n ON p.pronamespace = n.oid
                     AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
                     AND n.nspname NOT LIKE 'pg_temp_%'
                     AND n.nspname NOT LIKE 'pg_toast_temp_%'
  WHERE p.prokind IN ('f', 'w');
";
        PerformPostgreSqlCasting(command, "Functions");
    }

    private void CastPostgreSqlAggregates(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Aggregates' AS Folder,
       n.nspname || '.' || p.proname AS FullName,
       'CREATE OR REPLACE AGGREGATE ' || n.nspname || '.' || p.proname || '(' || FORMAT_TYPE(a.aggtranstype, null) || ') '
       || '(sfunc = ' || a.aggtransfn
       || ', stype = ' || FORMAT_TYPE(a.aggtranstype, null)
       || CASE WHEN op.oprname IS NULL THEN '' ELSE ', sortop = ' || op.oprname END
       || CASE WHEN a.agginitval IS NULL THEN '' ELSE ', initcond = ' || a.agginitval END
       || ')' AS Code
  FROM pg_proc p
  JOIN pg_namespace n ON p.pronamespace = n.oid
  JOIN pg_aggregate a ON a.aggfnoid = p.oid
  LEFT JOIN pg_operator op ON op.oid = a.aggsortop
  WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND n.nspname NOT LIKE 'pg_temp_%'
    AND n.nspname NOT LIKE 'pg_toast_temp_%';
";
        PerformPostgreSqlCasting(command, "Aggregates");
    }

    private void CastPostgreSqlProcedures(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Procedures' AS Folder,
       n.nspname || '.' || p.proname AS FullName,
       PG_GET_FUNCTIONDEF(p.oid) AS Code
  FROM pg_proc p
  JOIN pg_namespace n ON p.pronamespace = n.oid
                     AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
                     AND n.nspname NOT LIKE 'pg_temp_%'
                     AND n.nspname NOT LIKE 'pg_toast_temp_%'
  WHERE p.prokind = 'p';
";
        PerformPostgreSqlCasting(command, "Procedures");
    }

    private void CastPostgreSqlSequences(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Sequences' AS Folder,
       s.relnamespace::regnamespace || '.' || s.relname AS FullName,
       'CREATE SEQUENCE IF NOT EXISTS ""' || s.relnamespace::regnamespace || '"".""' || s.relname || E'""\n' ||
       '  ' || CASE WHEN seq.seqtypid = 'smallint'::regtype THEN 'AS SMALLINT '
                    WHEN seq.seqtypid = 'integer'::regtype THEN 'AS INT '
                    WHEN seq.seqtypid = 'bigint'::regtype THEN 'AS BIGINT '
                    ELSE '' END || 'INCREMENT BY ' || seq.seqincrement || E'\n' ||
       '  MINVALUE ' || seq.seqmin || E'\n' ||
       '  MAXVALUE ' || seq.seqmax || E'\n' ||
       '  START WITH ' || seq.seqstart || E'\n' ||
       '  CACHE ' || seq.seqcache || E'\n' ||
       '  ' || CASE WHEN seq.seqcycle THEN 'CYCLE' ELSE 'NO CYCLE' END ||
       ';' AS Code
  FROM pg_class s
  JOIN pg_sequence seq ON s.oid = seq.seqrelid
  JOIN pg_namespace n ON n.oid = s.relnamespace
                     AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
                     AND n.nspname NOT LIKE 'pg_temp_%'
                     AND n.nspname NOT LIKE 'pg_toast_temp_%'
  WHERE s.relkind = 'S'
    AND NOT EXISTS (SELECT 1 FROM pg_depend d
                    WHERE d.objid = s.oid AND d.deptype = 'i'
                      AND d.classid = 'pg_class'::regclass);
";
        PerformPostgreSqlCasting(command, "Sequences");
    }

    private void CastPostgreSqlRules(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Rules' AS Folder,
       ns.nspname || '.' || c.relname || '.' || r.rulename AS FullName,
       REPLACE(PG_GET_RULEDEF(r.oid), 'CREATE RULE', 'CREATE OR REPLACE RULE') AS Code
  FROM pg_rewrite r
  JOIN pg_class c ON r.ev_class = c.oid
  JOIN pg_namespace ns ON c.relnamespace = ns.oid
  WHERE ns.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND ns.nspname NOT LIKE 'pg_temp_%'
    AND ns.nspname NOT LIKE 'pg_toast_temp_%'
    AND r.rulename != '_RETURN';
";
        PerformPostgreSqlCasting(command, "Rules");
    }

    private void CastPostgreSqlTriggers(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Triggers' AS Folder,
       ns.nspname || '.' || tbl.relname || '.' || t.tgname AS FullName,
       REPLACE(PG_GET_TRIGGERDEF(t.oid), 'CREATE TRIGGER', 'CREATE OR REPLACE TRIGGER') AS Code
  FROM pg_trigger t
  JOIN pg_class tbl ON t.tgrelid = tbl.oid
  JOIN pg_namespace ns ON tbl.relnamespace = ns.oid
  WHERE NOT t.tgisinternal
    AND ns.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND ns.nspname NOT LIKE 'pg_temp_%'
    AND ns.nspname NOT LIKE 'pg_toast_temp_%';
";
        PerformPostgreSqlCasting(command, "Triggers");
    }

    private void CastPostgreSqlViews(IDbCommand command)
    {
        command.CommandText = @"
SELECT 'Views' AS Folder,
       schemaname || '.' || viewname AS FullName,
       'CREATE OR REPLACE VIEW ""' || schemaname || '"".""' || viewname || '"" AS' || E'\n' || definition AS Code
  FROM pg_views
  WHERE schemaname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND schemaname NOT LIKE 'pg_temp_%'
    AND schemaname NOT LIKE 'pg_toast_temp_%';
";
        PerformPostgreSqlCasting(command, "Views");
    }

    private void CastPostgreSqlMaterializedViews(IDbCommand command)
    {
        command.CommandText = @"
SELECT mv.schemaname, mv.matviewname
  FROM pg_matviews mv
  WHERE mv.schemaname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'SchemaSmith')
    AND mv.schemaname NOT LIKE 'pg_temp_%'
    AND mv.schemaname NOT LIKE 'pg_toast_temp_%'
  ORDER BY mv.schemaname, mv.matviewname;
";

        _progressLog.Info("Casting Materialized View Structures");
        var matViews = new List<(string Schema, string Name)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read()) matViews.Add((reader["schemaname"].ToString(), reader["matviewname"].ToString()));
        }

        if (matViews.Count == 0) return;

        // Install the extraction function
        command.CommandText = ResourceLoader.Load("SchemaSmith.GenerateMaterializedViewJson.sql", _platform);
        command.ExecuteNonQuery();

        var castPath = Path.Combine(_templatePath, "Materialized Views");
        DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);

        foreach (var (schema, name) in matViews)
        {
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains($"{schema}.{name}".ToLower()) && !_objectsToCast.Contains(name.ToLower())) continue;

            _progressLog.Info($"  Cast Json for {schema}.{name}");
            command.CommandText = $"SELECT \"SchemaSmith\".\"GenerateMaterializedViewJson\"('{schema}', '{name}')";
            var viewJson = command.ExecuteScalar()?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(viewJson) || viewJson.Trim().Equals("{}"))
            {
                _progressLog.Error($"    No json returned for {schema}.{name}");
                continue;
            }
            var viewObj = JsonConvert.DeserializeObject<PostgreSqlMaterializedView>(viewJson);
            var viewFile = ResolveOutputPath(castPath, EncodeFileName(schema, name, ".json"));
            _progressLog.Info($"    Casting {viewFile}");
            JsonHelper.Write(viewFile, viewObj);
            _stats.MaterializedViews++;
        }
    }

    private void PerformPostgreSqlCasting(IDbCommand command, string castType)
    {
        _progressLog.Info($"Casting {castType}");

        var records = new List<(string CastPath, string FullName, string FileName, string Script, string FolderName, bool Skip)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var castPath = Path.Combine(_templatePath, reader["Folder"].ToString());
                DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
                var fullName = reader["FullName"].ToString();
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(fullName.ToLower()) && !_objectsToCast.Contains($"{fullName}.~~~".Split('.')[1].ToLower())) continue;

                var fileName = ResolveOutputPath(castPath, EncodeFullName(fullName, ".sql"));
                var folderName = reader["Folder"].ToString();
                if (ShouldSkipKnownBadScript(fileName)) { IncrementStatForFolder(folderName); continue; }
                var script = string.Join("\r\n", reader["Code"].ToString());
                records.Add((castPath, fullName, fileName, script, folderName, false));
            }
        }

        foreach (var (_, _, fileName, script, folderName, _) in records)
        {
            _progressLog.Info($"  Casting {fileName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            var objectType = ScriptFolderTypeInference.InferFromFolderName(folderName);
            ValidateAndHandleScript(command.Connection, fileName, script, objectType);
            IncrementStatForFolder(folderName);
        }
    }

    #endregion

    #region MySQL Extraction

    private void CastMySqlObjects(string targetSchema)
    {
        try
        {
            using var connection = GetConnection(targetSchema);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SET sql_mode='PIPES_AS_CONCAT';";
                command.ExecuteNonQuery();

                _progressLog.Info("Kindling The Forge");
                ForgeKindler.KindleTheForge(command, _platform);

                if (_includeTables) ExtractMySqlTableDefinitions(command, targetSchema);
                if (_includeUserDefinedFunctions) ScriptMySqlFunctions(command, targetSchema);
                if (_includeViews) ScriptMySqlViews(command, targetSchema);
                if (_includeStoredProcedures) ScriptMySqlProcedures(command, targetSchema);
                if (_includeTableTriggers) ScriptMySqlTriggers(command, targetSchema);
                if (_includeEvents) ScriptMySqlEvents(command, targetSchema);
            }
            finally
            {
                connection.Close();
            }
        }
        catch (MySqlException ex)
        {
            _progressLog.Error($"MySQL Error: {ex.Message}");
            throw new Exception($"Database connection or query failed: {ex.Message}", ex);
        }
    }

    private void ScriptMySqlFunctions(IDbCommand command, string targetSchema)
    {
        _progressLog.Info("Casting Function Scripts");
        command.CommandText = $@"
SELECT 'Functions' AS Folder,
       ROUTINE_NAME AS FullName,
       'DROP FUNCTION IF EXISTS `' || ROUTINE_NAME || '`;\nDELIMITER //\n' ||
       'CREATE FUNCTION `' || ROUTINE_NAME || '` (' ||
       COALESCE((SELECT GROUP_CONCAT(PARAMETER_NAME || ' ' || DTD_IDENTIFIER ORDER BY ORDINAL_POSITION SEPARATOR ',')
                   FROM INFORMATION_SCHEMA.PARAMETERS p
                   WHERE p.SPECIFIC_NAME = r.ROUTINE_NAME
                     AND p.ROUTINE_TYPE = r.ROUTINE_TYPE
                     AND p.SPECIFIC_SCHEMA = r.ROUTINE_SCHEMA), '') || ')\n  RETURNS ' || DTD_IDENTIFIER ||
       '\n' || '  LANGUAGE ' || EXTERNAL_LANGUAGE ||
       '\n' || CASE WHEN IS_DETERMINISTIC = 'Yes' THEN '  DETERMINISTIC' ELSE '  NOT DETERMINISTIC' END ||
       CASE WHEN NULLIF(SQL_DATA_ACCESS, '') IS NOT NULL THEN '\n  ' || SQL_DATA_ACCESS ELSE '' END ||
       '\n' || '  SQL SECURITY ' || SECURITY_TYPE ||
       '\n' || ROUTINE_DEFINITION || ' //\nDELIMITER ;' AS Code
  FROM INFORMATION_SCHEMA.ROUTINES r
  WHERE ROUTINE_SCHEMA = '{targetSchema}'
    AND ROUTINE_TYPE = 'FUNCTION'
    AND ROUTINE_NAME NOT LIKE 'SchemaSmith\_%'
";
        _stats.Functions = PerformMySqlCasting(command, "Functions");
    }

    private void ScriptMySqlViews(IDbCommand command, string targetSchema)
    {
        _progressLog.Info("Casting View Scripts");
        command.CommandText = $@"
SELECT 'Views' AS Folder,
       TABLE_NAME AS FullName,
       'DROP VIEW IF EXISTS `' || TABLE_NAME || '`;\nCREATE VIEW `' || TABLE_NAME || '` AS\n' || VIEW_DEFINITION AS Code
  FROM INFORMATION_SCHEMA.VIEWS
  WHERE TABLE_SCHEMA = '{targetSchema}'
    AND TABLE_NAME NOT LIKE 'SchemaSmith\_%'
";
        _stats.Views = PerformMySqlCasting(command, "Views");
    }

    private void ScriptMySqlProcedures(IDbCommand command, string targetSchema)
    {
        _progressLog.Info("Casting Stored Procedure Scripts");
        command.CommandText = $@"
SELECT 'Procedures' AS Folder,
       ROUTINE_NAME AS FullName,
       'DROP PROCEDURE IF EXISTS `' || ROUTINE_NAME || '`;\nDELIMITER //\n' ||
       'CREATE PROCEDURE `' || ROUTINE_NAME || '` (' ||
       COALESCE((SELECT GROUP_CONCAT(PARAMETER_MODE || ' ' || PARAMETER_NAME || ' ' || DTD_IDENTIFIER ORDER BY ORDINAL_POSITION SEPARATOR ',')
                   FROM INFORMATION_SCHEMA.PARAMETERS p
                   WHERE p.SPECIFIC_NAME = r.ROUTINE_NAME
                     AND p.ROUTINE_TYPE = r.ROUTINE_TYPE
                     AND p.SPECIFIC_SCHEMA = r.ROUTINE_SCHEMA), '') || ')' ||
       '\n' || '  LANGUAGE ' || EXTERNAL_LANGUAGE ||
       '\n' || CASE WHEN IS_DETERMINISTIC = 'Yes' THEN '  DETERMINISTIC' ELSE '  NOT DETERMINISTIC' END ||
       CASE WHEN NULLIF(SQL_DATA_ACCESS, '') IS NOT NULL THEN '\n  ' || SQL_DATA_ACCESS ELSE '' END ||
       '\n' || '  SQL SECURITY ' || SECURITY_TYPE ||
       '\n' || ROUTINE_DEFINITION || ' //\nDELIMITER ;' AS Code
  FROM INFORMATION_SCHEMA.ROUTINES r
  WHERE ROUTINE_SCHEMA = '{targetSchema}'
    AND ROUTINE_TYPE = 'PROCEDURE'
    AND ROUTINE_NAME NOT LIKE 'SchemaSmith\_%'
";
        _stats.Procedures = PerformMySqlCasting(command, "Procedures");
    }

    private void ScriptMySqlTriggers(IDbCommand command, string targetSchema)
    {
        _progressLog.Info("Casting Trigger Scripts");
        command.CommandText = $@"
SELECT 'Triggers' AS Folder,
       TRIGGER_NAME AS FullName,
       'DROP TRIGGER IF EXISTS `' || TRIGGER_NAME || '`;\nDELIMITER //\nCREATE TRIGGER `' || TRIGGER_NAME || '`\n  ' || ACTION_TIMING || ' ' || EVENT_MANIPULATION || '\n  ON `' || EVENT_OBJECT_TABLE || '` ' ||
       '\n  FOR EACH ' || ACTION_ORIENTATION || ' \nBEGIN\n' || ACTION_STATEMENT || ';\nEND //\nDELIMITER ;' AS Code
  FROM INFORMATION_SCHEMA.TRIGGERS
  WHERE TRIGGER_SCHEMA = '{targetSchema}'
    AND TRIGGER_NAME NOT LIKE 'SchemaSmith\_%'
";
        _stats.Triggers = PerformMySqlCasting(command, "Triggers");
    }

    private void ScriptMySqlEvents(IDbCommand command, string targetSchema)
    {
        _progressLog.Info("Casting Event Scripts");
        command.CommandText = $@"
SELECT 'Events' AS Folder,
       EVENT_NAME AS FullName,
       'DROP EVENT IF EXISTS `' || EVENT_NAME || '`;\nDELIMITER //\nCREATE EVENT `' || EVENT_NAME || '`\n  ON SCHEDULE ' ||
       CASE EVENT_TYPE
           WHEN 'ONE TIME' THEN 'AT ''' || COALESCE(EXECUTE_AT, NOW()) || ''''
           ELSE 'EVERY ' || INTERVAL_VALUE || ' ' || INTERVAL_FIELD ||
               COALESCE('\n    STARTS ''' || STARTS || '''', '') ||
               COALESCE('\n    ENDS ''' || ENDS || '''', '')
       END ||
       '\n  ON COMPLETION ' || CASE WHEN ON_COMPLETION = 'PRESERVE' THEN 'PRESERVE' ELSE 'NOT PRESERVE' END ||
       '\n  ' || STATUS ||
       CASE WHEN NULLIF(EVENT_COMMENT, '') IS NOT NULL THEN '\n  COMMENT ''' || REPLACE(EVENT_COMMENT, '''', '''''') || '''' ELSE '' END ||
       '\n  DO ' || EVENT_DEFINITION || ' //\nDELIMITER ;' AS Code
  FROM INFORMATION_SCHEMA.EVENTS
  WHERE EVENT_SCHEMA = '{targetSchema}'
    AND EVENT_NAME NOT LIKE 'SchemaSmith\_%'
";
        _stats.Events = PerformMySqlCasting(command, "Events");
    }

    private void ExtractMySqlTableDefinitions(IDbCommand command, string targetSchema)
    {
        using var connectionJson = GetConnection(targetSchema);
        try
        {
            using var commandJson = connectionJson.CreateCommand();

            command.CommandText = $@"
SELECT TABLE_SCHEMA, TABLE_NAME
  FROM INFORMATION_SCHEMA.TABLES t
  WHERE TABLE_TYPE = 'BASE TABLE'
    AND TABLE_SCHEMA = '{targetSchema}'
    AND TABLE_SCHEMA <> 'SchemaSmith'
    AND TABLE_NAME NOT LIKE 'SchemaSmith\_%'
  ORDER BY TABLE_NAME
";

            var tableDir = Path.Combine(_templatePath, "Tables");
            DirectoryWrapper.GetFromFactory().CreateDirectory(tableDir);

            var tables = new List<(string Schema, string Table)>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    tables.Add(($"{reader["TABLE_SCHEMA"]}", $"{reader["TABLE_NAME"]}"));
                }
            }

            var totalTables = tables.Count;
            var filteredCount = _objectsToCast.Length > 0
                ? tables.Count(t => _objectsToCast.Contains(t.Table.ToLower()) || _objectsToCast.Contains($"{t.Schema}.{t.Table}".ToLower()))
                : totalTables;

            _progressLog.Info($"Casting Table Structures ({filteredCount} of {totalTables} tables)");

            var currentTable = 0;
            foreach (var (schema, table) in tables)
            {
                if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(table.ToLower()) && !_objectsToCast.Contains($"{schema}.{table}".ToLower())) continue;

                currentTable++;
                _progressLog.Info($"  [{currentTable}/{filteredCount}] Extracting {table}...");

                try
                {
                    commandJson.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{MySqlReservedWords.Unquote(schema)}', '{MySqlReservedWords.Unquote(table)}')";

                    string json = "";
                    using (var jsonReader = commandJson.ExecuteReader())
                    {
                        while (jsonReader.Read())
                            json += $"{jsonReader[0]}\r\n";
                    }

                    if (string.IsNullOrWhiteSpace(json) || json.Trim().Equals("{}"))
                    {
                        _progressLog.Error($"    ERROR: No json returned for {table}");
                        _stats.TableErrors++;
                        continue;
                    }

                    var tableObj = JsonConvert.DeserializeObject<Table>(json);
                    if (tableObj == null)
                    {
                        _progressLog.Error($"    ERROR: Failed to deserialize json for {table}");
                        _stats.TableErrors++;
                        continue;
                    }

                    var filename = ResolveOutputPath(tableDir, EncodeFileName(table, ".json"));
                    var oldTableFile = !string.IsNullOrEmpty(tableObj.OldName)
                        ? ResolveOutputPath(tableDir, EncodeFileName(tableObj.OldName.Trim('`'), ".json"))
                        : null;

                    if (FileWrapper.GetFromFactory().Exists(filename) || (oldTableFile != null && FileWrapper.GetFromFactory().Exists(oldTableFile)))
                    {
                        var originalPath = FileWrapper.GetFromFactory().Exists(filename) ? filename : oldTableFile;
                        var original = JsonHelper.Load<Table>(originalPath);
                        ImportTableHelper.PreserveDataDeliveryAndCustomProperties(tableObj, original);
                    }

                    JsonHelper.Write(filename, tableObj);
                    _stats.Tables++;
                }
                catch (MySqlException ex)
                {
                    _progressLog.Error($"    ERROR: MySQL error extracting {table}: {ex.Message}");
                    _stats.TableErrors++;
                }
                catch (JsonException ex)
                {
                    _progressLog.Error($"    ERROR: JSON parsing error for {table}: {ex.Message}");
                    _stats.TableErrors++;
                }
                catch (IOException ex)
                {
                    _progressLog.Error($"    ERROR: File write error for {table}: {ex.Message}");
                    _stats.TableErrors++;
                }
            }
        }
        finally
        {
            connectionJson.Close();
        }
    }

    private int PerformMySqlCasting(IDbCommand command, string castType)
    {
        var count = 0;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var castPath = Path.Combine(_templatePath, reader["Folder"].ToString());
            DirectoryWrapper.GetFromFactory().CreateDirectory(castPath);
            var fullName = reader["FullName"].ToString();
            if (_objectsToCast.Length > 0 && !_objectsToCast.Contains(fullName.ToLower()) && !_objectsToCast.Contains($"{fullName}.~~~".Split('.')[1].ToLower())) continue;

            var fileName = ResolveOutputPath(castPath, EncodeFullName(fullName, ".sql"));
            if (ShouldSkipKnownBadScript(fileName)) { count++; continue; }
            var script = string.Join("\r\n", reader["Code"].ToString());
            _progressLog.Info($"  Casting {fullName}");
            FileWrapper.GetFromFactory().WriteAllText(fileName, script);
            var folderName = reader["Folder"].ToString();
            var objectType = ScriptFolderTypeInference.InferFromFolderName(folderName);
            ValidateAndHandleScript(command.Connection, fileName, script, objectType);
            count++;
        }
        return count;
    }

    #endregion

    #region Table Extraction (SQL Server)

    private void ExtractTableDefinitions(IDbCommand command, string targetDb)
    {
        using var connectionJson = GetConnection(targetDb);
        try
        {
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
                    _stats.TableErrors++;
                    continue;
                }

                var filename = ResolveOutputPath(tableDir, EncodeFileName($"{reader["TABLE_SCHEMA"]}", $"{reader["TABLE_NAME"]}", ".json"));
                _progressLog.Info($"    Casting {filename}");
                var tableObj = JsonConvert.DeserializeObject<Table>(json);

                if (_checkConstraintStyle == CheckConstraintStyle.TableLevel && _platform == Platform.SqlServer && tableObj is SqlServerTable sqlTable)
                {
                    commandJson.CommandText = $@"
SELECT cc.name AS [Name],
       SchemaSmith.fn_StripParenWrapping(cc.definition) AS [Expression],
       cc.parent_column_id
  FROM sys.check_constraints cc WITH (NOLOCK)
 WHERE cc.parent_object_id = OBJECT_ID('{EscapeSql($"{reader["TABLE_SCHEMA"]}")}.{EscapeSql($"{reader["TABLE_NAME"]}")}')
 ORDER BY cc.name";

                    var allConstraints = new List<CheckConstraint>();
                    using (var ccReader = commandJson.ExecuteReader())
                    {
                        while (ccReader.Read())
                            allConstraints.Add(new CheckConstraint { Name = $"{ccReader["Name"]}", Expression = $"{ccReader["Expression"]}" });
                    }
                    PromoteCheckConstraintsToTableLevel(sqlTable, allConstraints);
                }

                var oldTableFile = ResolveOutputPath(tableDir, EncodeFileName($"{reader["TABLE_SCHEMA"]}", tableObj.OldName.Trim('"'), ".json"));
                if (FileWrapper.GetFromFactory().Exists(filename) || FileWrapper.GetFromFactory().Exists(oldTableFile))
                {
                    var original = JsonHelper.Load<Table>(FileWrapper.GetFromFactory().Exists(filename) ? filename : oldTableFile);
                    ImportTableHelper.PreserveDataDeliveryAndCustomProperties(tableObj, original);
                }
                JsonHelper.Write(filename, tableObj);
                _stats.Tables++;
            }
        }
        finally
        {
            connectionJson.Close();
        }
    }

    #endregion

    #region Helpers

    internal static void PromoteCheckConstraintsToTableLevel(SqlServerTable table, List<CheckConstraint> allConstraints)
    {
        foreach (var col in table.Columns.OfType<SqlServerColumn>())
            col.CheckExpression = null;

        table.CheckConstraints = allConstraints;
    }

    internal static string EscapeSql(string value) => value.Replace("'", "''");

    internal static string FormatBaseType(string baseType, short maxLength, byte precision, byte scale)
    {
        var lower = baseType.ToLower();
        switch (lower)
        {
            case "nvarchar":
            case "nchar":
                return maxLength == -1 ? $"[{baseType}](max)" : $"[{baseType}]({maxLength / 2})";
            case "varchar":
            case "char":
            case "varbinary":
            case "binary":
                return maxLength == -1 ? $"[{baseType}](max)" : $"[{baseType}]({maxLength})";
            case "decimal":
            case "numeric":
                return $"[{baseType}]({precision}, {scale})";
            case "datetime2":
            case "datetimeoffset":
            case "time":
                return scale != 7 ? $"[{baseType}]({scale})" : $"[{baseType}]";
            default:
                return $"[{baseType}]";
        }
    }

    private void IncrementStatForFolder(string folder)
    {
        switch (folder)
        {
            case "Schemas": _stats.Schemas++; break;
            case "Domain Types": _stats.DomainTypes++; break;
            case "Enum Types": _stats.EnumTypes++; break;
            case "Composite Types": _stats.CompositeTypes++; break;
            case "Functions": _stats.Functions++; break;
            case "Trigger Functions": _stats.Functions++; break;
            case "Window Functions": _stats.Functions++; break;
            case "Aggregates": _stats.Aggregates++; break;
            case "Procedures": _stats.Procedures++; break;
            case "Sequences": _stats.Sequences++; break;
            case "Rules": _stats.Rules++; break;
            case "Triggers": _stats.Triggers++; break;
            case "Views": _stats.Views++; break;
            case "Materialized Views": _stats.MaterializedViews++; break;
        }
    }

    private void LogSummary()
    {
        _progressLog.Info("");
        _progressLog.Info("=== Casting Summary ===");
        _progressLog.Info($"  Tables:     {_stats.Tables} extracted, {_stats.TableErrors} errors");

        switch (_platform)
        {
            case Platform.SqlServer:
                LogIfPositive("  Schemas:    ", _stats.Schemas);
                LogIfPositive("  DataTypes:  ", _stats.DataTypes);
                LogIfPositive("  Functions:  ", _stats.Functions);
                LogIfPositive("  Views:      ", _stats.Views);
                LogIfPositive("  Procedures: ", _stats.Procedures);
                LogIfPositive("  Triggers:   ", _stats.Triggers);
                LogIfPositive("  DDLTriggers:", _stats.DDLTriggers);
                LogIfPositive("  FTCatalogs: ", _stats.FullTextCatalogs);
                LogIfPositive("  FTStopLists:", _stats.FullTextStopLists);
                LogIfPositive("  XmlSchemas: ", _stats.XmlSchemaCollections);
                LogIfPositive("  IdxViews:   ", _stats.IndexedViews);
                break;

            case Platform.PostgreSQL:
                LogIfPositive("  Schemas:    ", _stats.Schemas);
                LogIfPositive("  Domain Types:", _stats.DomainTypes);
                LogIfPositive("  Enum Types: ", _stats.EnumTypes);
                LogIfPositive("  Composites: ", _stats.CompositeTypes);
                LogIfPositive("  Functions:  ", _stats.Functions);
                LogIfPositive("  Aggregates: ", _stats.Aggregates);
                LogIfPositive("  Procedures: ", _stats.Procedures);
                LogIfPositive("  Sequences:  ", _stats.Sequences);
                LogIfPositive("  Rules:      ", _stats.Rules);
                LogIfPositive("  Triggers:   ", _stats.Triggers);
                LogIfPositive("  Views:      ", _stats.Views);
                LogIfPositive("  MatViews:   ", _stats.MaterializedViews);
                break;

            case Platform.MySQL:
                LogIfPositive("  Views:      ", _stats.Views);
                LogIfPositive("  Functions:  ", _stats.Functions);
                LogIfPositive("  Procedures: ", _stats.Procedures);
                LogIfPositive("  Triggers:   ", _stats.Triggers);
                LogIfPositive("  Events:     ", _stats.Events);
                break;
        }

        _progressLog.Info($"  Elapsed:    {_stopwatch.Elapsed.TotalSeconds:F1} seconds");
        _progressLog.Info("");
        _progressLog.Info(_stats.TableErrors > 0
            ? "Casting Completed with Errors"
            : "Casting Completed Successfully");
    }

    private void LogIfPositive(string label, int count)
    {
        if (count > 0)
            _progressLog.Info($"{label}{count} extracted");
    }

    #endregion

    internal class ExtractionStats
    {
        public int Tables { get; set; }
        public int TableErrors { get; set; }
        public int Schemas { get; set; }
        public int DataTypes { get; set; }
        public int DomainTypes { get; set; }
        public int EnumTypes { get; set; }
        public int CompositeTypes { get; set; }
        public int Functions { get; set; }
        public int Aggregates { get; set; }
        public int Views { get; set; }
        public int MaterializedViews { get; set; }
        public int IndexedViews { get; set; }
        public int Procedures { get; set; }
        public int Triggers { get; set; }
        public int DDLTriggers { get; set; }
        public int Sequences { get; set; }
        public int Rules { get; set; }
        public int Events { get; set; }
        public int FullTextCatalogs { get; set; }
        public int FullTextStopLists { get; set; }
        public int XmlSchemaCollections { get; set; }
    }
}
