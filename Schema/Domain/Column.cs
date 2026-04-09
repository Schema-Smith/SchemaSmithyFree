// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using SchemaSmith.Pro;

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

        [JsonProperty(Order = 91)]
        public string OldName { get; set; }
    }
}
