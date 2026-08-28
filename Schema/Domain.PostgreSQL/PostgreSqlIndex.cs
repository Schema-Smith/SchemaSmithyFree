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
    }
}
