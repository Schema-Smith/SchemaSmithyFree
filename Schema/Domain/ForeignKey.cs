// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain
{
    public class ForeignKey : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string Columns { get; set; } = "";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 5)]
        public string RelatedTable { get; set; } = "";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 6)]
        public string RelatedColumns { get; set; } = "";

        [SchemaProperty(Pattern = "NO ACTION|RESTRICT|CASCADE|SET NULL|SET DEFAULT")]
        [JsonProperty(Order = 7)]
        public string DeleteAction { get; set; }

        [SchemaProperty(Pattern = "NO ACTION|RESTRICT|CASCADE|SET NULL|SET DEFAULT")]
        [JsonProperty(Order = 8)]
        public string UpdateAction { get; set; }

        [JsonProperty(Order = 90)]
        public string ShouldApplyExpression { get; set; }
    }
}
