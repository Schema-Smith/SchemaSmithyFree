// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using SchemaSmith.Pro;

namespace Schema.Domain.PostgreSQL;

/// <summary>
/// PostgreSQL-specific data delivery configuration. Adds PG-only merge options
/// (rule disabling, descendant table update propagation).
/// </summary>
public class PostgreSqlDataDelivery : DataDelivery
{
    [JsonProperty(Order = 6)]
    public bool MergeDisableRules { get; set; }

    [JsonProperty(Order = 7)]
    public bool MergeUpdateDescendents { get; set; }
}
