// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain;

public class Index
{
    [JsonProperty(Order = 1)]
    [SchemaProperty(Required = true)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    public string CompressionType { get; set; } = "NONE";
    [JsonProperty(Order = 3)]
    public bool PrimaryKey { get; set; }
    [JsonProperty(Order = 4)]
    public bool Unique { get; set; }
    [JsonProperty(Order = 5)]
    public bool UniqueConstraint { get; set; }
    [JsonProperty(Order = 6)]
    public bool Clustered { get; set; }
    [JsonProperty(Order = 7)]
    public bool ColumnStore { get; set; }
    [JsonProperty(Order = 8)]
    [SchemaProperty(Minimum = 0, Maximum = 100)]
    public byte FillFactor { get; set; }
    [JsonProperty(Order = 9)]
    [SchemaProperty(Required = true)]
    public string IndexColumns { get; set; }
    [JsonProperty(Order = 10)]
    public string IncludeColumns { get; set; }
    [JsonProperty(Order = 11)]
    public string FilterExpression { get; set; }
    [JsonProperty(Order = 12)]
    public bool UpdateFillFactor { get; set; }
}
