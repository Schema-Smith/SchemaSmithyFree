// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Newtonsoft.Json;

namespace Schema.Domain;

public class XmlIndex
{
    [JsonProperty(Order = 1)]
    [SchemaProperty(Required = true)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    public bool IsPrimary { get; set; }
    [JsonProperty(Order = 3)]
    [SchemaProperty(Required = true)]
    public string Column { get; set; }
    [JsonProperty(Order = 4)]
    public string PrimaryIndex { get; set; } // only when not IsPrimary 
    [JsonProperty(Order = 5)]
    [SchemaProperty(Pattern = "VALUE|PATH|PROPERTY")]
    public string SecondaryIndexType { get; set; } // only when not IsPrimary
}
