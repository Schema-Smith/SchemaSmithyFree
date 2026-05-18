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

namespace SchemaQuench.IntegrationTests.PostgreSQL;

/// <summary>
/// Slice-3 schema-template integration tests (design §10.4). Verifies the engine
/// core end-to-end against live PostgreSQL: multi-tenant happy path, TemplateOrder
/// enforcement, reserved-name discovery rejection, per-iteration token resolution,
/// migration tracking per (template, schema), and cross-schema FK references.
///
/// <para>Fixtures pre-create the tenant schemas — slice 4 wires up
/// <c>CreateSchemaIfMissing</c>. Failure-isolation modes (<c>ContinueOnSchemaFailure</c> /
/// <c>ContinueOnDatabaseFailure</c>), tenant lifecycle (onboarding / offboarding), and
/// selective execution scope (<c>Target.*</c>) are deferred to later slices and out of
/// scope here.</para>
/// </summary>
[Category("PostgreSQL")]
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
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres",
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
                TestHelper.GetTestProductPath("PostgreSQL", ProductName);

            try
            {
                RunSchemaQuench();

                _progressLog.DidNotReceive().Error(Arg.Any<string>());
                _environment.DidNotReceive().Exit(2);
                _environment.DidNotReceive().Exit(3);

                _progressLog.Received(1).Info($"Completed quench of {ProductName}");
                _progressLog.Received(1).Info($"[{_server}].[{_mainDb}] Successfully Quenched");
                foreach (var tenant in DefaultTenants)
                    _progressLog.Received(1).Info($"[{_server}].[{_mainDb}] [Schema: {tenant}] Successfully Quenched");

                // Shared content in public: tenants, lookup, shared_audit + seed row.
                AssertTableExists("public", "tenants");
                AssertTableExists("public", "lookup");
                AssertTableExists("public", "shared_audit");
                Assert.That(ScalarCount("SELECT COUNT(*) FROM public.lookup WHERE lookup_id = 1 AND code = 'ALPHA'"),
                    Is.EqualTo(1), "Shared SeedLookup migration should have inserted exactly one row.");

                // Regression for slice-3 audit B1: Shared template's ProductOwnership rows
                // must carry template_name = 'Shared' AND survive TenantBody iteration's
                // FixupTableOwnership prune + ModifiedTableQuench drop pass.
                foreach (var sharedTable in new[] { "tenants", "lookup", "shared_audit" })
                {
                    var ownershipCount = ScalarCount(
                        $"SELECT COUNT(*) FROM \"SchemaSmith\".\"ProductOwnership\" WHERE \"ProductName\" = '{ProductName}' AND \"Schema\" = 'public' AND \"TableName\" = '{sharedTable}' AND \"IndexName\" IS NULL AND template_name = '{SharedTemplate}'");
                    Assert.That(ownershipCount, Is.EqualTo(1),
                        $"Shared table public.{sharedTable} must have exactly one ProductOwnership row scoped to template '{SharedTemplate}' (survived tenant iterations).");
                }

                // Per-tenant structure.
                foreach (var tenant in DefaultTenants)
                {
                    AssertTableExists(tenant, "customers");
                    AssertTableExists(tenant, "orders");
                }
                AssertIdenticalColumnsAcrossSchemas(DefaultTenants, "customers");
                AssertIdenticalColumnsAcrossSchemas(DefaultTenants, "orders");

                // Migration tracking.
                AssertMigrationTracked(SharedTemplate, "", "Before Scripts/SeedLookup.sql");
                foreach (var tenant in DefaultTenants)
                    AssertMigrationTracked(TenantBodyTemplate, tenant, "Before Scripts/SeedTenantMarker.sql");

                // Per-iteration marker INSERT landed.
                foreach (var tenant in DefaultTenants)
                {
                    var count = ScalarCount(
                        $"SELECT COUNT(*) FROM \"{tenant}\".customers WHERE marker = '{tenant}_marker'");
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
                TestHelper.GetTestProductPath("PostgreSQL", ProductName);

            try
            {
                RunSchemaQuench();

                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                var infos = _progressLog.ReceivedCalls()
                    .Where(c => c.GetMethodInfo().Name == nameof(ILog.Info))
                    .Select(c => (string)c.GetArguments()[0])
                    .ToList();

                var sharedIdx = infos.FindIndex(s => s == $"Quenching Template: {SharedTemplate}");
                var tenantBodyIdx = infos.FindIndex(s => s == $"Quenching Template: {TenantBodyTemplate}");
                Assert.That(sharedIdx, Is.GreaterThanOrEqualTo(0), "Shared template start log not found");
                Assert.That(tenantBodyIdx, Is.GreaterThan(sharedIdx),
                    "TenantBody must start AFTER Shared starts (TemplateOrder).");

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

            // Insert a poisoned 'public' tenant row so discovery returns a reserved name.
            ExecuteOnMainDb("INSERT INTO public.tenants (name) VALUES ('public')");

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("PostgreSQL", ProductName);

            try
            {
                RunSchemaQuench();

                _progressLog.Received().Error(Arg.Is<string>(s =>
                    s.Contains("Schema discovery FAILED") &&
                    s.Contains("reserved schema name 'public'")));
                _environment.Received().Exit(2);

                // Other discovered schemas in the same run must NOT be deployed.
                foreach (var tenant in DefaultTenants)
                {
                    var customers = ScalarCount(
                        $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{tenant}' AND table_name = 'customers'");
                    Assert.That(customers, Is.EqualTo(0),
                        $"Tenant '{tenant}' must not have customers — TenantBody must not have dispatched after reserved-name discovery failure.");
                }
            }
            finally
            {
                ExecuteOnMainDb("DELETE FROM public.tenants WHERE name = 'public'");
                DropTenantSchemas(DefaultTenants);
                LogFactory.Clear();
                FactoryContainer.Unregister<IEnvironment>();
            }
        }
    }

    [Test]
    public void Per_Iteration_Query_Token_Resolves_Differently_Per_Tenant_And_Lands_In_Function_Body()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();
            ResetTrackingAndCreateTenantSchemas(DefaultTenants);
            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] =
                TestHelper.GetTestProductPath("PostgreSQL", ProductName);

            try
            {
                RunSchemaQuench();

                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                // Each tenant's get_tenant_label function body should contain the tenant-specific
                // resolved {{TenantLabel}} value: '<tenant>_label'. pg_get_functiondef returns the
                // full body.
                foreach (var tenant in DefaultTenants)
                {
                    var fnDef = ScalarString($@"
SELECT pg_get_functiondef(p.oid)
  FROM pg_proc p
  INNER JOIN pg_namespace n ON p.pronamespace = n.oid
  WHERE n.nspname = '{tenant}' AND p.proname = 'get_tenant_label'");
                    Assert.That(fnDef, Is.Not.Null.And.Not.Empty,
                        $"Tenant '{tenant}' should have get_tenant_label function deployed.");
                    Assert.That(fnDef, Contains.Substring($"{tenant}_label"),
                        $"Tenant '{tenant}' function body should carry its iteration-resolved TenantLabel.");

                    foreach (var otherTenant in DefaultTenants.Where(t => t != tenant))
                        Assert.That(fnDef, Does.Not.Contain($"{otherTenant}_label"),
                            $"Tenant '{tenant}' function body must not contain '{otherTenant}_label'.");
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
                TestHelper.GetTestProductPath("PostgreSQL", ProductName);

            try
            {
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                foreach (var tenant in DefaultTenants)
                    AssertMigrationTracked(TenantBodyTemplate, tenant, "Before Scripts/SeedTenantMarker.sql");
                AssertMigrationTracked(SharedTemplate, "", "Before Scripts/SeedLookup.sql");

                foreach (var tenant in DefaultTenants)
                    ExecuteOnMainDb($"UPDATE \"{tenant}\".customers SET marker = 'mutated' WHERE customer_id = 1");

                _progressLog.ClearReceivedCalls();
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                foreach (var tenant in DefaultTenants)
                {
                    var marker = ScalarString(
                        $"SELECT marker FROM \"{tenant}\".customers WHERE customer_id = 1");
                    Assert.That(marker, Is.EqualTo("mutated"),
                        $"Tenant '{tenant}': second quench must skip the migration — marker should still be 'mutated'.");
                }

                foreach (var tenant in DefaultTenants)
                {
                    var trackingCount = ScalarCount(
                        $"SELECT COUNT(*) FROM \"SchemaSmith\".\"CompletedMigrationScripts\" WHERE \"ProductName\" = '{ProductName}' AND template_name = '{TenantBodyTemplate}' AND schema_name = '{tenant}' AND \"ScriptPath\" = 'Before Scripts/SeedTenantMarker.sql'");
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
                TestHelper.GetTestProductPath("PostgreSQL", ProductName);

            try
            {
                RunSchemaQuench();
                _progressLog.DidNotReceive().Error(Arg.Any<string>());

                foreach (var tenant in DefaultTenants)
                {
                    var sameIterationFk = ScalarCount($@"
SELECT COUNT(*) FROM information_schema.table_constraints tc
  INNER JOIN information_schema.referential_constraints rc ON tc.constraint_name = rc.constraint_name AND tc.constraint_schema = rc.constraint_schema
  INNER JOIN information_schema.constraint_column_usage ccu ON rc.unique_constraint_name = ccu.constraint_name AND rc.unique_constraint_schema = ccu.constraint_schema
  WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_schema = '{tenant}'
    AND tc.table_name = 'orders'
    AND tc.constraint_name = 'fk_orders_customers'
    AND ccu.table_schema = '{tenant}'
    AND ccu.table_name = 'customers'");
                    Assert.That(sameIterationFk, Is.GreaterThanOrEqualTo(1),
                        $"Tenant '{tenant}' must have fk_orders_customers same-iteration FK.");

                    var crossSchemaFk = ScalarCount($@"
SELECT COUNT(*) FROM information_schema.table_constraints tc
  INNER JOIN information_schema.referential_constraints rc ON tc.constraint_name = rc.constraint_name AND tc.constraint_schema = rc.constraint_schema
  INNER JOIN information_schema.constraint_column_usage ccu ON rc.unique_constraint_name = ccu.constraint_name AND rc.unique_constraint_schema = ccu.constraint_schema
  WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_schema = '{tenant}'
    AND tc.table_name = 'orders'
    AND tc.constraint_name = 'fk_orders_lookup'
    AND ccu.table_schema = 'public'
    AND ccu.table_name = 'lookup'");
                    Assert.That(crossSchemaFk, Is.GreaterThanOrEqualTo(1),
                        $"Tenant '{tenant}' must have fk_orders_lookup cross-schema FK pointing at public.lookup.");
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
        // disk alone leaves stale cache entries that skip checkpoint-tracked steps).
        Schema.Checkpointing.FileCheckpointManager.GetFromFactory().DeleteCheckpoints(ProductName);
    }

    private void ResetTrackingAndCreateTenantSchemas(IEnumerable<string> tenants)
    {
        ClearCheckpointsForProduct();
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @$"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'SchemaSmith' AND table_name = 'CompletedMigrationScripts') THEN
        DELETE FROM ""SchemaSmith"".""CompletedMigrationScripts"" WHERE ""ProductName"" = '{ProductName}';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'SchemaSmith' AND table_name = 'ProductOwnership') THEN
        DELETE FROM ""SchemaSmith"".""ProductOwnership"" WHERE ""ProductName"" = '{ProductName}';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'tenants') THEN
        DELETE FROM public.tenants;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'lookup') THEN
        DELETE FROM public.lookup;
    END IF;
END;
$$;";
        cmd.ExecuteNonQuery();

        DropTenantSchemasInternal(cmd, tenants);

        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{tenant}\";";
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS public.tenants (name VARCHAR(128) NOT NULL CONSTRAINT pk_tenants PRIMARY KEY);";
        cmd.ExecuteNonQuery();

        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"INSERT INTO public.tenants (name) VALUES ('{tenant}');";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }

    private void DropTenantSchemas(IEnumerable<string> tenants)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;
        DropTenantSchemasInternal(cmd, tenants);

        cmd.CommandText = @$"
DROP TABLE IF EXISTS public.shared_audit CASCADE;
DROP TABLE IF EXISTS public.lookup CASCADE;
DROP TABLE IF EXISTS public.tenants CASCADE;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'SchemaSmith' AND table_name = 'CompletedMigrationScripts') THEN
        DELETE FROM ""SchemaSmith"".""CompletedMigrationScripts"" WHERE ""ProductName"" = '{ProductName}';
    END IF;
END;
$$;";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private static void DropTenantSchemasInternal(IDbCommand cmd, IEnumerable<string> tenants)
    {
        foreach (var tenant in tenants)
        {
            cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{tenant}\" CASCADE;";
            cmd.ExecuteNonQuery();
        }
    }

    private void ExecuteOnMainDb(string sql)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
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
            $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{schema}' AND table_name = '{tableName}'");
        Assert.That(count, Is.EqualTo(1), $"Expected table \"{schema}\".{tableName} to exist after quench.");
    }

    private void AssertIdenticalColumnsAcrossSchemas(IReadOnlyList<string> schemas, string tableName)
    {
        List<string> reference = null;
        foreach (var schema in schemas)
        {
            var cols = QueryRows(
                $"SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema = '{schema}' AND table_name = '{tableName}' ORDER BY ordinal_position");
            reference ??= cols;
            Assert.That(cols, Is.EquivalentTo(reference),
                $"Schema '{schema}' table '{tableName}' columns differ from reference set — per-iteration structure is not identical.");
        }
    }

    private void AssertMigrationTracked(string templateName, string schemaName, string scriptPath)
    {
        var count = ScalarCount(
            $"SELECT COUNT(*) FROM \"SchemaSmith\".\"CompletedMigrationScripts\" WHERE \"ProductName\" = '{ProductName}' AND template_name = '{templateName}' AND schema_name = '{schemaName}' AND \"ScriptPath\" = '{scriptPath}'");
        Assert.That(count, Is.EqualTo(1),
            $"Expected a tracking row for (template='{templateName}', schema='{schemaName}', script='{scriptPath}').");
    }

    private int ScalarCount(string sql)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
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
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
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
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
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
