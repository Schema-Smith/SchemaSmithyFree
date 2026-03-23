// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Isolators;
using Schema.Utility;
using Newtonsoft.Json;

namespace Schema.Domain;

public class Template
{
    [SchemaProperty(Required = true)]
    public string Name { get; set; }
    [SchemaProperty(Required = true)]
    public string DatabaseIdentificationScript { get; set; }
    public string VersionStampScript { get; set; }
    public bool UpdateFillFactor { get; set; } = true;
    public string BaselineValidationScript { get; set; }
    public Dictionary<string, string> ScriptTokens { get; set; } = [];

    private readonly List<TemplateFolder> _scriptFolders = GetTemplateFolders();

    [JsonIgnore]
    public List<SqlScript> BeforeScripts => _scriptFolders.Where(f => f.QuenchSlot == TemplateQuenchSlot.Before).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> ObjectScripts => _scriptFolders.Where(f => f.QuenchSlot == TemplateQuenchSlot.Objects).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> BetweenTablesAndKeysScripts => _scriptFolders.Where(f => f.QuenchSlot == TemplateQuenchSlot.BetweenTablesAndKeys).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> AfterTablesScripts => _scriptFolders.Where(f => f.QuenchSlot == TemplateQuenchSlot.AfterTablesScripts).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    // Includes the objects scripts so that we can retry ALL remaining unapplied objects scripts
    public List<SqlScript> AfterTablesObjectScripts => _scriptFolders.Where(f => f.QuenchSlot is TemplateQuenchSlot.Objects or TemplateQuenchSlot.AfterTablesObjects).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> TableDataScripts => _scriptFolders.Where(f => f.QuenchSlot == TemplateQuenchSlot.TableData).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> AfterScripts => _scriptFolders.Where(f => f.QuenchSlot == TemplateQuenchSlot.After).SelectMany(f => f.Scripts).ToList();

    [JsonIgnore]
    public List<Table> Tables { get; } = [];
    [JsonIgnore]
    public string TableSchema { get; set; }
    [JsonIgnore]
    public List<IndexedView> IndexedViews { get; } = [];
    [JsonIgnore]
    public string IndexedViewSchema { get; set; } = "[]";

    [JsonIgnore]
    public string FilePath { get; set; }
    [JsonIgnore]
    public string LogPath => LongPathSupport.StripLongPathPrefix(FilePath);

    public static Template Load(string templateName, Product product)
    {
        var SchemaPackagePath = Path.GetDirectoryName(product.FilePath) ?? "";

        var templatePath = Path.Combine(SchemaPackagePath, "Templates", templateName);
        var templateFilePath = Path.Combine(templatePath, "Template.json");
        var template = JsonHelper.ProductLoad<Template>(templateFilePath);
        template.FilePath = templateFilePath;

        template.Load(product.ScriptTokens);

        return template;
    }

    public void ResetScripts()
    {
        foreach (var script in _scriptFolders.SelectMany(f => f.Scripts).ToList())
        {
            script.Error = null;
            script.HasBeenQuenched = false;
        }
    }

    private void Load(Dictionary<string, string> productScriptTokens)
    {
        LoadTables();
        TableSchema = JsonConvert.SerializeObject(Tables, Formatting.Indented);
        LoadIndexedViews();
        IndexedViewSchema = JsonConvert.SerializeObject(IndexedViews, Formatting.Indented);

        // Merge tokens: template overrides product, then add auto-tokens
        var mergedTokens = ScriptTokens
            .Concat(productScriptTokens.Where(pt => !ScriptTokens.ContainsKey(pt.Key)))
            .Concat([new("TemplateName", Name ?? "UNSPECIFIED")])
            .ToList();

        foreach (var folder in _scriptFolders)
            folder.LoadSqlFiles(Path.GetDirectoryName(FilePath), mergedTokens);

        DatabaseIdentificationScript = Product.TokenReplace(DatabaseIdentificationScript, mergedTokens);
        VersionStampScript = Product.TokenReplace(VersionStampScript, mergedTokens);
        BaselineValidationScript = Product.TokenReplace(BaselineValidationScript, mergedTokens);
    }

    private void LoadTables()
    {
        var filePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "Tables");
        if (!ProductDirectoryWrapper.GetFromFactory().Exists(filePath)) return;

        var files = ProductDirectoryWrapper.GetFromFactory().GetFiles(filePath, "*.json", SearchOption.AllDirectories)
            .OrderBy(x => x);
        Tables.AddRange(files.Select(Table.Load));
    }

    private void LoadIndexedViews()
    {
        var filePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "Indexed Views");
        if (!ProductDirectoryWrapper.GetFromFactory().Exists(filePath)) return;

        var files = ProductDirectoryWrapper.GetFromFactory().GetFiles(filePath, "*.json", SearchOption.AllDirectories)
            .OrderBy(x => x);
        foreach (var file in files)
        {
            try
            {
                IndexedViews.Add(JsonHelper.ProductLoad<IndexedView>(file));
            }
            catch (Exception e)
            {
                throw new Exception($"Error loading indexed view from {LongPathSupport.StripLongPathPrefix(file)}\r\n{e.Message}", e);
            }
        }
    }

    internal static List<TemplateFolder> GetTemplateFolders()
    {
        return
        [
            new TemplateFolder { FolderPath = "MigrationScripts/Before", QuenchSlot = TemplateQuenchSlot.Before },
            new TemplateFolder { FolderPath = "Schemas", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "DataTypes", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "FullTextCatalogs", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "FullTextStopLists", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "XMLSchemaCollections", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "Functions", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "Procedures", QuenchSlot = TemplateQuenchSlot.Objects },
            new TemplateFolder { FolderPath = "MigrationScripts/BetweenTablesAndKeys", QuenchSlot = TemplateQuenchSlot.BetweenTablesAndKeys },
            new TemplateFolder { FolderPath = "MigrationScripts/AfterTablesScripts", QuenchSlot = TemplateQuenchSlot.AfterTablesScripts },
            new TemplateFolder { FolderPath = "Triggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects },
            new TemplateFolder { FolderPath = "DDLTriggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects },
            new TemplateFolder { FolderPath = "TableData", QuenchSlot = TemplateQuenchSlot.TableData },
            new TemplateFolder { FolderPath = "MigrationScripts/After", QuenchSlot = TemplateQuenchSlot.After },
        ];
    }
}
