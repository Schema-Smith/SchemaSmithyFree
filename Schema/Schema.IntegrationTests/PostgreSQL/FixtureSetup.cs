// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[SetUpFixture]
public class FixtureSetup
{
    private static string _integrationMainDb = "";
    private static string _postgresConnectionString = "";
    private static string _server = "";
    private static string _port = "";
    private static string _user = "";
    private static string _password = "";
    private static Dictionary<string, string> _connectionProperties = new();
    private static bool _initialized;
    private static readonly object _lock = new();

    public static string MainDb
    {
        get
        {
            EnsureInitialized();
            return _integrationMainDb;
        }
    }

    /// <summary>
    /// Ensures the test database is initialized. Called automatically when accessing properties.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            new FixtureSetup().Initialize();
            _initialized = true;
        }
    }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        EnsureInitialized();
    }

    private void Initialize()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);

        _server = config["PostgreSQL:Server"] ?? "127.0.0.1";
        _port = config["PostgreSQL:Port"];
        _user = config["PostgreSQL:User"];
        _password = config["PostgreSQL:Password"];
        _connectionProperties = ConnectionString.ReadProperties(config, "PostgreSQL:ConnectionProperties");

        _postgresConnectionString = ConnectionString.Build(Platform.PostgreSQL, _server, "postgres", _user, _password, _port, _connectionProperties);
        _integrationMainDb = GenerateUniqueDBName("schemainttest");

        CreateTestDatabase();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabase();
    }

    private void CreateTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_postgresConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"CREATE DATABASE ""{_integrationMainDb}"";";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationMainDb);
        ForgeKindler.KindleTheForge(cmd, Platform.PostgreSQL);

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}".ToLowerInvariant();
    }

    private void DropTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_postgresConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '{_integrationMainDb}' AND pid <> pg_backend_pid();";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"DROP DATABASE IF EXISTS ""{_integrationMainDb}"";";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    /// <summary>
    /// Gets a connection string targeting the main test database.
    /// </summary>
    public static string GetMainDbConnectionString()
    {
        EnsureInitialized();
        return ConnectionString.Build(Platform.PostgreSQL, _server, _integrationMainDb, _user, _password, _port, _connectionProperties);
    }
}
