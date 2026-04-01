// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Schema.DataAccess;

public static class ConnectionString
{
    public static string Build(string serverName, string dbName, string user, string password,
        string port = null, Dictionary<string, string> connectionProperties = null)
    {
        var security = !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password)
            ? $"User ID={user};Password={password}"
            : "Integrated Security=True";

        var server = !string.IsNullOrWhiteSpace(port) ? $"{serverName},{port}" : serverName;

        var sb = new StringBuilder($"data source={server};Initial Catalog={dbName};{security};");
        AppendConnectionProperties(sb, connectionProperties);
        return sb.ToString();
    }

    /// <summary>
    /// Reads ConnectionProperties from an IConfigurationRoot section.
    /// Returns an empty dictionary if the section doesn't exist.
    /// </summary>
    public static Dictionary<string, string> ReadProperties(IConfigurationRoot config, string section)
    {
        return config.GetSection(section)
            .GetChildren()
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value!);
    }

    private static void AppendConnectionProperties(StringBuilder sb, Dictionary<string, string> connectionProperties)
    {
        if (connectionProperties == null || connectionProperties.Count == 0)
            return;

        foreach (var kvp in connectionProperties)
            sb.Append($"{kvp.Key}={kvp.Value};");
    }
}
