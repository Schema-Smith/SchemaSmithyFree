// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SchemaQuench;

/// <summary>The disposition a WhatIf-mode script would have had, mirroring the progress-log wording.</summary>
public enum WhatIfCategory { Apply, Skip, Deliver }

public sealed record WhatIfRun(WhatIfCategory Category, string Scope, string Script);

/// <summary>
/// Thread-safe collector of WhatIf-mode would-apply/skip/deliver entries (#243 Deployment Summary
/// Report, E4c), mirroring the existing <c>SafeProgressLog</c> calls in <see cref="DatabaseQuench"/>'s
/// <c>WhatIfLog*</c> methods. Purely passive capture — a later slice (E4d) assembles this into the
/// report's <c>whatIf</c> section; nothing reads it yet. Safe under the fan-out's concurrent worker pool.
/// </summary>
public sealed class WhatIfCapture
{
    private readonly ConcurrentBag<WhatIfRun> _entries = new();

    /// <param name="category">Whether the script would have been applied, skipped, or delivered.</param>
    /// <param name="scope">Per-target LogPrefix, e.g. <c>[primary].[TenantA] [Schema: sales]</c>.</param>
    /// <param name="script">The script's full LogPath.</param>
    public void Record(WhatIfCategory category, string scope, string script) =>
        _entries.Add(new WhatIfRun(category, scope, script));

    /// <summary>Stable snapshot of every entry recorded so far.</summary>
    public IReadOnlyList<WhatIfRun> Snapshot() => _entries.ToArray();
}
