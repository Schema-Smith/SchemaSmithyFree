// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain.PostgreSQL
{
    public class PostgreSqlCheckConstraint : CheckConstraint
    {
        [JsonProperty(Order = 100)]
        public bool Deferrable { get; set; }

        [JsonProperty(Order = 101)]
        public bool InitiallyDeferred { get; set; }
    }
}
