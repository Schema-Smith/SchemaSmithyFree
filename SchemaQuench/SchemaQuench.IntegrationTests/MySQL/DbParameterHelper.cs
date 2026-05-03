// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Helper to add parameters to IDbCommand without platform-specific methods.
/// Replaces MySqlCommand.AddParameterWithValue() usage.
/// </summary>
internal static class DbParameterHelper
{
    public static IDbDataParameter AddParameterWithValue(this IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
        return parameter;
    }
}
