// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Schema.Delivery;

namespace Schema.Domain.MySQL
{
    public class MySqlTable : Table, IDeliverableTable
    {
        string IDeliverableTable.Schema => null;

        [JsonIgnore]
        public IReadOnlyList<IDeliverableColumn> DeliverableColumns => Columns.Cast<IDeliverableColumn>().ToList();

        [JsonIgnore]
        public IReadOnlyList<IDeliverableForeignKey> DeliverableForeignKeys => ForeignKeys.Cast<IDeliverableForeignKey>().ToList();

        public override void ResolveScriptTokensInTableComponentScripts(List<KeyValuePair<string, string>> tokens)
        {
            base.ResolveScriptTokensInTableComponentScripts(tokens);
            var tableTokens = tokens.Concat(GetCustomTokens(Extensions, "Table.")).ToList();
            foreach (var column in Columns.OfType<MySqlColumn>())
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(column.Extensions)).ToList();
                column.GenerationExpression = TableTokenReplace(column.GenerationExpression, scriptTokens);
            }
        }

        [JsonProperty(Order = 100)]
        public string Engine { get; set; } = "InnoDB";

        [SchemaProperty(Pattern = "Dynamic|Compact|Compressed|Redundant|Fixed")]
        [JsonProperty(Order = 101)]
        public string RowFormat { get; set; }

        [JsonProperty(Order = 102)]
        public string CharacterSet { get; set; }

        [JsonProperty(Order = 103)]
        public string Collation { get; set; }

        [JsonProperty(Order = 104)]
        public string Comment { get; set; }

        [SchemaProperty(Minimum = 1)]
        [JsonProperty(Order = 105)]
        public ulong? AutoIncrementValue { get; set; }

        [JsonProperty(Order = 106)]
        public List<FullTextIndex> FullTextIndexes { get; set; } = [];
    }
}
