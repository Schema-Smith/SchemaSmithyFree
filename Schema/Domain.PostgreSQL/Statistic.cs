// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.PostgreSQL
{
    public class Statistic : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; }

        [JsonProperty(Order = 2)]
        public string Kind { get; set; }

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 3)]
        public string StatisticsColumns { get; set; }

        [JsonProperty(Order = 4)]
        public string ShouldApplyExpression { get; set; }
    }
}
