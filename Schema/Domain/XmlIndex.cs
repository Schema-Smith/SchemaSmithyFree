using Newtonsoft.Json;

namespace Schema.Domain;

public class XmlIndex
{
    [JsonProperty(Order = 1)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    public bool IsPrimary { get; set; }
    [JsonProperty(Order = 3)]
    public string Column { get; set; }
    [JsonProperty(Order = 4)]
    public string PrimaryIndex { get; set; } // only when not IsPrimary 
    [JsonProperty(Order = 5)]
    public string SecondaryIndexType { get; set; } // only when not IsPrimary 
}