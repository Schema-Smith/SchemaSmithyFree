// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;
using Schema.Isolators;

namespace Schema.Utility;

public class SchemaFileResult
{
    public string FileName { get; set; }
    public bool WasCreated { get; set; }
}

/// <summary>
/// Helpers for initializing and updating schema package repositories.
/// Platform-aware: uses platform-appropriate validation scripts, template structures, and schema file names.
/// </summary>
public static class RepositoryHelper
{
    /// <summary>
    /// Initializes or updates a Product.json and adds missing schema files for the given platform.
    /// Schema files are only added if they don't already exist. Use WriteSchemaFiles for merge behavior.
    /// </summary>
    public static void UpdateOrInitRepository(string productPath, string productName, string templateName, string dbName, Platform platform)
    {
        var file = FileWrapper.GetFromFactory();
        var directory = DirectoryWrapper.GetFromFactory();
        directory.CreateDirectory(Path.Combine(productPath, "Templates"));
        var productFile = Path.Combine(productPath, "Product.json");
        if (string.IsNullOrEmpty(productName)) productName = Path.GetFileName(productPath.TrimEnd(' ', '/', '\\'));
        if (string.IsNullOrEmpty(templateName)) templateName = dbName;

        var product = new Product
        {
            Name = productName,
            Platform = platform,
            ValidationScript = GetValidationScript(templateName, platform)
        };

        if (file.Exists(productFile)) product = JsonHelper.Load<Product>(productFile) ?? product;
        if (product.Platform == Platform.Unknown)
            product.Platform = platform;
        else if (product.Platform != platform)
            throw new Exception($"Platform mismatch: Product '{product.Name}' is configured for {product.Platform} but config specifies {platform}.");
        product.FilePath = productFile;
        if (!product.ScriptTokens.Any(t => t.Key.EqualsIgnoringCase($"{templateName}Db")))
            product.ScriptTokens.Add($"{templateName}Db", dbName);
        if (product.TemplateOrder.All(t => !t.EqualsIgnoringCase(templateName)))
            product.TemplateOrder.Add(templateName);
        JsonHelper.Write(productFile, product);
    }

    /// <summary>
    /// Adds schema files that don't already exist. Does not merge or overwrite existing files.
    /// </summary>
    public static void AddMissingSchemaFiles(string productPath, Platform platform)
    {
        var file = FileWrapper.GetFromFactory();
        var directory = DirectoryWrapper.GetFromFactory();
        var schemaPath = Path.Combine(productPath, ".json-schemas");
        directory.CreateDirectory(schemaPath);

        foreach (var fileName in GetSchemaFileNames(platform))
        {
            var schemaFile = Path.Combine(schemaPath, fileName);
            if (file.Exists(schemaFile)) continue;

            var generated = SchemaGenerator.GenerateSchema(GetTypeForSchemaFile(fileName, platform));
            file.WriteAllText(schemaFile, generated.ToString(Formatting.Indented));
        }
    }

    /// <summary>
    /// Writes or merges schema files for the given platform into the .json-schemas folder.
    /// </summary>
    public static void WriteSchemaFiles(string productPath, Platform platform)
    {
        WriteSchemaFilesWithResults(productPath, platform);
    }

    /// <summary>
    /// Writes or merges schema files and returns detailed results for each file.
    /// </summary>
    public static List<SchemaFileResult> WriteSchemaFilesWithResults(string productPath, Platform platform)
    {
        var directory = DirectoryWrapper.GetFromFactory();
        var schemaPath = Path.Combine(productPath, ".json-schemas");
        directory.CreateDirectory(schemaPath);

        var schemaFileNames = GetSchemaFileNames(platform);
        var results = new List<SchemaFileResult>();
        foreach (var fileName in schemaFileNames)
            results.Add(WriteSchemaFileWithResult(schemaPath, fileName, platform));
        return results;
    }

    /// <summary>
    /// Initializes or updates a Template.json and creates platform-appropriate script folders.
    /// </summary>
    public static string UpdateOrInitTemplate(string productPath, string templateName, string dbName, Platform platform)
    {
        var file = FileWrapper.GetFromFactory();
        var directory = DirectoryWrapper.GetFromFactory();
        if (string.IsNullOrEmpty(templateName)) templateName = dbName;
        var templatePath = Path.Combine(productPath, "Templates", templateName);
        directory.CreateDirectory(templatePath);
        var templateFile = Path.Combine(templatePath, "Template.json");

        var template = new Template
        {
            Name = templateName,
            DatabaseIdentificationScript = GetDatabaseIdentificationScript(templateName, platform)
        };

        if (!file.Exists(templateFile))
        {
            AddDefaultScriptFolders(template, platform);
            JsonHelper.Write(templateFile, template);
        }
        else
        {
            template = JsonHelper.Load<Template>(templateFile) ?? template;

            var needsUpgrade = template.ScriptFolders.Any(f => f.ObjectType == ScriptObjectType.None
                && ScriptFolderTypeInference.InferFromFolderName(f.FolderPath) != ScriptObjectType.None);
            if (needsUpgrade)
            {
                foreach (var folder in template.ScriptFolders.Where(sf => sf.ObjectType == ScriptObjectType.None))
                    folder.ObjectType = ScriptFolderTypeInference.InferFromFolderName(folder.FolderPath);
                JsonHelper.Write(templateFile, template);
            }
        }

        foreach (var folder in template.ScriptFolders)
            directory.CreateDirectory(Path.Combine(templatePath, folder.FolderPath));
        return templatePath;
    }

    private static SchemaFileResult WriteSchemaFileWithResult(string schemaPath, string fileName, Platform platform)
    {
        var file = FileWrapper.GetFromFactory();
        var schemaFile = Path.Combine(schemaPath, fileName);
        var generated = SchemaGenerator.GenerateSchema(GetTypeForSchemaFile(fileName, platform));

        if (!file.Exists(schemaFile))
        {
            file.WriteAllText(schemaFile, generated.ToString(Formatting.Indented));
            return new SchemaFileResult { FileName = fileName, WasCreated = true };
        }

        var existing = file.ReadAllText(schemaFile);
        var existingObj = JObject.Parse(existing);
        var merged = SchemaGenerator.MergeExtensionsDefinition(generated, existingObj);
        file.WriteAllText(schemaFile, merged.ToString(Formatting.Indented));
        return new SchemaFileResult { FileName = fileName };
    }

    private static Type GetTypeForSchemaFile(string fileName, Platform platform)
    {
        var objectPart = fileName.Split('.')[0]; // "products", "templates", "tables", "indexedviews", "materializedviews"

        return (objectPart, platform) switch
        {
            ("products", _) => typeof(Product),
            ("templates", Platform.SqlServer) => typeof(SqlServerTemplate),
            ("templates", Platform.PostgreSQL) => typeof(PostgreSqlTemplate),
            ("templates", Platform.MySQL) => typeof(MySqlTemplate),
            ("tables", Platform.SqlServer) => typeof(SqlServerTable),
            ("tables", Platform.PostgreSQL) => typeof(PostgreSqlTable),
            ("tables", Platform.MySQL) => typeof(MySqlTable),
            ("indexedviews", Platform.SqlServer) => typeof(SqlServerIndexedView),
            ("materializedviews", Platform.PostgreSQL) => typeof(PostgreSqlMaterializedView),
            _ => throw new ArgumentException($"Unknown schema file mapping: {fileName} for platform {platform}")
        };
    }

    public static string[] GetSchemaFileNames(Platform platform)
    {
        var platformName = platform.ToCanonicalString().ToLower();
        var files = new List<string>
        {
            $"products.{platformName}.schema",
            $"templates.{platformName}.schema",
            $"tables.{platformName}.schema"
        };
        if (platform == Platform.PostgreSQL)
            files.Add($"materializedviews.{platformName}.schema");
        if (platform == Platform.SqlServer)
            files.Add($"indexedviews.{platformName}.schema");
        return files.ToArray();
    }

    internal static string GetValidationScript(string templateName, Platform platform) => platform switch
    {
        Platform.SqlServer => $"SELECT CAST(CASE WHEN EXISTS(SELECT * FROM master.sys.databases WHERE [Name] = '{{{{{templateName}Db}}}}') THEN 1 ELSE 0 END AS BIT)",
        Platform.PostgreSQL => $"SELECT EXISTS(SELECT * FROM pg_database WHERE datname = '{{{{{templateName}Db}}}}')",
        Platform.MySQL => $"SELECT EXISTS(SELECT * FROM information_schema.schemata WHERE SCHEMA_NAME = '{{{{{templateName}Db}}}}')",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}")
    };

    internal static string GetDatabaseIdentificationScript(string templateName, Platform platform) => platform switch
    {
        Platform.SqlServer => $"SELECT [Name] FROM master.sys.databases WHERE [Name] = '{{{{{templateName}Db}}}}'",
        Platform.PostgreSQL => $"SELECT datname FROM pg_database WHERE datname = '{{{{{templateName}Db}}}}'",
        Platform.MySQL => $"SELECT SCHEMA_NAME FROM information_schema.schemata WHERE SCHEMA_NAME = '{{{{{templateName}Db}}}}'",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}")
    };

    internal static Template CreateDefaultTemplate(string templateName, Platform platform)
    {
        var template = new Template
        {
            Name = templateName,
            DatabaseIdentificationScript = GetDatabaseIdentificationScript(templateName, platform)
        };
        AddDefaultScriptFolders(template, platform);
        return template;
    }

    private static void AddDefaultScriptFolders(Template template, Platform platform)
    {
        template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Before Scripts", QuenchSlot = TemplateQuenchSlot.Before });

        switch (platform)
        {
            case Platform.SqlServer:
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Schemas", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Schemas });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "DataTypes", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.DataTypes });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "FullTextCatalogs", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.FullTextCatalogs });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "FullTextStopLists", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.FullTextStopLists });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "XMLSchemaCollections", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.XMLSchemaCollections });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Functions });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Views });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Procedures", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Procedures });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Triggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Triggers });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "DDLTriggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.DDLTriggers });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Table Data", QuenchSlot = TemplateQuenchSlot.TableData });
                break;

            case Platform.PostgreSQL:
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Schemas", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Schemas });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Domain Types", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.DomainTypes });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Enum Types", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.EnumTypes });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Composite Types", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.CompositeTypes });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Functions });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Trigger Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.TriggerFunctions });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Window Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.WindowFunctions });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Aggregates", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Aggregates });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Procedures", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Procedures });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Sequences", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Sequences });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Rules", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Rules });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Triggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Triggers });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Views });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Table Data", QuenchSlot = TemplateQuenchSlot.TableData });
                break;

            case Platform.MySQL:
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Events", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Events });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Functions });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Procedures", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Procedures });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Triggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Triggers });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Views });
                template.ScriptFolders.Add(new TemplateFolder { FolderPath = "Table Data", QuenchSlot = TemplateQuenchSlot.TableData });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}");
        }

        template.ScriptFolders.Add(new TemplateFolder { FolderPath = "After Scripts", QuenchSlot = TemplateQuenchSlot.After });
    }
}
