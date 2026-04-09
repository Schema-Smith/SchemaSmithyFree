// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;

namespace Schema.DataAccess;

public static class DbConnectionFactory
{
    public static IDbConnectionFactory ForPlatform(Platform platform) => platform switch
    {
        Platform.SqlServer => new SqlServerConnectionFactory(),
        Platform.PostgreSQL => new PostgreSqlConnectionFactory(),
        Platform.MySQL => new MySqlConnectionFactory(),
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}")
    };
}
