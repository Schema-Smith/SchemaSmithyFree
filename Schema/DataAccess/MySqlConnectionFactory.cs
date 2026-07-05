// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Concurrent;
using System.Data;
using MySqlConnector;
using Schema.Isolators;

namespace Schema.DataAccess;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    // Same rationale as PostgreSqlConnectionFactory: a MySqlDataSource owns a pool and must be shared,
    // not created per call (which leaks a pool per call). Cache one per connection string.
    private static readonly ConcurrentDictionary<string, Lazy<MySqlDataSource>> DataSources = new();

    internal static int CachedDataSourceCount => DataSources.Count;

    public IDbConnection GetDbConnection(string connectionString)
    {
        var dataSource = DataSources.GetOrAdd(connectionString,
            cs => new Lazy<MySqlDataSource>(() => new MySqlDataSource(cs))).Value;
        return dataSource.CreateConnection();
    }

    public static IDbConnectionFactory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDbConnectionFactory, MySqlConnectionFactory>();
    }
}
