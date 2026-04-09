// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SchemaSmith.Pro;

namespace Schema.Domain.SqlServer
{
    public class SqlServerTable : Table, IDeliverableTable
    {
        [JsonIgnore]
        public IReadOnlyList<IDeliverableColumn> DeliverableColumns => Columns.Cast<IDeliverableColumn>().ToList();

        [JsonIgnore]
        public IReadOnlyList<IDeliverableForeignKey> DeliverableForeignKeys => ForeignKeys.Cast<IDeliverableForeignKey>().ToList();

        public override void ResolveScriptTokensInTableComponentScripts(List<KeyValuePair<string, string>> tokens)
        {
            base.ResolveScriptTokensInTableComponentScripts(tokens);
            var tableTokens = tokens.Concat(GetCustomTokens(Extensions, "Table.")).ToList();
            foreach (var column in Columns.OfType<SqlServerColumn>())
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(column.Extensions)).ToList();
                column.CheckExpression = TableTokenReplace(column.CheckExpression, scriptTokens);
                column.ComputedExpression = TableTokenReplace(column.ComputedExpression, scriptTokens);
            }
            foreach (var index in Indexes.OfType<SqlServerIndex>())
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(index.Extensions)).ToList();
                index.FilterExpression = TableTokenReplace(index.FilterExpression, scriptTokens);
            }
            foreach (var xmlIndex in XmlIndexes)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(xmlIndex.Extensions)).ToList();
                xmlIndex.ShouldApplyExpression = TableTokenReplace(xmlIndex.ShouldApplyExpression, scriptTokens);
            }
            foreach (var statistic in Statistics)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(statistic.Extensions)).ToList();
                statistic.ShouldApplyExpression = TableTokenReplace(statistic.ShouldApplyExpression, scriptTokens);
                statistic.FilterExpression = TableTokenReplace(statistic.FilterExpression, scriptTokens);
            }
        }

        [JsonProperty(Order = 100)]
        public string Schema { get; set; } = "dbo";

        [JsonProperty(Order = 101)]
        public string CompressionType { get; set; } = "NONE";

        [JsonProperty(Order = 102)]
        public bool IsTemporal { get; set; }

        [JsonProperty(Order = 103)]
        public List<XmlIndex> XmlIndexes { get; set; } = [];

        [JsonProperty(Order = 104)]
        public List<Statistic> Statistics { get; set; } = [];

        [JsonProperty(Order = 105)]
        public FullTextIndex FullTextIndex { get; set; }

        [JsonProperty(Order = 106)]
        public bool UpdateFillFactor { get; set; }

        [JsonProperty(Order = 107)]
        public bool EnableCDC { get; set; }

    }
}
