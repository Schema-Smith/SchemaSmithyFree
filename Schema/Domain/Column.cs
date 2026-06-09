// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Delivery;

namespace Schema.Domain
{
    public class Column : DynamicBase, IDeliverableColumn
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string DataType { get; set; } = "";

        [JsonProperty(Order = 3)]
        public bool Nullable { get; set; }

        [JsonProperty(Order = 4)]
        public string Default { get; set; }

        [JsonProperty(Order = 90)]
        public string ShouldApplyExpression { get; set; }

        // Labels a conditional variant: the intent behind its ShouldApplyExpression,
        // echoed in quench log messages when the variant applies.
        [SchemaProperty(MaxLength = 128, Description = "Optional label for a conditional variant — names the intent behind its ShouldApplyExpression and appears in deployment logging when the variant is applied.")]
        [JsonProperty(Order = 92)]
        public string VariantName { get; set; }

        [JsonProperty(Order = 91)]
        public string OldName { get; set; }
    }
}
