// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Delivery;

namespace Schema.Domain.SqlServer
{
    public class SqlServerForeignKey : ForeignKey, IDeliverableForeignKey
    {
        [JsonProperty(Order = 100)]
        public string RelatedTableSchema { get; set; } = "dbo";
    }
}
