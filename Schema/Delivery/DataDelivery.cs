// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.Delivery;

/// <summary>
/// Data delivery configuration for a table. Defines how table data is merged
/// during deployment.
/// </summary>
public class DataDelivery
{
    [SchemaProperty]
    [JsonProperty(Order = 1)]
    public string ContentFile { get; set; }

    [SchemaProperty(Pattern = "Insert|Insert/Update|Insert/Update/Delete")]
    [JsonProperty(Order = 2)]
    public string MergeType { get; set; }

    [SchemaProperty]
    [JsonProperty(Order = 3)]
    public string MatchColumns { get; set; }

    [SchemaProperty]
    [JsonProperty(Order = 4)]
    public string MergeFilter { get; set; }

    [SchemaProperty]
    [JsonProperty(Order = 5)]
    public bool MergeDisableTriggers { get; set; }

    /// <summary>PostgreSQL-specific: disable rules during merge.</summary>
    [SchemaProperty]
    [JsonProperty(Order = 6)]
    public bool MergeDisableRules { get; set; }

    /// <summary>PostgreSQL-specific: update descendant tables during merge.</summary>
    [SchemaProperty]
    [JsonProperty(Order = 7)]
    public bool MergeUpdateDescendents { get; set; }

    [SchemaProperty]
    [JsonProperty(Order = 8)]
    public string ShouldApplyExpression { get; set; }

    // Labels a conditional variant: the intent behind its ShouldApplyExpression, echoed in
    // quench log messages when the delivery applies, and the round-trip identity key on re-extraction.
    [SchemaProperty(MaxLength = 128, Description = "Optional label for a conditional data-delivery variant — names the intent behind its ShouldApplyExpression, appears in deployment logging when the delivery applies, and is the identity key that preserves N deliveries across re-extraction.")]
    [JsonProperty(Order = 9)]
    public string VariantName { get; set; }
}
