// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.SqlServer
{
    public class SqlServerColumn : Column
    {
        [JsonProperty(Order = 100)]
        public string CheckExpression { get; set; }

        [JsonProperty(Order = 101)]
        public string ComputedExpression { get; set; }

        [JsonProperty(Order = 102)]
        public bool Persisted { get; set; }

        [JsonProperty(Order = 103)]
        public bool Sparse { get; set; }

        [JsonProperty(Order = 104)]
        public string Collation { get; set; }

        [JsonProperty(Order = 105)]
        public string DataMaskFunction { get; set; }

        [SchemaProperty(Pattern = "DETERMINISTIC|RANDOMIZED|NONE")]
        [JsonProperty(Order = 106)]
        public string EncryptionType { get; set; } = "NONE";

        [JsonProperty(Order = 107)]
        public string EncryptionKey { get; set; }

        [JsonProperty(Order = 108)]
        public string EncryptionAlgorithm { get; set; }

    }
}
