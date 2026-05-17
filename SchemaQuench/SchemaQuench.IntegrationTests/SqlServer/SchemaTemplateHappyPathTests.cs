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
using System.Linq;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Slice-3 schema-template integration tests (design §10.4). Verifies the engine
/// core end-to-end against live SQL Server: multi-tenant happy path, TemplateOrder
/// enforcement, reserved-name discovery rejection, per-iteration token resolution,
/// migration tracking per (template, schema), and cross-schema FK references.
///
/// <para>Fixtures pre-create the tenant schemas — slice 4 wires up
/// <c>CreateSchemaIfMissing</c>. Failure-isolation modes (<c>ContinueOnSchemaFailure</c> /
/// <c>ContinueOnDatabaseFailure</c>), tenant lifecycle (onboarding / offboarding), and
/// selective execution scope (<c>Target.*</c>) are deferred to later slices and out of
/// scope here.</para>
/// </summary>
[Category("SqlServer")]
public class SchemaTemplateHappyPathTests
{
    private const string ProductName = "SchemaTemplateProduct";
    private const string TenantBodyTemplate = "TenantBody";
    private const string SharedTemplate = "Shared";

    private static readonly string[] DefaultTenants = ["tenant_acme", "tenant_beta", "tenant_globex"];

    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;
    private readonly string _server;

    public SchemaTemplateHappyPathTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
        _server = config["Target:Server"];
    }

    [Test]
    public void Happy_Path_Multi_Tenant_Deploy_Creates_Identical_Per_Tenant_Structure_And_Shared_Content_Once()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                RunSchemaQuench();

                // No errors; non-zero-exit not invoked.
                _progressLog.DidNotReceive().Error(Arg.Any<string>());
                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                _progressLog.Received(1).Info($"Completed quench of {ProductName}");
                _progressLog.Received(1).Info($"[{_server}].[{_mainDb}] Successfully Quenched");
                foreach (var tenant in DefaultTenants)
                    _progressLog.Received(1).Info($"[{_server}].[{_mainDb}] [Schema: {tenant}] Successfully Quenched");

                // Shared content present once in dbo: Tenants, Lookup, SharedAudit + seed row.
                AssertTableExists("dbo", "Tenants");
                AssertTableExists("dbo", "Lookup");
                AssertTableExists("dbo", "SharedAudit");
                Assert.That(ScalarCount("SELECT COUNT(*) FROM dbo.Lookup WHERE LookupID = 1 AND Code = N'ALPHA'"),
                    Is.EqualTo(1), "Shared SeedLookup migration should have inserted exactly one row.");

                // Per-tenant structure: Customers + Orders exist in every tenant schema.
                foreach (var tenant in DefaultTenants)
                {
                    AssertTableExists(tenant, "Customers");
                    AssertTableExists(tenant, "Orders");
                }

                // Identical per-tenant structure: column shape matches across tenants.
                AssertIdenticalColumnsAcrossSchemas(DefaultTenants, "Customers");
                AssertIdenticalColumnsAcrossSchemas(DefaultTenants, "Orders");

                // Migration tracking per (template, schema): one row per tenant for SeedTenantMarker,
                // plus one shared row for SeedLookup.
                AssertMigrationTracked(SharedTemplate, "", "MigrationScripts/Before/SeedLookup.sql");
                foreach (var tenant in DefaultTenants)
                    AssertMigrationTracked(TenantBodyTemplate, tenant, "MigrationScripts/Before/SeedTenantMarker.sql");

                // Per-iteration marker INSERT landed.
                foreach (var tenant in DefaultTenants)
                {
                    var count = ScalarCount(
                        $"SELECT COUNT(*) FROM [{tenant}].[Customers] WITH (NOLOCK) WHERE Marker = N'{tenant}_marker'");
                    Assert.That(count, Is.EqualTo(1), $"Tenant '{tenant}' should have its per-iteration marker row.");
                }
            }
            finally
            {
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void Template_Order_Is_Enforced_Shared_Completes_Before_Any_TenantBody_Iteration_Starts()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                RunSchemaQuench();

                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Capture the ordered sequence of Info() messages and assert that:
                //   Shared "Quenching Template" precedes every per-iteration "[Schema: ...] Begin Quench".
                var infos = _progressLog.ReceivedCalls()
                    .Where(c => c.GetMethodInfo().Name == nameof(ILog.Info))
                    .Select(c => (string)c.GetArguments()[0])
                    .ToList();

                var sharedIdx = infos.FindIndex(s => s == $"Quenching Template: {SharedTemplate}");
                var tenantBodyIdx = infos.FindIndex(s => s == $"Quenching Template: {TenantBodyTemplate}");
                Assert.That(sharedIdx, Is.GreaterThanOrEqualTo(0), "Shared template start log not found");
                Assert.That(tenantBodyIdx, Is.GreaterThan(sharedIdx),
                    "TenantBody must start AFTER Shared starts (TemplateOrder).");

                // Shared has no schema iterations, so it produces a non-[Schema: ] Successfully Quenched
                // BEFORE any tenant iteration begins. Concretely: the dbo-level (no [Schema:]) line
                // for the Shared template must precede every [Schema: <tenant>] iteration.
                var sharedDoneIdx = infos.FindIndex(s => s == $"[{_server}].[{_mainDb}] Successfully Quenched");
                Assert.That(sharedDoneIdx, Is.GreaterThan(sharedIdx),
                    "Shared template must reach 'Successfully Quenched' (no [Schema:] prefix) after starting.");
                foreach (var tenant in DefaultTenants)
                {
                    var iterIdx = infos.FindIndex(s => s == $"[{_server}].[{_mainDb}] [Schema: {tenant}] Begin Quench");
                    Assert.That(iterIdx, Is.GreaterThan(sharedDoneIdx),
                        $"Tenant '{tenant}' iteration must start AFTER Shared completes.");
                }
            }
            finally
            {
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void Reserved_Schema_Name_From_Discovery_Aborts_Template_With_Clear_Error()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);

            // Insert a poisoned 'dbo' tenant row so discovery returns a reserved name.
            // Real product code's reserved-name guard should reject this and abort the template.
            ExecuteOnMainDb("INSERT INTO dbo.Tenants ([Name]) VALUES (N'dbo')");

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                RunSchemaQuench();

                // Discovery failure should surface an error mentioning the reserved name and exit 2.
                _progressLog.Received().Error(Arg.Is<string>(s =>
                    s.Contains("Schema discovery FAILED") &&
                    s.Contains("reserved schema name 'dbo'")));
                _environment.Received().Exit(2);

                // Other discovered schemas in the same run must NOT be deployed. Concretely: no tenant
                // schema has Customers because TenantBody never dispatched (slice-3 abort-on-discovery).
                foreach (var tenant in DefaultTenants)
                {
                    var customers = ScalarCount(
                        $"SELECT COUNT(*) FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = '{tenant}' AND t.name = 'Customers'");
                    Assert.That(customers, Is.EqualTo(0),
                        $"Tenant '{tenant}' must not have Customers — TenantBody must not have dispatched after reserved-name discovery failure.");
                }
            }
            finally
            {
                ExecuteOnMainDb("DELETE FROM dbo.Tenants WHERE [Name] = N'dbo'");
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void Per_Iteration_Query_Token_Resolves_Differently_Per_Tenant_And_Lands_In_Procedure_Body()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                RunSchemaQuench();

                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Each tenant's GetTenantLabel procedure body should contain the tenant-specific
                // resolved {{TenantLabel}} value: '<tenant>_label'. Read sys.sql_modules.definition
                // and assert.
                foreach (var tenant in DefaultTenants)
                {
                    var procDefinition = ScalarString(
                        $"SELECT m.definition FROM sys.sql_modules m INNER JOIN sys.objects o ON m.object_id = o.object_id INNER JOIN sys.schemas s ON o.schema_id = s.schema_id WHERE s.name = '{tenant}' AND o.name = 'GetTenantLabel'");
                    Assert.That(procDefinition, Is.Not.Null.And.Not.Empty,
                        $"Tenant '{tenant}' should have GetTenantLabel procedure deployed.");
                    Assert.That(procDefinition, Contains.Substring($"{tenant}_label"),
                        $"Tenant '{tenant}' procedure body should carry its iteration-resolved TenantLabel.");

                    // And the other tenants' labels must NOT be present in this tenant's procedure body.
                    foreach (var otherTenant in DefaultTenants.Where(t => t != tenant))
                        Assert.That(procDefinition, Does.Not.Contain($"{otherTenant}_label"),
                            $"Tenant '{tenant}' procedure body must not contain '{otherTenant}_label' — token re-resolution per iteration is the contract.");
                }
            }
            finally
            {
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void Second_Quench_Skips_Per_Tenant_Migration_Scripts_Tracking_Is_Per_Template_And_Schema()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                // First quench creates schemas and tracking rows.
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Verify each tenant has its own tracking row.
                foreach (var tenant in DefaultTenants)
                    AssertMigrationTracked(TenantBodyTemplate, tenant, "MigrationScripts/Before/SeedTenantMarker.sql");
                AssertMigrationTracked(SharedTemplate, "", "MigrationScripts/Before/SeedLookup.sql");

                // Mutate the per-tenant marker so we can prove SeedTenantMarker is NOT re-run.
                foreach (var tenant in DefaultTenants)
                    ExecuteOnMainDb($"UPDATE [{tenant}].[Customers] SET Marker = N'mutated' WHERE CustomerID = 1");

                // Second quench: same package, no changes — every tenant's SeedTenantMarker should
                // already be in the tracking table and skipped.
                _progressLog.ClearReceivedCalls();
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Markers should still be 'mutated' — SeedTenantMarker must have been skipped.
                foreach (var tenant in DefaultTenants)
                {
                    var marker = ScalarString(
                        $"SELECT Marker FROM [{tenant}].[Customers] WITH (NOLOCK) WHERE CustomerID = 1");
                    Assert.That(marker, Is.EqualTo("mutated"),
                        $"Tenant '{tenant}': second quench must skip the migration — marker should still be 'mutated'.");
                }

                // Tracking table still has exactly one row per (TenantBody, tenant).
                foreach (var tenant in DefaultTenants)
                {
                    var trackingCount = ScalarCount(
                        $"SELECT COUNT(*) FROM SchemaSmith.CompletedMigrationScripts WITH (NOLOCK) WHERE ProductName = '{ProductName}' AND template_name = '{TenantBodyTemplate}' AND schema_name = '{tenant}' AND ScriptPath = 'MigrationScripts/Before/SeedTenantMarker.sql'");
                    Assert.That(trackingCount, Is.EqualTo(1),
                        $"Tenant '{tenant}' should still have exactly one tracking row for SeedTenantMarker.");
                }
            }
            finally
            {
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void Cross_Schema_Foreign_Key_To_Shared_Lookup_Is_Created_Per_Tenant()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("SqlServer", ProductName);

            try
            {
                RunSchemaQuench();

                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Per-tenant: FK_Orders_Customers (same iteration) AND FK_Orders_Lookup (cross-schema to dbo).
                foreach (var tenant in DefaultTenants)
                {
                    var sameIterationFk = ScalarCount($@"
SELECT COUNT(*) FROM sys.foreign_keys fk
  INNER JOIN sys.tables ft ON fk.parent_object_id = ft.object_id
  INNER JOIN sys.schemas fs ON ft.schema_id = fs.schema_id
  INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
  INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
WHERE fs.name = '{tenant}' AND ft.name = 'Orders' AND fk.name = 'FK_Orders_Customers'
  AND rs.name = '{tenant}' AND rt.name = 'Customers'");
                    Assert.That(sameIterationFk, Is.EqualTo(1),
                        $"Tenant '{tenant}' must have FK_Orders_Customers same-iteration FK.");

                    var crossSchemaFk = ScalarCount($@"
SELECT COUNT(*) FROM sys.foreign_keys fk
  INNER JOIN sys.tables ft ON fk.parent_object_id = ft.object_id
  INNER JOIN sys.schemas fs ON ft.schema_id = fs.schema_id
  INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
  INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
WHERE fs.name = '{tenant}' AND ft.name = 'Orders' AND fk.name = 'FK_Orders_Lookup'
  AND rs.name = 'dbo' AND rt.name = 'Lookup'");
                    Assert.That(crossSchemaFk, Is.EqualTo(1),
                        $"Tenant '{tenant}' must have FK_Orders_Lookup cross-schema FK pointing at dbo.Lookup.");
                }
            }
            finally
            {
                DropTenantSchemas(DefaultTenants);
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

    private void RunSchemaQuench()
    {
        Program.Main(["SkipKindlingForge"]);
    }

    private static void ClearCheckpointsForProduct()
    {
        // Delegate to the live FileCheckpointManager so its in-memory cache is also cleared
        // (the static singleton survives across tests in the same NUnit run; deleting files on
        // disk alone leaves stale cache entries that skip checkpoint-tracked steps and leave
        // session-scoped temp tables uncreated).
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

        // Reset migration tracking + shared content from prior runs in this fixture's lifetime.
        cmd.CommandText = @$"
IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.CompletedMigrationScripts WHERE ProductName = '{ProductName}';
IF OBJECT_ID('dbo.Tenants', 'U') IS NOT NULL DELETE FROM dbo.Tenants;
IF OBJECT_ID('dbo.Lookup', 'U') IS NOT NULL DELETE FROM dbo.Lookup;";
        cmd.ExecuteNonQuery();

        // Drop any leftover tenant schemas from a previously aborted run.
        DropTenantSchemasInternal(cmd, tenants);

        // Pre-create each tenant schema (slice 3 assumes schemas exist — slice 4 adds CreateSchemaIfMissing).
        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"IF SCHEMA_ID('{tenant}') IS NULL EXEC('CREATE SCHEMA [{tenant}]');";
            cmd.ExecuteNonQuery();
        }

        // The discovery script reads dbo.Tenants — that table is created by the Shared template, but
        // it must already exist on the FIRST quench (TenantBody's DatabaseIdentificationScript runs
        // before Shared's tables are created if we left this to the fixture only). Create it
        // pre-emptively here so the test bootstraps cleanly; the Shared template's own CREATE-IF-MISSING
        // semantics will tolerate the pre-existing table.
        cmd.CommandText = @"
IF OBJECT_ID('dbo.Tenants', 'U') IS NULL
    CREATE TABLE dbo.Tenants ([Name] NVARCHAR(128) NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY);";
        cmd.ExecuteNonQuery();

        // Insert tenant rows.
        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"INSERT INTO dbo.Tenants ([Name]) VALUES (N'{tenant}');";
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

        // Reset shared content as a courtesy so the next test starts clean.
        cmd.CommandText = @$"
IF OBJECT_ID('dbo.SharedAudit', 'U') IS NOT NULL DROP TABLE dbo.SharedAudit;
IF OBJECT_ID('dbo.Lookup', 'U') IS NOT NULL DROP TABLE dbo.Lookup;
IF OBJECT_ID('dbo.Tenants', 'U') IS NOT NULL DROP TABLE dbo.Tenants;
IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts', 'U') IS NOT NULL
    DELETE FROM SchemaSmith.CompletedMigrationScripts WHERE ProductName = '{ProductName}';";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private static void DropTenantSchemasInternal(IDbCommand cmd, IEnumerable<string> tenants)
    {
        foreach (var tenant in tenants)
        {
            // Drop all objects in the schema, then drop the schema itself. Order: FKs first (procs +
            // tables can be in any order once FKs are gone).
            cmd.CommandText = $@"
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + 'ALTER TABLE [' + s.name + '].[' + t.name + '] DROP CONSTRAINT [' + fk.name + '];' + CHAR(10)
  FROM sys.foreign_keys fk
  INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
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

    private void ExecuteOnMainDb(string sql)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private void AssertTableExists(string schema, string tableName)
    {
        var count = ScalarCount(
            $"SELECT COUNT(*) FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = '{schema}' AND t.name = '{tableName}'");
        Assert.That(count, Is.EqualTo(1), $"Expected table [{schema}].[{tableName}] to exist after quench.");
    }

    private void AssertIdenticalColumnsAcrossSchemas(IReadOnlyList<string> schemas, string tableName)
    {
        // Pull column shape (name + data type ordinal) for each schema's table; assert all sets match.
        List<string> reference = null;
        foreach (var schema in schemas)
        {
            var cols = QueryRows(
                $"SELECT c.name + ':' + ty.name FROM sys.columns c INNER JOIN sys.tables t ON c.object_id = t.object_id INNER JOIN sys.schemas s ON t.schema_id = s.schema_id INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id WHERE s.name = '{schema}' AND t.name = '{tableName}' ORDER BY c.column_id");
            reference ??= cols;
            Assert.That(cols, Is.EquivalentTo(reference),
                $"Schema '{schema}' table '{tableName}' columns differ from reference set — per-iteration structure is not identical.");
        }
    }

    private void AssertMigrationTracked(string templateName, string schemaName, string scriptPath)
    {
        var count = ScalarCount(
            $"SELECT COUNT(*) FROM SchemaSmith.CompletedMigrationScripts WITH (NOLOCK) WHERE ProductName = '{ProductName}' AND template_name = '{templateName}' AND schema_name = '{schemaName}' AND ScriptPath = '{scriptPath}'");
        Assert.That(count, Is.EqualTo(1),
            $"Expected a tracking row for (template='{templateName}', schema='{schemaName}', script='{scriptPath}').");
    }

    private int ScalarCount(string sql)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        conn.Close();
        return Convert.ToInt32(result);
    }

    private string ScalarString(string sql)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        conn.Close();
        return result?.ToString();
    }

    private List<string> QueryRows(string sql)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add(reader[0]?.ToString());
        conn.Close();
        return rows;
    }
}
