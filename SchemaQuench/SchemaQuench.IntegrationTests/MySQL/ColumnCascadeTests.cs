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

namespace SchemaQuench.IntegrationTests.MySQL;

// Verifies the C#-resolved cascade tiers (env/product/template) for DropColumnsRemovedFromProduct
// end-to-end through ProductQuench, plus the AND-composition rule (a table-level true cannot
// re-enable a higher-tier false). Structural template: CascadeFoundationTests.
[Category("MySQL")]
public class ColumnCascadeTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public ColumnCascadeTests()
    {
        FixtureSetup.EnsureInitialized();
        _connectionString = FixtureSetup.ConnectionString + "Database=information_schema;";
        _mainDb = FixtureSetup.MainDb;
    }

    // Env-tier DropColumnsRemovedFromProduct:
    // Part A — env=false → OrphanCol survives after being removed from the table JSON.
    // Part B — env absent (default true) → OrphanCol is dropped.
    [Test]
    public void EnvTier_FalseVetoesColumnDrop_AbsentAllowsDrop()
    {
        var tempSuppressed = Path.Join(Path.GetTempPath(), $"CascCol_EnvFalse_{Guid.NewGuid():N}");
        var tempEnabled = Path.Join(Path.GetTempPath(), $"CascCol_EnvAbsent_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // --- Part A: env false → OrphanCol survives ---
                CopyFixtureTo("CascadeColumnTier", tempSuppressed);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempSuppressed;
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["DropColumnsRemovedFromProduct"] = "false";
                RunSchemaQuench();

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True, "Setup A: OrphanCol should exist after initial quench.");
                Assert.That(ColumnExists(cmd, "KeepCol"), Is.True, "Setup A: KeepCol should exist after initial quench.");

                RemoveOrphanColFromTableJson(tempSuppressed);

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True,
                        "Env-tier false must veto the column drop: OrphanCol must survive.");
                    Assert.That(ColumnExists(cmd, "KeepCol"), Is.True,
                        "KeepCol must still exist.");
                });

                DropCascColTable(cmd);

                // --- Part B: env absent (default true) → OrphanCol is dropped ---
                CopyFixtureTo("CascadeColumnTier", tempEnabled);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempEnabled;
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["DropColumnsRemovedFromProduct"] = string.Empty;
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True, "Setup B: OrphanCol should exist after initial quench.");

                RemoveOrphanColFromTableJson(tempEnabled);

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.False,
                    "Env absent (default true) must drop OrphanCol.");
                Assert.That(ColumnExists(cmd, "KeepCol"), Is.True,
                    "KeepCol must still exist after Part B drop.");
            }
            finally
            {
                DropCascColTable(cmd);
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["DropColumnsRemovedFromProduct"] = string.Empty;
                if (Directory.Exists(tempSuppressed)) Directory.Delete(tempSuppressed, true);
                if (Directory.Exists(tempEnabled)) Directory.Delete(tempEnabled, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    // Product-tier DropColumnsRemovedFromProduct:
    // Part A — Product.json false (env absent) → OrphanCol survives.
    // Part B — Product.json absent (env absent, default true) → OrphanCol is dropped.
    [Test]
    public void ProductTier_FalseVetoesColumnDrop_AbsentAllowsDrop()
    {
        var tempSuppressed = Path.Join(Path.GetTempPath(), $"CascCol_ProdFalse_{Guid.NewGuid():N}");
        var tempEnabled = Path.Join(Path.GetTempPath(), $"CascCol_ProdAbsent_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                // --- Part A: product false → OrphanCol survives ---
                CopyFixtureTo("CascadeColumnTier", tempSuppressed);
                SetProductColumnDropFlag(tempSuppressed, false);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempSuppressed;
                RunSchemaQuench();

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True, "Setup A: OrphanCol should exist after initial quench.");

                RemoveOrphanColFromTableJson(tempSuppressed);

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True,
                    "Product-tier false must veto the column drop: OrphanCol must survive.");
                Assert.That(ColumnExists(cmd, "KeepCol"), Is.True,
                    "KeepCol must still exist.");

                DropCascColTable(cmd);

                // --- Part B: product absent (env also absent, default true) → OrphanCol is dropped ---
                CopyFixtureTo("CascadeColumnTier", tempEnabled);
                // No product flag set — fixture Product.json has no DropColumnsRemovedFromProduct

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempEnabled;
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True, "Setup B: OrphanCol should exist after initial quench.");

                RemoveOrphanColFromTableJson(tempEnabled);

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.False,
                    "Product absent + env absent (default true) must drop OrphanCol.");
                Assert.That(ColumnExists(cmd, "KeepCol"), Is.True,
                    "KeepCol must still exist after Part B drop.");
            }
            finally
            {
                DropCascColTable(cmd);
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                if (Directory.Exists(tempSuppressed)) Directory.Delete(tempSuppressed, true);
                if (Directory.Exists(tempEnabled)) Directory.Delete(tempEnabled, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    // Template-tier DropColumnsRemovedFromProduct:
    // Template.json false (product + env absent) → OrphanCol survives.
    [Test]
    public void TemplateTier_FalseVetoesColumnDrop()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"CascCol_TplFalse_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                CopyFixtureTo("CascadeColumnTier", tempDir);
                SetTemplateColumnDropFlag(tempDir, false);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True, "Setup: OrphanCol should exist after initial quench.");
                Assert.That(ColumnExists(cmd, "KeepCol"), Is.True, "Setup: KeepCol should exist after initial quench.");

                RemoveOrphanColFromTableJson(tempDir);

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True,
                        "Template-tier false must veto the column drop: OrphanCol must survive.");
                    Assert.That(ColumnExists(cmd, "KeepCol"), Is.True,
                        "KeepCol must still exist.");
                });
            }
            finally
            {
                DropCascColTable(cmd);
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    // AND-composition: a table-level true CANNOT re-enable a higher-tier false.
    // Product.json DropColumnsRemovedFromProduct:false AND table JSON DropColumnsRemovedFromProduct:true
    // → OrphanCol SURVIVES (the cascade is AND-composed, not OR).
    [Test]
    public void Composition_TableTrueCannot_ReenableHigherTierFalse()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"CascCol_Compose_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                CopyFixtureTo("CascadeColumnTier", tempDir);
                // Higher-tier (product) says false — suppress drops
                SetProductColumnDropFlag(tempDir, false);
                // Table-level says true — attempts to re-enable drops
                SetTableColumnDropFlag(tempDir, true);

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();

                Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True, "Setup: OrphanCol should exist after initial quench.");
                Assert.That(ColumnExists(cmd, "KeepCol"), Is.True, "Setup: KeepCol should exist after initial quench.");

                RemoveOrphanColFromTableJson(tempDir);

                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(ColumnExists(cmd, "OrphanCol"), Is.True,
                        "AND-composition: product-false vetoes the drop even when table-level is true.");
                    Assert.That(ColumnExists(cmd, "KeepCol"), Is.True,
                        "KeepCol must still exist.");
                });
            }
            finally
            {
                DropCascColTable(cmd);
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
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
        FactoryContainer.Register(FixtureSetup.Config);
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private static void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private static void CopyFixtureTo(string productName, string dest)
    {
        var src = TestHelper.GetTestProductPath("MySQL", productName);
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

    private static void SetProductColumnDropFlag(string packageDir, bool value)
    {
        var path = Path.Join(packageDir, "Product.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["DropColumnsRemovedFromProduct"] = value;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetTemplateColumnDropFlag(string packageDir, bool value)
    {
        var path = Path.Join(packageDir, "Templates", "Main", "Template.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["DropColumnsRemovedFromProduct"] = value;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetTableColumnDropFlag(string packageDir, bool value)
    {
        var path = Path.Join(packageDir, "Templates", "Main", "Tables", "CascColTable.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["DropColumnsRemovedFromProduct"] = value;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RemoveOrphanColFromTableJson(string packageDir)
    {
        var path = Path.Join(packageDir, "Templates", "Main", "Tables", "CascColTable.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var columns = json["Columns"]!.AsArray();
        for (var i = columns.Count - 1; i >= 0; i--)
        {
            if (columns[i]?["Name"]?.GetValue<string>() == "`OrphanCol`")
            {
                columns.RemoveAt(i);
                break;
            }
        }
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private bool ColumnExists(System.Data.IDbCommand cmd, string columnName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = 'CascColTable' AND COLUMN_NAME = '{columnName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void DropCascColTable(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`CascColTable`;";
        cmd.ExecuteNonQuery();
    }
}
