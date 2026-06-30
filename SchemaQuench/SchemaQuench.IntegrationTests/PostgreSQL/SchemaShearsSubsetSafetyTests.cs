// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests;
using Schema.Isolators;
using Schema.Utility;
using SchemaShears;
using System;
using System.IO;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

// Verifies that a SchemaShears-emitted patch is subset-safe: a table omitted from the patch
// manifest is NOT dropped when the patch is deployed, because PatchBuilder stamps
// DropTablesRemovedFromProduct: false on every category not listed in AllowDrops.
// Companion test confirms that with AllowDrops = ["Tables"] the drop does happen,
// proving the stamp is precisely what provides the protection.
[Category("PostgreSQL")]
public class SchemaShearsSubsetSafetyTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public SchemaShearsSubsetSafetyTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    [Test]
    public void PatchWithAllowDropsEmpty_ProtectsOmittedTable()
    {
        var tempPatchDir = Path.Join(Path.GetTempPath(), $"ShearsSubset_Protected_{Guid.NewGuid():N}");
        var manifestFile = Path.Join(Path.GetTempPath(), $"ShearsManifest_{Guid.NewGuid():N}.txt");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                var sourcePath = TestHelper.GetTestProductPath("PostgreSQL", "ShearsSource");

                // Deploy the full source product: establishes both tables with product ownership.
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = sourcePath;
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "ShearsKeep"), Is.True, "Setup: ShearsKeep should exist after full deploy.");
                Assert.That(TableExists(cmd, "ShearsRemovable"), Is.True, "Setup: ShearsRemovable should exist after full deploy.");

                // Build a patch that includes only ShearsKeep; AllowDrops is empty so
                // DropTablesRemovedFromProduct is stamped false in the patch Product.json.
                File.WriteAllText(manifestFile, "Templates/Main/Tables/public.ShearsKeep.json");
                new PatchBuilder().Build(new PatchBuildRequest
                {
                    SourcePath = sourcePath,
                    ManifestPath = manifestFile,
                    OutputPath = tempPatchDir,
                    AllowDrops = Array.Empty<string>()
                });

                // Deploy the patch. Ambient env-level DropTablesRemovedFromProduct defaults true
                // (SchemaQuench.settings.json). The patch's stamped false must win.
                _environment.ClearReceivedCalls();
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempPatchDir;
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, "ShearsRemovable"), Is.True,
                        "Patch stamp DropTablesRemovedFromProduct:false must veto the drop: ShearsRemovable must still exist.");
                    Assert.That(TableExists(cmd, "ShearsKeep"), Is.True,
                        "ShearsKeep (in the patch) must still exist.");
                });
            }
            finally
            {
                DropTablesAndCleanup(cmd);
                conn.Close();
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                if (File.Exists(manifestFile)) File.Delete(manifestFile);
                if (Directory.Exists(tempPatchDir)) Directory.Delete(tempPatchDir, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void PatchWithAllowDropsTables_DropsOmittedTable()
    {
        var tempPatchDir = Path.Join(Path.GetTempPath(), $"ShearsSubset_Allowed_{Guid.NewGuid():N}");
        var manifestFile = Path.Join(Path.GetTempPath(), $"ShearsManifest_{Guid.NewGuid():N}.txt");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                var sourcePath = TestHelper.GetTestProductPath("PostgreSQL", "ShearsSource");

                // Deploy the full source product: establishes both tables with product ownership.
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = sourcePath;
                RunSchemaQuench();

                Assert.That(TableExists(cmd, "ShearsKeep"), Is.True, "Setup: ShearsKeep should exist after full deploy.");
                Assert.That(TableExists(cmd, "ShearsRemovable"), Is.True, "Setup: ShearsRemovable should exist after full deploy.");

                // Build a patch with AllowDrops = ["Tables"]: DropTablesRemovedFromProduct is
                // NOT stamped false, so the patch Product.json defaults true → drop executes.
                File.WriteAllText(manifestFile, "Templates/Main/Tables/public.ShearsKeep.json");
                new PatchBuilder().Build(new PatchBuildRequest
                {
                    SourcePath = sourcePath,
                    ManifestPath = manifestFile,
                    OutputPath = tempPatchDir,
                    AllowDrops = new[] { "Tables" }
                });

                // Deploy the patch. DropTablesRemovedFromProduct is not stamped false,
                // so the ambient true (and the patch Product.json default true) wins → drop.
                _environment.ClearReceivedCalls();
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = tempPatchDir;
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, "ShearsRemovable"), Is.False,
                        "AllowDrops=[Tables] leaves DropTablesRemovedFromProduct unstamped (defaults true): ShearsRemovable must be dropped.");
                    Assert.That(TableExists(cmd, "ShearsKeep"), Is.True,
                        "ShearsKeep (in the patch) must still exist.");
                });
            }
            finally
            {
                DropTablesAndCleanup(cmd);
                conn.Close();
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                if (File.Exists(manifestFile)) File.Delete(manifestFile);
                if (Directory.Exists(tempPatchDir)) Directory.Delete(tempPatchDir, true);
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

    private static bool TableExists(System.Data.IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $"SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName}')";
        return (bool)cmd.ExecuteScalar()!;
    }

    private static void DropTablesAndCleanup(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = @"
DROP TABLE IF EXISTS public.""ShearsRemovable"";
DROP TABLE IF EXISTS public.""ShearsKeep"";";
        cmd.ExecuteNonQuery();
    }
}
