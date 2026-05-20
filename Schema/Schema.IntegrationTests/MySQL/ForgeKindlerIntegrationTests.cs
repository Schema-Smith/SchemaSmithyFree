// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;

using NUnit.Framework;
using Schema.Utility;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for ForgeKindler against a live MySQL database.
/// Uses dynamically created test databases via FixtureSetup.
/// Note: ForgeKindler is deployed once by FixtureSetup - these tests verify the deployment.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class ForgeKindlerIntegrationTests
{
    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void KindleTheForge_CreatesCompletedMigrationScriptsTable()
    {
        // ForgeKindler is already deployed by FixtureSetup - verify the table exists in target database
        using var command = _connection.CreateCommand();

        command.CommandText = $@"
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
            AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'";
        var result = command.ExecuteScalar();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ToString(), Is.EqualTo("SchemaSmith_CompletedMigrationScripts"));
    }

    [Test]
    public void KindleTheForge_CreatesGenerateTableJSONProcedure()
    {
        // ForgeKindler is already deployed by FixtureSetup - verify the procedure exists in target database
        using var command = _connection.CreateCommand();

        command.CommandText = $@"
            SELECT ROUTINE_NAME
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_SCHEMA = '{FixtureSetup.MainDb}'
            AND ROUTINE_NAME = 'SchemaSmith_GenerateTableJSON'
            AND ROUTINE_TYPE = 'PROCEDURE'";
        var result = command.ExecuteScalar();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ToString(), Is.EqualTo("SchemaSmith_GenerateTableJSON"));
    }

    [Test]
    public void KindleTheForge_CanBeRunMultipleTimes()
    {
        // TestUser has SYSTEM_USER privilege via docker init script
        using var freshConnection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        freshConnection.Open();
        using var command = freshConnection.CreateCommand();

        // Should not throw on multiple runs (idempotent)
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(command, Platform.MySQL));
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(command, Platform.MySQL));
    }

    [Test]
    public void KindleTheForge_CreatesCompletedMigrationScriptsSlotScopeIndex()
    {
        // Post-slice-8 cleanup (Commit B): a secondary index on
        // (ProductName, QuenchSlot, template_name, schema_name) covers GetCompletedEntriesBySlot
        // lookups. The PK leads with `Id` and uk_script leads with ScriptPath — both miss the
        // tracking-lookup pattern. The index must exist after KindleTheForge runs (which
        // happens in FixtureSetup).
        using var command = _connection.CreateCommand();
        command.CommandText = $@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
              AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'
              AND INDEX_NAME = 'ix_completedmigrationscripts_slot_scope'";
        var matchingRows = System.Convert.ToInt32(command.ExecuteScalar());

        // Multi-column index produces one row per column in INFORMATION_SCHEMA.STATISTICS;
        // 4 columns -> 4 rows. Counting "at least one" proves the index exists; the exact
        // column-set check follows.
        Assert.That(matchingRows, Is.GreaterThan(0),
            "Secondary index ix_completedmigrationscripts_slot_scope must be created by KindleTheForge.");

        command.CommandText = $@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
              AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'
              AND INDEX_NAME = 'ix_completedmigrationscripts_slot_scope'
            ORDER BY SEQ_IN_INDEX";
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
        // KindleTheForge runs the kindling script on every quench — the information_schema-
        // guarded PREPARE/EXECUTE block must keep the secondary index creation a no-op when
        // the index already exists (MySQL 8.0.x patch levels predate CREATE INDEX IF NOT EXISTS
        // so the procedural guard is what carries the idempotence here).
        using var freshConnection = DbConnectionFactory.ForPlatform(Platform.MySQL)
            .GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        freshConnection.Open();
        using var command = freshConnection.CreateCommand();

        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(command, Platform.MySQL),
            "Repeated KindleTheForge must not throw — the secondary index DDL must be idempotent.");

        command.CommandText = $@"
            SELECT COUNT(DISTINCT INDEX_NAME)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
              AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'
              AND INDEX_NAME = 'ix_completedmigrationscripts_slot_scope'";
        var indexCount = System.Convert.ToInt32(command.ExecuteScalar());
        Assert.That(indexCount, Is.EqualTo(1),
            "Re-running KindleTheForge must not produce a duplicate secondary index.");
    }

    [Test]
    public void KindleTheForge_LegacyCompletedMigrationScripts_BootstrapAddsColumnsAndSecondaryIndex()
    {
        // BootstrapTableQuench refactor: pre-slice-2 / pre-Commit-B MySQL table is upgraded
        // in a single kindling pass via Bootstrap's information_schema-guarded ADD COLUMN +
        // ADD INDEX. Replaces the now-deleted Kindling_AlterCompletedMigrationScripts.sql.
        using var freshConnection = DbConnectionFactory.ForPlatform(Platform.MySQL)
            .GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        freshConnection.Open();
        using var command = freshConnection.CreateCommand();

        try
        {
            command.CommandText = "DROP TABLE IF EXISTS `SchemaSmith_CompletedMigrationScripts`";
            command.ExecuteNonQuery();
            command.CommandText = @"
                CREATE TABLE `SchemaSmith_CompletedMigrationScripts` (
                    `Id` INT AUTO_INCREMENT PRIMARY KEY,
                    `ProductName` VARCHAR(100) NOT NULL,
                    `QuenchSlot` VARCHAR(50) NOT NULL,
                    `ScriptPath` VARCHAR(500) NOT NULL,
                    `CompletedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY `uk_script` (`ProductName`, `QuenchSlot`, `ScriptPath`(255))
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
            command.ExecuteNonQuery();

            ForgeKindler.KindleTheForge(command, Platform.MySQL);

            command.CommandText = $@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
                  AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'
                  AND COLUMN_NAME IN ('template_name', 'schema_name')";
            Assert.That(System.Convert.ToInt64(command.ExecuteScalar()), Is.EqualTo(2),
                "Bootstrap must add template_name and schema_name to legacy tables.");

            command.CommandText = $@"
                SELECT COUNT(DISTINCT INDEX_NAME) FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
                  AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'
                  AND INDEX_NAME = 'ix_completedmigrationscripts_slot_scope'";
            Assert.That(System.Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1),
                "Bootstrap must create the secondary index from the shared JSON definition.");
        }
        finally
        {
            command.CommandText = "DROP TABLE IF EXISTS `SchemaSmith_CompletedMigrationScripts`";
            command.ExecuteNonQuery();
            ForgeKindler.KindleTheForge(command, Platform.MySQL);
        }
    }

    [Test]
    public void KindleTheForge_FreshInstall_ProductOwnershipAndStatusMessagesExist()
    {
        // BootstrapTableQuench creates all three MySQL kindling tables on a fresh install.
        // Verify ProductOwnership and StatusMessages are present (CompletedMigrationScripts
        // is covered by KindleTheForge_CreatesCompletedMigrationScriptsTable above).
        using var command = _connection.CreateCommand();
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
              AND TABLE_NAME IN ('SchemaSmith_ProductOwnership', 'SchemaSmith_StatusMessages')";
        Assert.That(System.Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(2),
            "Both ProductOwnership and StatusMessages must be created on fresh install by Bootstrap.");
    }
}
