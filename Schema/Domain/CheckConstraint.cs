// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schema.Domain;

public class CheckConstraint
{
    [JsonProperty(Order = 1)]
    [SchemaProperty(Required = true)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    [SchemaProperty(Required = true)]
    public string Expression { get; set; }
    [JsonProperty(Order = 3)]
    public JToken Extensions { get; set; }
}
