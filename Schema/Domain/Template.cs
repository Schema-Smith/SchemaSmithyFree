using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Schema.Domain;

public class Template
{
    public string Name { get; set; }
    public Product Product { get; set; }
    public string DatabaseIdentificationScript { get; set; }
    public string VersionStampScript { get; set; }
    public bool UpdateFillFactor { get; set; } = true;

    [JsonIgnore]
    public List<ScriptFolder> ScriptFolders { get; } = GetTemplateFolders();
    [JsonIgnore]
    public List<SqlScript> BeforeScripts => ScriptFolders.Where(f => f.QuenchSlot == QuenchSlot.Before).SelectMany(f => f.Scripts).ToList();
    [JsonIgnore]
    public List<SqlScript> ObjectScripts => ScriptFolders.Where(f => f.QuenchSlot == QuenchSlot.Objects).SelectMany(f => f.Scripts).ToList();
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
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var SchemaPackagePath = config["SchemaPackagePath"] ?? "";

        var templatePath = Path.Combine(SchemaPackagePath, "Templates", templateName);
        var templateFilePath = Path.Combine(templatePath, "Template.json");
        var template = JsonHelper.Load<Template>(templateFilePath);
        template.FilePath = templateFilePath;

        template.Product = product;
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
    }

    private void LoadTables()
    {
        var filePath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "Tables");
        if (!DirectoryWrapper.GetFromFactory().Exists(filePath)) return;

        var files = DirectoryWrapper.GetFromFactory().GetFiles(filePath, "*.json", SearchOption.AllDirectories)
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
                new ScriptFolder { FolderPath = "Functions", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "Views", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "Procedures", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "Triggers", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "DDLTriggers", QuenchSlot = QuenchSlot.Objects },
                new ScriptFolder { FolderPath = "MigrationScripts/After", QuenchSlot = QuenchSlot.After },
            ];
    }
}