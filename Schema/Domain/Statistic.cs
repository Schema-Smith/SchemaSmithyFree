// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain;

public class Statistic
{
    [JsonProperty(Order = 1)]
    [SchemaProperty(Required = true)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    [SchemaProperty(Required = true)]
    public string Columns { get; set; }
    [JsonProperty(Order = 3)]
    [SchemaProperty(Minimum = 0, Maximum = 100)]
    public byte SampleSize { get; set; }
    [JsonProperty(Order = 4)]
    public string FilterExpression { get; set; }
}