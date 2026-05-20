// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.SqlServer;

/// <summary>
/// Integration tests for ForgeKindler against a live SQL Server database.
/// Uses dynamically created test databases via FixtureSetup.
/// Note: ForgeKindler is deployed once by FixtureSetup - these tests verify the deployment.
/// </summary>
[Category("SqlServer")]
[Category("Integration")]
[TestFixture]
public class ForgeKindlerIntegrationTests
{
    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.SqlServer)
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
        // Post-slice-8 cleanup (Commit B): a secondary NONCLUSTERED index on
        // (ProductName, QuenchSlot, template_name, schema_name) covers GetCompletedEntriesBySlot
        // lookups. The clustered PK leads with ScriptPath, so the (ProductName, QuenchSlot,
        // template_name, schema_name) tracking-lookup pattern was previously serviced by a
        // clustered-index scan. KindleTheForge ran during FixtureSetup; the index must exist.
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE name = 'IX_CompletedMigrationScripts_Slot_Scope'
              AND object_id = OBJECT_ID('SchemaSmith.CompletedMigrationScripts')";
        var indexCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(indexCount, Is.EqualTo(1),
            "Secondary index IX_CompletedMigrationScripts_Slot_Scope must be created by KindleTheForge.");

        command.CommandText = @"
            SELECT c.name
            FROM sys.indexes i
              INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
              INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.name = 'IX_CompletedMigrationScripts_Slot_Scope'
              AND i.object_id = OBJECT_ID('SchemaSmith.CompletedMigrationScripts')
            ORDER BY ic.key_ordinal";
        var columns = new List<string>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                columns.Add(reader[0].ToString());

        Assert.That(columns, Is.EqualTo(new[] { "ProductName", "QuenchSlot", "template_name", "schema_name" }),
            "Index must cover (ProductName, QuenchSlot, template_name, schema_name) in order — leading columns are the GetCompletedEntriesBySlot filter.");
    }

    [Test]
    public void KindleTheForge_SlotScopeIndex_IsIdempotent()
    {
        // KindleTheForge runs the kindling script on every quench — the IF NOT EXISTS guard
        // must keep the secondary index creation a no-op when the index already exists.
        // Run KindleTheForge a second time and assert the index is still present (and unique).
        using var command = _connection.CreateCommand();
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(command, Platform.SqlServer),
            "Repeated KindleTheForge must not throw — the secondary index DDL must be idempotent.");

        command.CommandText = @"
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE name = 'IX_CompletedMigrationScripts_Slot_Scope'
              AND object_id = OBJECT_ID('SchemaSmith.CompletedMigrationScripts')";
        var indexCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(indexCount, Is.EqualTo(1),
            "Re-running KindleTheForge must not produce a duplicate secondary index.");
    }
}
