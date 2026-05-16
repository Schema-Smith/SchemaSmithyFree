// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Delivery;

namespace Schema.Domain.SqlServer
{
    public class SqlServerForeignKey : ForeignKey, IDeliverableForeignKey
    {
        // Default resolution moved to SchemaDefaultResolver. Regular templates: defaults to "dbo".
        // Schema templates: defaults to "{{SchemaName}}"; explicit literals are preserved as
        // cross-schema references (e.g. tenant FK referencing dbo.Countries).
        [JsonProperty(Order = 100, NullValueHandling = NullValueHandling.Ignore)]
        public string RelatedTableSchema { get; set; }
    }
}
