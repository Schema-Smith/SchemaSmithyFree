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
/// Slice-2/3 (#257) TemplateTargets integration tests for SQL Server.
/// <para>Slice 2 verifies the existing-tenants happy path: a <c>Target.TemplateTargets.TenantBody.Schemas</c>
/// override REPLACES the per-DB SchemaDiscovery result so the per-iteration deployment runs
/// only against the overridden schemas, the source-disclosure log line surfaces the override
/// origin, and downstream tracking matches a discovery-driven run on the same tenant set.</para>
/// <para>Slice 3 adds the provisioning + skip-missing paths: <c>CreateIfMissing: true</c> provisions
/// missing schemas via per-engine idempotent DDL; <c>CreateIfMissing: false</c> (default) skips
/// missing entries with an info log. Database-axis provisioning lands in slice 4.</para>
/// </summary>
[Category("SqlServer")]
public class TemplateTargetsHappyPathTests
{
    private const string ProductName = "SchemaTemplateProduct";
    private const string TenantBodyTemplate = "TenantBody";

    private static readonly string[] DefaultTenants = ["tenant_acme", "tenant_beta", "tenant_globex"];

    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;
    private readonly string _server;

    public TemplateTargetsHappyPathTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["Target:Server"], "master",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
        _server = config["Target:Server"];
    }

    [Test]
    public void OverrideSchemasListReplacesDiscoveryScript_ExistingTenants()
    {
        // Two tenants exist on the target. Override Target.TemplateTargets.TenantBody.Schemas
        // to those two and quench. The override REPLACES SchemaDiscovery — even though the
        // dbo.SchemaTemplateTenants table is populated, the override list IS the universe.
        // To prove that, populate dbo.SchemaTemplateTenants with EXTRA tenants the override
        // omits and assert no per-tenant work landed for those extras.
        var overrideTenants = new[] { "tenant_acme", "tenant_globex" };
        var extraTenantIgnoredByOverride = "tenant_beta";

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            config["SchemaPackagePath"] = TestHelper.GetTestProductPath("SqlServer", ProductName);
            ClearTargetFilters(config);
            ClearTemplateTargets(config);

            // Configure the override after the dbo.SchemaTemplateTenants table is seeded with
            // ALL three tenants — the override must override the discovery result.
            config["Target:TemplateTargets:TenantBody:Schemas:0"] = overrideTenants[0];
            config["Target:TemplateTargets:TenantBody:Schemas:1"] = overrideTenants[1];

            try
            {
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Only the two overridden tenants were quenched.
                foreach (var tenant in overrideTenants)
                    _progressLog.Received(1).Info(
                        $"[{_server}].[{_mainDb}] [Schema: {tenant}] Successfully Quenched");

                _progressLog.DidNotReceive().Info(
                    $"[{_server}].[{_mainDb}] [Schema: {extraTenantIgnoredByOverride}] Successfully Quenched");

                // Source-disclosure log line names the override origin for the schema axis.
                _progressLog.Received().Info(Arg.Is<string>(s =>
                    s.Contains("source: ") &&
                    s.Contains("schema=TemplateTargets:TenantBody:Schemas")));

                // Both overridden tenants got their per-iteration migration tracking.
                foreach (var tenant in overrideTenants)
                    AssertMigrationTracked(TenantBodyTemplate, tenant,
                        "MigrationScripts/Before/SeedTenantMarker.sql");

                // The omitted tenant's schema was NOT touched: no tracking row.
                AssertMigrationNotTracked(TenantBodyTemplate, extraTenantIgnoredByOverride,
                    "MigrationScripts/Before/SeedTenantMarker.sql");
            }
            finally
            {
                ClearTemplateTargets(config);
                ClearTargetFilters(config);
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void OverrideWithCreateIfMissing_ProvisionsMissingSchemasAndDeploys()
    {
        // CreateIfMissing: true — override lists one existing tenant + one schema NOT yet created
        // on the target. SchemaProvisioner emits idempotent CREATE SCHEMA for the missing one,
        // then per-iteration deployment runs against both. Both end up with template content
        // and a provisioning log line surfaces for the created schema.
        const string existingTenant = "tenant_acme";
        const string newTenant = "tenant_newly_created";
        var overrideSchemas = new[] { existingTenant, newTenant };

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            // Pre-create only the existing tenant. The new tenant's schema does NOT exist yet.
            ResetTrackingAndCreateTenantSchemas(new[] { existingTenant });
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            config["SchemaPackagePath"] = TestHelper.GetTestProductPath("SqlServer", ProductName);
            ClearTargetFilters(config);
            ClearTemplateTargets(config);

            config["Target:TemplateTargets:TenantBody:Schemas:0"] = overrideSchemas[0];
            config["Target:TemplateTargets:TenantBody:Schemas:1"] = overrideSchemas[1];
            config["Target:TemplateTargets:TenantBody:CreateIfMissing"] = "true";

            try
            {
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Both tenants completed their iteration (the missing one was provisioned first).
                foreach (var tenant in overrideSchemas)
                    _progressLog.Received(1).Info(
                        $"[{_server}].[{_mainDb}] [Schema: {tenant}] Successfully Quenched");

                // Provisioning log line surfaced for the new tenant.
                _progressLog.Received().Info(Arg.Is<string>(s =>
                    s.Contains($"Creating schema [{newTenant}]") &&
                    s.Contains("CreateIfMissing: true")));

                // The new tenant's schema now exists on the target.
                Assert.That(SchemaExists(newTenant), Is.True,
                    "CreateIfMissing: true must provision missing override entries.");

                // Both tenants have migration tracking rows.
                foreach (var tenant in overrideSchemas)
                    AssertMigrationTracked(TenantBodyTemplate, tenant,
                        "MigrationScripts/Before/SeedTenantMarker.sql");
            }
            finally
            {
                ClearTemplateTargets(config);
                ClearTargetFilters(config);
                DropTenantSchemas(new[] { existingTenant, newTenant });
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void OverrideWithoutCreateIfMissing_SkipsMissingSchemasWithInfoLog()
    {
        // CreateIfMissing: false (default) — override lists one existing tenant + one missing tenant.
        // The missing one is SKIPPED with an info log naming it; the existing one deploys normally.
        // The missing schema is NOT created (the negative-control: the engine must not silently
        // upgrade a skip-missing override into a create).
        const string existingTenant = "tenant_acme";
        const string missingTenant = "tenant_skipped";
        var overrideSchemas = new[] { existingTenant, missingTenant };

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(new[] { existingTenant });
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            config["SchemaPackagePath"] = TestHelper.GetTestProductPath("SqlServer", ProductName);
            ClearTargetFilters(config);
            ClearTemplateTargets(config);

            config["Target:TemplateTargets:TenantBody:Schemas:0"] = overrideSchemas[0];
            config["Target:TemplateTargets:TenantBody:Schemas:1"] = overrideSchemas[1];
            // CreateIfMissing intentionally absent — defaults to false.

            try
            {
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Existing tenant completed.
                _progressLog.Received(1).Info(
                    $"[{_server}].[{_mainDb}] [Schema: {existingTenant}] Successfully Quenched");

                // Missing tenant did NOT complete (skip-missing — the unit short-circuits).
                _progressLog.DidNotReceive().Info(
                    $"[{_server}].[{_mainDb}] [Schema: {missingTenant}] Successfully Quenched");

                // Skip log line names the missing schema + the CreateIfMissing-is-false reason.
                _progressLog.Received().Info(Arg.Is<string>(s =>
                    s.Contains($"Schema '{missingTenant}'") &&
                    s.Contains("TemplateTargets CreateIfMissing is false") &&
                    s.Contains("skipping this iteration")));

                // Missing schema must NOT have been created (negative control).
                Assert.That(SchemaExists(missingTenant), Is.False,
                    "Skip-missing must NOT provision the schema.");

                // Migration tracking exists for the existing tenant, NOT for the skipped one.
                AssertMigrationTracked(TenantBodyTemplate, existingTenant,
                    "MigrationScripts/Before/SeedTenantMarker.sql");
                AssertMigrationNotTracked(TenantBodyTemplate, missingTenant,
                    "MigrationScripts/Before/SeedTenantMarker.sql");
            }
            finally
            {
                ClearTemplateTargets(config);
                ClearTargetFilters(config);
                DropTenantSchemas(new[] { existingTenant, missingTenant });
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

    private static void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private static void ClearTargetFilters(IConfigurationRoot config)
    {
        foreach (var dim in new[] { "Templates", "Databases", "Schemas" })
            foreach (var child in config.GetSection($"Target:{dim}").GetChildren().ToList())
                config[$"Target:{dim}:{child.Key}"] = null;
    }

    private static void ClearTemplateTargets(IConfigurationRoot config)
    {
        // Walk the full Target:TemplateTargets:* tree so any prior-test residue is gone before
        // the next test plants its own. Snapshot via .ToList() before mutating (the in-memory
        // provider iterates the same dict you're nulling keys in).
        foreach (var templateEntry in config.GetSection("Target:TemplateTargets").GetChildren().ToList())
        {
            foreach (var axisEntry in templateEntry.GetChildren().ToList())
            {
                if (axisEntry.Key is "Databases" or "Schemas")
                {
                    foreach (var item in axisEntry.GetChildren().ToList())
                        config[$"Target:TemplateTargets:{templateEntry.Key}:{axisEntry.Key}:{item.Key}"] = null;
                }
                else
                {
                    // CreateIfMissing scalar
                    config[$"Target:TemplateTargets:{templateEntry.Key}:{axisEntry.Key}"] = null;
                }
            }
        }
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
IF OBJECT_ID('dbo.Lookup', 'U') IS NOT NULL DELETE FROM dbo.Lookup;";
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

    private void AssertMigrationTracked(string templateName, string schemaName, string scriptPath)
    {
        var count = ScalarCount(
            $"SELECT COUNT(*) FROM SchemaSmith.CompletedMigrationScripts WITH (NOLOCK) WHERE ProductName = '{ProductName}' AND template_name = '{templateName}' AND schema_name = '{schemaName}' AND ScriptPath = '{scriptPath}'");
        Assert.That(count, Is.EqualTo(1),
            $"Expected a tracking row for (template='{templateName}', schema='{schemaName}', script='{scriptPath}').");
    }

    private void AssertMigrationNotTracked(string templateName, string schemaName, string scriptPath)
    {
        var count = ScalarCount(
            $"SELECT COUNT(*) FROM SchemaSmith.CompletedMigrationScripts WITH (NOLOCK) WHERE ProductName = '{ProductName}' AND template_name = '{templateName}' AND schema_name = '{schemaName}' AND ScriptPath = '{scriptPath}'");
        Assert.That(count, Is.EqualTo(0),
            $"Expected NO tracking row for (template='{templateName}', schema='{schemaName}', script='{scriptPath}') — the targeted run must not have touched this scope.");
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

    private bool SchemaExists(string schemaName)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sys.schemas WHERE name = @name";
        var p = cmd.CreateParameter();
        p.ParameterName = "@name";
        p.Value = schemaName;
        cmd.Parameters.Add(p);
        var result = cmd.ExecuteScalar();
        conn.Close();
        return Convert.ToInt32(result) > 0;
    }
}
