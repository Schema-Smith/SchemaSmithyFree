// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;

namespace Schema.Domain;

public class ForeignKey
{
    [JsonProperty(Order = 1)]
    [SchemaProperty(Required = true)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    [SchemaProperty(Required = true)]
    public string Columns { get; set; }
    [JsonProperty(Order = 3)]
    public string RelatedTableSchema { get; set; } = "dbo";
    [JsonProperty(Order = 4)]
    [SchemaProperty(Required = true)]
    public string RelatedTable { get; set; }
    [JsonProperty(Order = 5)]
    [SchemaProperty(Required = true)]
    public string RelatedColumns { get; set; }
    [JsonProperty(Order = 6)]
    [SchemaProperty(Pattern = "NO ACTION|CASCADE|SET NULL|SET DEFAULT")]
    public string DeleteAction { get; set; }
    [JsonProperty(Order = 7)]
    [SchemaProperty(Pattern = "NO ACTION|CASCADE|SET NULL|SET DEFAULT")]
    public string UpdateAction { get; set; }
}
