using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Schema.Utility;

namespace Schema.Domain;

public class Table
{
    [JsonProperty(Order = 1)]
    public string Schema { get; set; } = "dbo";
    [JsonProperty(Order = 2)]
    public string Name { get; set; }
    [JsonProperty(Order = 3)]
    public string CompressionType { get; set; } = "NONE";
    [JsonProperty(Order = 4)]
    public bool IsTemporal { get; set; }
    [JsonProperty(Order = 5)]
    public List<Column> Columns { get; set; } = [];
    [JsonProperty(Order = 6)]
    public List<Index> Indexes { get; set; } = [];
    [JsonProperty(Order = 7)]
    public List<XmlIndex> XmlIndexes { get; set; } = [];
    [JsonProperty(Order = 8)]
    public List<ForeignKey> ForeignKeys { get; set; } = [];
    [JsonProperty(Order = 9)]
    public List<CheckConstraint> CheckConstraints { get; set; } = [];
    [JsonProperty(Order = 10)]
    public List<Statistic> Statistics { get; set; } = [];
    [JsonProperty(Order = 11)]
    public FullTextIndex FullTextIndex { get; set; }
    [JsonProperty(Order = 12)]
    public string OldName { get; set; } = "";

    public static Table Load(string filePath)
    {
        try
        {
            return JsonHelper.ProductLoad<Table>(filePath);
        }
        catch (Exception e)
        {
            throw new Exception($"Error loading table from {LongPathSupport.StripLongPathPrefix(filePath)}\r\n{e.Message}", e);
        }
    }
}