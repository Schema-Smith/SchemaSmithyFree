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
        public bool UpdateFillFactor { get; set; }

        // Filegroup placement (#filegroups): same name-only, null-means-default contract as
        // SqlServerTable.FileGroup -- see that property's comment. A table and its indexes are commonly
        // split across filegroups on purpose, so this is independent of the table's own FileGroup.
        [JsonProperty(Order = 107, NullValueHandling = NullValueHandling.Ignore)]
        public string FileGroup { get; set; }
    }
}
