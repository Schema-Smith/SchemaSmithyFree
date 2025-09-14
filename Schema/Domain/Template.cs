using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Isolators;
using Schema.Utility;
using Newtonsoft.Json;

namespace Schema.Domain;

public class Template
{
    public string Name { get; set; }
    public string DatabaseIdentificationScript { get; set; }
    public string VersionStampScript { get; set; }
    public bool UpdateFillFactor { get; set; } = true;
    public string BaselineValidationScript { get; set; }

    [JsonIgnore]
    public List<ScriptFolder> ScriptFolders { get; } = GetTemplateFolders();
    [JsonIgnore]
    public List<SqlScript> BeforeScripts => ScriptFolders.Where(f => f.QuenchSlot == QuenchSlot.Before).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> ObjectScripts => ScriptFolders.Where(f => f.QuenchSlot == QuenchSlot.Objects).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    // Includes the objects scripts so that we can retry ALL remaining unapplied objects scripts
    public List<SqlScript> AfterTablesObjectScripts => ScriptFolders.Where(f => f.QuenchSlot is QuenchSlot.Objects or QuenchSlot.AfterTablesObjects).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> TableDataScripts => ScriptFolders.Where(f => f.QuenchSlot == QuenchSlot.TableData).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> AfterScripts => ScriptFolders.Where(f => f.QuenchSlot == QuenchSlot.After).SelectMany(f => f.Scripts).ToList();

    [JsonIgnore]
    public List<Table> Tables { get; } = [];
    [JsonIgnore]
    public string TableSchema { get; set; }

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
        foreach (var script in ScriptFolders.SelectMany(f => f.Scripts).ToList())
        {
            script.Error = null;
            script.HasBeenQuenched = false;
        }
    }

    private void Load(Dictionary<string, string> scriptTokens)
    {
        LoadTables();
        TableSchema = JsonConvert.SerializeObject(Tables, Formatting.Indented);

        var tokens = scriptTokens.Concat([new("TemplateName", Name ?? "UNSPECIFIED")]).ToList();
        foreach (var folder in ScriptFolders)
            folder.LoadSqlFiles(Path.GetDirectoryName(FilePath));

        DatabaseIdentificationScript = Product.TokenReplace(DatabaseIdentificationScript, tokens);
        VersionStampScript = Product.TokenReplace(VersionStampScript, tokens);
        BaselineValidationScript = Product.TokenReplace(BaselineValidationScript, tokens);
    }

    private void LoadTables()
    {
        var filePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "Tables");
        if (!ProductDirectoryWrapper.GetFromFactory().Exists(filePath)) return;

        var files = ProductDirectoryWrapper.GetFromFactory().GetFiles(filePath, "*.json", SearchOption.AllDirectories)
            .OrderBy(x => x);
        Tables.AddRange(files.Select(Table.Load));
    }

    public static List<ScriptFolder> GetTemplateFolders()
    {
        return
            [
                new ScriptFolder { FolderPath = "MigrationScripts/Before", QuenchSlot = QuenchSlot.Before },
                new ScriptFolder { FolderPath = "Schemas", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "DataTypes", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "FullTextCatalogs", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "FullTextStopLists", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "XMLSchemaCollections", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "Functions", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "Views", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "Procedures", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "Triggers", QuenchSlot = QuenchSlot.AfterTablesObjects },
                new ScriptFolder { FolderPath = "DDLTriggers", QuenchSlot = QuenchSlot.AfterTablesObjects },
                new ScriptFolder { FolderPath = "TableData", QuenchSlot = QuenchSlot.TableData },
                new ScriptFolder { FolderPath = "MigrationScripts/After", QuenchSlot = QuenchSlot.After },
            ];
    }
}