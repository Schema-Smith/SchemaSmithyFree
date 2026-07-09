// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace SchemaQuench.Reporting;

/// <summary>
/// Serializes a <see cref="DeploymentSummary"/> to the frozen v1 JSON contract (#243). Pure
/// projection — no scrubbing, no transformation. Any redaction of script text is E4's job, done
/// on the model BEFORE it reaches <see cref="Serialize"/> (see <c>LogScrubber</c>); this type
/// must never reference it.
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
