// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.SqlServer
{
    public class FullTextIndex : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string FullTextCatalog { get; set; }

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string KeyIndex { get; set; }

        [JsonProperty(Order = 3)]
        public string ChangeTracking { get; set; } = "AUTO";

        [JsonProperty(Order = 4)]
        public string StopList { get; set; } = "SYSTEM";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 5)]
        public string Columns { get; set; }

        [JsonProperty(Order = 6)]
        public string ShouldApplyExpression { get; set; }
    }
}
