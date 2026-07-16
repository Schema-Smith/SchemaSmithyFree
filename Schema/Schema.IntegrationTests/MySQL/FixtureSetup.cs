// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests.Shared;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.IntegrationTests.MySQL;

[Category("MySQL")]
[SetUpFixture]
public class FixtureSetup
{
    private static string _integrationMainDb = "";
    private static string _integrationSecondaryDb = "";
    private static string _connectionString = "";
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

    public static string SecondaryDb
    {
        get
        {
            EnsureInitialized();
            return _integrationSecondaryDb;
        }
    }

    public static string ConnectionString
    {
        get
        {
            EnsureInitialized();
            return _connectionString;
        }
    }

    /// <summary>
    /// Ensures the test databases are initialized. Called automatically when accessing properties.
    /// Can also be called explicitly from other test projects' SetUpFixture.
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

        var server = config["MySQL:Server"] ?? "127.0.0.1";
        var port = config["MySQL:Port"] ?? "3306";
        var user = config["MySQL:User"] ?? "TestUser";
        var password = config["MySQL:Password"] ?? "aCa2d805-41E5@40c4!98e7#92F93zzxo176";

        var mysqlProps = Schema.DataAccess.ConnectionString.ReadProperties(config, "MySQL:ConnectionProperties");
        var extraProps = string.Join("", mysqlProps.Select(p => $"{p.Key}={p.Value};"));
        // Non-pooled: the suite creates a unique database per test, and retained idle pool connections
        // across those per-DB pools otherwise pile up past the MySQL server's max_connections ceiling.
        _connectionString = $"Server={server};Port={port};User={user};Password={password};AllowUserVariables=true;Pooling=false;{extraProps}";

        // Use the literal base token, NOT config["ScriptTokens:*"]: this method overwrites that key
        // below with the full generated name, and ConfigHelper returns a shared config instance reused
        // by the sibling MariaDb fixture. Reading the key back would compound base-on-base
        // (`TestMain_MariaTest_..._Test_...`) past MySQL's 64-char identifier limit when both the MySQL
        // and MariaDb categories run in one process (error 1059). The `_Test_`/`_MariaTest_` prefix in
        // GenerateUniqueDBName already keeps the two engines' database names distinct.
        _integrationSecondaryDb = GenerateUniqueDBName("TestSecondary");
        _integrationMainDb = GenerateUniqueDBName("TestMain");

        // Map MySQL config to Target:* keys used by tools (mutate existing config, don't replace —
        // replacing would lose SqlServer:* and PostgreSQL:* keys needed by other test assemblies).
        // Publish under the shared lock: the four engine fixtures write these same global Target:* /
        // ScriptTokens:* keys, and in the full unfiltered run this OneTimeSetUp can run on the parallel
        // worker lane while a SqlServer/PostgreSQL schema-template test holds the lock mid-quench.
        // Guarding the write means it lands strictly before or after that locked test body, never during.
        lock (FactoryContainer.SharedLockObject)
        {
            config["Target:Server"] = server;
            config["Target:Port"] = port;
            config["Target:User"] = user;
            config["Target:Password"] = password;
            foreach (var prop in mysqlProps)
                config[$"Target:ConnectionProperties:{prop.Key}"] = prop.Value;
            // Product-side connections the quench opens per target DB are non-pooled too (same ceiling reason).
            config["Target:ConnectionProperties:Pooling"] = "false";
            config["ScriptTokens:MainDB"] = _integrationMainDb;
            config["ScriptTokens:SecondaryDB"] = _integrationSecondaryDb;
        }

        CreateTestDatabases();

        // Register cleanup on process exit for when tests are run from other assemblies
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
    }

    private static bool _cleanedUp;

    private static void Cleanup()
    {
        if (_cleanedUp || !_initialized) return;
        _cleanedUp = true;
        new FixtureSetup().DropTestDatabases();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
    }

    private void CreateTestDatabases()
    {
        // TestUser has been granted CREATE/DROP and SYSTEM_USER privileges via docker init script
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Create test databases
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_integrationSecondaryDb}`;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_integrationMainDb}`;";
        cmd.ExecuteNonQuery();

        // Deploy ForgeKindler to main database
        conn.ChangeDatabase(_integrationMainDb);
        ForgeKindler.KindleTheForge(cmd, Platform.MySQL);

        // Create test tables that mirror Sakila structure for integration tests
        MySqlFamilyTestSchema.Create(cmd, _integrationMainDb);

        // Deploy ForgeKindler to secondary database
        conn.ChangeDatabase(_integrationSecondaryDb);
        ForgeKindler.KindleTheForge(cmd, Platform.MySQL);

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabases()
    {
        try
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            DropOneDatabase(cmd, _integrationSecondaryDb);
            DropOneDatabase(cmd, _integrationMainDb);

            conn.Close();
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private static void DropOneDatabase(IDbCommand cmd, string dbName)
    {
        if (string.IsNullOrEmpty(dbName)) return;
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{dbName}`;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets a connection string for the main test database.
    /// </summary>
    public static string GetMainDbConnectionString()
    {
        EnsureInitialized();
        return _connectionString + $"Database={_integrationMainDb};";
    }

    /// <summary>
    /// Gets a connection string for the secondary test database.
    /// </summary>
    public static string GetSecondaryDbConnectionString()
    {
        EnsureInitialized();
        return _connectionString + $"Database={_integrationSecondaryDb};";
    }
}
