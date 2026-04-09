// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.SqlServer
{
    public class Statistic : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; }

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string Columns { get; set; }

        [SchemaProperty(Minimum = 0, Maximum = 100)]
        [JsonProperty(Order = 3)]
        public byte SampleSize { get; set; }

        [JsonProperty(Order = 4)]
        public string FilterExpression { get; set; }

        [JsonProperty(Order = 5)]
        public string ShouldApplyExpression { get; set; }
    }
}
