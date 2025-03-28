using Newtonsoft.Json;

namespace Schema.Domain;

public class Statistic
{
    [JsonProperty(Order = 1)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    public string Columns { get; set; }
    [JsonProperty(Order = 3)]
    public byte SampleSize { get; set; }
    [JsonProperty(Order = 4)]
    public string FilterExpression { get; set; }
}