// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Schema.Domain;

namespace Schema.DataAccess;

public static class ConnectionString
{
    public static string Build(Platform platform, string serverName, string dbName, string user, string password,
        string port = null, Dictionary<string, string> connectionProperties = null)
    {
        return platform switch
        {
            Platform.SqlServer => BuildSqlServer(serverName, dbName, user, password, port, connectionProperties),
            Platform.PostgreSQL => BuildPostgreSql(serverName, dbName, user, password, port, connectionProperties),
            Platform.MySQL => BuildMySql(serverName, dbName, user, password, port, connectionProperties),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}")
        };
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

    private static string BuildSqlServer(string serverName, string dbName, string user, string password, string port,
        Dictionary<string, string> connectionProperties)
    {
        var security = !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password)
            ? $"User ID={user};Password={password}"
            : "Integrated Security=True";

        // SQL Server uses comma-separated port: "server,port"
        var server = !string.IsNullOrWhiteSpace(port) ? $"{serverName},{port}" : serverName;

        var sb = new StringBuilder($"data source={server};Initial Catalog={dbName};{security};");
        AppendConnectionProperties(sb, connectionProperties);
        return sb.ToString();
    }

    private static string BuildPostgreSql(string serverName, string dbName, string user, string password, string port,
        Dictionary<string, string> connectionProperties)
    {
        var portSegment = !string.IsNullOrWhiteSpace(port) ? $"Port={port};" : "";
        var sb = new StringBuilder($"Host={serverName};{portSegment}Database={dbName};Username={user};Password={password};");
        AppendConnectionProperties(sb, connectionProperties);
        return sb.ToString();
    }

    private static string BuildMySql(string serverName, string dbName, string user, string password, string port,
        Dictionary<string, string> connectionProperties)
    {
        var portSegment = !string.IsNullOrWhiteSpace(port) ? $"Port={port};" : "";
        // AllowUserVariables=true is structural — required for PREPARE/EXECUTE dynamic SQL in stored procedures.
        // It must always be present and cannot be overridden via connectionProperties.
        var sb = new StringBuilder($"Server={serverName};{portSegment}Database={dbName};Uid={user};Pwd={password};AllowUserVariables=true;");
        AppendConnectionProperties(sb, connectionProperties, structuralKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AllowUserVariables" });
        return sb.ToString();
    }

    private static void AppendConnectionProperties(StringBuilder sb, Dictionary<string, string> connectionProperties,
        HashSet<string> structuralKeys = null)
    {
        if (connectionProperties == null || connectionProperties.Count == 0)
            return;

        foreach (var kvp in connectionProperties)
        {
            if (structuralKeys != null && structuralKeys.Contains(kvp.Key))
                continue;

            sb.Append($"{kvp.Key}={kvp.Value};");
        }
    }
}
