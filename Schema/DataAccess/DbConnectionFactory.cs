// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;

namespace Schema.DataAccess;

public static class DbConnectionFactory
{
    public static IDbConnectionFactory ForPlatform(Platform platform) => platform switch
    {
        Platform.SqlServer => SqlServerConnectionFactory.GetFromFactory(),
        Platform.PostgreSQL => PostgreSqlConnectionFactory.GetFromFactory(),
        Platform.MySQL => MySqlConnectionFactory.GetFromFactory(),
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}")
    };
}
