// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
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
        command.Parameters?.Clear(); // defensive: match the codebase discipline of not reusing a dirtied command
        command.CommandText = NormalizeToSelect(expression);
        return ProductQuench.ScalarToBool(command.ExecuteScalar());
    }

    /// <summary>
    /// A folder gate runs as a statement, so it needs a full <c>SELECT</c>. Accept a bare boolean
    /// predicate too (the form component gates use) by wrapping it; an expression already in
    /// <c>SELECT</c> form runs as-is. The wrapper shape is identical across SQL Server, PostgreSQL,
    /// and MySQL.
    /// </summary>
    internal static string NormalizeToSelect(string expression)
    {
        var trimmed = expression.TrimStart();
        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && (trimmed.Length == 6 || char.IsWhiteSpace(trimmed[6])))
            return expression;
        return $"SELECT CASE WHEN ({expression}) THEN 1 ELSE 0 END";
    }
}
