// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.SqlServer
{
    public class SqlServerIndex : Index
    {
        [JsonProperty(Order = 100)]
        public string FilterExpression { get; set; }

        [JsonProperty(Order = 101)]
        public string CompressionType { get; set; } = "NONE";

        [JsonProperty(Order = 102)]
        public bool Clustered { get; set; }

        [JsonProperty(Order = 103)]
        public bool ColumnStore { get; set; }

        [SchemaProperty(Minimum = 0, Maximum = 100)]
        [JsonProperty(Order = 104)]
        public byte FillFactor { get; set; }

        [JsonProperty(Order = 105)]
        public string IncludeColumns { get; set; }

        [JsonProperty(Order = 106)]
        [SchemaProperty(AuthoredOnly = true)]
        public bool UpdateFillFactor { get; set; }

        // Filegroup placement (#filegroups): same name-only, null-means-default contract as
        // SqlServerTable.FileGroup -- see that property's comment. A table and its indexes are commonly
        // split across filegroups on purpose, so this is independent of the table's own FileGroup.
        [JsonProperty(Order = 107, NullValueHandling = NullValueHandling.Ignore)]
        public string FileGroup { get; set; }

        /// <summary>
        /// <c>IGNORE_DUP_KEY</c>. Not a tuning knob -- it changes what the engine does with the same
        /// statement. Verified on SQL Server 2022: with it ON, a multi-row INSERT containing a duplicate
        /// SUCCEEDS, the duplicate row is discarded with a warning and the other rows land; with it OFF,
        /// the whole statement fails with 2601 and nothing lands. Two databases whose index definitions
        /// otherwise match will disagree about whether an application's INSERT works.
        /// <para>Only meaningful on a unique index or unique constraint; SQL Server rejects it elsewhere.</para>
        /// </summary>
        [JsonProperty(Order = 108)]
        public bool IgnoreDuplicateKey { get; set; }

        /// <summary>
        /// <c>PAD_INDEX</c> -- applies <see cref="FillFactor"/> to the intermediate index pages as well as
        /// the leaf. Meaningless on its own, which is why it belongs with the property it modifies rather
        /// than standing alone. Read from <c>sys.indexes.is_padded</c>.
        /// <para>Unlike IGNORE_DUP_KEY there is no <c>ALTER INDEX ... SET</c> for it (error 155), so a
        /// change rides the ordinary index drop-and-recreate rather than an in-place alter.</para>
        /// </summary>
        [JsonProperty(Order = 109)]
        public bool PadIndex { get; set; }
    }
}
