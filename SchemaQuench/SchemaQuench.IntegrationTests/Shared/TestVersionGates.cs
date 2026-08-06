// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

/// <summary>
/// Version-gate helpers for test fixtures that do NOT inherit <see cref="BaseTableQuenchTests"/> (which carries
/// the instance-method equivalents). Used to Assert.Ignore feature-gap tests below a MySQL-family floor.
/// </summary>
internal static class TestVersionGates
{
    /// <summary>Whether the target enforces CHECK constraints + exposes INFORMATION_SCHEMA.CHECK_CONSTRAINTS.
    /// MySQL: 8.0.16 (major >= 8). MariaDB (>= 10.2 floor), SQL Server, PostgreSQL: always.</summary>
    public static bool SupportsCheckConstraints(Platform platform, string connectionString)
    {
        if (platform != Platform.MySQL) return true;
        using var conn = DbConnectionFactory.ForPlatform(platform).GetDbConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT VERSION()";
        var parts = (cmd.ExecuteScalar()?.ToString() ?? "").Split('.');
        return !(parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
               || major * 100 + minor >= 800;
    }
}
