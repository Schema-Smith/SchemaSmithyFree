// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

using SchemaSmith.Pro;

namespace Schema.Domain.PostgreSQL
{
    public class PostgreSqlTable : Table, IDeliverableTable
    {
        [JsonIgnore]
        public IReadOnlyList<IDeliverableColumn> DeliverableColumns => Columns.Cast<IDeliverableColumn>().ToList();

        [JsonIgnore]
        public IReadOnlyList<IDeliverableForeignKey> DeliverableForeignKeys => ForeignKeys.Cast<IDeliverableForeignKey>().ToList();

        public override void ResolveScriptTokensInTableComponentScripts(List<KeyValuePair<string, string>> tokens)
        {
            base.ResolveScriptTokensInTableComponentScripts(tokens);
            var tableTokens = tokens.Concat(GetCustomTokens(Extensions, "Table.")).ToList();
            foreach (var column in Columns.OfType<PostgreSqlColumn>())
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(column.Extensions)).ToList();
                column.GenerationExpression = TableTokenReplace(column.GenerationExpression, scriptTokens);
            }
            foreach (var index in Indexes.OfType<PostgreSqlIndex>())
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(index.Extensions)).ToList();
                index.FilterExpression = TableTokenReplace(index.FilterExpression, scriptTokens);
            }
            foreach (var statistic in Statistics)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(statistic.Extensions)).ToList();
                statistic.ShouldApplyExpression = TableTokenReplace(statistic.ShouldApplyExpression, scriptTokens);
            }
            foreach (var exclude in ExcludeConstraints)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(exclude.Extensions)).ToList();
                exclude.ShouldApplyExpression = TableTokenReplace(exclude.ShouldApplyExpression, scriptTokens);
                exclude.FilterExpression = TableTokenReplace(exclude.FilterExpression, scriptTokens);
            }
        }

        [JsonProperty(Order = 100)]
        public string Schema { get; set; } = "public";

        [JsonProperty(Order = 101)]
        public List<Statistic> Statistics { get; set; } = [];

        [JsonProperty(Order = 102)]
        public List<ExcludeConstraint> ExcludeConstraints { get; set; } = [];

        [JsonProperty(Order = 103)]
        public new PostgreSqlDataDelivery DataDelivery
        {
            get => (PostgreSqlDataDelivery)base.DataDelivery;
            set => base.DataDelivery = value;
        }

        [JsonProperty(Order = 105)]
        public bool RowLevelSecurity { get; set; }

        [JsonProperty(Order = 106)]
        public bool ForceRowLevelSecurity { get; set; }

        [JsonProperty(Order = 107)]
        public string AccessMethod { get; set; }

        [JsonProperty(Order = 108)]
        public string PersistenceType { get; set; }

        [JsonProperty(Order = 109)]
        public bool UpdateFillFactor { get; set; }

        [SchemaProperty(Minimum = 0, Maximum = 100)]
        [JsonProperty(Order = 110)]
        public short FillFactor { get; set; }
    }
}
