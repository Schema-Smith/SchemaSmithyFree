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
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Slice-3 audit Phase 4: <c>--ResumeQuench</c> mid-schema-template-run coverage (design §5.11).
/// Verifies the resume mechanism wires through correctly to schema-template iterations — the
/// checkpoint file path includes the iteration schema name (slice 2 filename change), and the
/// engine recognizes a seeded checkpoint on a per-iteration basis.
/// </summary>
[Category("SqlServer")]
public class SchemaTemplateResumeTests
{
    private const string ProductName = "SchemaTemplateProduct";

    private static readonly string[] DefaultTenants = ["tenant_acme", "tenant_beta", "tenant_globex"];

    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;
    private readonly string _server;
    private string _checkpointDir;

    public SchemaTemplateResumeTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
        _server = config["Target:Server"];
    }

    [SetUp]
    public void SetUp()
    {
        _checkpointDir = Path.Combine(Path.GetTempPath(), $"SchemaQuench_SchemaTemplateResume_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_checkpointDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_checkpointDir))
                Directory.Delete(_checkpointDir, true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Test]
    public void Resume_Recognizes_Per_Tenant_Checkpoint_File_And_Logs_Resuming_From_Checkpoint()
    {
        // Design §5.11: a schema iteration with an existing checkpoint emits the
        // "Resuming from checkpoint" log line. The checkpoint filename includes the iteration
        // schema name in the 5th slot (slice 2). This test seeds a checkpoint for tenant_acme
        // and asserts the engine recognizes it on the per-iteration scope.
        //
        // Scope deliberately narrow: we don't seed every step / script as completed — that's a
        // larger surface that exercises the script-level skip path inside QuenchTemplateScriptsWithCheckpoint
        // and would brittlely couple this test to the exact pipeline shape. The recognized-resume
        // contract (per-iteration filename + scope match) is the slice-3 design surface that
        // needs coverage; the script-level skip is already covered by existing
        // CheckpointIntegrationTests.ShouldResumeFromDatabaseCheckpoint on a regular template.
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);

            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            config["SchemaPackagePath"] = TestHelper.GetTestProductPath("SqlServer", ProductName);
            config["CheckpointDirectory"] = _checkpointDir;

            try
            {
                // Seed a per-iteration checkpoint for tenant_acme. Only KindleForge is marked
                // complete — that step is suppressed via SkipKindlingForge anyway, and the rest
                // of the iteration runs fresh. The recognized "Resuming from checkpoint" log
                // is the assertion we're after.
                var product = Product.Load();
                var checkpointPath = Path.Combine(_checkpointDir,
                    $"{FileNameEncoder.Encode(product.Name)}.{FileNameEncoder.Encode("TenantBody")}.{FileNameEncoder.Encode(_server)}.{FileNameEncoder.Encode(_mainDb)}.{FileNameEncoder.Encode("tenant_acme")}.checkpoint");
                File.WriteAllText(checkpointPath, $@"# SchemaQuench Database Checkpoint
# Product: {product.Name}
# Template: TenantBody
# Server: {_server}
# Database: {_mainDb}
# SchemaName: tenant_acme
# Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

[Completed Steps]
KindleForge

[Before Scripts]

[Object Scripts]

[After Tables Object Scripts]

[Between Tables And Keys Scripts]

[After Table Scripts]

[Table Data Scripts]

[After Scripts]
");

                RunSchemaQuench();

                // The Slice-3 contract: tenant_acme's iteration must recognize the per-tenant
                // checkpoint and emit "Resuming from checkpoint". Other tenants must NOT show
                // a resume line (no checkpoint seeded for them).
                _progressLog.Received(1).Info(Arg.Is<string>(s =>
                    s.Contains("[Schema: tenant_acme]") && s.Contains("Resuming from checkpoint")));

                foreach (var tenant in new[] { "tenant_beta", "tenant_globex" })
                {
                    _progressLog.DidNotReceive().Info(Arg.Is<string>(s =>
                        s.Contains($"[Schema: {tenant}]") && s.Contains("Resuming from checkpoint")));
                }

                // All three tenants still complete successfully — partial checkpoint doesn't
                // strand any iteration. tenant_acme picks up after KindleForge; the others run
                // from scratch.
                foreach (var tenant in DefaultTenants)
                {
                    _progressLog.Received(1).Info(
                        $"[{_server}].[{_mainDb}] [Schema: {tenant}] Successfully Quenched");
                }
            }
            finally
            {
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
                FactoryContainer.Unregister<Schema.Checkpointing.ICheckpointing>();
            }
        }
    }

    // ----- Helpers (shape matches SchemaTemplateHappyPathTests) ---------------------------------

    private void SetupSharedMocks()
    {
        _progressLog.ClearReceivedCalls();
        _errorLog.ClearReceivedCalls();
        _environment.ClearReceivedCalls();
        // CheckpointIntegrationTests sets the resume flag on the mock IEnvironment so
        // CommandLineParser.HasSwitch picks it up. Mirror that here so the engine treats
        // the seeded checkpoint files as "resume from" rather than "stale, ignore".
        _environment.CommandLine.Returns("--SkipKindlingForge --ResumeQuench");
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private void RunSchemaQuench()
    {
        Program.Main(["SkipKindlingForge"]);
    }

    private static void ClearCheckpointsForProduct()
    {
        Schema.Checkpointing.FileCheckpointManager.GetFromFactory().DeleteCheckpoints(ProductName);
    }

    private void ResetTrackingAndCreateTenantSchemas(IEnumerable<string> tenants)
    {
        ClearCheckpointsForProduct();
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @$"
IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.CompletedMigrationScripts WHERE ProductName = '{ProductName}';
IF OBJECT_ID('dbo.SchemaTemplateTenants', 'U') IS NOT NULL DELETE FROM dbo.SchemaTemplateTenants;
IF OBJECT_ID('dbo.Lookup', 'U') IS NOT NULL DELETE FROM dbo.Lookup;
IF OBJECT_ID('dbo.SharedAudit', 'U') IS NOT NULL DELETE FROM dbo.SharedAudit;";
        cmd.ExecuteNonQuery();

        DropTenantSchemasInternal(cmd, tenants);

        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"IF SCHEMA_ID('{tenant}') IS NULL EXEC('CREATE SCHEMA [{tenant}]');";
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = @"
IF OBJECT_ID('dbo.SchemaTemplateTenants', 'U') IS NULL
    CREATE TABLE dbo.SchemaTemplateTenants ([Name] NVARCHAR(128) NOT NULL CONSTRAINT PK_SchemaTemplateTenants PRIMARY KEY);";
        cmd.ExecuteNonQuery();

        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"INSERT INTO dbo.SchemaTemplateTenants ([Name]) VALUES (N'{tenant}');";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }

    private void DropTenantSchemas(IEnumerable<string> tenants)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;
        DropTenantSchemasInternal(cmd, tenants);

        cmd.CommandText = @$"
IF OBJECT_ID('dbo.SharedAudit', 'U') IS NOT NULL DROP TABLE dbo.SharedAudit;
IF OBJECT_ID('dbo.Lookup', 'U') IS NOT NULL DROP TABLE dbo.Lookup;
IF OBJECT_ID('dbo.SchemaTemplateTenants', 'U') IS NOT NULL DROP TABLE dbo.SchemaTemplateTenants;
IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.CompletedMigrationScripts WHERE ProductName = '{ProductName}';";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private static void DropTenantSchemasInternal(IDbCommand cmd, IEnumerable<string> tenants)
    {
        foreach (var tenant in tenants)
        {
            cmd.CommandText = $@"
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + 'ALTER TABLE [' + s.name + '].[' + t.name + '] DROP CONSTRAINT [' + fk.name + '];' + CHAR(10)
  FROM sys.foreign_keys fk
  INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
  WHERE s.name = '{tenant}';
SELECT @sql = @sql + 'DROP VIEW [' + s.name + '].[' + v.name + '];' + CHAR(10)
  FROM sys.views v
  INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
  WHERE s.name = '{tenant}';
SELECT @sql = @sql + 'DROP PROCEDURE [' + s.name + '].[' + o.name + '];' + CHAR(10)
  FROM sys.objects o
  INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
  WHERE s.name = '{tenant}' AND o.type = 'P';
SELECT @sql = @sql + 'DROP TABLE [' + s.name + '].[' + t.name + '];' + CHAR(10)
  FROM sys.tables t
  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
  WHERE s.name = '{tenant}';
IF @sql <> '' EXEC sp_executesql @sql;
IF SCHEMA_ID('{tenant}') IS NOT NULL EXEC('DROP SCHEMA [{tenant}]');";
            cmd.ExecuteNonQuery();
        }
    }
}
