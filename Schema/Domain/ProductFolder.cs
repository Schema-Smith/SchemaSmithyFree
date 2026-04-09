// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Schema.Domain.SqlServer;

namespace Schema.Domain
{
    public class ProductFolder : ScriptFolder
    {
        [JsonProperty(Order = 2)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ServerToQuench ServerToQuench { get; set; } = ServerToQuench.Primary;

        [JsonProperty(Order = 3)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ProductQuenchSlot QuenchSlot { get; set; }

        [JsonIgnore]
        public bool QuenchOnPrimary => ServerToQuench is ServerToQuench.Primary or ServerToQuench.Both;

        [JsonIgnore]
        public bool QuenchOnSecondary => ServerToQuench is ServerToQuench.Secondary or ServerToQuench.Both;
    }
}
