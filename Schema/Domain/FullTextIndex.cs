using Newtonsoft.Json;

namespace Schema.Domain;

public class FullTextIndex
{
    [JsonProperty(Order = 1)]
    public string FullTextCatalog { get; set; }
    [JsonProperty(Order = 2)]
    public string KeyIndex { get; set; }
    [JsonProperty(Order = 3)]
    public string ChangeTracking { get; set; }
    [JsonProperty(Order = 4)]
    public string StopList { get; set; }
    [JsonProperty(Order = 5)]
    public string Columns { get; set; }
}
