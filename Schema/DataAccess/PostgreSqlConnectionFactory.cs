// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Concurrent;
using System.Data;
using Npgsql;
using Schema.Isolators;

namespace Schema.DataAccess;

public class PostgreSqlConnectionFactory : IDbConnectionFactory
{
    // An NpgsqlDataSource owns a connection pool and is meant to be long-lived and shared. Creating
    // (and never disposing) one per GetDbConnection call leaked a pool per call — invisible to
    // NpgsqlConnection.ClearAllPools() — exhausting the server in long-running / fleet processes that
    // deploy to many databases in one process. Cache one data source per connection string so pooled
    // connections are reused rather than accumulated; Lazy<> guarantees a single Create per key.
    private static readonly ConcurrentDictionary<string, Lazy<NpgsqlDataSource>> DataSources = new();

    internal static int CachedDataSourceCount => DataSources.Count;

    public IDbConnection GetDbConnection(string connectionString)
    {
        var dataSource = DataSources.GetOrAdd(connectionString,
            cs => new Lazy<NpgsqlDataSource>(() => NpgsqlDataSource.Create(cs))).Value;
        return dataSource.CreateConnection();
    }

    public static IDbConnectionFactory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDbConnectionFactory, PostgreSqlConnectionFactory>();
    }
}
