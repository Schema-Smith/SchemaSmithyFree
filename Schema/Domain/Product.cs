using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Schema.Domain;

public class Product
{
    public string Name { get; set; }
    public string ValidationScript { get; set; }
    public bool DropUnknownIndexes { get; set; } = false;
    public List<string> TemplateOrder { get; set; } = [];
    public Dictionary<string, string> ScriptTokens { get; set; } = [];
    public string BaselineValidationScriopt { get; set; }
    public string VersionStampScript { get; set; }

    [JsonIgnore]
    public string FilePath { get; set; }

    public static Product Load()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var SchemaPackagePath = config["SchemaPackagePath"] ?? "";
        if (string.IsNullOrWhiteSpace(SchemaPackagePath))
            throw new Exception("SchemaPackagePath is not configured in appsettings.json or environment variables.");

        if (!DirectoryWrapper.GetFromFactory().Exists(SchemaPackagePath))
            throw new Exception($"SchemaPackagePath not found {SchemaPackagePath}");

        var productFilePath = Path.Combine(SchemaPackagePath, "Product.json");
        var product = JsonHelper.Load<Product>(productFilePath);
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
        BaselineValidationScriopt = TokenReplace(BaselineValidationScriopt, scriptTokens);
        VersionStampScript = TokenReplace(VersionStampScript, scriptTokens);
    }

    public static string TokenReplace(string script, List<KeyValuePair<string, string>> scriptTokens)
    {
        if (!string.IsNullOrEmpty(script))
            scriptTokens.ForEach(token => { script = Regex.Replace(script, $@"\{{\{{{token.Key}\}}\}}", token.Value, RegexOptions.IgnoreCase); });
        return script;
    }
}