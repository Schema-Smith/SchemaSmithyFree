// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schema.Domain;

public abstract class DynamicBase
{
    [JsonProperty(Order = 999)]
    public JToken Extensions { get; set; }
}
