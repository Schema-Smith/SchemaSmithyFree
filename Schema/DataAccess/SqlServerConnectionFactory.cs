// Copyright (c) SchemaSmith, LLC. All rights reserved.

using System.Data;
using Microsoft.Data.SqlClient;
using Schema.Isolators;

namespace Schema.DataAccess;

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    public IDbConnection GetDbConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }

    public static IDbConnectionFactory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDbConnectionFactory, SqlServerConnectionFactory>();
    }
}
