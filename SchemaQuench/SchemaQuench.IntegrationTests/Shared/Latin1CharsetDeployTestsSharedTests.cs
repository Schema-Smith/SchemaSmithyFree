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

namespace SchemaQuench.IntegrationTests.Shared;

// Regression for #359: deploying to a MySQL/MariaDB database whose default character set is latin1
// (MariaDB's stock compiled default) must succeed. The forge procs take stored-procedure VARCHAR
// parameters (p_DatabaseName, p_ProductName) in the TARGET database's charset, so on a latin1 database
// a bare `<param> COLLATE utf8mb4_unicode_ci` was rejected ("COLLATION 'utf8mb4_unicode_ci' is not
// valid for CHARACTER SET 'latin1'"), breaking the first table create. The forge kindles fine (its own
// tracking tables are utf8mb4-explicit); the failure is the reconciliation procs' parameter charset.
public abstract class Latin1CharsetDeployTestsSharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string BaseConnectionString { get; }
    protected abstract Microsoft.Extensions.Configuration.IConfigurationRoot FixtureConfig { get; }
    protected abstract string ProductPlatformFolder { get; }

    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();

    [Test]
    public void Deploy_ToLatin1Database_CreatesTablesAndConstraints_Exit0()
    {
        var latin1Db = "TestLatin1_" + Guid.NewGuid().ToString("N")[..12];
        var tempDir = Path.Join(Path.GetTempPath(), $"Latin1Deploy_{Guid.NewGuid():N}");
        var serverConnectionString = BaseConnectionString + "Database=information_schema;";

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CopyFixtureTo(tempDir);

            using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(serverConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            var config = FactoryContainer.Resolve<Microsoft.Extensions.Configuration.IConfigurationRoot>();
            var savedMainDb = config["ScriptTokens:MainDB"];

            try
            {
                // A latin1 target database — the exact condition that broke the deploy (#359).
                cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{latin1Db}` CHARACTER SET latin1 COLLATE latin1_swedish_ci;";
                cmd.ExecuteNonQuery();
                conn.ChangeDatabase(latin1Db);
                // The forge kindles cleanly even on latin1 (its tables are utf8mb4-explicit).
                ForgeKindler.KindleTheForge(cmd, Platform);

                config["SchemaPackagePath"] = tempDir;
                config["ScriptTokens:MainDB"] = latin1Db;
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                Assert.Multiple(() =>
                {
                    Assert.That(TableExists(cmd, latin1Db, "KeeperTable"), Is.True,
                        "A table must deploy to a latin1 database (#359).");
                    Assert.That(IndexExists(cmd, latin1Db, "KeeperTable", "IX_KeeperTable_Notes"), Is.True,
                        "The table's index must deploy to a latin1 database (#359).");
                    // CHECK constraints require MySQL 8.0.16 — below the floor SchemaSmith degrades them, so verify
                    // the check only where the target stores it (the latin1 table+index coverage still runs on 5.7).
                    if (TestVersionGates.SupportsCheckConstraints(Platform, serverConnectionString))
                        Assert.That(CheckConstraintExists(cmd, latin1Db, "CK_KeeperTable_IdPos"), Is.True,
                            "The table's check constraint must deploy to a latin1 database (#359).");
                });
            }
            finally
            {
                config["ScriptTokens:MainDB"] = savedMainDb;
                config["SchemaPackagePath"] = string.Empty;
                try
                {
                    conn.ChangeDatabase("information_schema");
                    cmd.CommandText = $"DROP DATABASE IF EXISTS `{latin1Db}`;";
                    cmd.ExecuteNonQuery();
                }
                catch { /* best-effort cleanup */ }
                conn.Close();
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
        FactoryContainer.Register(FixtureConfig);
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private static void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private void CopyFixtureTo(string dest)
    {
        var src = TestHelper.GetTestProductPath(ProductPlatformFolder, "StickyPreventDrop");
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

    private static bool TableExists(System.Data.IDbCommand cmd, string db, string tableName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{db}' AND TABLE_NAME = '{tableName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool IndexExists(System.Data.IDbCommand cmd, string db, string tableName, string indexName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = '{db}' AND TABLE_NAME = '{tableName}' AND INDEX_NAME = '{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool CheckConstraintExists(System.Data.IDbCommand cmd, string db, string checkName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.CHECK_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = '{db}' AND CONSTRAINT_NAME = '{checkName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
