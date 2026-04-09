// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;

namespace Schema.Domain
{
    public class ScriptFolder
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string FolderPath { get; set; } = "";

        [SchemaProperty]
        [JsonProperty(Order = 10)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScriptObjectType ObjectType { get; set; }

        [JsonIgnore]
        public List<SqlScript> Scripts { get; } = [];

        public void LoadSqlFiles(string basePath, List<KeyValuePair<string, string>> scriptTokens, Platform platform = Platform.SqlServer)
        {
            var sqlFilePath = Path.Combine(basePath, FolderPath);
            if (!ProductDirectoryWrapper.GetFromFactory().Exists(sqlFilePath)) return;
            var files = ProductDirectoryWrapper.GetFromFactory()
                .GetFiles(sqlFilePath, "*.sql", SearchOption.AllDirectories)
                .OrderBy(x => x)
                .ToList();

            // Parallel file load — mostly I/O bound (file reads + token resolution)
            var loaded = new ConcurrentDictionary<string, SqlScript>();
            var queue = new TaskQueueManager<string>(Environment.ProcessorCount * 2);
            files.ForEach(f => queue.AddToQueue(f, file => loaded[file] = SqlScript.Load(file, scriptTokens, platform)));
            queue.WaitForAll();

            // Preserve original file ordering
            Scripts.AddRange(files.Select(f => loaded[f]));
        }

        protected ScriptFolder DeepClone(ScriptFolder clone)
        {
            clone.FolderPath = FolderPath;
            clone.ObjectType = ObjectType;
            clone.Scripts.AddRange(Scripts.Select(s => s.Clone()));
            return clone;
        }
    }
}
