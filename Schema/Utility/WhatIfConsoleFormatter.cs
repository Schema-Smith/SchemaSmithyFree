// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;

namespace Schema.Utility;

/// <summary>Console verbosity for WhatIf output (the <c>--WhatIfDetail</c> switch).</summary>
public enum WhatIfDetail
{
    /// <summary>Collapse a section's per-script lines into one per-category count line.</summary>
    Concise,

    /// <summary>Today's behavior — one line per script (default).</summary>
    Normal,

    /// <summary>One line per script (reserved for future extra detail).</summary>
    Verbose
}

/// <summary>One WhatIf console entry: a short <paramref name="Category"/> for concise grouping
/// (e.g. <c>apply</c>/<c>skip</c>/<c>deliver</c>), the full <paramref name="Label"/> for the
/// per-line form (e.g. <c>APPLY</c>, <c>SKIP (previously quenched)</c>), and the <paramref name="Display"/>
/// path/name shown after it.</summary>
public readonly record struct WhatIfConsoleEntry(string Category, string Label, string Display);

/// <summary>
/// Renders a WhatIf section's console lines at the requested verbosity, without touching the
/// WhatIf summary file (callers still record every entry to <c>WhatIfCapture</c> independently).
/// </summary>
public static class WhatIfConsoleFormatter
{
    /// <summary>Parses the <c>--WhatIfDetail</c> switch value; unknown/blank -> <see cref="WhatIfDetail.Normal"/>.</summary>
    public static WhatIfDetail ParseDetail(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "concise" => WhatIfDetail.Concise,
        "verbose" => WhatIfDetail.Verbose,
        _ => WhatIfDetail.Normal
    };

    /// <summary>
    /// In <see cref="WhatIfDetail.Concise"/> mode, returns a single "N would apply, M would skip"
    /// line (empty when there are no entries). Otherwise returns one "    Would &lt;Label&gt;: &lt;Display&gt;"
    /// line per entry — byte-identical to the pre-switch output.
    /// </summary>
    public static IEnumerable<string> Render(IReadOnlyList<WhatIfConsoleEntry> entries, WhatIfDetail detail)
    {
        if (detail == WhatIfDetail.Concise)
        {
            if (entries.Count == 0) yield break;
            var parts = entries
                .GroupBy(e => e.Category)
                .Select(g => $"{g.Count()} would {g.Key}");
            yield return "    " + string.Join(", ", parts);
            yield break;
        }

        foreach (var e in entries)
            yield return $"    Would {e.Label}: {e.Display}";
    }
}
