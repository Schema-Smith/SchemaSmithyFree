// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;

namespace Schema.Utility;

/// <summary>The action extraction takes for an item that has an authored variant set on disk/in the model.</summary>
public enum VariantAction
{
    /// <summary>Exactly one variant's gate is active on the source — refresh that variant with the extracted shape.</summary>
    RefreshActive,

    /// <summary>No single variant is active — emit the extracted shape as an ungated entry (surfaced by validation).</summary>
    EmitUngated
}

/// <summary><see cref="VariantAction"/> plus the index of the active variant (or -1 when emitting ungated).</summary>
public readonly record struct VariantDecision(VariantAction Action, int ActiveIndex);

/// <summary>
/// Decides how an extracted item is reconciled against an authored variant set, uniformly for the
/// component layer (<see cref="ImportTableHelper"/>) and the table-file layer (SchemaTongs). The
/// extracted shape is never discarded: exactly one active gate → refresh that variant; zero or more
/// than one active → emit the shape ungated so validation (SS-DUP-001) forces the author to reconcile.
/// </summary>
public static class VariantAttribution
{
    /// <param name="isActive">
    /// Evaluates a gate expression against the extraction source. May throw — callers let it
    /// propagate so a broken gate fails the extraction (fail-closed) rather than mis-attributing.
    /// </param>
    public static VariantDecision Decide<T>(IReadOnlyList<T> variants, Func<T, string> gateOf, Func<string, bool> isActive)
    {
        var activeIndex = -1;
        var activeCount = 0;
        for (var i = 0; i < variants.Count; i++)
        {
            if (!isActive(gateOf(variants[i]))) continue;
            activeCount++;
            activeIndex = i;
        }

        return activeCount == 1
            ? new VariantDecision(VariantAction.RefreshActive, activeIndex)
            : new VariantDecision(VariantAction.EmitUngated, -1);
    }
}
