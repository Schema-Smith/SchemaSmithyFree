// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.Isolators;
using SchemaSmith.Pro;

namespace Schema.DataAccess;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    public IDbConnection GetDbConnection(string connectionString)
    {
        var dataSource = new MySqlConnector.MySqlDataSource(connectionString);
        return dataSource.CreateConnection();
    }

    public static IDbConnectionFactory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDbConnectionFactory, MySqlConnectionFactory>();
    }
}
