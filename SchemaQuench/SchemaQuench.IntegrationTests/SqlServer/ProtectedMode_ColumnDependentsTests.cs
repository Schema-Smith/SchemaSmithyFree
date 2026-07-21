// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests;
using Schema.Isolators;
using Schema.Utility;
using System;
using System.IO;
using System.Linq;

namespace SchemaQuench.IntegrationTests.SqlServer;

// Regression for #358: with env-level PreventDrop, removing a COLUMN from the product keeps the
// column — and must ALSO keep every dependent object of that column (its index, statistics, DEFAULT,
// and any CHECK referencing it). Before the fix the column survived but its dependents were dropped
// "in preparation" for a column drop that PreventDrop then suppressed.
[Category("SqlServer")]
public class ProtectedMode_ColumnDependentsTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public ProtectedMode_ColumnDependentsTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master", config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    [Test]
    public void ProtectedMode_RemovedColumn_KeepsAllDependents_Exit0()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_ColDeps_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);

            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // First quench (protection off): establish Child with Extra + all four dependents.
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.Multiple(() =>
                {
                    Assert.That(ColumnExists(cmd, "Child", "Extra"), Is.True, "Setup: Child.Extra should exist.");
                    Assert.That(IndexExists(cmd, "Child", "IX_Child_Extra"), Is.True, "Setup: IX_Child_Extra should exist.");
                    Assert.That(StatisticExists(cmd, "Child", "ST_Extra"), Is.True, "Setup: ST_Extra should exist.");
                    Assert.That(CheckOnColumnExists(cmd, "Child", "Extra"), Is.True, "Setup: CHECK on Extra should exist.");
                    Assert.That(DefaultOnColumnExists(cmd, "Child", "Extra"), Is.True, "Setup: DEFAULT on Extra should exist.");
                });

                // Retire the Extra column the ordinary way: remove the column (its DEFAULT + CHECK go
                // with it) and its index + statistics from the package.
                var childJson = Path.Join(tempDir, "Templates", "Main", "Tables", "dbo.Child.json");
                RemoveColumnFromTable(childJson, "[Extra]");
                RemoveIndexFromTable(childJson, "[IX_Child_Extra]");
                RemoveStatisticFromTable(childJson, "[ST_Extra]");

                // Second quench WITH environment protection: the column AND all its dependents survive.
                FactoryContainer.Resolve<IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.Multiple(() =>
                {
                    Assert.That(ColumnExists(cmd, "Child", "Extra"), Is.True,
                        "Protected: the Extra column must survive.");
                    Assert.That(IndexExists(cmd, "Child", "IX_Child_Extra"), Is.True,
                        "Protected: the preserved column's INDEX must survive (#358).");
                    Assert.That(StatisticExists(cmd, "Child", "ST_Extra"), Is.True,
                        "Protected: the preserved column's STATISTICS must survive (#358).");
                    Assert.That(CheckOnColumnExists(cmd, "Child", "Extra"), Is.True,
                        "Protected: the preserved column's CHECK must survive (#358).");
                    Assert.That(DefaultOnColumnExists(cmd, "Child", "Extra"), Is.True,
                        "Protected: the preserved column's DEFAULT must survive (#358).");
                });
            }
            finally
            {
                FactoryContainer.Resolve<IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd);
                conn.Close();
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    private static void RemoveColumnFromTable(string tableJsonPath, string columnName)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        ((JArray)root["Columns"]!).First(c => (string)c["Name"]! == columnName).Remove();
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private static void RemoveIndexFromTable(string tableJsonPath, string indexName)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        ((JArray)root["Indexes"]!).First(i => (string)i["Name"]! == indexName).Remove();
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private static void RemoveStatisticFromTable(string tableJsonPath, string statName)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        ((JArray)root["Statistics"]!).First(s => (string)s["Name"]! == statName).Remove();
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private static bool ColumnExists(System.Data.IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $"SELECT CASE WHEN COL_LENGTH('dbo.{tableName}', '{columnName}') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static bool IndexExists(System.Data.IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('dbo.{tableName}')) THEN 1 ELSE 0 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static bool StatisticExists(System.Data.IDbCommand cmd, string tableName, string statName)
    {
        cmd.CommandText = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.stats WHERE name = '{statName}' AND object_id = OBJECT_ID('dbo.{tableName}')) THEN 1 ELSE 0 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static bool CheckOnColumnExists(System.Data.IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"SELECT CASE WHEN EXISTS(
            SELECT 1 FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID('dbo.{tableName}')
              AND [definition] LIKE '%{columnName}%') THEN 1 ELSE 0 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static bool DefaultOnColumnExists(System.Data.IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"SELECT CASE WHEN EXISTS(
            SELECT 1 FROM sys.default_constraints
            WHERE parent_object_id = OBJECT_ID('dbo.{tableName}')
              AND COL_NAME(parent_object_id, parent_column_id) = '{columnName}') THEN 1 ELSE 0 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private void SetupSharedMocks()
    {
        _progressLog.ClearReceivedCalls();
        _errorLog.ClearReceivedCalls();
        _environment.ClearReceivedCalls();
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private static void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private static void CopyFixtureTo(string dest)
    {
        var src = TestHelper.GetTestProductPath("SqlServer", "ColumnDependentProtection");
        CopyDirectory(src, dest);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Join(dest, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Join(dest, Path.GetFileName(dir)));
    }

    private static void DropTablesAndCleanup(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = "DROP TABLE IF EXISTS [dbo].[Child];";
        cmd.ExecuteNonQuery();
    }
}
