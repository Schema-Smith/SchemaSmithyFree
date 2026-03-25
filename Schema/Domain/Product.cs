// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain;

public class Product
{
    [SchemaProperty(Required = true)]
    public string Name { get; set; }
    [SchemaProperty(Required = true)]
    public string ValidationScript { get; set; }
    public bool DropUnknownIndexes { get; set; } = false;
    public List<string> TemplateOrder { get; set; } = [];
    public Dictionary<string, string> ScriptTokens { get; set; } = [];
    public string BaselineValidationScript { get; set; }
    public string VersionStampScript { get; set; }
    public string Platform { get; set; } = ConfigHelper.Platform;
    public SqlServerVersion? MinimumVersion { get; set; }
    [JsonConverter(typeof(StringEnumConverter))]
    public CheckConstraintStyle CheckConstraintStyle { get; set; }

    [JsonIgnore]
    public string FilePath { get; set; }

    private readonly List<ProductFolder> _scriptFolders = GetProductFolders();

    [JsonIgnore]
    public List<ProductFolder> BeforeFolders => _scriptFolders.Where(f => f.QuenchSlot == ProductQuenchSlot.Before).ToList();
    [JsonIgnore]
    public List<ProductFolder> AfterFolders => _scriptFolders.Where(f => f.QuenchSlot == ProductQuenchSlot.After).ToList();

    public static Product Load()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var schemaPackagePath = config["SchemaPackagePath"] ?? "";
        if (string.IsNullOrWhiteSpace(schemaPackagePath))
            throw new Exception("SchemaPackagePath is not configured in appsettings.json or environment variables.");

        if (ZipFileWrapper.IsValidZipFile(schemaPackagePath))
        {
            var zipFileWrapper = ZipFileWrapper.GetFromFactory(schemaPackagePath) as ZipFileWrapper;
            _ = ZipDirectoryWrapper.GetFromFactory(zipFileWrapper?.ZipEntries);
            schemaPackagePath = ""; // use root of zip
        }
        else if (!DirectoryWrapper.GetFromFactory().Exists(schemaPackagePath))
            throw new Exception($"SchemaPackagePath not found {schemaPackagePath}");

        var productFilePath = Path.Combine(schemaPackagePath, "Product.json");
        var product = JsonHelper.ProductLoad<Product>(productFilePath);
        if (!ConfigHelper.IsValidPlatform(product.Platform))
            throw new Exception($"Product platform '{product.Platform}' is not supported. Valid platforms: SqlServer, MSSQL (legacy).");
        product.FilePath = productFilePath;
        OverrideProductScriptTokens(config, product);
        product.ScriptTokens.Add("ProductName", product.Name);

        product.InstanceLoad();

        return product;
    }

    private static void OverrideProductScriptTokens(IConfigurationRoot config, Product product)
    {
        foreach (var token in GetScriptTokensFromAppConfig(config).Where(token => product.ScriptTokens.ContainsKey(token.Key) && !string.IsNullOrEmpty(token.Value)))
            product.ScriptTokens[token.Key] = token.Value;
    }

    private static IEnumerable<KeyValuePair<string, string>> GetScriptTokensFromAppConfig(IConfigurationRoot config)
    {
        return config.GetSection("ScriptTokens")
            .AsEnumerable()
            .Where(x => x.Value != null)
            .Select(x => new KeyValuePair<string, string>(x.Key.Replace("ScriptTokens:", ""), x.Value));
    }

    private void InstanceLoad()
    {
        var scriptTokens = ScriptTokens.ToList();
        ValidationScript = TokenReplace(ValidationScript, scriptTokens);
        BaselineValidationScript = TokenReplace(BaselineValidationScript, scriptTokens);
        VersionStampScript = TokenReplace(VersionStampScript, scriptTokens);

        var productDir = Path.GetDirectoryName(FilePath) ?? "";
        foreach (var folder in _scriptFolders)
            folder.LoadSqlFiles(productDir, scriptTokens);
    }

    public static string TokenReplace(string script, List<KeyValuePair<string, string>> scriptTokens)
    {
        if (!string.IsNullOrEmpty(script))
            scriptTokens.ForEach(token => { script = Regex.Replace(script, $@"\{{\{{{token.Key}\}}\}}", token.Value, RegexOptions.IgnoreCase); });
        return script;
    }

    private static List<ProductFolder> GetProductFolders()
    {
        return
        [
            new ProductFolder { FolderPath = "ProductScripts/Before", QuenchSlot = ProductQuenchSlot.Before },
            new ProductFolder { FolderPath = "ProductScripts/After", QuenchSlot = ProductQuenchSlot.After },
        ];
    }
}
