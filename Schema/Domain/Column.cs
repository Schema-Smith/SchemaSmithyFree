using Newtonsoft.Json;

namespace Schema.Domain;

public class Column
{
    [JsonProperty(Order = 1)]
    public string Name { get; set; }
    [JsonProperty(Order = 2)]
    public string DataType { get; set; }
    [JsonProperty(Order = 3)]
    public bool Nullable { get; set; }
    [JsonProperty(Order = 4)]
    public string Default { get; set; }
    [JsonProperty(Order = 5)]
    public string CheckExpression { get; set; }
    [JsonProperty(Order = 6)]
    public string ComputedExpression { get; set; }
    [JsonProperty(Order = 7)]
    public bool Persisted { get; set; }
    [JsonProperty(Order = 8)]
    public bool Sparse { get; set; }
    [JsonProperty(Order = 9)]
    public string Collation { get; set; }
    [JsonProperty(Order = 10)]
    public string DataMaskFunction { get; set; }
}