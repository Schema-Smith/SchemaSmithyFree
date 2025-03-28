using Newtonsoft.Json;

namespace Schema.Domain;

public class CheckConstraint
{
    [JsonProperty(Order = 1)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    public string Expression { get; set; }
}