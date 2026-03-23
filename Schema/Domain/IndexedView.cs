// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Newtonsoft.Json;
using Schema.Utility;

namespace Schema.Domain;

public class IndexedView
{
    [JsonProperty(Order = 1)]
    [SchemaProperty(Required = true)]
    public string Name { get; set; } = "";

    [JsonProperty(Order = 2)]
    public string Schema { get; set; } = "dbo";

    [JsonProperty(Order = 3)]
    [SchemaProperty(Required = true)]
    public string Definition { get; set; } = "";

    [JsonProperty(Order = 4)]
    public List<Index> Indexes { get; set; } = [];
}
