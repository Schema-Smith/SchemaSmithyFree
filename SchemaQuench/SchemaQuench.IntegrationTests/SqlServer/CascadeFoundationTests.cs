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

// Verifies the template-tier cascade for DropTablesRemovedFromProduct and the env-config
// DropUnknownIndexes retrofit (env-level override that was previously package-only).
[Category("SqlServer")]
public class CascadeFoundationTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public CascadeFoundationTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master", config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    // Template.json carries DropTablesRemovedFromProduct:false → absent table in that template survives
    // even though the env-level flag is true and Product.json is silent (defaults true).
    [Test]
    public void TemplateTierFalse_VetoesDropEvenWhenEnvAndProductAllowDrop()
    {
        var tempSuppressed = Path.Combine(Path.GetTempPath(), $"CascTpl_Suppress_{Guid.NewGuid():N}");
        var tempEnabled = Path.Combine(Path.GetTempPath(), $"CascTpl_Enable_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // --- Part A: template-false vetoes the drop ---
                CopyFixtureTo("CascadeTemplateTier", tempSuppressed);

                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempSuppressed;
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "CascKeep"), Is.True, "Setup A: CascKeep should exist.");
                Assert.That(TableExists(cmd, "CascRemovable"), Is.True, "Setup A: CascRemovable should exist.");

                File.Delete(Path.Combine(tempSuppressed, "Templates", "Main", "Tables", "dbo.CascRemovable.json"));

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

                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempEnabled;
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "CascKeep"), Is.True, "Setup B: CascKeep should exist.");
                Assert.That(TableExists(cmd, "CascRemovable"), Is.True, "Setup B: CascRemovable should exist.");

                File.Delete(Path.Combine(tempEnabled, "Templates", "Main", "Tables", "dbo.CascRemovable.json"));

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
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                if (Directory.Exists(tempSuppressed)) Directory.Delete(tempSuppressed, true);
                if (Directory.Exists(tempEnabled)) Directory.Delete(tempEnabled, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    // DropUnknownIndexes is now env-config-overridable. The two scenarios:
    // A) env true  → an out-of-band index (never stamped in product) is dropped.
    // B) env absent → the same out-of-band index survives.
    // "Out-of-band" = created directly via SQL, never delivered through the product package,
    // so it is never stamped with a ProductName extended property; it is unknown to SchemaSmith.
    [Test]
    public void EnvDropUnknownIndexes_TrueDropsIndex_UnsetPreservesIndex()
    {
        var tempDrop = Path.Combine(Path.GetTempPath(), $"CascIdx_Drop_{Guid.NewGuid():N}");
        var tempPreserve = Path.Combine(Path.GetTempPath(), $"CascIdx_Preserve_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // --- Part A: env DropUnknownIndexes=true → out-of-band index is dropped ---
                // Deploy the minimal fixture (only PK, no secondary index).
                CopyFixtureTo("CascadeIndexEnv", tempDrop);
                RemoveSecondaryIndexFromTableJson(tempDrop);

                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempDrop;
                RunSchemaQuench();

                Assert.That(IndexExists(cmd, "CascIdx", "PK_CascIdx"), Is.True, "Setup A: PK_CascIdx should exist.");
                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.False, "Setup A: IX_CascIdx_Name not yet created.");

                // Add the secondary index directly — it is NOT part of the product definition.
                cmd.CommandText = "CREATE NONCLUSTERED INDEX [IX_CascIdx_Name] ON [dbo].[CascIdx] ([Name]);";
                cmd.ExecuteNonQuery();
                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.True, "Setup A: out-of-band IX_CascIdx_Name must exist before re-quench.");

                // Re-quench with env DropUnknownIndexes=true — the out-of-band index should be dropped.
                FactoryContainer.Resolve<IConfigurationRoot>()["DropUnknownIndexes"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.False,
                    "Env DropUnknownIndexes=true must drop the out-of-band index IX_CascIdx_Name.");
                Assert.That(IndexExists(cmd, "CascIdx", "PK_CascIdx"), Is.True,
                    "PK_CascIdx must not be dropped.");

                DropCascadeIndexEnvTables(cmd);

                // --- Part B: env DropUnknownIndexes absent → out-of-band index survives ---
                CopyFixtureTo("CascadeIndexEnv", tempPreserve);
                RemoveSecondaryIndexFromTableJson(tempPreserve);

                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempPreserve;
                FactoryContainer.Resolve<IConfigurationRoot>()["DropUnknownIndexes"] = string.Empty;

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                Assert.That(IndexExists(cmd, "CascIdx", "PK_CascIdx"), Is.True, "Setup B: PK_CascIdx should exist.");

                // Add the secondary index directly again — out-of-band, not in product.
                cmd.CommandText = "CREATE NONCLUSTERED INDEX [IX_CascIdx_Name] ON [dbo].[CascIdx] ([Name]);";
                cmd.ExecuteNonQuery();
                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.True, "Setup B: out-of-band IX_CascIdx_Name must exist before re-quench.");

                // Re-quench with env DropUnknownIndexes absent — the out-of-band index must survive.
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(IndexExists(cmd, "CascIdx", "IX_CascIdx_Name"), Is.True,
                    "Env DropUnknownIndexes absent must preserve the out-of-band index IX_CascIdx_Name.");
                Assert.That(IndexExists(cmd, "CascIdx", "PK_CascIdx"), Is.True,
                    "PK_CascIdx must still exist.");
            }
            finally
            {
                DropCascadeIndexEnvTables(cmd);
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                FactoryContainer.Resolve<IConfigurationRoot>()["DropUnknownIndexes"] = string.Empty;
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
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private static void CopyFixtureTo(string productName, string dest)
    {
        var src = TestHelper.GetTestProductPath("SqlServer", productName);
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

    private static void SetTemplateDropFlag(string packageDir, bool value)
    {
        var path = Path.Combine(packageDir, "Templates", "Main", "Template.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["DropTablesRemovedFromProduct"] = value;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RemoveSecondaryIndexFromTableJson(string packageDir)
    {
        var path = Path.Combine(packageDir, "Templates", "Main", "Tables", "dbo.CascIdx.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var indexes = json["Indexes"]!.AsArray();
        // Keep only the PK (first entry); remove the secondary index.
        while (indexes.Count > 1)
            indexes.RemoveAt(indexes.Count - 1);
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool TableExists(System.Data.IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $"SELECT CASE WHEN OBJECT_ID('dbo.{tableName}') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static bool IndexExists(System.Data.IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*)
FROM sys.indexes i
JOIN sys.objects o ON i.object_id = o.object_id
WHERE o.name = '{tableName}' AND i.name = '{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static void DropCascadeTemplateTierTables(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[CascRemovable];
DROP TABLE IF EXISTS [dbo].[CascKeep];";
        cmd.ExecuteNonQuery();
    }

    private static void DropCascadeIndexEnvTables(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = "DROP TABLE IF EXISTS [dbo].[CascIdx];";
        cmd.ExecuteNonQuery();
    }
}
