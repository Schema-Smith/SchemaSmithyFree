// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Schema.Delivery;

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
            foreach (var fullTextIndex in FullTextIndex)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(fullTextIndex.Extensions)).ToList();
                fullTextIndex.ShouldApplyExpression = TableTokenReplace(fullTextIndex.ShouldApplyExpression, scriptTokens);
            }
        }

        // Default resolution moved to SchemaDefaultResolver (called from Template.Load) so schema
        // templates can default to "{{SchemaName}}" instead of "dbo". Regular templates still see
        // "dbo" post-load; bare-constructor instances see null until the resolver runs.
        [JsonProperty(Order = 100, NullValueHandling = NullValueHandling.Ignore)]
        public string Schema { get; set; }

        [JsonProperty(Order = 101)]
        public string CompressionType { get; set; } = "NONE";

        [JsonProperty(Order = 102)]
        public bool IsTemporal { get; set; }

        // Temporal history-table identity + retention (#depth-gap): a history table has a name and a
        // schema like any other table, so it gets the same two-flat-string shape ForeignKey already uses
        // for RelatedTableSchema/RelatedTable. Null means "SchemaSmith's own default" -- same schema as
        // the versioned table, name "<Table>_Hist" -- so existing IsTemporal-only packages deploy exactly
        // as before. HistoryRetentionPeriod is stored as the raw SQL Server token ("5 YEARS", "INFINITE")
        // rather than a split number+enum: CompressionType/DeleteAction/UpdateAction already store native
        // DB tokens as plain strings and let the engine validate them, and the DDL clause it feeds
        // (HISTORY_RETENTION_PERIOD = <token>) takes that exact shape. Null means unset (SQL Server's own
        // default, INFINITE unless a database-level default retention is configured) -- preserving
        // today's silent loss-free behavior for packages that don't set it.
        [JsonProperty(Order = 108, NullValueHandling = NullValueHandling.Ignore)]
        public string HistoryTableSchema { get; set; }

        [JsonProperty(Order = 109, NullValueHandling = NullValueHandling.Ignore)]
        public string HistoryTableName { get; set; }

        [JsonProperty(Order = 110, NullValueHandling = NullValueHandling.Ignore)]
        public string HistoryRetentionPeriod { get; set; }

        [JsonProperty(Order = 103)]
        public List<XmlIndex> XmlIndexes { get; set; } = [];

        [JsonProperty(Order = 104)]
        public List<Statistic> Statistics { get; set; } = [];

        [JsonProperty(Order = 105)]
        [JsonConverter(typeof(FullTextIndexListJsonConverter))]
        [SchemaProperty(SingleOrArray = true)]
        public List<FullTextIndex> FullTextIndex { get; set; } = [];

        public bool ShouldSerializeFullTextIndex() => FullTextIndex is { Count: > 0 };

        [JsonProperty(Order = 106)]
        [SchemaProperty(AuthoredOnly = true)]
        public bool UpdateFillFactor { get; set; }

        [JsonProperty(Order = 107)]
        public bool EnableCDC { get; set; }

        // Table-level Change Tracking (#change-tracking). Distinct from the FullTextIndex option spelled
        // WITH CHANGE_TRACKING = AUTO|MANUAL|OFF, which is unrelated and already implemented.
        // Requires Change Tracking enabled on the DATABASE (sys.change_tracking_databases). SchemaSmith
        // does not turn that on -- ALTER DATABASE ... SET CHANGE_TRACKING = ON changes retention and
        // cleanup for every table in the database. Declaring it without the database toggle is reported
        // through UnsupportedFeaturePolicy rather than silently skipped.
        [JsonProperty(Order = 112)]
        public bool EnableChangeTracking { get; set; }

        // Only meaningful when EnableChangeTracking is true; ignored otherwise. Records WHICH columns
        // changed, not merely that the row did, at the cost of extra tracking storage.
        [JsonProperty(Order = 113)]
        public bool TrackColumnsUpdated { get; set; }

        // Filegroup placement (#filegroups): a NAME only -- never a physical file path, which would make
        // the package non-portable across environments. Null means "SQL Server's own default filegroup",
        // preserving today's behavior for every existing package. SchemaSmith does not create filegroups
        // (it errors loudly if the named one is missing on the target) and does not rebuild a table onto a
        // newly-declared filegroup (that is a rebuild, deferred to the roadmap's Table Rebuild Triggers
        // item) -- it errors if the declared name differs from where the table already lives.
        [JsonProperty(Order = 111, NullValueHandling = NullValueHandling.Ignore)]
        public string FileGroup { get; set; }
        // FILESTREAM_ON <filegroup>: which FILESTREAM filegroup this table's FILESTREAM data lands on.
        // Name only, like FileGroup, and null means "the database's default FILESTREAM filegroup".
        // Effectively immutable once assigned -- ALTER TABLE ... SET (FILESTREAM_ON = ...) fails 1726 on a
        // table that already has one -- so a declared name that differs from the live one is refused
        // rather than silently ignored, the same posture FileGroup takes.
        [JsonProperty(Order = 114, NullValueHandling = NullValueHandling.Ignore)]
        public string FileStreamFileGroup { get; set; }

    }
}
