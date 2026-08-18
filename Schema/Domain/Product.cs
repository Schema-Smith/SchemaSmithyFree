// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.Domain
{
    public class Product
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string ValidationScript { get; set; }

        [JsonProperty(Order = 3)]
        public List<string> TemplateOrder { get; set; } = [];

        [JsonProperty(Order = 4)]
        public Dictionary<string, string> ScriptTokens { get; set; } = [];

        [JsonProperty(Order = 5)]
        public List<ProductFolder> ScriptFolders { get; set; } = [];

        [JsonProperty(Order = 6)]
        public string BranchNameFile { get; set; } = "{{repo_path}}/.git/HEAD";

        [JsonProperty(Order = 7)]
        public string BeforeBranchNameMask { get; set; } = "ref: refs/heads/";

        [JsonProperty(Order = 8)]
        public string AfterBranchNameMask { get; set; } = "";

        [JsonProperty(Order = 9)]
        public bool? DropUnknownIndexes { get; set; }

        [JsonProperty(Order = 10)]
        public string BaselineValidationScript { get; set; }

        [JsonProperty(Order = 11)]
        public string VersionStampScript { get; set; }

        [JsonConverter(typeof(PlatformJsonConverter))]
        [JsonProperty(Order = 12)]
        public Platform Platform { get; set; }

        [JsonProperty(Order = 13)]
        public string MinimumVersion { get; set; }

        [SchemaProperty]
        [JsonProperty(Order = 14)]
        [JsonConverter(typeof(StringEnumConverter))]
        public CheckConstraintStyle CheckConstraintStyle { get; set; }

        [JsonProperty(Order = 15)]
        [DefaultValue(true)]
        public bool DropTablesRemovedFromProduct { get; set; } = true;

        [JsonProperty(Order = 16)]
        public bool? DropColumnsRemovedFromProduct { get; set; }

        [JsonProperty(Order = 17)]
        public bool? DropForeignKeysRemovedFromProduct { get; set; }

        [JsonProperty(Order = 18)]
        public bool? DropCheckConstraintsRemovedFromProduct { get; set; }

        [JsonProperty(Order = 19)]
        public bool? DropExcludeConstraintsRemovedFromProduct { get; set; }

        [JsonProperty(Order = 20)]
        public bool? DropStatisticsRemovedFromProduct { get; set; }

        [JsonProperty(Order = 21)]
        public bool? DropIndexesRemovedFromProduct { get; set; }

        [JsonIgnore]
        public List<ProductFolder> BeforeFolders => ScriptFolders?.FindAll(f => f.QuenchSlot == ProductQuenchSlot.Before) ?? [];

        [JsonIgnore]
        public List<ProductFolder> AfterFolders => ScriptFolders?.FindAll(f => f.QuenchSlot == ProductQuenchSlot.After) ?? [];

        [JsonIgnore]
        public string FilePath { get; set; } = "";

        [JsonIgnore]
        public Dictionary<string, string> QueryTokens { get; set; } = [];

        [JsonIgnore]
        public Dictionary<string, string> NonQueryTokens { get; set; } = [];

        /// <summary>
        /// Loads a Product from a Product.json file path for display purposes only.
        /// Does NOT resolve tokens or load scripts. Use Load() (config-based) for runtime use.
        /// </summary>
        public static Product LoadForDisplay(string productFilePath)
        {
            var product = JsonHelper.ProductLoad(productFilePath);
            product.FilePath = Path.GetDirectoryName(productFilePath) ?? "";
            return product;
        }

        /// <summary>
        /// Loads a Product from the configured SchemaPackagePath (config or zip).
        /// Unified: no platform validation — accepts all platforms.
        /// </summary>
        public static Product Load()
        {
            var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
            var schemaPackagePath = config["SchemaPackagePath"] ?? "";

            if (ZipFileWrapper.IsValidZipFile(schemaPackagePath))
            {
                var zipFileWrapper = ZipFileWrapper.GetFromFactory(schemaPackagePath) as ZipFileWrapper;
                _ = ZipDirectoryWrapper.GetFromFactory(zipFileWrapper.ZipEntries);
                schemaPackagePath = ""; // use root of zip
            }
            else if (!DirectoryWrapper.GetFromFactory().Exists(schemaPackagePath))
                throw new Exception($"SchemaPackagePath not found '{schemaPackagePath}'");

            var productFilePath = Path.Combine(schemaPackagePath, "Product.json");
            var product = JsonHelper.ProductLoad(productFilePath);
            product.FilePath = productFilePath;
            OverrideProductScriptTokens(config, product);
            TokenHelper.ResolveFileTokens(product.ScriptTokens, schemaPackagePath, product.Platform);
            product.ScriptTokens.Add("ProductName", product.Name);

            product.InstanceLoad();

            return product;
        }

        /// <summary>
        /// Saves a Product to a JSON file using canonical platform serialization.
        /// </summary>
        public static void Save(string filePath, Product product)
        {
            JsonHelper.Write(filePath, product);
        }

        private static void OverrideProductScriptTokens(IConfigurationRoot config, Product product)
        {
            foreach (var token in GetScriptTokensFromAppConfig(config)
                         .Where(token => product.ScriptTokens.ContainsKey(token.Key) && !string.IsNullOrEmpty(token.Value)))
                product.ScriptTokens[token.Key] = token.Value;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetScriptTokensFromAppConfig(IConfigurationRoot config)
        {
            return config.GetSection("ScriptTokens")
                .AsEnumerable()
                .Where(x => x.Value != null)
                .Select(x => new KeyValuePair<string, string>(x.Key.Replace("ScriptTokens:", ""), x.Value ?? ""));
        }

        internal void InstanceLoad()
        {
            QueryTokens = TokenHelper.SplitOutQueryTokens(ScriptTokens);
            NonQueryTokens = ScriptTokens;
            var scriptTokens = ScriptTokens.ToList();
            foreach (var folder in ScriptFolders)
                folder.LoadSqlFiles(Path.GetDirectoryName(FilePath) ?? "", scriptTokens, Platform);
            ValidationScript = SqlScript.TokenReplace(ValidationScript ?? "", scriptTokens);
            BaselineValidationScript = SqlScript.TokenReplace(BaselineValidationScript ?? "", scriptTokens);
            VersionStampScript = SqlScript.TokenReplace(VersionStampScript ?? "", scriptTokens);
        }
    }
}
