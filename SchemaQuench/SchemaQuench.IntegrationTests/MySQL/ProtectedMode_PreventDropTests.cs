// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
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

namespace SchemaQuench.IntegrationTests.MySQL;

// Verifies the environment-level no-drop protection tier (env PreventDrop). When on, the environment
// never drops an object for being ABSENT from the product: an unprotected table removed from the
// package survives (unlike the sticky per-table case, nothing here is individually marked), the run
// completes normally (exit 0, no abort), and the suppressed drop is itemized in the deployment
// summary's PreventDrop manifest.
[Category("MySQL")]
public class ProtectedMode_PreventDropTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public ProtectedMode_PreventDropTests()
    {
        FixtureSetup.EnsureInitialized();
        _connectionString = FixtureSetup.ConnectionString + "Database=information_schema;";
        _mainDb = FixtureSetup.MainDb;
    }

    [Test]
    public void ProtectedMode_RemovedTable_NotDropped_ManifestLists_Exit0()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_Skip_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.That(TableExists(cmd, "RemovableTable"), Is.True, "Setup: RemovableTable should exist.");

                File.Delete(Path.Join(tempDir, "Templates", "Main", "Tables", "RemovableTable.json"));

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = "true";
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

                var manifest = ReadWouldDropNames();
                Assert.That(manifest.Any(n => n.Contains("RemovableTable")), Is.True,
                    $"PreventDrop manifest must list the suppressed RemovableTable drop. Manifest: [{string.Join(", ", manifest)}]");
            }
            finally
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd);
                conn.Close();
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
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

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = "true";
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
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd);
                conn.Close();
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void ProtectedMode_RemovedColumn_NotDropped_ManifestLists_Exit0()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_Col_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.That(ColumnExists(cmd, "KeeperTable", "Notes"), Is.True, "Setup: KeeperTable.Notes should exist.");

                RemoveColumnFromTable(Path.Join(tempDir, "Templates", "Main", "Tables", "KeeperTable.json"), "`Notes`");

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.That(ColumnExists(cmd, "KeeperTable", "Notes"), Is.True,
                    "Protected environment must NOT drop the Notes column for being absent from the product.");

                var manifest = ReadWouldDropNames();
                Assert.That(manifest.Any(n => n.Contains("Notes")), Is.True,
                    $"PreventDrop manifest must list the suppressed Notes column drop. Manifest: [{string.Join(", ", manifest)}]");
            }
            finally
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd);
                conn.Close();
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void ProtectedMode_RemovedCheckConstraint_NotDropped_ManifestLists_Exit0()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_Chk_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);

            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.That(CheckConstraintExists(cmd, "CK_KeeperTable_Id"), Is.True, "Setup: check constraint should exist.");

                RemoveCheckConstraints(Path.Join(tempDir, "Templates", "Main", "Tables", "KeeperTable.json"));

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                var manifest = ReadWouldDropNames();
                Assert.Multiple(() =>
                {
                    Assert.That(CheckConstraintExists(cmd, "CK_KeeperTable_Id"), Is.True,
                        "Protected environment must NOT drop the table-level check for being absent from the product.");
                    Assert.That(manifest.Any(n => n.Contains("CK_KeeperTable_Id")), Is.True,
                        $"Manifest must list the suppressed table-level check (via the session-var mechanism). Manifest: [{string.Join(", ", manifest)}]");
                    Assert.That(CheckConstraintExists(cmd, "CK_KeeperTable_IdPos"), Is.True,
                        "Protected environment must NOT drop a single-column NAMED check for being absent from the product.");
                    Assert.That(manifest.Any(n => n.Contains("CK_KeeperTable_IdPos")), Is.True,
                        $"Manifest must list the suppressed single-column named check. Manifest: [{string.Join(", ", manifest)}]");
                });
            }
            finally
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd);
                conn.Close();
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void ProtectedMode_RemovedIndex_NotDropped_ManifestLists_Exit0()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_Idx_{Guid.NewGuid():N}");
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand(); cmd.CommandTimeout = 300;
            try
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.That(IndexExists(cmd, "KeeperTable", "IX_KeeperTable_Notes"), Is.True, "Setup: index should exist.");

                RemoveIndexFromTable(Path.Join(tempDir, "Templates", "Main", "Tables", "KeeperTable.json"), "IX_KeeperTable_Notes");

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.That(IndexExists(cmd, "KeeperTable", "IX_KeeperTable_Notes"), Is.True,
                    "Protected environment must NOT drop the index for being absent from the product.");
                var manifest = ReadWouldDropNames();
                Assert.That(manifest.Any(n => n.Contains("IX_KeeperTable_Notes")), Is.True,
                    $"PreventDrop manifest must list the suppressed index drop. Manifest: [{string.Join(", ", manifest)}]");
            }
            finally
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd); conn.Close();
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true); LogFactory.Clear(); FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void ProtectedMode_ModifiedIndex_IsRecreated_NotInManifest()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_IdxMod_{Guid.NewGuid():N}");
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand(); cmd.CommandTimeout = 300;
            try
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.That(IndexExists(cmd, "KeeperTable", "IX_KeeperTable_Notes"), Is.True, "Setup: index should exist.");

                ModifyIndexColumns(Path.Join(tempDir, "Templates", "Main", "Tables", "KeeperTable.json"), "IX_KeeperTable_Notes", "`Id`");

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.Multiple(() =>
                {
                    Assert.That(IndexExists(cmd, "KeeperTable", "IX_KeeperTable_Notes"), Is.True,
                        "A modified index must still be recreated under protection (transient drop-recreate is allowed).");
                    Assert.That(ReadWouldDropNames().Any(n => n.Contains("IX_KeeperTable_Notes")), Is.False,
                        "A MODIFIED (for-change) index drop must NOT appear in the PreventDrop manifest — only by-absence drops do.");
                });
            }
            finally
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd); conn.Close();
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true); LogFactory.Clear(); FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    private static void RemoveIndexFromTable(string tableJsonPath, string indexName)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        ((JArray)root["Indexes"]!).First(i => (string)i["Name"]! == indexName).Remove();
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private static void ModifyIndexColumns(string tableJsonPath, string indexName, string newColumns)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        ((JArray)root["Indexes"]!).First(i => (string)i["Name"]! == indexName)["IndexColumns"] = newColumns;
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private bool IndexExists(System.Data.IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND INDEX_NAME = '{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    [Test]
    public void ProtectedMode_ModifiedCheckConstraint_IsRecreated_NotInManifest()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"ProtMode_ChkMod_{Guid.NewGuid():N}");
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand(); cmd.CommandTimeout = 300;
            try
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = tempDir;
                RunSchemaQuench();
                Assert.That(CheckConstraintExists(cmd, "CK_KeeperTable_IdPos"), Is.True, "Setup: single-column check should exist.");

                ModifyCheckExpression(Path.Join(tempDir, "Templates", "Main", "Tables", "KeeperTable.json"), "CK_KeeperTable_IdPos", "`Id` >= 0");

                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = "true";
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.Multiple(() =>
                {
                    Assert.That(CheckConstraintExists(cmd, "CK_KeeperTable_IdPos"), Is.True,
                        "A modified check must still be recreated under protection (transient drop-recreate is allowed).");
                    Assert.That(ReadWouldDropNames().Any(n => n.Contains("CK_KeeperTable_IdPos")), Is.False,
                        "A MODIFIED check must NOT appear in the manifest — only by-absence removals do.");
                });
            }
            finally
            {
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["PreventDrop"] = string.Empty;
                DropTablesAndCleanup(cmd); conn.Close();
                FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>()["SchemaPackagePath"] = string.Empty;
                Directory.Delete(tempDir, true); LogFactory.Clear(); FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    private static void RemoveCheckConstraints(string tableJsonPath)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        root.Remove("CheckConstraints");
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private static void ModifyCheckExpression(string tableJsonPath, string checkName, string newExpression)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        ((JArray)root["CheckConstraints"]!).First(c => (string)c["Name"]! == checkName)["Expression"] = newExpression;
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private bool CheckConstraintExists(System.Data.IDbCommand cmd, string checkName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.CHECK_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = '{_mainDb}' AND CONSTRAINT_NAME = '{checkName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static void RemoveColumnFromTable(string tableJsonPath, string columnName)
    {
        var root = JObject.Parse(File.ReadAllText(tableJsonPath));
        var columns = (JArray)root["Columns"]!;
        columns.First(c => (string)c["Name"]! == columnName).Remove();
        File.WriteAllText(tableJsonPath, root.ToString());
    }

    private bool ColumnExists(System.Data.IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
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
        var src = TestHelper.GetTestProductPath("MySQL", "StickyPreventDrop");
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

    private bool TableExists(System.Data.IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void DropTablesAndCleanup(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`RemovableTable`;
DROP TABLE IF EXISTS `{_mainDb}`.`ProtectedTable`;
DROP TABLE IF EXISTS `{_mainDb}`.`KeeperTable`;";
        cmd.ExecuteNonQuery();
    }
}
