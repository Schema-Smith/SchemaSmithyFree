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

// Verifies the environment-level no-drop protection tier (env PreventDrop). When on, the environment
// never drops an object for being ABSENT from the product: an unprotected table removed from the
// package survives (unlike the sticky per-table case, nothing here is individually marked), the run
// completes normally (exit 0, no abort), and the suppressed drop is itemized in the deployment
// summary's PreventDrop manifest. Contrast the sibling sticky test where only a marked table survives.
[Category("SqlServer")]
public class ProtectedMode_PreventDropTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public ProtectedMode_PreventDropTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master", config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    [Test]
    public void ProtectedMode_RemovedTable_NotDropped_ManifestLists_Exit0()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_Skip_{Guid.NewGuid():N}");

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
                // First quench (protection off): establish all three tables.
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.That(TableExists(cmd, "RemovableTable"), Is.True, "Setup: RemovableTable should exist.");

                // Remove the UNPROTECTED table from the package — normally a drop-by-absence.
                File.Delete(Path.Join(tempDir, "Templates", "Main", "Tables", "dbo.RemovableTable.json"));

                // Second quench WITH environment protection on: the removed table must NOT drop.
                FactoryContainer.Resolve<IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, "RemovableTable"), Is.True,
                        "Protected environment must NOT drop RemovableTable for being absent from the product.");
                    Assert.That(TableExists(cmd, "KeeperTable"), Is.True, "KeeperTable stays in the package and must survive.");
                });

                // The suppressed drop must be itemized in the PreventDrop manifest.
                var manifest = ReadWouldDropNames();
                Assert.That(manifest.Any(n => n.Contains("RemovableTable")), Is.True,
                    $"PreventDrop manifest must list the suppressed RemovableTable drop. Manifest: [{string.Join(", ", manifest)}]");
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

    [Test]
    public void ProtectedMode_NothingRemoved_EmptyManifest_Exit0()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_Empty_{Guid.NewGuid():N}");

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
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();

                // Protection on, but nothing removed from the package: manifest is empty, run proceeds.
                FactoryContainer.Resolve<IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, "RemovableTable"), Is.True, "Nothing removed — all tables survive.");
                    Assert.That(ReadWouldDropNames(), Is.Empty,
                        "Protection on but nothing suppressed — the manifest must be empty.");
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

    // Reads the PreventDrop.WouldDrop object names from the written deployment-summary JSON.
    private static string[] ReadWouldDropNames()
    {
        var jsonPath = Path.Join(ConfigHelper.ResolveLogPath(), "SchemaQuench - Summary.json");
        var root = JObject.Parse(File.ReadAllText(jsonPath));
        var wouldDrop = root["preventDrop"]?["wouldDrop"] as JArray;
        return wouldDrop == null
            ? Array.Empty<string>()
            : wouldDrop.Select(e => e["objectName"]!.Value<string>()!).ToArray();
    }

    private static void CopyFixtureTo(string dest)
    {
        var src = TestHelper.GetTestProductPath("SqlServer", "StickyPreventDrop");
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

    private static bool TableExists(System.Data.IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $"SELECT CASE WHEN OBJECT_ID('dbo.{tableName}') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static void DropTablesAndCleanup(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[RemovableTable];
DROP TABLE IF EXISTS [dbo].[ProtectedTable];
DROP TABLE IF EXISTS [dbo].[KeeperTable];";
        cmd.ExecuteNonQuery();
    }
}
