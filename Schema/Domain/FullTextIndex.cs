// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Newtonsoft.Json;

using Newtonsoft.Json;

namespace Schema.Domain;

public class FullTextIndex
{
    [JsonProperty(Order = 1)]
    [SchemaProperty(Required = true)]
    public string FullTextCatalog { get; set; }
    [JsonProperty(Order = 2)]
    [SchemaProperty(Required = true)]
    public string KeyIndex { get; set; }
    [JsonProperty(Order = 3)]
    public string ChangeTracking { get; set; }
    [JsonProperty(Order = 4)]
    public string StopList { get; set; }
    [JsonProperty(Order = 5)]
    [SchemaProperty(Required = true)]
    public string Columns { get; set; }
}
