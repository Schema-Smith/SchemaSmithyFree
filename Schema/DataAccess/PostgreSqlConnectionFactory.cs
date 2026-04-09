// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Npgsql;
using Schema.Isolators;
using SchemaSmith.Pro;

namespace Schema.DataAccess;

public class PostgreSqlConnectionFactory : IDbConnectionFactory
{
    public IDbConnection GetDbConnection(string connectionString)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        return dataSource.CreateConnection();
    }

    public static IDbConnectionFactory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDbConnectionFactory, PostgreSqlConnectionFactory>();
    }
}
