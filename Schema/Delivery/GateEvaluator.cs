// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;

namespace Schema.Delivery;

/// <summary>
/// Client-side evaluation of a <c>ShouldApplyExpression</c> against a target. A blank expression
/// always applies; a non-blank expression runs as a scalar query and is coerced to a boolean.
/// Evaluation errors propagate so callers fail closed — a broken gate surfaces as a deployment
/// error, never a silent skip. Shared by folder gating (SchemaQuench.FolderGate) and data-delivery gating.
/// </summary>
public static class GateEvaluator
{
    public static bool ShouldApply(IDbCommand command, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        command.Parameters?.Clear();
        command.CommandText = NormalizeToSelect(expression);
        return ScalarToBool(command.ExecuteScalar());
    }

    public static string NormalizeToSelect(string expression)
    {
        var trimmed = expression.TrimStart();
        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && (trimmed.Length == 6 || char.IsWhiteSpace(trimmed[6])))
            return expression;
        return $"SELECT CASE WHEN ({expression}) THEN 1 ELSE 0 END";
    }

    public static bool ScalarToBool(object result) => result switch
    {
        null or DBNull => false,
        bool b => b,
        _ => Convert.ToInt64(result) != 0
    };
}
