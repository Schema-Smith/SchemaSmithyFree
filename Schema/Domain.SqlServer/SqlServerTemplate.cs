// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain.SqlServer
{
    public class SqlServerTemplate : Template
    {
        [JsonProperty(Order = 100)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ServerToQuench ServerToQuench { get; set; } = ServerToQuench.Primary;

        [JsonIgnore]
        public bool QuenchOnPrimary => ServerToQuench is ServerToQuench.Primary or ServerToQuench.Both;

        [JsonIgnore]
        public bool QuenchOnSecondary => ServerToQuench is ServerToQuench.Secondary or ServerToQuench.Both;
    }
}
