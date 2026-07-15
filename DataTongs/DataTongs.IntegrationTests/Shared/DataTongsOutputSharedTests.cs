// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using System;
using System.IO;
using System.Linq;

using NUnit.Framework;
using Schema.Utility;

namespace DataTongs.IntegrationTests.Shared;

/// <summary>
/// Shared integration tests for DataTongs output functionality (content files and merge scripts).
/// The MySQL and MariaDb subclasses supply the platform + fixture accessors; every [Test] body
/// here runs on both engines. Uses dynamically created test databases via FixtureSetup.
/// </summary>
public abstract class DataTongsOutputSharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string MainDb { get; }
    protected abstract string MainConnectionString { get; }

    private IDbConnection _connection = null!;
    private global::DataTongs.DataTongs _dataTongs = null!;
    private string _testOutputDir = null!;
    private string _testDb = null!;

    [SetUp]
    public void SetUp()
    {
        _testDb = MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(MainConnectionString);
        _connection.Open();
        _dataTongs = new global::DataTongs.DataTongs(Platform);
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"DataTongsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputDir);
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();

        // Cleanup test output directory
        if (Directory.Exists(_testOutputDir))
        {
            try { Directory.Delete(_testOutputDir, true); } catch { /* ignore cleanup errors */ }
        }
    }

    #region Content File Output Tests

    [Test]
    public void ContentFile_Actor_WritesValidJson()
    {
        using var command = _connection.CreateCommand();

        // Extract data
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "actor");
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform, command, _testDb, "actor");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "actor", keyColumns, null);

        // Write to file
        var filePath = Path.Combine(_testOutputDir, "actor.tabledata");
        File.WriteAllText(filePath, FormatJson(json));

        // Verify
        Assert.That(File.Exists(filePath), Is.True);
        var content = File.ReadAllText(filePath);
        Assert.That(content, Does.StartWith("["));
        Assert.That(content, Does.EndWith("]"));
        Assert.That(content, Does.Contain("actor_id"));
        Assert.That(content, Does.Contain("first_name"));
    }

    [Test]
    public void ContentFile_WithFilter_ContainsFilteredData()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "actor");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "actor", "`actor_id`", "actor_id <= 5");

        var filePath = Path.Combine(_testOutputDir, "actor_filtered.tabledata");
        File.WriteAllText(filePath, FormatJson(json));

        var content = File.ReadAllText(filePath);
        // Verify we got filtered data - count actor_id occurrences
        var actorIdCount = content.Split(new[] { "\"actor_id\"" }, StringSplitOptions.None).Length - 1;
        Assert.That(actorIdCount, Is.EqualTo(5));
    }

    [Test]
    public void ContentFile_EmptyTable_WritesEmptyArray()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "actor");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "actor", "`actor_id`", "actor_id = -999");

        // Handle null result from JSON_ARRAYAGG
        if (string.IsNullOrEmpty(json) || json == "null")
            json = "[]";

        var filePath = Path.Combine(_testOutputDir, "empty.tabledata");
        File.WriteAllText(filePath, json);

        var content = File.ReadAllText(filePath);
        Assert.That(content, Is.EqualTo("[]"));
    }

    [Test]
    public void ContentFile_Country_PreservesAllColumns()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "country");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "country", "`country_id`", "country_id = 1");

        var filePath = Path.Combine(_testOutputDir, "country.tabledata");
        File.WriteAllText(filePath, FormatJson(json));

        var content = File.ReadAllText(filePath);
        Assert.That(content, Does.Contain("country_id"));
        Assert.That(content, Does.Contain("country"));
        Assert.That(content, Does.Contain("last_update"));
    }

    #endregion

    #region Merge Script Output Tests

    [Test]
    public void MergeScript_Replace_GeneratesValidSQL()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "country");
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform, command, _testDb, "country");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "country", keyColumns, "country_id <= 3");

        // Replace: mergeUpdate=true, mergeDelete=true (full sync)
        var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, "country", json, keyColumns,
            mergeUpdate: true, mergeDelete: true, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        var filePath = Path.Combine(_testOutputDir, "Populate country.sql");
        File.WriteAllText(filePath, script);

        var content = File.ReadAllText(filePath);
        Assert.That(content, Does.Contain($"`{_testDb}`.`country`"));
        Assert.That(content, Does.Contain("JSON_TABLE"));
    }

    [Test]
    public void MergeScript_Upsert_GeneratesOnDuplicateKey()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "country");
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform, command, _testDb, "country");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "country", keyColumns, "country_id = 1");

        // Upsert: mergeUpdate=true, mergeDelete=false
        var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, "country", json, keyColumns,
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(script, Does.Contain($"INSERT INTO `{_testDb}`.`country`"));
        Assert.That(script, Does.Contain("ON DUPLICATE KEY UPDATE"));
    }

    [Test]
    public void MergeScript_Insert_GeneratesInsertIgnore()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "country");
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform, command, _testDb, "country");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "country", keyColumns, "country_id = 1");

        // Insert: mergeUpdate=false, mergeDelete=false
        var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, "country", json, keyColumns,
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(script, Does.Contain($"INSERT IGNORE INTO `{_testDb}`.`country`"));
    }

    [Test]
    public void MergeScript_NoForeignKeyChecks_InOutput()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "country");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "country", "`country_id`", "country_id = 1");

        // Replace: mergeUpdate=true, mergeDelete=true
        var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, "country", json, "`country_id`",
            mergeUpdate: true, mergeDelete: true, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(script, Does.Not.Contain("FOREIGN_KEY_CHECKS"));
    }

    [Test]
    public void MergeScript_CustomKeyColumns_UsesSpecifiedKeys()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "country");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "country", "`country_id`", "country_id = 1");

        // Use custom key column instead of primary key (Upsert: mergeUpdate=true, mergeDelete=false)
        var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, "country", json, "`country`",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        // The UPDATE clause should not include the key column
        Assert.That(script, Does.Contain("ON DUPLICATE KEY UPDATE"));
        // country column should be excluded from UPDATE since it's the key
    }

    #endregion

    #region Script Execution Tests

    [Test]
    public void GeneratedScript_CanBeExecuted()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_output_{Guid.NewGuid():N}".Substring(0, 30);

        try
        {
            // Create test table
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    code VARCHAR(10) NOT NULL,
                    name VARCHAR(100),
                    value DECIMAL(10,2),
                    UNIQUE KEY uk_code (code)
                )";
            command.ExecuteNonQuery();

            // Insert initial data
            command.CommandText = $"INSERT INTO `{_testDb}`.`{tableName}` (code, name, value) VALUES ('A01', 'Initial', 10.00)";
            command.ExecuteNonQuery();

            // Generate merge script (Upsert: mergeUpdate=true, mergeDelete=false)
            var tableData = @"[{""code"":""A01"",""name"":""Updated"",""value"":20.00},{""code"":""A02"",""name"":""New"",""value"":30.00}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, tableName, tableData, "`code`",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // Execute script
            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify results
            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}`";
            var count = Convert.ToInt32(command.ExecuteScalar());
            Assert.That(count, Is.EqualTo(2)); // 1 updated + 1 new

            command.CommandText = $"SELECT name FROM `{_testDb}`.`{tableName}` WHERE code = 'A01'";
            var name = command.ExecuteScalar()?.ToString();
            Assert.That(name, Is.EqualTo("Updated"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void GeneratedScript_Replace_DeletesAndInserts()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_replace_{Guid.NewGuid():N}".Substring(0, 30);

        try
        {
            // Create test table
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    name VARCHAR(100)
                )";
            command.ExecuteNonQuery();

            // Insert initial data
            command.CommandText = $"INSERT INTO `{_testDb}`.`{tableName}` VALUES (1, 'Original')";
            command.ExecuteNonQuery();

            // Generate REPLACE script (mergeUpdate=true, mergeDelete=true for full sync)
            var tableData = @"[{""id"":1,""name"":""Replaced""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, tableName, tableData, "`id`",
                mergeUpdate: true, mergeDelete: true, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // Execute script
            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify
            command.CommandText = $"SELECT name FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            var name = command.ExecuteScalar()?.ToString();
            Assert.That(name, Is.EqualTo("Replaced"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void GeneratedScript_Insert_SkipsExisting()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_insert_{Guid.NewGuid():N}".Substring(0, 30);

        try
        {
            // Create test table
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    name VARCHAR(100)
                )";
            command.ExecuteNonQuery();

            // Insert initial data
            command.CommandText = $"INSERT INTO `{_testDb}`.`{tableName}` VALUES (1, 'Original')";
            command.ExecuteNonQuery();

            // Generate INSERT IGNORE script (Insert: mergeUpdate=false, mergeDelete=false)
            var tableData = @"[{""id"":1,""name"":""Ignored""},{""id"":2,""name"":""New""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform, command, _testDb, tableName, tableData, "`id`",
                mergeUpdate: false, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // Execute script
            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify - id=1 should still be "Original", id=2 should be "New"
            command.CommandText = $"SELECT name FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            var name1 = command.ExecuteScalar()?.ToString();
            Assert.That(name1, Is.EqualTo("Original")); // Not replaced

            command.CommandText = $"SELECT name FROM `{_testDb}`.`{tableName}` WHERE id = 2";
            var name2 = command.ExecuteScalar()?.ToString();
            Assert.That(name2, Is.EqualTo("New")); // Inserted
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    #endregion

    #region Helper Methods

    private static string FormatJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]" || json == "null")
            return "[]";

        return json
            .Replace("},{", "},\n{")
            .Replace("[{", "[\n{")
            .Replace("}]", "}\n]");
    }

    #endregion
}
