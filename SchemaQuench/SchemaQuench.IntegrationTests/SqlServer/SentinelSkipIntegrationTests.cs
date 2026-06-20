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
using System.Data;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Integration tests verifying the per-script "should not apply" sentinel skip on SQL Server.
/// Three scenarios per the feature design: run-once migration sentinel-skips and is recorded as
/// completed (no re-run), object script sentinel-skips without failing the run, and a
/// non-sentinel error still fails the run (control).
/// </summary>
[Category("SqlServer")]
public class SentinelSkipIntegrationTests
{
    private const string ProductName = "SentinelSkipProduct";

    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;
    private readonly string _server;

    public SentinelSkipIntegrationTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
        _server = config["Target:Server"];
    }

    /// <summary>
    /// A run-once migration that raises the sentinel is treated as success AND recorded as
    /// completed in CompletedMigrationScripts. A second quench does NOT re-execute it —
    /// the marker-table row count stays at 1.
    /// </summary>
    [Test]
    public void RunOnceMigration_SentinelSkip_IsRecordedAsCompleted_AndNotReRunOnSecondQuench()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CleanupSentinelState();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                // Quench 1: migration script runs, raises sentinel, should be recorded as done.
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Skipped (ShouldNotApply)") && s.Contains("SentinelMigration.sql")));

                // Tracking row must exist — sentinel skip is a success, so it's recorded.
                Assert.That(CountCompletedMigrationRows("MigrationScripts/Before/SentinelMigration.sql"), Is.EqualTo(1),
                    "SentinelMigration.sql must be recorded in CompletedMigrationScripts after sentinel skip.");

                // Pre-sentinel INSERT committed before the sentinel was raised.
                Assert.That(CountMarkerRows(), Is.EqualTo(1),
                    "INSERT before the sentinel must have committed on the first quench.");

                // Quench 2: migration is already recorded, must be skipped entirely.
                _progressLog.ClearReceivedCalls();
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                // Marker row count must not increase — the migration did not re-execute.
                Assert.That(CountMarkerRows(), Is.EqualTo(1),
                    "Marker row count must not increase on second quench — migration must not re-execute.");
            }
            finally
            {
                CleanupSentinelState();
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    /// <summary>
    /// An object script (stored procedure) that raises the sentinel is skipped and the run
    /// still succeeds — no exit(2), "Skipped (ShouldNotApply)" logged.
    /// </summary>
    [Test]
    public void ObjectScript_SentinelSkip_DoesNotFailRun()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            CleanupSentinelState();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                RunSchemaQuench();

                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);
                _progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Skipped (ShouldNotApply)") && s.Contains("dbo.SentinelProc.sql")));
            }
            finally
            {
                CleanupSentinelState();
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    /// <summary>
    /// Control: a script raising a non-sentinel error still fails the run. Sentinel skip must
    /// not turn all errors into skips.
    /// </summary>
    [Test]
    public void NonSentinelError_StillFailsRun()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", "BeforeTemplateScriptError");

            RunSchemaQuench();

            _environment.Received(1).Exit(2);
            _progressLog.Received().Error(Arg.Is<string>(s => s.Contains("KABOOM!")));

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
        }
    }

    // ----- Helpers -------------------------------------------------------------------------------

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

    private void CleanupSentinelState()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
IF OBJECT_ID('dbo.SentinelMarker', 'U') IS NOT NULL DROP TABLE dbo.SentinelMarker;
IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.CompletedMigrationScripts WHERE ProductName = '{ProductName}';";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private int CountCompletedMigrationRows(string scriptPath)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM SchemaSmith.CompletedMigrationScripts WITH (NOLOCK) WHERE ProductName = '{ProductName}' AND ScriptPath = '{scriptPath}'";
        var result = Convert.ToInt32(cmd.ExecuteScalar());
        conn.Close();
        return result;
    }

    private int CountMarkerRows()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.SentinelMarker WITH (NOLOCK)";
        var result = Convert.ToInt32(cmd.ExecuteScalar());
        conn.Close();
        return result;
    }
}
