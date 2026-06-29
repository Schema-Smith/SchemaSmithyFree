// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests;
using Schema.Isolators;
using Schema.Utility;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchemaQuench.IntegrationTests.SqlServer;

// Verifies that a product-level DropTablesRemovedFromProduct: false suppresses table-drop-by-absence
// even when the env-level flag is true (the default). Drives the full ProductQuench path
// (Program.Main → ProductQuench → ResolveDropTablesRemovedFromProduct → DatabaseQuench → proc)
// so the Slice A AND-composition is exercised, not just the proc parameter.
[Category("SqlServer")]
public class TableQuench_ProductLevelDropProtectionTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public TableQuench_ProductLevelDropProtectionTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master", config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    [Test]
    public void ProductLevelFalse_VetoesDropEvenWhenEnvLevelTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"DropProt_Suppress_{Guid.NewGuid():N}");

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
                // First quench: establish both tables with product ownership.
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "DropProtKeep"), Is.True, "Setup: DropProtKeep should exist.");
                Assert.That(TableExists(cmd, "DropProtRemovable"), Is.True, "Setup: DropProtRemovable should exist.");

                // Remove the removable table's JSON from the temp package.
                File.Delete(Path.Combine(tempDir, "Templates", "Main", "Tables", "dbo.DropProtRemovable.json"));

                // Env-level DropTablesRemovedFromProduct is true (SchemaQuench.settings.json default).
                // Product-level is false → should veto the drop.
                // Clear first-quench call history so the DidNotReceive assertions below target only the second quench.
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, "DropProtRemovable"), Is.True,
                        "Product-level false must veto the drop: DropProtRemovable must still exist.");
                    Assert.That(TableExists(cmd, "DropProtKeep"), Is.True,
                        "DropProtKeep must still exist.");
                });
            }
            finally
            {
                DropTablesAndCleanup(cmd);
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void ProductLevelTrue_DropsRemovedTableWhenEnvLevelTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"DropProt_Default_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            CopyFixtureTo(tempDir);

            // Override product-level flag to true so the default drop path runs.
            SetProductDropFlag(tempDir, true);

            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // First quench: establish both tables.
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "DropProtKeep"), Is.True, "Setup: DropProtKeep should exist.");
                Assert.That(TableExists(cmd, "DropProtRemovable"), Is.True, "Setup: DropProtRemovable should exist.");

                // Remove the removable table's JSON from the temp package.
                File.Delete(Path.Combine(tempDir, "Templates", "Main", "Tables", "dbo.DropProtRemovable.json"));

                // Both env-level and product-level are true → drop should execute.
                // Clear first-quench call history so the DidNotReceive assertions below target only the second quench.
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, "DropProtRemovable"), Is.False,
                        "Product-level true + env true must drop DropProtRemovable.");
                    Assert.That(TableExists(cmd, "DropProtKeep"), Is.True,
                        "DropProtKeep must still exist.");
                });
            }
            finally
            {
                DropTablesAndCleanup(cmd);
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

    private void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private static void CopyFixtureTo(string dest)
    {
        var src = TestHelper.GetTestProductPath("SqlServer", "DropProtection");
        CopyDirectory(src, dest);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static void SetProductDropFlag(string packageDir, bool value)
    {
        var path = Path.Combine(packageDir, "Product.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["DropTablesRemovedFromProduct"] = value;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool TableExists(System.Data.IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $"SELECT CASE WHEN OBJECT_ID('dbo.{tableName}') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private void DropTablesAndCleanup(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[DropProtRemovable];
DROP TABLE IF EXISTS [dbo].[DropProtKeep];";
        cmd.ExecuteNonQuery();
    }
}
