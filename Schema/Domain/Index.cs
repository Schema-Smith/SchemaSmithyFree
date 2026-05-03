// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain
{
    public class Index : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        [JsonProperty(Order = 2)]
        public bool PrimaryKey { get; set; }

        [JsonProperty(Order = 3)]
        public bool Unique { get; set; }

        [JsonProperty(Order = 4)]
        public bool UniqueConstraint { get; set; }

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 10)]
        public string IndexColumns { get; set; } = "";

        [JsonProperty(Order = 90)]
        public string ShouldApplyExpression { get; set; }
    }
}
