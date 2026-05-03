// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.MySQL
{
    public class FullTextIndex : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; }

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string Columns { get; set; }

        [JsonProperty(Order = 3)]
        public string Parser { get; set; }

        [JsonProperty(Order = 4)]
        public string Comment { get; set; }

        [JsonProperty(Order = 20)]
        public string ShouldApplyExpression { get; set; }
    }
}
