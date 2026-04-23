// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.Domain
{
    public class Template
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string DatabaseIdentificationScript { get; set; }

        /// <summary>
        /// Backward compatibility: MySQL uses "Schema" and "Database" interchangeably.
        /// Accepts "SchemaIdentificationScript" in JSON, maps to DatabaseIdentificationScript.
        /// Write-only — never serialized back to JSON.
        /// </summary>
        [JsonProperty("SchemaIdentificationScript")]
        private string SchemaIdentificationScriptCompat { set => DatabaseIdentificationScript ??= value; }

        [JsonProperty(Order = 3)]
        public string VersionStampScript { get; set; }

        [JsonProperty(Order = 4)]
        public bool IndexOnlyTableQuenches { get; set; }

        [JsonProperty(Order = 5)]
        public List<TemplateFolder> ScriptFolders { get; } = [];

        [JsonProperty(Order = 6)]
        public Dictionary<string, string> ScriptTokens { get; set; } = [];

        [JsonProperty(Order = 7)]
        [DefaultValue(true)]
        public bool UpdateFillFactor { get; set; } = true;

        [JsonProperty(Order = 8)]
        public string BaselineValidationScript { get; set; }

        [JsonProperty(Order = 9)]
        [DefaultValue(true)]
        public bool Required { get; set; } = true;

        [JsonProperty(Order = 10)]
        public bool SkipIfReadOnly { get; set; }

        [JsonIgnore]
        public Product Product { get; set; }

        [JsonIgnore]
        public List<Table> Tables { get; } = [];

        [JsonIgnore]
        public string TableSchema { get; set; } = "";

        [JsonIgnore]
        public List<PostgreSqlMaterializedView> MaterializedViews { get; } = [];

        [JsonIgnore]
        public string MaterializedViewSchema { get; set; } = "[]";

        [JsonIgnore]
        public List<SqlServerIndexedView> IndexedViews { get; } = [];

        [JsonIgnore]
        public string IndexedViewSchema { get; set; } = "[]";

        [JsonIgnore]
        public string FilePath { get; set; }

        [JsonIgnore]
        public Dictionary<string, string> QueryTokens { get; set; } = [];

        [JsonIgnore]
        public Dictionary<string, string> NonQueryTokens { get; set; } = [];

        [JsonIgnore]
        public Dictionary<string, string> LoggableTokens { get; set; } = [];

        /// <summary>
        /// The path used for logging (stripped of long path prefix).
        /// </summary>
        [JsonIgnore]
        public string LogPath => LongPathSupport.StripLongPathPrefix(FilePath);

        #region Computed Script Collections

        [JsonIgnore]
        public List<SqlScript> BeforeScripts => ScriptFolders
            .Where(f => f.QuenchSlot == TemplateQuenchSlot.Before)
            .SelectMany(f => f.Scripts).ToList();

        [JsonIgnore]
        public List<SqlScript> ObjectScripts => ScriptFolders
            .Where(f => f.QuenchSlot is TemplateQuenchSlot.Objects)
            .SelectMany(f => f.Scripts).ToList();

        [JsonIgnore]
        public List<SqlScript> BetweenTablesAndKeysScripts => ScriptFolders
            .Where(f => f.QuenchSlot == TemplateQuenchSlot.BetweenTablesAndKeys)
            .SelectMany(f => f.Scripts).ToList();

        [JsonIgnore]
        public List<SqlScript> AfterTableScripts => ScriptFolders
            .Where(f => f.QuenchSlot == TemplateQuenchSlot.AfterTablesScripts)
            .SelectMany(f => f.Scripts).ToList();

        [JsonIgnore]
        public List<SqlScript> AfterTablesObjectScripts => ScriptFolders
            .Where(f => f.QuenchSlot is TemplateQuenchSlot.Objects or TemplateQuenchSlot.AfterTablesObjects)
            .SelectMany(f => f.Scripts).ToList();

        [JsonIgnore]
        public List<SqlScript> TableDataScripts => ScriptFolders
            .Where(f => f.QuenchSlot == TemplateQuenchSlot.TableData)
            .SelectMany(f => f.Scripts).ToList();

        [JsonIgnore]
        public List<SqlScript> AfterScripts => ScriptFolders
            .Where(f => f.QuenchSlot == TemplateQuenchSlot.After)
            .SelectMany(f => f.Scripts).ToList();

        #endregion

        public Template Clone()
        {
            var clone = new Template
            {
                Name = Name,
                FilePath = FilePath,
                TableSchema = TableSchema,
                DatabaseIdentificationScript = DatabaseIdentificationScript,
                VersionStampScript = VersionStampScript,
                Product = Product,
                ScriptTokens = new Dictionary<string, string>(ScriptTokens),
                NonQueryTokens = new Dictionary<string, string>(NonQueryTokens),
                LoggableTokens = new Dictionary<string, string>(LoggableTokens)
            };
            foreach (var token in QueryTokens)
                clone.QueryTokens.Add(token.Key, token.Value);
            clone.ScriptFolders.AddRange(ScriptFolders.Select(s => s.Clone()));
            clone.Tables.AddRange(Tables);
            clone.MaterializedViews.AddRange(MaterializedViews);
            clone.MaterializedViewSchema = MaterializedViewSchema;
            clone.IndexedViews.AddRange(IndexedViews);
            clone.IndexedViewSchema = IndexedViewSchema;
            return clone;
        }

        /// <summary>
        /// Loads a Template and its Tables from disk using platform-aware deserialization.
        /// Resolves file tokens, merges product + template tokens, loads scripts, and applies token replacement.
        /// </summary>
        public static Template Load(string templateName, Product product)
        {
            var schemaPackagePath = Path.GetDirectoryName(product.FilePath) ?? "";
            var templatePath = Path.Combine(schemaPackagePath, "Templates", templateName);
            var templateFilePath = Path.Combine(templatePath, "Template.json");
            var template = JsonHelper.TemplateLoad(templateFilePath, product.Platform);
            template.FilePath = templateFilePath;
            template.Product = product;

            foreach (var token in template.ScriptTokens)
                template.LoggableTokens.Add(token.Key, token.Value);

            TokenHelper.ResolveFileTokens(template.ScriptTokens, templatePath, product.Platform);

            // Merge template and product script tokens — template takes precedence
            var scriptTokens = template.ScriptTokens
                .Concat(product.ScriptTokens.Concat(product.QueryTokens)
                    .Where(st => !template.ScriptTokens.ContainsKey(st.Key)))
                .ToDictionary(k => k.Key, v => v.Value);

            template.InstanceLoad(scriptTokens, product.Platform);
            return template;
        }

        private void InstanceLoad(Dictionary<string, string> scriptTokens, Platform platform)
        {
            LoadTables(platform);
            LoadMaterializedViews(platform);
            LoadIndexedViews(platform);
            QueryTokens = TokenHelper.SplitOutQueryTokens(scriptTokens);
            TokenHelper.ResolveSpecificTableTokens(scriptTokens, Tables, platform);
            TokenHelper.ResolveSpecificMaterializedViewTokens(scriptTokens, MaterializedViews);
            TokenHelper.ResolveSpecificIndexedViewTokens(scriptTokens, IndexedViews);
            NonQueryTokens = scriptTokens;
            var tokens = scriptTokens.ToList();
            tokens.Add(new("TemplateName", Name ?? "UNSPECIFIED"));

            var tableQueue = new TaskQueueManager<Table>(Environment.ProcessorCount * 2);
            Tables.ForEach(table => tableQueue.AddToQueue(table, t => t.ResolveScriptTokensInTableComponentScripts(tokens)));
            tableQueue.WaitForAll();

            MaterializedViews.ForEach(mv =>
            {
                var viewTokens = tokens.Concat(Table.GetCustomTokens(mv.Extensions, "MaterializedView.")).ToList();
                mv.ShouldApplyExpression = Table.TableTokenReplace(mv.ShouldApplyExpression, viewTokens);
                foreach (var index in mv.Indexes)
                {
                    var indexTokens = viewTokens.Concat(Table.GetCustomTokens(index.Extensions)).ToList();
                    index.ShouldApplyExpression = Table.TableTokenReplace(index.ShouldApplyExpression, indexTokens);
                    index.FilterExpression = Table.TableTokenReplace(index.FilterExpression, indexTokens);
                }
            });

            IndexedViews.ForEach(iv =>
            {
                var viewTokens = tokens.Concat(Table.GetCustomTokens(iv.Extensions, "IndexedView.")).ToList();
                iv.ShouldApplyExpression = Table.TableTokenReplace(iv.ShouldApplyExpression, viewTokens);
                foreach (var index in iv.Indexes)
                {
                    var indexTokens = viewTokens.Concat(Table.GetCustomTokens(index.Extensions)).ToList();
                    index.ShouldApplyExpression = Table.TableTokenReplace(index.ShouldApplyExpression, indexTokens);
                }
            });

            TableSchema = JArray.FromObject(Tables).ToString();
            tokens.Add(new("TableSchema", TableSchema.Replace("'", "''")));

            MaterializedViewSchema = JArray.FromObject(MaterializedViews).ToString();
            tokens.Add(new("MaterializedViewSchema", MaterializedViewSchema.Replace("'", "''")));

            IndexedViewSchema = JArray.FromObject(IndexedViews).ToString();
            tokens.Add(new("IndexedViewSchema", IndexedViewSchema.Replace("'", "''")));

            if (ScriptFolders.Count == 0) ScriptFolders.AddRange(GetDefaultTemplateFolders(platform));

            var templateDir = Path.GetDirectoryName(FilePath) ?? "";
            if (platform == Platform.SqlServer)
                ApplyLegacyFolderFallbacks(templateDir);

            var folderQueue = new TaskQueueManager<ScriptFolder>(Environment.ProcessorCount * 2);
            ScriptFolders.ForEach(folder => folderQueue.AddToQueue(folder, f => f.LoadSqlFiles(templateDir, tokens, platform)));
            folderQueue.WaitForAll();

            DatabaseIdentificationScript = SqlScript.TokenReplace(DatabaseIdentificationScript ?? "", tokens);
            VersionStampScript = SqlScript.TokenReplace(VersionStampScript ?? "", tokens);
            BaselineValidationScript = SqlScript.TokenReplace(BaselineValidationScript ?? "", tokens);
        }

        public static List<TemplateFolder> GetDefaultTemplateFolders(Platform platform) => platform switch
        {
            Platform.SqlServer =>
            [
                new TemplateFolder { FolderPath = "Before Scripts", QuenchSlot = TemplateQuenchSlot.Before },
                new TemplateFolder { FolderPath = "Schemas", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Schemas },
                new TemplateFolder { FolderPath = "DataTypes", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.DataTypes },
                new TemplateFolder { FolderPath = "FullTextCatalogs", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.FullTextCatalogs },
                new TemplateFolder { FolderPath = "FullTextStopLists", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.FullTextStopLists },
                new TemplateFolder { FolderPath = "XMLSchemaCollections", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.XMLSchemaCollections },
                new TemplateFolder { FolderPath = "Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Functions },
                new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Views },
                new TemplateFolder { FolderPath = "Procedures", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Procedures },
                new TemplateFolder { FolderPath = "Triggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Triggers },
                new TemplateFolder { FolderPath = "DDLTriggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.DDLTriggers },
                new TemplateFolder { FolderPath = "Table Data", QuenchSlot = TemplateQuenchSlot.TableData },
                new TemplateFolder { FolderPath = "After Scripts", QuenchSlot = TemplateQuenchSlot.After },
            ],
            Platform.PostgreSQL =>
            [
                new TemplateFolder { FolderPath = "Before Scripts", QuenchSlot = TemplateQuenchSlot.Before },
                new TemplateFolder { FolderPath = "Schemas", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Schemas },
                new TemplateFolder { FolderPath = "Domain Types", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.DomainTypes },
                new TemplateFolder { FolderPath = "Enum Types", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.EnumTypes },
                new TemplateFolder { FolderPath = "Composite Types", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.CompositeTypes },
                new TemplateFolder { FolderPath = "Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Functions },
                new TemplateFolder { FolderPath = "Trigger Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.TriggerFunctions },
                new TemplateFolder { FolderPath = "Window Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.WindowFunctions },
                new TemplateFolder { FolderPath = "Aggregates", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Aggregates },
                new TemplateFolder { FolderPath = "Procedures", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Procedures },
                new TemplateFolder { FolderPath = "Sequences", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Sequences },
                new TemplateFolder { FolderPath = "Rules", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Rules },
                new TemplateFolder { FolderPath = "Triggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Triggers },
                new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Views },
                new TemplateFolder { FolderPath = "Table Data", QuenchSlot = TemplateQuenchSlot.TableData },
                new TemplateFolder { FolderPath = "After Scripts", QuenchSlot = TemplateQuenchSlot.After },
            ],
            Platform.MySQL =>
            [
                new TemplateFolder { FolderPath = "Before Scripts", QuenchSlot = TemplateQuenchSlot.Before },
                new TemplateFolder { FolderPath = "Events", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Events },
                new TemplateFolder { FolderPath = "Functions", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Functions },
                new TemplateFolder { FolderPath = "Procedures", QuenchSlot = TemplateQuenchSlot.Objects, ObjectType = ScriptObjectType.Procedures },
                new TemplateFolder { FolderPath = "Triggers", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Triggers },
                new TemplateFolder { FolderPath = "Views", QuenchSlot = TemplateQuenchSlot.AfterTablesObjects, ObjectType = ScriptObjectType.Views },
                new TemplateFolder { FolderPath = "Table Data", QuenchSlot = TemplateQuenchSlot.TableData },
                new TemplateFolder { FolderPath = "After Scripts", QuenchSlot = TemplateQuenchSlot.After },
            ],
            Platform.Unknown => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Platform has not been assigned."),
            _ => []
        };

        /// <summary>
        /// For SQL Server products with no explicit ScriptFolders, checks whether the default
        /// folder names exist on disk. If the new standardized names (Before Scripts, After Scripts,
        /// Table Data) don't exist but the legacy names (MigrationScripts/Before, MigrationScripts/After,
        /// TableData) do, falls back to the legacy paths for backward compatibility.
        /// </summary>
        internal void ApplyLegacyFolderFallbacks(string templateDir)
        {
            var dir = ProductDirectoryWrapper.GetFromFactory();
            foreach (var folder in ScriptFolders)
            {
                if (dir.Exists(Path.Combine(templateDir, folder.FolderPath))) continue;

                var legacyPath = folder.FolderPath switch
                {
                    "Before Scripts" => "MigrationScripts/Before",
                    "After Scripts" => "MigrationScripts/After",
                    "Table Data" => "TableData",
                    _ => null
                };

                if (legacyPath != null && dir.Exists(Path.Combine(templateDir, legacyPath)))
                    folder.FolderPath = legacyPath;
            }
        }

        private void LoadMaterializedViews(Platform platform)
        {
            if (platform != Platform.PostgreSQL) return;
            var matViewsPath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "Materialized Views");
            if (!ProductDirectoryWrapper.GetFromFactory().Exists(matViewsPath)) return;
            var files = ProductDirectoryWrapper.GetFromFactory()
                .GetFiles(matViewsPath, "*.json", SearchOption.AllDirectories)
                .OrderBy(x => x);
            MaterializedViews.AddRange(files.Select(f =>
            {
                try
                {
                    var json = ProductFileWrapper.GetFromFactory().ReadAllText(f);
                    return PlatformDeserializer.DeserializeMaterializedView(json, platform);
                }
                catch (Exception e)
                {
                    throw new Exception($"Error loading materialized view from {f}\r\n{e.Message}", e);
                }
            }));
        }

        private void LoadIndexedViews(Platform platform)
        {
            if (platform != Platform.SqlServer) return;
            var indexedViewsPath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "Indexed Views");
            if (!ProductDirectoryWrapper.GetFromFactory().Exists(indexedViewsPath)) return;
            var files = ProductDirectoryWrapper.GetFromFactory()
                .GetFiles(indexedViewsPath, "*.json", SearchOption.AllDirectories)
                .OrderBy(x => x);
            IndexedViews.AddRange(files.Select(f =>
            {
                try
                {
                    var json = ProductFileWrapper.GetFromFactory().ReadAllText(f);
                    return PlatformDeserializer.DeserializeIndexedView(json, platform);
                }
                catch (Exception e)
                {
                    throw new Exception($"Error loading indexed view from {f}\r\n{e.Message}", e);
                }
            }));
        }

        private void LoadTables(Platform platform)
        {
            var tablesPath = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "Tables");
            if (!ProductDirectoryWrapper.GetFromFactory().Exists(tablesPath)) return;
            var files = ProductDirectoryWrapper.GetFromFactory()
                .GetFiles(tablesPath, "*.json", SearchOption.AllDirectories)
                .OrderBy(x => x);
            Tables.AddRange(files.Select(f => Table.Load(f, platform)));
        }
    }
}
