// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Linq;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests.Shared;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb integration fixture — the MariaDb-engine twin of the MySQL FixtureSetup. Same setup
/// (unique per-run databases, kindled forge, shared Sakila-mirroring schema); differs only in the
/// config prefix (MariaDB:*), category, and Platform.MariaDb. Its own static state so it can run in
/// the same process as the MySQL fixture without collision.
/// </summary>
[Category("MariaDb")]
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

        var server = config["MariaDB:Server"] ?? "127.0.0.1";
        var port = config["MariaDB:Port"] ?? "3306";
        var user = config["MariaDB:User"] ?? "TestUser";
        var password = config["MariaDB:Password"] ?? "aCa2d805-41E5@40c4!98e7#92F93zzxo176";

        var mariaProps = Schema.DataAccess.ConnectionString.ReadProperties(config, "MariaDB:ConnectionProperties");
        var extraProps = string.Join("", mariaProps.Select(p => $"{p.Key}={p.Value};"));
        // Non-pooled: the suite creates a unique database per test, and retained idle pool connections
        // across those per-DB pools otherwise pile up past the server's max_connections ceiling.
        _connectionString = $"Server={server};Port={port};User={user};Password={password};AllowUserVariables=true;Pooling=false;{extraProps}";

        // Use the literal base token, NOT config["ScriptTokens:*"]: this method overwrites that key
        // below with the full generated name, and ConfigHelper returns a shared config instance reused
        // by the sibling MySQL fixture. Reading the key back would compound base-on-base
        // (`TestMain_Test_..._MariaTest_...`) past the 64-char identifier limit when both the MySQL and
        // MariaDb categories run in one process (error 1059). The `_MariaTest_`/`_Test_` prefix in
        // GenerateUniqueDBName already keeps the two engines' database names distinct.
        _integrationSecondaryDb = GenerateUniqueDBName("TestSecondary");
        _integrationMainDb = GenerateUniqueDBName("TestMain");

        // Map MariaDB config to Target:* keys used by tools (mutate existing config, don't replace —
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
            foreach (var prop in mariaProps)
                config[$"Target:ConnectionProperties:{prop.Key}"] = prop.Value;
            // Product-side connections the quench opens per target DB are non-pooled too (same ceiling reason).
            config["Target:ConnectionProperties:Pooling"] = "false";
            config["ScriptTokens:MainDB"] = _integrationMainDb;
            config["ScriptTokens:SecondaryDB"] = _integrationSecondaryDb;
        }

        CreateTestDatabases();

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
        using var conn = DbConnectionFactory.ForPlatform(Platform.MariaDb).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_integrationSecondaryDb}`;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_integrationMainDb}`;";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationMainDb);
        ForgeKindler.KindleTheForge(cmd, Platform.MariaDb);

        MySqlFamilyTestSchema.Create(cmd, _integrationMainDb);

        conn.ChangeDatabase(_integrationSecondaryDb);
        ForgeKindler.KindleTheForge(cmd, Platform.MariaDb);

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{dbName}_MariaTest_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabases()
    {
        try
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MariaDb).GetDbConnection(_connectionString);
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

    public static string GetMainDbConnectionString()
    {
        EnsureInitialized();
        return _connectionString + $"Database={_integrationMainDb};";
    }

    public static string GetSecondaryDbConnectionString()
    {
        EnsureInitialized();
        return _connectionString + $"Database={_integrationSecondaryDb};";
    }
}
