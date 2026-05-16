// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.Domain.PostgreSQL
{
    public class PostgreSqlMaterializedView : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "";

        // Default resolution moved to SchemaDefaultResolver. Regular templates: defaults to "public".
        // Schema templates: defaults to "{{SchemaName}}"; explicit literals are rejected at load.
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)]
        public string Schema { get; set; }

        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 3)]
        public string Definition { get; set; } = "";

        [JsonProperty(Order = 4)]
        [DefaultValue(true)]
        public bool WithData { get; set; } = true;

        [JsonProperty(Order = 5)]
        public string Tablespace { get; set; }

        [JsonProperty(Order = 6)]
        public string AccessMethod { get; set; }

        [JsonProperty(Order = 7)]
        public string ShouldApplyExpression { get; set; }

        [JsonProperty(Order = 10)]
        public List<PostgreSqlIndex> Indexes { get; set; } = [];
    }
}
