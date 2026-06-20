// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;

namespace SchemaQuench;

/// <summary>
/// Client-side evaluation of a folder's <c>ShouldApplyExpression</c> against a target. A blank
/// expression always applies; a non-blank expression is run as a scalar query and interpreted as a
/// boolean. Evaluation errors propagate so callers fail closed — a broken gate must surface as a
/// deployment error, never silently skip the folder.
/// </summary>
internal static class FolderGate
{
    internal static bool ShouldApply(IDbCommand command, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        command.CommandText = expression;
        return ProductQuench.ScalarToBool(command.ExecuteScalar());
    }
}
