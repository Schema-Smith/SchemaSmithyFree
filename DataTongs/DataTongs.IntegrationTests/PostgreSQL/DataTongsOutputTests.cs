// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

using NUnit.Framework;

namespace DataTongs.IntegrationTests.PostgreSQL;

/// <summary>
/// Integration tests for DataTongs merge script output against PostgreSQL.
/// Tests that generated merge scripts use correct PostgreSQL MERGE syntax (PostgreSQL 15+).
/// </summary>
[Category("PostgreSQL")]
[TestFixture]
[Category("Integration")]
public class DataTongsOutputTests
{
    private string _integrationDb = "";
    private string _connectionString = "";
    private Dictionary<string, string> _connProps = null!;
    private IDbConnection _connection = null!;
    private global::DataTongs.DataTongs _dataTongs = null!;
    private string _testOutputDir = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        _connProps = ConnectionString.ReadProperties(config, "PostgreSQL:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["PostgreSQL:Server"], "postgres", config["PostgreSQL:User"], config["PostgreSQL:Password"], config["PostgreSQL:Port"], _connProps);
        _integrationDb = GenerateUniqueDBName("DTOutput");

        CreateTestDatabase();
        CreateTestTables();
    }

    [SetUp]
    public void SetUp()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var dbConnString = ConnectionString.Build(Platform.PostgreSQL, config["PostgreSQL:Server"], _integrationDb, config["PostgreSQL:User"], config["PostgreSQL:Password"], config["PostgreSQL:Port"], _connProps);
        _connection = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(dbConnString);
        _connection.Open();
        _dataTongs = new global::DataTongs.DataTongs(Platform.PostgreSQL);
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"DataTongsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputDir);
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();

        if (Directory.Exists(_testOutputDir))
        {
            try { Directory.Delete(_testOutputDir, true); } catch { /* ignore cleanup errors */ }
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        DropTestDatabase();
    }

    #region Merge Script Output Tests

    [Test]
    public void MergeScript_Upsert_GeneratesMergeStatement()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, "public", "TestCountry");
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform.PostgreSQL, command, "public", "TestCountry");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, "public", "TestCountry", keyColumns, "\"CountryId\" <= 3");

        // Upsert: mergeUpdate=true, mergeDelete=false
        var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command, "public", "TestCountry", json, keyColumns,
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(script, Does.Contain("MERGE INTO"));
        Assert.That(script, Does.Contain("WHEN MATCHED"));
        Assert.That(script, Does.Contain("WHEN NOT MATCHED"));
        Assert.That(script, Does.Contain("v_json"));
    }

    [Test]
    public void MergeScript_Insert_GeneratesMergeWithoutUpdate()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, "public", "TestCountry");
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform.PostgreSQL, command, "public", "TestCountry");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, "public", "TestCountry", keyColumns, "\"CountryId\" = 1");

        // Insert: mergeUpdate=false, mergeDelete=false
        var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command, "public", "TestCountry", json, keyColumns,
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(script, Does.Contain("MERGE INTO"));
        Assert.That(script, Does.Contain("WHEN NOT MATCHED"));
        // Should not have WHEN MATCHED THEN UPDATE
        Assert.That(script, Does.Not.Contain("WHEN MATCHED THEN UPDATE"));
    }

    [Test]
    public void MergeScript_Replace_GeneratesMergeWithDelete()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, "public", "TestCountry");
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform.PostgreSQL, command, "public", "TestCountry");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, "public", "TestCountry", keyColumns, "\"CountryId\" <= 3");

        // Replace: mergeUpdate=true, mergeDelete=true (full sync)
        var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command, "public", "TestCountry", json, keyColumns,
            mergeUpdate: true, mergeDelete: true, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(script, Does.Contain("MERGE INTO"));
        Assert.That(script, Does.Contain("DELETE"));
    }

    #endregion

    #region Script Execution Tests

    [Test]
    public void GeneratedScript_CanBeExecuted()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"TestExec_{Guid.NewGuid():N}".Substring(0, 30);

        try
        {
            // Create test table
            command.CommandText = $@"
                CREATE TABLE public.""{tableName}"" (
                    ""Id"" INT PRIMARY KEY,
                    ""Code"" VARCHAR(10) NOT NULL UNIQUE,
                    ""Name"" VARCHAR(100),
                    ""Value"" DECIMAL(10,2)
                )";
            command.ExecuteNonQuery();

            // Insert initial data
            command.CommandText = $@"INSERT INTO public.""{tableName}"" (""Id"", ""Code"", ""Name"", ""Value"") VALUES (1, 'A01', 'Initial', 10.00)";
            command.ExecuteNonQuery();

            // Generate merge script (Upsert: mergeUpdate=true, mergeDelete=false)
            var tableData = @"[{""Code"":""A01"",""Id"":1,""Name"":""Updated"",""Value"":20.00},{""Code"":""A02"",""Id"":2,""Name"":""New"",""Value"":30.00}]";
            var keyColumns = MergeScriptHelper.GetKeyColumns(Platform.PostgreSQL, command, "public", tableName);
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command, "public", tableName, tableData, keyColumns,
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // Execute script - PostgreSQL scripts don't need statement splitting
            command.CommandText = script;
            command.ExecuteNonQuery();

            // Verify results
            command.CommandText = $@"SELECT COUNT(*) FROM public.""{tableName}""";
            var count = Convert.ToInt32(command.ExecuteScalar());
            Assert.That(count, Is.EqualTo(2)); // 1 updated + 1 new

            command.CommandText = $@"SELECT ""Name"" FROM public.""{tableName}"" WHERE ""Code"" = 'A01'";
            var name = command.ExecuteScalar()?.ToString();
            Assert.That(name, Is.EqualTo("Updated"));
        }
        finally
        {
            command.CommandText = $@"DROP TABLE IF EXISTS public.""{tableName}""";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void GeneratedScript_Insert_SkipsExisting()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"TestIns_{Guid.NewGuid():N}".Substring(0, 30);

        try
        {
            // Create test table
            command.CommandText = $@"
                CREATE TABLE public.""{tableName}"" (
                    ""Id"" INT PRIMARY KEY,
                    ""Name"" VARCHAR(100)
                )";
            command.ExecuteNonQuery();

            // Insert initial data
            command.CommandText = $@"INSERT INTO public.""{tableName}"" VALUES (1, 'Original')";
            command.ExecuteNonQuery();

            // Generate INSERT-only script (mergeUpdate=false, mergeDelete=false)
            var tableData = @"[{""Id"":1,""Name"":""Ignored""},{""Id"":2,""Name"":""New""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command, "public", tableName, tableData, "\"Id\"",
                mergeUpdate: false, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // Execute script
            command.CommandText = script;
            command.ExecuteNonQuery();

            // Verify - id=1 should still be "Original", id=2 should be "New"
            command.CommandText = $@"SELECT ""Name"" FROM public.""{tableName}"" WHERE ""Id"" = 1";
            var name1 = command.ExecuteScalar()?.ToString();
            Assert.That(name1, Is.EqualTo("Original")); // Not replaced

            command.CommandText = $@"SELECT ""Name"" FROM public.""{tableName}"" WHERE ""Id"" = 2";
            var name2 = command.ExecuteScalar()?.ToString();
            Assert.That(name2, Is.EqualTo("New")); // Inserted
        }
        finally
        {
            command.CommandText = $@"DROP TABLE IF EXISTS public.""{tableName}""";
            command.ExecuteNonQuery();
        }
    }

    #endregion

    #region Database Setup

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void CreateTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"CREATE DATABASE ""{_integrationDb}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private void CreateTestTables()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var dbConnString = ConnectionString.Build(Platform.PostgreSQL, config["PostgreSQL:Server"], _integrationDb, config["PostgreSQL:User"], config["PostgreSQL:Password"], config["PostgreSQL:Port"], _connProps);
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(dbConnString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE public.""TestCountry"" (
    ""CountryId"" INT NOT NULL PRIMARY KEY,
    ""CountryName"" VARCHAR(50) NOT NULL,
    ""LastUpdate"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO public.""TestCountry"" (""CountryId"", ""CountryName"") VALUES
    (1, 'Afghanistan'),
    (2, 'Algeria'),
    (3, 'American Samoa'),
    (4, 'Angola'),
    (5, 'Anguilla');
";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    private void DropTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_integrationDb}' AND pid <> pg_backend_pid();
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @$"DROP DATABASE IF EXISTS ""{_integrationDb}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    #endregion
}
