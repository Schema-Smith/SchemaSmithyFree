// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.SqlServer
{
    public class XmlIndex : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; }

        [JsonProperty(Order = 2)]
        public bool IsPrimary { get; set; }

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 3)]
        public string Column { get; set; }

        [JsonProperty(Order = 4)]
        public string PrimaryIndex { get; set; }

        [SchemaProperty(Pattern = "VALUE|PATH|PROPERTY")]
        [JsonProperty(Order = 5)]
        public string SecondaryIndexType { get; set; }

        [JsonProperty(Order = 6)]
        public string ShouldApplyExpression { get; set; }
    }
}
