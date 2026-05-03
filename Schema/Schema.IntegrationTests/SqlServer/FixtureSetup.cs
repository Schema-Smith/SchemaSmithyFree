// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.SqlServer;

[Category("SqlServer")]
[SetUpFixture]
public class FixtureSetup
{
    private static string _integrationMainDb = "";
    private static string _masterConnectionString = "";
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

        _server = config["SqlServer:Server"] ?? "127.0.0.1";
        _port = config["SqlServer:Port"];
        _user = config["SqlServer:User"];
        _password = config["SqlServer:Password"];
        _connectionProperties = ConnectionString.ReadProperties(config, "SqlServer:ConnectionProperties");

        _masterConnectionString = ConnectionString.Build(Platform.SqlServer, _server, "master", _user, _password, _port, _connectionProperties);
        _integrationMainDb = GenerateUniqueDBName("SchemaIntTest");

        CreateTestDatabase();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabase();
    }

    private void CreateTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{_integrationMainDb}];";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationMainDb);
        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer);

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @$"
IF DB_ID('{_integrationMainDb}') IS NOT NULL
  ALTER DATABASE [{_integrationMainDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{_integrationMainDb}];
";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    /// <summary>
    /// Gets a connection string targeting the main test database.
    /// </summary>
    public static string GetMainDbConnectionString()
    {
        EnsureInitialized();
        return ConnectionString.Build(Platform.SqlServer, _server, _integrationMainDb, _user, _password, _port, _connectionProperties);
    }
}
