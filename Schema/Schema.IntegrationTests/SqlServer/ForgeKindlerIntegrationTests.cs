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

    [Test]
    public void KindleTheForge_LegacyTable_BootstrapAddsTemplateNameSchemaNameAndSecondaryIndex()
    {
        // BootstrapTableQuench refactor: a pre-slice-2 / pre-Commit-B table (no template_name,
        // no schema_name, no IX_CompletedMigrationScripts_Slot_Scope) is upgraded in a single
        // kindling pass via BootstrapTableQuench's ADD COLUMN + CREATE INDEX guards.
        using var command = _connection.CreateCommand();
        try
        {
            command.CommandText = @"
                IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts') IS NOT NULL
                    DROP TABLE SchemaSmith.CompletedMigrationScripts;
                CREATE TABLE SchemaSmith.CompletedMigrationScripts (
                    ScriptPath  VARCHAR(800) NOT NULL,
                    ProductName VARCHAR(100) NOT NULL,
                    QuenchSlot  VARCHAR(30)  NOT NULL,
                    QuenchDate  DATETIME     NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT PK_CompletedMigrationScripts PRIMARY KEY CLUSTERED (ScriptPath, ProductName, QuenchSlot)
                );";
            command.ExecuteNonQuery();

            ForgeKindler.KindleTheForge(command, Platform.SqlServer);

            // template_name + schema_name added with the correct type, nullability, and default.
            // The plan's risk mitigation requires verifying these properties — a regression that
            // produced a nullable column or a missing default would silently break PK extension
            // and GetCompletedEntriesBySlot filtering.
            command.CommandText = @"
                SELECT c.name, t.name AS type_name, c.max_length, c.is_nullable, dc.definition
                  FROM sys.columns c
                  JOIN sys.types t ON c.user_type_id = t.user_type_id
                  LEFT JOIN sys.default_constraints dc
                    ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                 WHERE c.object_id = OBJECT_ID('SchemaSmith.CompletedMigrationScripts')
                   AND c.name IN ('template_name', 'schema_name')
                 ORDER BY c.name";
            using (var reader = command.ExecuteReader())
            {
                var rows = new List<(string Name, string Type, int MaxLen, bool Nullable, string DefaultDef)>();
                while (reader.Read())
                {
                    rows.Add((reader.GetString(0), reader.GetString(1), reader.GetInt16(2),
                        reader.GetBoolean(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
                }
                reader.Close();
                Assert.That(rows, Has.Count.EqualTo(2),
                    "Bootstrap must add both template_name and schema_name.");
                foreach (var row in rows)
                {
                    Assert.That(row.Type, Is.EqualTo("nvarchar"), $"{row.Name} type");
                    Assert.That(row.MaxLen, Is.EqualTo(512), $"{row.Name} max_length (NVARCHAR(256) = 512 bytes)");
                    Assert.That(row.Nullable, Is.False, $"{row.Name} nullability — must be NOT NULL");
                    Assert.That(row.DefaultDef, Is.Not.Null,
                        $"{row.Name} must carry a default constraint (DEFAULT '')");
                    Assert.That(row.DefaultDef, Does.Contain("''"),
                        $"{row.Name} default must be empty string");
                }
            }

            // IX_CompletedMigrationScripts_Slot_Scope created (covers GetCompletedEntriesBySlot).
            // Verify the column list, not just the index name — a regression that created an
            // index with the right name but wrong columns would pass a name-only assertion.
            command.CommandText = @"
                SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
                  FROM sys.indexes i
                  JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                  JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                 WHERE i.name = 'IX_CompletedMigrationScripts_Slot_Scope'
                   AND i.object_id = OBJECT_ID('SchemaSmith.CompletedMigrationScripts')";
            var indexCols = command.ExecuteScalar() as string;
            Assert.That(indexCols, Is.EqualTo("ProductName,QuenchSlot,template_name,schema_name"),
                "Secondary index must lead with the GetCompletedEntriesBySlot filter columns in order.");
        }
        finally
        {
            // Reset the table to current shape so subsequent fixtures inherit a clean state.
            command.CommandText = @"IF OBJECT_ID('SchemaSmith.CompletedMigrationScripts') IS NOT NULL
                                    DROP TABLE SchemaSmith.CompletedMigrationScripts";
            command.ExecuteNonQuery();
            ForgeKindler.KindleTheForge(command, Platform.SqlServer);
        }
    }
}
