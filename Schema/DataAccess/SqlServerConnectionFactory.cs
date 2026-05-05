// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

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
