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

namespace SchemaQuench.IntegrationTests.PostgreSQL;

// Parity guard for #358 (a SQL-Server-only defect). PostgreSQL drops a column's dependents via
// DROP COLUMN ... CASCADE, which rides the gated column drop, so a column preserved by env PreventDrop
// keeps its dependents by construction. This test asserts that guarantee holds on PostgreSQL.
[Category("PostgreSQL")]
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
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
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

            using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.Multiple(() =>
                {
                    Assert.That(ColumnExists(cmd, "Child", "Extra"), Is.True, "Setup: Child.Extra should exist.");
                    Assert.That(IndexExists(cmd, "IX_Child_Extra"), Is.True, "Setup: IX_Child_Extra should exist.");
                    Assert.That(CheckOnColumnExists(cmd, "Child", "Extra"), Is.True, "Setup: CHECK on Extra should exist.");
                    Assert.That(DefaultOnColumnExists(cmd, "Child", "Extra"), Is.True, "Setup: DEFAULT on Extra should exist.");
                });

                var childJson = Path.Join(tempDir, "Templates", "Main", "Tables", "public.Child.json");
                RemoveColumnFromTable(childJson, "Extra");
                RemoveIndexFromTable(childJson, "IX_Child_Extra");

                FactoryContainer.Resolve<IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.Multiple(() =>
                {
                    Assert.That(ColumnExists(cmd, "Child", "Extra"), Is.True,
                        "Protected: the Extra column must survive.");
                    Assert.That(IndexExists(cmd, "IX_Child_Extra"), Is.True,
                        "Protected: the preserved column's INDEX must survive (#358).");
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

    private static bool ColumnExists(System.Data.IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $"SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{tableName}' AND column_name = '{columnName}')";
        return (bool)cmd.ExecuteScalar()!;
    }

    private static bool IndexExists(System.Data.IDbCommand cmd, string indexName)
    {
        cmd.CommandText = $"SELECT EXISTS(SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname = '{indexName}')";
        return (bool)cmd.ExecuteScalar()!;
    }

    private static bool CheckOnColumnExists(System.Data.IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"SELECT EXISTS(
            SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
            WHERE t.relname = '{tableName}' AND c.contype = 'c'
              AND pg_get_constraintdef(c.oid) LIKE '%{columnName}%')";
        return (bool)cmd.ExecuteScalar()!;
    }

    private static bool DefaultOnColumnExists(System.Data.IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $"SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{tableName}' AND column_name = '{columnName}' AND column_default IS NOT NULL)";
        return (bool)cmd.ExecuteScalar()!;
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
        var src = TestHelper.GetTestProductPath("PostgreSQL", "ColumnDependentProtection");
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
        cmd.CommandText = @"DROP TABLE IF EXISTS public.""Child"";";
        cmd.ExecuteNonQuery();
    }
}
