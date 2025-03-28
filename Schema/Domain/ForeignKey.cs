using Newtonsoft.Json;

namespace Schema.Domain;

public class ForeignKey
{
    [JsonProperty(Order = 1)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    public string Columns { get; set; }
    [JsonProperty(Order = 3)]
    public string RelatedTableSchema { get; set; } = "dbo";
    [JsonProperty(Order = 4)]
    public string RelatedTable { get; set; }
    [JsonProperty(Order = 5)]
    public string RelatedColumns { get; set; }
    [JsonProperty(Order = 6)]
    public bool CascadeOnDelete { get; set; }
    [JsonProperty(Order = 7)]
    public bool CascadeOnUpdate { get; set; }
}