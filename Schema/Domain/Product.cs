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
    public List<string> TemplateOrder { get; set; } = [];
    public Dictionary<string, string> ScriptTokens { get; set; } = [];

    [JsonIgnore]
    public string FilePath { get; set; }

    private string _branchNameFile;
    public string BranchNameFile
    {
        get => _branchNameFile ??= Path.Combine(Path.GetDirectoryName(FilePath), ".git", "HEAD"); // default to git
        set => _branchNameFile = value;
    }
    public string BeforeBranchNameMask { get; set; } = "ref: refs/heads/"; // default to git
    public string AfterBranchNameMask { get; set; }

    public static Product Load()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var SchemaPackagePath = config["SchemaPackagePath"] ?? "";

        if (!DirectoryWrapper.GetFromFactory().Exists(SchemaPackagePath))
            throw new Exception($"Path not found {SchemaPackagePath}");

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
    }

    public static string TokenReplace(string script, List<KeyValuePair<string, string>> scriptTokens)
    {
        if (!string.IsNullOrEmpty(script))
            scriptTokens.ForEach(token => { script = Regex.Replace(script, $@"\{{\{{{token.Key}\}}\}}", token.Value, RegexOptions.IgnoreCase); });
        return script;
    }
}