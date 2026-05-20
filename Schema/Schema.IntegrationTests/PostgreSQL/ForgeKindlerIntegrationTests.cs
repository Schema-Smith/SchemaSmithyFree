// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// Integration tests for ForgeKindler against a live PostgreSQL database.
/// Uses dynamically created test databases via FixtureSetup.
/// Note: ForgeKindler is deployed once by FixtureSetup - these tests verify the deployment.
/// </summary>
[Category("PostgreSQL")]
[Category("Integration")]
[TestFixture]
public class ForgeKindlerIntegrationTests
{
    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.PostgreSQL)
            .GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void KindleTheForge_CreatesCompletedMigrationScriptsSlotScopeIndex()
    {
        // Post-slice-8 cleanup (Commit B): a secondary index on
        // (ProductName, QuenchSlot, template_name, schema_name) covers GetCompletedEntriesBySlot
        // lookups. The PK leads with ScriptPath — the (ProductName, QuenchSlot, template_name,
        // schema_name) tracking-lookup pattern was previously serviced by a sequential /
        // clustered-index scan. KindleTheForge ran during FixtureSetup; the index must exist.
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'SchemaSmith'
              AND tablename = 'CompletedMigrationScripts'
              AND indexname = 'ix_completedmigrationscripts_slot_scope'";
        var indexCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(indexCount, Is.EqualTo(1),
            "Secondary index ix_completedmigrationscripts_slot_scope must be created by KindleTheForge.");

        // Verify the column list. pg_indexes.indexdef is the CREATE INDEX statement text;
        // we assert the four leading columns appear in order.
        command.CommandText = @"
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'SchemaSmith'
              AND tablename = 'CompletedMigrationScripts'
              AND indexname = 'ix_completedmigrationscripts_slot_scope'";
        var indexDef = command.ExecuteScalar()?.ToString() ?? "";

        // indexdef looks like:
        // CREATE INDEX ix_completedmigrationscripts_slot_scope ON "SchemaSmith"."CompletedMigrationScripts" USING btree ("ProductName", "QuenchSlot", template_name, schema_name)
        Assert.That(indexDef, Does.Contain("\"ProductName\""),
            $"Index must include ProductName as a key column. indexdef={indexDef}");
        Assert.That(indexDef, Does.Contain("\"QuenchSlot\""),
            $"Index must include QuenchSlot as a key column. indexdef={indexDef}");
        Assert.That(indexDef, Does.Contain("template_name"),
            $"Index must include template_name as a key column. indexdef={indexDef}");
        Assert.That(indexDef, Does.Contain("schema_name"),
            $"Index must include schema_name as a key column. indexdef={indexDef}");

        // Verify ordering: ProductName appears before QuenchSlot, etc. — the engine's
        // GetCompletedEntriesBySlot filters in this order, so the leading-column ordering matters.
        var productNameIdx = indexDef.IndexOf("\"ProductName\"", StringComparison.Ordinal);
        var quenchSlotIdx = indexDef.IndexOf("\"QuenchSlot\"", StringComparison.Ordinal);
        var templateIdx = indexDef.IndexOf("template_name", StringComparison.Ordinal);
        var schemaIdx = indexDef.IndexOf("schema_name", StringComparison.Ordinal);
        Assert.That(productNameIdx, Is.LessThan(quenchSlotIdx),
            "ProductName must lead QuenchSlot in the index.");
        Assert.That(quenchSlotIdx, Is.LessThan(templateIdx),
            "QuenchSlot must lead template_name in the index.");
        Assert.That(templateIdx, Is.LessThan(schemaIdx),
            "template_name must lead schema_name in the index.");
    }

    [Test]
    public void KindleTheForge_SlotScopeIndex_IsIdempotent()
    {
        // KindleTheForge runs the kindling script on every quench — the CREATE INDEX IF NOT
        // EXISTS guard must keep the secondary index creation a no-op when the index already
        // exists. Run KindleTheForge a second time and assert no exception + a unique index.
        using var command = _connection.CreateCommand();
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(command, Platform.PostgreSQL),
            "Repeated KindleTheForge must not throw — the secondary index DDL must be idempotent.");

        command.CommandText = @"
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'SchemaSmith'
              AND tablename = 'CompletedMigrationScripts'
              AND indexname = 'ix_completedmigrationscripts_slot_scope'";
        var indexCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(indexCount, Is.EqualTo(1),
            "Re-running KindleTheForge must not produce a duplicate secondary index.");
    }
}
