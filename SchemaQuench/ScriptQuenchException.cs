// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;

namespace SchemaQuench;

/// <summary>
/// Thrown when one or more user scripts in a slot fail to quench (#338 refinement). Carries each
/// failed script's specific error and resolved-SQL artifact path so the failure roll-up
/// (<c>SchemaQuench - Failures.log</c>) can surface <c>Unable to quench '&lt;path&gt;': &lt;error&gt;</c>
/// on its <c>Error:</c> line and the <c>Failed &lt;script&gt;</c> artifact on <c>Debug SQL:</c> —
/// parity with mechanical failures, instead of the generic <c>Unable to quench all scripts</c> /
/// <c>n/a</c>. The base message is preserved so existing callers that key on it still match.
/// </summary>
public sealed class ScriptQuenchException : Exception
{
    public IReadOnlyList<ScriptFailure> Failures { get; }

    public ScriptQuenchException(IReadOnlyList<ScriptFailure> failures)
        : base("Unable to quench all scripts")
        => Failures = failures;
}

public sealed record ScriptFailure(string LogPath, string Error, string ArtifactPath);
