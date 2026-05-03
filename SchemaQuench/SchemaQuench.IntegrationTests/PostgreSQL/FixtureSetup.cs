// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[SetUpFixture]
public class FixtureSetup
{
    private string _integrationMainDb = "";
    private string _integrationSecondaryDb = "";
    private string _connectionString;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);

        // Map PostgreSQL-specific config to Target:* keys used by ProductQuench
        config["Target:Server"] = config["PostgreSQL:Server"] ?? "127.0.0.1";
        config["Target:Port"] = config["PostgreSQL:Port"];
        config["Target:User"] = config["PostgreSQL:User"];
        config["Target:Password"] = config["PostgreSQL:Password"];
        var pgConnProps = ConnectionString.ReadProperties(config, "PostgreSQL:ConnectionProperties");
        foreach (var prop in pgConnProps)
            config[$"Target:ConnectionProperties:{prop.Key}"] = prop.Value;

        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres", config["Target:User"], config["Target:Password"], config["Target:Port"], pgConnProps);

        _integrationSecondaryDb = GenerateUniqueDBName("TestSecondary");
        config["ScriptTokens:SecondaryDB"] = _integrationSecondaryDb;
        _integrationMainDb = GenerateUniqueDBName("TestMain");
        config["ScriptTokens:MainDB"] = _integrationMainDb;

        CreateTestDatabases();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
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
        cmd.CommandText = @$"DROP DATABASE IF EXISTS ""{dbName}"" WITH (FORCE);";
        cmd.ExecuteNonQuery();
    }
}
