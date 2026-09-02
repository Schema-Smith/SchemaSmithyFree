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

        // XML_COMPRESSION, the sibling of DATA_COMPRESSION above and independent of it -- a table can be
        // PAGE compressed and XML compressed at once.
        //
        // THE VERSION STORY IS ASYMMETRIC, and verified live rather than read. The clause deploys from
        // SQL Server 2022, but sys.partitions.xml_compression does NOT exist there: on 2022 CU25 the
        // column is only on sys.internal_partitions, which reports NULL for an ordinary table. It appears
        // on sys.partitions in 2025. So 2022-2024 can deploy this and can never read it back, the same
        // shape as MariaDB application-time periods (declarable 10.4.3, readable 11.4). Extraction on
        // those versions therefore PRESERVES whatever the package already declared instead of emitting
        // nothing, which would silently drop it -- see ImportTableHelper.
        //
        // Unlike TextImageFileGroup this is not create-time only: ALTER TABLE ... REBUILD WITH changes it.
        [SchemaProperty(Description = "SQL Server 2022+. Compresses XML column data in place. Sibling of CompressionType (DATA_COMPRESSION), and independent of it. **Deployable from 2022, but only EXTRACTABLE from 2025** — sys.partitions.xml_compression does not exist before then, so on 2022-2024 SchemaTongs carries the value forward from the package it is refreshing rather than dropping it.")]
        [JsonProperty(Order = 118)]
        public bool XmlCompression { get; set; }

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

        // TEXTIMAGE_ON <filegroup>: which filegroup this table's large-object data lands on -- text,
        // ntext, image, xml, and the (MAX) types. The third placement clause alongside FileGroup (ON) and
        // FileStreamFileGroup (FILESTREAM_ON); supporting two of the three was an accident of what got
        // built rather than a decision.
        //
        // Create-time only, like both siblings: there is no ALTER for LOB placement, so a declared name
        // that differs from the live one is refused rather than silently ignored. SQL Server also REJECTS
        // the clause outright (error 1709) on a table with no large-object column, so it is emitted only
        // when one is declared and refused by name otherwise.
        [JsonProperty(Order = 117, NullValueHandling = NullValueHandling.Ignore)]
        public string TextImageFileGroup { get; set; }
        // Graph tables (#graph): "Node" or "Edge" appends AS NODE / AS EDGE to the CREATE TABLE.
        // Null or "None" is an ordinary table.
        //
        // Create-time only, and that is the whole design constraint: SQL Server has no ALTER for it --
        // ALTER TABLE ... SET (AS NODE) is not syntax at all (error 156) -- so changing this on a table
        // that already exists is refused by name rather than attempted.
        //
        // The system-generated pseudo-columns SQL Server adds ($node_id, $edge_id, graph_id and the edge
        // *_id pair) are excluded from extraction via sys.columns.graph_type; see #402.
        [SchemaProperty(Pattern = "None|Node|Edge")]
        [JsonProperty(Order = 115, NullValueHandling = NullValueHandling.Ignore)]
        public string GraphType { get; set; }
        // Ledger tables (#ledger, SQL Server 2022): "AppendOnly" or "Updatable". Null or "Off" is an
        // ordinary table.
        //
        // Create-time only, like GraphType -- ALTER TABLE ... SET (LEDGER = ON) is error 102, not syntax --
        // so a change on a deployed table is refused rather than attempted.
        //
        // Cannot be combined with IsTemporal: an updatable ledger table is created WITH
        // (SYSTEM_VERSIONING = ON, LEDGER = ON), which overlaps what IsTemporal turns on, and sys.tables
        // then reports the table as NON_TEMPORAL_TABLE -- so both declarations together leave the package
        // permanently disagreeing with the target.
        //
        // Note that DROP on a ledger table is not a drop: SQL Server retains it as
        // MSSQL_DroppedLedgerTable_<name>_<guid>. Those retained objects are excluded from extraction
        // (#403).
        [SchemaProperty(Pattern = "Off|AppendOnly|Updatable")]
        [JsonProperty(Order = 116, NullValueHandling = NullValueHandling.Ignore)]
        public string Ledger { get; set; }

    }
}
