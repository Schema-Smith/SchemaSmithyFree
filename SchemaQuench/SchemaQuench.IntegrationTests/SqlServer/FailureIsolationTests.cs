// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests;
using Schema.Isolators;
using Schema.Utility;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Slice-4 failure isolation integration tests (design §5.5 and §10.4).
///
/// <para><b>ContinueOnSchemaFailure tests</b> use SchemaTemplateFailureProduct — a 5-tenant
/// fixture where one tenant ('tenant_boom') triggers a deliberate RAISERROR in Boom.sql.
/// AllowParallel is false so unit execution is deterministic.</para>
///
/// <para><b>ContinueOnDatabaseFailure tests</b> use SchemaTemplateDbFailureProduct — a fixture
/// whose SchemaIdentificationScript returns 'dbo' (a reserved name), causing SchemaDiscovery
/// to throw, which is classified as a DB-level failure. With a single-database fixture we
/// verify the continue / abort routing without requiring a second physical database.</para>
///
/// <para>Abort-mode variants mutate the shared fixture's Template.json for the duration of the
/// test and restore it in finally — no separate *AbortProduct fixture directories needed.</para>
/// </summary>
[Category("SqlServer")]
public class FailureIsolationTests
{
    private const string FailureProductName = "SchemaTemplateFailureProduct";
    private const string DbFailureProductName = "SchemaTemplateDbFailureProduct";

    private static readonly string[] FiveTenants = ["tenant_a", "tenant_b", "tenant_boom", "tenant_c", "tenant_d"];

    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;
    private readonly string _server;

    public FailureIsolationTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
        _server = config["Target:Server"];
    }

    /// <summary>
    /// ContinueOnSchemaFailure: true (default). 5 tenants, one fails (tenant_boom via Boom.sql
    /// RAISERROR). The remaining 4 tenants complete. Exit code is non-zero. Failure summary
    /// names the failing tenant.
    /// </summary>
    [Test]
    public void ContinueOnSchemaFailure_True_OneFailure_OthersComplete_ExitNonZero()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(FiveTenants, FailureProductName);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", FailureProductName);

            try
            {
                RunSchemaQuench();

                // Non-zero exit (failure occurred).
                _environment.Received().Exit(2);

                // Exactly one error: the boom tenant.
                _progressLog.Received().Error(Arg.Is<string>(msg => msg.Contains("tenant_boom")));

                // The 4 non-boom tenants must complete.
                foreach (var tenant in new[] { "tenant_a", "tenant_b", "tenant_c", "tenant_d" })
                    _progressLog.Received(1).Info($"[{_server}].[{_mainDb}] [Schema: {tenant}] Successfully Quenched");

                // The boom tenant must NOT have quenched successfully.
                _progressLog.DidNotReceive().Info($"[{_server}].[{_mainDb}] [Schema: tenant_boom] Successfully Quenched");
            }
            finally
            {
                DropTenantSchemas(FiveTenants, FailureProductName);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    /// <summary>
    /// ContinueOnSchemaFailure: false. After the failure, the dispatcher stops dispatching
    /// new units for this template; in-flight units drain naturally. Subsequent templates
    /// (PostFailure) do not run. Exit invoked.
    /// </summary>
    [Test]
    public void ContinueOnSchemaFailure_False_FailureAbortsTemplate_SubsequentTemplatesDoNotRun()
    {
        var templateJsonPath = Path.Combine(
            TestHelper.GetTestProductPath("SqlServer", FailureProductName),
            "Templates", "TenantBody", "Template.json");
        var originalJson = File.ReadAllText(templateJsonPath);

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(FiveTenants, FailureProductName);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", FailureProductName);

            var modifiedJson = originalJson.Replace(
                "\"AllowParallel\": false,",
                "\"AllowParallel\": false,\r\n    \"ContinueOnSchemaFailure\": false,");
            File.WriteAllText(templateJsonPath, modifiedJson);

            try
            {
                RunSchemaQuench();

                // Exit must have been called (abort).
                _environment.Received().Exit(2);

                // PostFailure template must NOT have been announced in the progress log.
                _progressLog.DidNotReceive().Info(Arg.Is<string>(msg =>
                    msg.Contains("Quenching Template: PostFailure")));
            }
            finally
            {
                File.WriteAllText(templateJsonPath, originalJson);
                DropTenantSchemas(FiveTenants, FailureProductName);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    /// <summary>
    /// Cross-policy: ContinueOnSchemaFailure false + ContinueOnDatabaseFailure true, and the
    /// failure is DB-kind (schema discovery throws). Engine must NOT abort — the DB-level
    /// continue policy governs. The next template (PostFailure) runs. Exit code non-zero.
    /// </summary>
    [Test]
    public void ContinueOnSchemaFailure_False_ContinueOnDatabaseFailure_True_DbFailure_ContinuesToNextTemplate()
    {
        var templateJsonPath = Path.Combine(
            TestHelper.GetTestProductPath("SqlServer", DbFailureProductName),
            "Templates", "TenantBody", "Template.json");
        var originalJson = File.ReadAllText(templateJsonPath);

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ClearCheckpointsForProduct(DbFailureProductName);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", DbFailureProductName);

            // Add ContinueOnSchemaFailure:false while keeping ContinueOnDatabaseFailure at its
            // default (true). The failure is DB-kind so the DB policy governs — should NOT abort.
            var modifiedJson = originalJson.Replace(
                "\"ScriptFolders\": []",
                "\"ContinueOnSchemaFailure\": false,\r\n    \"ScriptFolders\": []");
            File.WriteAllText(templateJsonPath, modifiedJson);

            try
            {
                RunSchemaQuench();

                // Non-zero exit (DB-level failure recorded).
                _environment.Received().Exit(2);

                // PostFailure template DID run — DB continue policy allowed continuation.
                _progressLog.Received(1).Info(Arg.Is<string>(msg =>
                    msg.Contains("Quenching Template: PostFailure")));
            }
            finally
            {
                File.WriteAllText(templateJsonPath, originalJson);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    /// <summary>
    /// Cross-policy: ContinueOnSchemaFailure true + ContinueOnDatabaseFailure false, and the
    /// failure is schema-kind (tenant_boom's Boom.sql RAISERROR). Engine must NOT abort — the
    /// schema-level continue policy governs. The next template (PostFailure) runs. Exit code non-zero.
    /// </summary>
    [Test]
    public void ContinueOnSchemaFailure_True_ContinueOnDatabaseFailure_False_SchemaFailure_ContinuesToNextTemplate()
    {
        var templateJsonPath = Path.Combine(
            TestHelper.GetTestProductPath("SqlServer", FailureProductName),
            "Templates", "TenantBody", "Template.json");
        var originalJson = File.ReadAllText(templateJsonPath);

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(FiveTenants, FailureProductName);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", FailureProductName);

            // Add ContinueOnDatabaseFailure:false while keeping ContinueOnSchemaFailure at its
            // default (true). The failure is schema-kind so the schema policy governs — should NOT abort.
            var modifiedJson = originalJson.Replace(
                "\"AllowParallel\": false,",
                "\"AllowParallel\": false,\r\n    \"ContinueOnDatabaseFailure\": false,");
            File.WriteAllText(templateJsonPath, modifiedJson);

            try
            {
                RunSchemaQuench();

                // Non-zero exit (schema-level failure recorded).
                _environment.Received().Exit(2);

                // PostFailure template DID run — schema continue policy allowed continuation.
                _progressLog.Received(1).Info(Arg.Is<string>(msg =>
                    msg.Contains("Quenching Template: PostFailure")));
            }
            finally
            {
                File.WriteAllText(templateJsonPath, originalJson);
                DropTenantSchemas(FiveTenants, FailureProductName);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    /// <summary>
    /// ContinueOnDatabaseFailure: true (default). SchemaIdentificationScript returns a reserved
    /// name ('dbo'), causing SchemaDiscovery to throw (a DB-level failure). The run logs the
    /// error but continues to the PostFailure template. Exit code is non-zero.
    /// </summary>
    [Test]
    public void ContinueOnDatabaseFailure_True_DbDiscoveryFails_ContinuesToNextTemplate()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ClearCheckpointsForProduct(DbFailureProductName);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", DbFailureProductName);

            try
            {
                RunSchemaQuench();

                // Exit code non-zero (DB-level failure).
                _environment.Received().Exit(2);

                // Error logged for the discovery failure.
                _progressLog.Received().Error(Arg.Is<string>(msg =>
                    msg.Contains("Schema discovery FAILED") || msg.Contains("TenantBody")));

                // PostFailure template DID run (continue mode keeps going after DB failure).
                _progressLog.Received(1).Info(Arg.Is<string>(msg =>
                    msg.Contains("Quenching Template: PostFailure")));
            }
            finally
            {
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    /// <summary>
    /// ContinueOnDatabaseFailure: false. DB-level discovery failure aborts the product run.
    /// PostFailure template does NOT run.
    /// </summary>
    [Test]
    public void ContinueOnDatabaseFailure_False_DbDiscoveryFails_AbortsRun()
    {
        var templateJsonPath = Path.Combine(
            TestHelper.GetTestProductPath("SqlServer", DbFailureProductName),
            "Templates", "TenantBody", "Template.json");
        var originalJson = File.ReadAllText(templateJsonPath);

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ClearCheckpointsForProduct(DbFailureProductName);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", DbFailureProductName);

            var modifiedJson = originalJson.Replace(
                "\"ScriptFolders\": []",
                "\"ContinueOnDatabaseFailure\": false,\r\n    \"ScriptFolders\": []");
            File.WriteAllText(templateJsonPath, modifiedJson);

            try
            {
                RunSchemaQuench();

                // Exit must have been called.
                _environment.Received().Exit(2);

                // PostFailure template must NOT have run.
                _progressLog.DidNotReceive().Info(Arg.Is<string>(msg =>
                    msg.Contains("Quenching Template: PostFailure")));
            }
            finally
            {
                File.WriteAllText(templateJsonPath, originalJson);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    // ----- Helpers ---------------------------------------------------------------------------

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

    private static void ClearCheckpointsForProduct(string productName)
    {
        Schema.Checkpointing.FileCheckpointManager.GetFromFactory().DeleteCheckpoints(productName);
    }

    private void ResetTrackingAndCreateTenantSchemas(IEnumerable<string> tenants, string productName)
    {
        ClearCheckpointsForProduct(productName);
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = $@"
IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.CompletedMigrationScripts WHERE ProductName = '{productName}';
IF OBJECT_ID('SchemaSmith.ProductOwnership', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.ProductOwnership WHERE ProductName = '{productName}';
IF OBJECT_ID('dbo.Tenants', 'U') IS NOT NULL DELETE FROM dbo.Tenants;";
        cmd.ExecuteNonQuery();

        DropTenantSchemasInternal(cmd, FiveTenants);

        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"IF SCHEMA_ID('{tenant}') IS NULL EXEC('CREATE SCHEMA [{tenant}]');";
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = @"
IF OBJECT_ID('dbo.Tenants', 'U') IS NULL
    CREATE TABLE dbo.Tenants ([Name] NVARCHAR(128) NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY);";
        cmd.ExecuteNonQuery();

        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"INSERT INTO dbo.Tenants ([Name]) VALUES (N'{tenant}');";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }

    private void DropTenantSchemas(IEnumerable<string> tenants, string productName)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;
        DropTenantSchemasInternal(cmd, tenants);

        cmd.CommandText = $@"
IF OBJECT_ID('dbo.Tenants', 'U') IS NOT NULL DROP TABLE dbo.Tenants;
IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.CompletedMigrationScripts WHERE ProductName = '{productName}';
IF OBJECT_ID('SchemaSmith.ProductOwnership', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.ProductOwnership WHERE ProductName = '{productName}';";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private static void DropTenantSchemasInternal(IDbCommand cmd, IEnumerable<string> tenants)
    {
        foreach (var tenant in tenants)
        {
            cmd.CommandText = $@"
DECLARE @sql NVARCHAR(MAX) = N'';
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
