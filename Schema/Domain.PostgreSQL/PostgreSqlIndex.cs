// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.PostgreSQL
{
    public class PostgreSqlIndex : Index
    {
        [JsonProperty(Order = 100)]
        public string FilterExpression { get; set; }

        [JsonProperty(Order = 101)]
        public bool Clustered { get; set; }

        [JsonProperty(Order = 102)]
        public string IncludeColumns { get; set; }

        [JsonProperty(Order = 103)]
        public string AccessMethod { get; set; }

        [SchemaProperty(Minimum = 0, Maximum = 100)]
        [JsonProperty(Order = 104)]
        public int FillFactor { get; set; }

        [JsonProperty(Order = 105)]
        public bool NullsNotDistinct { get; set; }

        [JsonProperty(Order = 106)]
        public bool Deferrable { get; set; }

        [JsonProperty(Order = 107)]
        public bool InitiallyDeferred { get; set; }

        [JsonProperty(Order = 108)]
        [SchemaProperty(AuthoredOnly = true)]
        public bool UpdateFillFactor { get; set; }

        // An index does NOT inherit its table's tablespace -- created with no clause it follows
        // default_tablespace, which is usually but not always the same place. Same unset-means-unmanaged
        // and create-time-only posture as PostgreSqlTable.Tablespace; see the comment there.
        [SchemaProperty(MaxLength = 128,
            Description = "PostgreSQL only. The tablespace this index lives on. Omit to leave placement unmanaged — an omitted value does NOT mean the database default, and an index does not automatically follow its table. Create-time only: moving an existing index rebuilds it, so a declared tablespace that differs from where the index already lives is refused rather than moved.")]
        [JsonProperty(Order = 113, NullValueHandling = NullValueHandling.Ignore)]
        public string Tablespace { get; set; }
    }
}
