// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain
{
    public class CheckConstraint : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string Expression { get; set; } = "";

        [JsonProperty(Order = 90)]
        public string ShouldApplyExpression { get; set; }
    }
}
