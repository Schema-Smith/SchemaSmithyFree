// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[SetUpFixture]
public class FixtureSetup
{
    private string _integrationMainDb = "";
    private string _integrationSecondaryDb = "";
    private string _connectionString;

    // All four engine SetUpFixtures publish to the SAME global Target:* / ScriptTokens:* keys on the
    // shared IConfigurationRoot (last-writer-wins). In the full unfiltered run a sibling engine
    // fixture's OneTimeSetUp (parallel worker lane) can overwrite them while a PostgreSQL schema-template
    // test is mid-quench. This captured snapshot lets those tests re-assert PostgreSQL's target under
    // SharedLockObject before the quench reads Target:* live. See ApplyTargetConfig.
    private static Dictionary<string, string> _targetConfig;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var pgConnProps = ConnectionString.ReadProperties(config, "PostgreSQL:ConnectionProperties");

        _integrationSecondaryDb = GenerateUniqueDBName("TestSecondary");
        _integrationMainDb = GenerateUniqueDBName("TestMain");

        _targetConfig = new Dictionary<string, string>
        {
            ["Target:Server"] = config["PostgreSQL:Server"] ?? "127.0.0.1",
            ["Target:Port"] = config["PostgreSQL:Port"],
            ["Target:User"] = config["PostgreSQL:User"],
            ["Target:Password"] = config["PostgreSQL:Password"],
            ["ScriptTokens:MainDB"] = _integrationMainDb,
            ["ScriptTokens:SecondaryDB"] = _integrationSecondaryDb,
        };
        foreach (var prop in pgConnProps)
            _targetConfig[$"Target:ConnectionProperties:{prop.Key}"] = prop.Value;

        // Publish under the shared lock so a concurrently-initialising sibling engine fixture can't
        // interleave a half-written Target block (each fixture's write is now lock-guarded).
        lock (FactoryContainer.SharedLockObject)
            ApplyTargetConfig(config);

        _connectionString = ConnectionString.Build(Platform.PostgreSQL, _targetConfig["Target:Server"], "postgres", _targetConfig["Target:User"], _targetConfig["Target:Password"], _targetConfig["Target:Port"], pgConnProps);

        CreateTestDatabases();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
    }

    /// <summary>
    /// Re-applies PostgreSQL's Target:* / ScriptTokens:* onto the shared config. All four engine
    /// SetUpFixtures write these same global keys, so a sibling fixture's OneTimeSetUp can overwrite
    /// them mid-run; PostgreSQL schema-template tests call this while holding SharedLockObject so the
    /// quench connects to PostgreSQL rather than a sibling engine's target. Caller MUST hold
    /// FactoryContainer.SharedLockObject.
    /// </summary>
    internal static void ApplyTargetConfig(IConfigurationRoot config)
    {
        if (_targetConfig == null) return;
        foreach (var kv in _targetConfig)
            config[kv.Key] = kv.Value;
    }

    private void CreateTestDatabases()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
CREATE DATABASE ""{_integrationSecondaryDb}"";

CREATE DATABASE ""{_integrationMainDb}"";
";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationMainDb);
        ForgeKindler.KindleTheForge(cmd, Platform.PostgreSQL);

        cmd.CommandText = @"
CREATE DOMAIN ""Flag"" AS BOOLEAN NOT NULL;
";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationSecondaryDb);
        ForgeKindler.KindleTheForge(cmd, Platform.PostgreSQL);

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace(" - ", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabases()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        DropOneDatabase(cmd, _integrationSecondaryDb);
        DropOneDatabase(cmd, _integrationMainDb);

        conn.Close();
    }

    private static void DropOneDatabase(IDbCommand cmd, string dbName)
    {
        cmd.CommandText = @$"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{dbName}' AND pid <> pg_backend_pid();";
        cmd.ExecuteNonQuery();
        cmd.CommandText = @$"DROP DATABASE IF EXISTS ""{dbName}"";";
        cmd.ExecuteNonQuery();
    }
}
