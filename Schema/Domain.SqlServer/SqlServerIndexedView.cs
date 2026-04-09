// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.Domain.SqlServer
{
    public class SqlServerIndexedView : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        [JsonProperty(Order = 2)]
        [DefaultValue("dbo")]
        public string Schema { get; set; } = "dbo";

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 3)]
        public string Definition { get; set; } = "";

        [JsonProperty(Order = 4)]
        public string ShouldApplyExpression { get; set; }

        [JsonProperty(Order = 10)]
        public List<SqlServerIndex> Indexes { get; set; } = [];
    }
}
