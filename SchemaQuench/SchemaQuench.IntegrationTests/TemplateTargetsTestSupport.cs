// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench.IntegrationTests;

/// <summary>
/// Shared integration-test helpers for <c>Target.TemplateTargets</c> / <c>Target.*</c> filter
/// cleanup (#257 batch A I8). The three engine-specific TemplateTargets fixtures previously each
/// carried near-identical local copies of <c>ClearTargetFilters</c> + <c>ClearTemplateTargets</c>,
/// both of which nulled leaf values but left the underlying key paths in the configuration
/// provider's data dictionary. That residue surfaced as phantom entries in <see cref="IConfiguration.GetChildren"/>
/// on subsequent tests in the same fixture, forcing production code to compensate with a
/// "phantom-entry skip" in <c>ReadTemplateTargets</c>. Lifting + correcting the cleanup here lets
/// the production-side skip narrow safely to "key genuinely absent" (I6) without silently
/// swallowing user-authored empty values.
/// <para>
/// Implementation note: <see cref="ConfigurationProvider.Data"/> is protected, so reflective access
/// is used to remove keys outright (not just null them). This works against any provider that
/// inherits from <see cref="ConfigurationProvider"/> — including <c>JsonConfigurationProvider</c>
/// (used by the integration tests' <c>test.settings.json</c>) and <c>MemoryConfigurationProvider</c>
/// (used in unit-test paths). For providers that don't expose a writable Data dictionary, the
/// helpers fall back to nulling values (today's behavior).
/// </para>
/// </summary>
internal static class TemplateTargetsTestSupport
{
    private const string TemplateTargetsRoot = "Target:TemplateTargets";

    private static readonly string[] FilterDimensions = { "Templates", "Databases", "Schemas" };

    /// <summary>
    /// Removes every <c>Target:Templates:*</c> / <c>Target:Databases:*</c> /
    /// <c>Target:Schemas:*</c> filter entry that a previous test in this fixture may have left in
    /// place. Use BEFORE a test sets its own filter values to guarantee a clean baseline.
    /// </summary>
    public static void ClearTargetFilters(IConfigurationRoot config)
    {
        foreach (var dim in FilterDimensions)
        {
            foreach (var child in config.GetSection($"Target:{dim}").GetChildren().ToList())
                RemoveOrNull(config, $"Target:{dim}:{child.Key}");
        }
    }

    /// <summary>
    /// Removes every <c>Target:TemplateTargets:*</c> entry (axes + CreateIfMissing scalar). Use
    /// BEFORE a test plants its own override and AFTER the test in a finally block so cross-test
    /// residue can't influence subsequent runs.
    /// </summary>
    public static void ClearTemplateTargets(IConfigurationRoot config)
    {
        foreach (var templateEntry in config.GetSection(TemplateTargetsRoot).GetChildren().ToList())
        {
            foreach (var axisEntry in templateEntry.GetChildren().ToList())
            {
                if (axisEntry.Key is "Databases" or "Schemas")
                {
                    foreach (var item in axisEntry.GetChildren().ToList())
                        RemoveOrNull(config, $"{TemplateTargetsRoot}:{templateEntry.Key}:{axisEntry.Key}:{item.Key}");
                    // Defensive: also remove the axis-section key itself so the parent template
                    // entry can be detected as truly empty by ReadTemplateTargets' narrowed skip.
                    RemoveOrNull(config, $"{TemplateTargetsRoot}:{templateEntry.Key}:{axisEntry.Key}");
                }
                else
                {
                    RemoveOrNull(config, $"{TemplateTargetsRoot}:{templateEntry.Key}:{axisEntry.Key}");
                }
            }
            // Finally, remove any residual marker on the template entry itself (rare but cheap).
            RemoveOrNull(config, $"{TemplateTargetsRoot}:{templateEntry.Key}");
        }
    }

    /// <summary>
    /// Removes <paramref name="key"/> from every provider that exposes a writable
    /// <c>Data</c> dictionary (i.e., any provider derived from
    /// <c>ConfigurationProvider</c>). Falls back to nulling the value when no writable dictionary
    /// is reachable so the helper degrades gracefully against custom provider implementations.
    /// </summary>
    private static void RemoveOrNull(IConfigurationRoot config, string key)
    {
        var removed = false;
        foreach (var provider in config.Providers)
        {
            // ConfigurationProvider.Data is protected — reflect to read + mutate.
            var dataProp = provider.GetType()
                .GetProperty("Data", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (dataProp?.GetValue(provider) is System.Collections.Generic.IDictionary<string, string> data
                && data.ContainsKey(key))
            {
                data.Remove(key);
                removed = true;
            }
        }
        if (!removed)
        {
            // Provider chain didn't expose a writable Data dictionary — fall back to nulling.
            config[key] = null;
        }
    }
}
