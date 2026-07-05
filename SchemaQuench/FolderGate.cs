// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;

namespace SchemaQuench;

/// <summary>
/// Client-side evaluation of a folder's <c>ShouldApplyExpression</c> against a target. Delegates
/// to <see cref="Schema.Delivery.GateEvaluator"/> — the shared evaluator also used by data-delivery
/// gating — so both call sites share one implementation. Kept as a thin internal wrapper so
/// existing SchemaQuench callers and tests are untouched.
/// </summary>
internal static class FolderGate
{
    internal static bool ShouldApply(IDbCommand command, string expression)
        => Schema.Delivery.GateEvaluator.ShouldApply(command, expression);

    internal static string NormalizeToSelect(string expression)
        => Schema.Delivery.GateEvaluator.NormalizeToSelect(expression);
}
