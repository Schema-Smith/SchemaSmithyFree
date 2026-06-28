// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.MySQL
{
    public class MySqlColumn : Column
    {
        [JsonProperty(Order = 100)]
        public string GenerationExpression { get; set; }

        [JsonProperty(Order = 101)]
        public bool AutoIncrement { get; set; }

        [SchemaProperty(Pattern = "STORED|VIRTUAL")]
        [JsonProperty(Order = 102)]
        public string Generated { get; set; }

        [JsonProperty(Order = 103)]
        public string CharacterSet { get; set; }

        [JsonProperty(Order = 104)]
        public string Collation { get; set; }

        [JsonProperty(Order = 105)]
        public string Comment { get; set; }

        [JsonProperty(Order = 106)]
        public string CheckExpression { get; set; }
    }
}
