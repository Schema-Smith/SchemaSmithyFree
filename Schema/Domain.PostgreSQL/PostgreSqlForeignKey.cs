// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using SchemaSmith.Pro;

namespace Schema.Domain.PostgreSQL
{
    public class PostgreSqlForeignKey : ForeignKey, IDeliverableForeignKey
    {
        [JsonProperty(Order = 100)]
        public string RelatedTableSchema { get; set; } = "public";

        [JsonProperty(Order = 101)]
        public bool Deferrable { get; set; }

        [JsonProperty(Order = 102)]
        public bool InitiallyDeferred { get; set; }

        [SchemaProperty(Pattern = "FULL|PARTIAL|SIMPLE")]
        [JsonProperty(Order = 103)]
        public string MatchType { get; set; } = "FULL";
    }
}
