// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace SchemaQuench.Reporting;

/// <summary>
/// Serializes a <see cref="DeploymentSummary"/> to the frozen v1 JSON contract (#243). Pure
/// projection — no scrubbing, no transformation. Phase 1 emits failure context
/// (<c>failures[].error</c> / <c>contextTail</c>) unscrubbed, exactly matching the existing
/// <c>SchemaQuench - Failures.log</c> (same content, same backup directory — no new exposure);
/// WhatIf and migration entries carry script PATHS, not SQL bodies. If value-level redaction is
/// ever needed it belongs on the model before it reaches <see cref="Serialize"/>, not here.
/// </summary>
public static class DeploymentSummaryJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new StringEnumConverter() },
        Formatting = Formatting.Indented
    };

    public static string Serialize(DeploymentSummary summary) =>
        JsonConvert.SerializeObject(summary, Settings);
}
