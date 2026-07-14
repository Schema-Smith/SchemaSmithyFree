// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
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

namespace SchemaQuench.IntegrationTests.Shared;

// Verifies the template-tier cascade for DropTablesRemovedFromProduct and the env-config
// DropUnknownIndexes retrofit (env-level override that was previously package-only).
public abstract class CascadeFoundationTestsSharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string MainDb { get; }
    protected abstract string BaseConnectionString { get; }
    protected abstract Microsoft.Extensions.Configuration.IConfigurationRoot FixtureConfig { get; }
    protected abstract string ProductPlatformFolder { get; }

    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    protected CascadeFoundationTestsSharedTests()
    {
        _connectionString = BaseConnectionString + "Database=information_schema;";
        _mainDb = MainDb;
    }

    // Template.json carries DropTablesRemovedFromProduct:false → absent table in that template survives
    // even though the env-level flag is true and Product.json is silent (defaults true).
    [Test]
    public void TemplateTierFalse_VetoesDropEvenWhenEnvAndProductAllowDrop()
    {
        var tempSuppressed = Path.Join(Path.GetTempPath(), $"CascTpl_Suppress_{Guid.NewGuid():N}");
        var tempEnabled = Path.Join(Path.GetTempPath(), $"CascTpl_Enable_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // --- Part A: template-false vetoes the drop ---
                CopyFixtureTo("CascadeTemplateTier", tempSuppressed);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempSuppressed;
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "CascKeep"), Is.True, "Setup A: CascKeep should exist.");
                Assert.That(TableExists(cmd, "CascRemovable"), Is.True, "Setup A: CascRemovable should exist.");

                File.Delete(Path.Join(tempSuppressed, "Templates", "Main", "Tables", "CascRemovable.json"));

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, "CascRemovable"), Is.True,
                        "Template-tier false must veto the drop: CascRemovable must survive.");
                    Assert.That(TableExists(cmd, "CascKeep"), Is.True,
                        "CascKeep must still exist.");
                });

                // Clean up Part A tables before Part B.
                DropCascadeTemplateTierTables(cmd);

                // --- Part B: template-false removed → drop is permitted ---
                CopyFixtureTo("CascadeTemplateTier", tempEnabled);
                SetTemplateDropFlag(tempEnabled, true);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempEnabled;
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "CascKeep"), Is.True, "Setup B: CascKeep should exist.");
                Assert.That(TableExists(cmd, "CascRemovable"), Is.True, "Setup B: CascRemovable should exist.");

                File.Delete(Path.Join(tempEnabled, "Templates", "Main", "Tables", "CascRemovable.json"));

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(TableExists(cmd, "CascRemovable"), Is.False,
                    "Template-tier true + env true must drop CascRemovable.");
                Assert.That(TableExists(cmd, "CascKeep"), Is.True,
                    "CascKeep must still exist after Part B drop.");
            }
            finally
            {
                DropCascadeTemplateTierTables(cmd);
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                if (Directory.Exists(tempSuppressed)) Directory.Delete(tempSuppressed, true);
                if (Directory.Exists(tempEnabled)) Directory.Delete(tempEnabled, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    // Index-B (#270) brought MySQL to SS/PG parity: a product-OWNED index removed from the JSON is
    // reconciled by DEFAULT, gated by DropIndexesRemovedFromProduct — NOT coupled to DropUnknownIndexes
    // (which now governs out-of-band, unowned indexes). MySQL tracks index ownership via
    // SchemaSmith_ProductOwnership. The two scenarios:
    // A) default config → a product-owned index removed from the JSON is dropped.
    // B) env DropIndexesRemovedFromProduct=false → the same index survives.
    [Test]
    public void EnvDropIndexesRemovedFromProduct_DefaultDrops_FalsePreserves()
    {
        var tempDrop = Path.Join(Path.GetTempPath(), $"CascIdx_Drop_{Guid.NewGuid():N}");
        var tempPreserve = Path.Join(Path.GetTempPath(), $"CascIdx_Preserve_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // --- Part A: default config → product-owned index removed from JSON is dropped ---
                // Deploy the full fixture (PK + secondary index) to establish product ownership.
                CopyFixtureTo("CascadeIndexEnv", tempDrop);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDrop;
                RunSchemaQuench();

                Assert.That(IndexExists(cmd, "CascIdx", "PRIMARY"), Is.True, "Setup A: PRIMARY should exist.");
                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.True, "Setup A: IX_CascIdx_Name should exist after full deploy.");

                // Remove the secondary index from the JSON — it remains in SchemaSmith_ProductOwnership.
                RemoveSecondaryIndexFromTableJson(tempDrop);

                // Re-quench with DEFAULT config — the product-owned removed index drops by default (no
                // DropUnknownIndexes needed; DropIndexesRemovedFromProduct defaults on).
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.False,
                    "Removed-from-product index must drop by default (DropIndexesRemovedFromProduct on).");
                Assert.That(IndexExists(cmd, "CascIdx", "PRIMARY"), Is.True,
                    "PRIMARY must not be dropped.");

                DropCascadeIndexEnvTables(cmd);

                // --- Part B: env DropIndexesRemovedFromProduct=false → product-owned removed index survives ---
                CopyFixtureTo("CascadeIndexEnv", tempPreserve);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempPreserve;

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                Assert.That(IndexExists(cmd, "CascIdx", "PRIMARY"), Is.True, "Setup B: PRIMARY should exist.");
                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.True, "Setup B: IX_CascIdx_Name should exist after full deploy.");

                // Remove the secondary index from the JSON — it remains in SchemaSmith_ProductOwnership.
                RemoveSecondaryIndexFromTableJson(tempPreserve);

                // Re-quench with env DropIndexesRemovedFromProduct=false — the removed index must survive.
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["DropIndexesRemovedFromProduct"] = "false";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.True,
                    "Env DropIndexesRemovedFromProduct=false must preserve IX_CascIdx_Name.");
                Assert.That(IndexExists(cmd, "CascIdx", "PRIMARY"), Is.True,
                    "PRIMARY must still exist.");
            }
            finally
            {
                DropCascadeIndexEnvTables(cmd);
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["DropUnknownIndexes"] = string.Empty;
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["DropIndexesRemovedFromProduct"] = string.Empty;
                if (Directory.Exists(tempDrop)) Directory.Delete(tempDrop, true);
                if (Directory.Exists(tempPreserve)) Directory.Delete(tempPreserve, true);
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
        FactoryContainer.Register(FixtureConfig);
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private static void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private void CopyFixtureTo(string productName, string dest)
    {
        var src = TestHelper.GetTestProductPath(ProductPlatformFolder, productName);
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

    private static void SetTemplateDropFlag(string packageDir, bool value)
    {
        var path = Path.Join(packageDir, "Templates", "Main", "Template.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["DropTablesRemovedFromProduct"] = value;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RemoveSecondaryIndexFromTableJson(string packageDir)
    {
        var path = Path.Join(packageDir, "Templates", "Main", "Tables", "CascIdx.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var indexes = json["Indexes"]!.AsArray();
        // Keep only the PK (first entry); remove the secondary index.
        while (indexes.Count > 1)
            indexes.RemoveAt(indexes.Count - 1);
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private bool TableExists(System.Data.IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private bool IndexExists(System.Data.IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND INDEX_NAME = '{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void DropCascadeTemplateTierTables(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`CascRemovable`;
DROP TABLE IF EXISTS `{_mainDb}`.`CascKeep`;";
        cmd.ExecuteNonQuery();
    }

    private void DropCascadeIndexEnvTables(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`CascIdx`;";
        cmd.ExecuteNonQuery();
    }
}
