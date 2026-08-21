// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using MySqlConnector;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

// Data-loss guard coverage: drop-by-absence has no partition awareness, so a product-owned table
// that grows and gets manually partitioned (SchemaSmith has no partitioning support of its own)
// looks like an ordinary drop-by-absence candidate once removed from the package. The guard in
// SchemaSmith_ModifiedTableQuench.sql fails closed rather than destroying partitioned data.
//
// MySQL's SIGNAL MESSAGE_TEXT is capped at 128 chars, so the guard logs the offending table
// name(s) to SchemaSmith_StatusMessages BEFORE signaling; the tests read that log rather than the
// (deliberately generic) exception message.
//
// Each test owns a UNIQUE product name so DropTablesRemovedFromProduct is scoped to its own tables
// and never drops a sibling test's tables under parallel execution.
[Category("Integration")]
public abstract class TableQuench_PartitionedTableDropGuardSharedTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_PartitionedOwnedTableRemovedFromProduct_IsNotDroppedAndRunFails()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var product = $"PartGuardProduct_{uid}";
        var table = $"PartGuardTable_{uid}";
        var keep = $"PartGuardKeep_{uid}"; // anchor table that stays in the package across both quenches

        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            RunTableQuenchProc(cmd, WithTables(table, keep), productName: product);
            Assert.That(ObjectExists(cmd, table), Is.True, "Setup: table should exist after the first quench.");

            // Manually partition the owned table -- exactly why anyone partitions: it grew.
            // SchemaSmith has no partitioning support, so this can only happen by hand.
            PartitionTable(cmd, table);
            Assert.That(IsPartitioned(cmd, table), Is.True, "Setup: table should be partitioned.");

            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();

            var ex = Assert.Throws<MySqlException>(() => RunTableQuenchProc(cmd, WithTable(keep), dropTablesRemovedFromProduct: true, productName: product),
                "The partition guard must fail the run instead of dropping a partitioned table.");
            Assert.That(ex!.Message, Does.Contain("PreventDrop"), "Failure message must tell the operator how to proceed (PreventDrop or manual drop).");

            cmd.CommandText = "SELECT GROUP_CONCAT(Message SEPARATOR ' | ') FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            var log = cmd.ExecuteScalar()?.ToString() ?? "";

            Assert.Multiple(() =>
            {
                Assert.That(log, Does.Contain(table), $"Run log must name the offending table. Log: {log}");
                Assert.That(ObjectExists(cmd, table), Is.True, "Guard must prevent the drop: the partitioned table must still exist.");
            });
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`{table}`; DROP TABLE IF EXISTS `{_mainDb}`.`{keep}`;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    // The guard must not over-reach: an ordinary (non-partitioned) owned table removed from the
    // product must still be dropped by absence exactly as before.
    [Test]
    public void TableQuench_OrdinaryOwnedTableRemovedFromProduct_IsStillDropped()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var product = $"PartGuardOrdinaryProduct_{uid}";
        var table = $"PartGuardOrdinaryTable_{uid}";
        var keep = $"PartGuardOrdinaryKeep_{uid}";

        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            RunTableQuenchProc(cmd, WithTables(table, keep), productName: product);
            Assert.That(ObjectExists(cmd, table), Is.True, "Setup: table should exist after the first quench.");

            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, WithTable(keep), dropTablesRemovedFromProduct: true, productName: product),
                "A non-partitioned table must still be dropped by absence -- the guard must not over-reach.");
            Assert.That(ObjectExists(cmd, table), Is.False, "Ordinary owned table removed from the product must be dropped.");
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`{table}`; DROP TABLE IF EXISTS `{_mainDb}`.`{keep}`;";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    // A partitioned table that is still present in the package must be entirely unaffected by the
    // guard -- it only inspects tables actually selected for drop-by-absence.
    [Test]
    public void TableQuench_PartitionedTableStillPresentInProduct_IsUnaffected()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var product = $"PartGuardKeptProduct_{uid}";
        var table = $"PartGuardKeptTable_{uid}";

        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            RunTableQuenchProc(cmd, WithTable(table), productName: product);
            PartitionTable(cmd, table);
            Assert.That(IsPartitioned(cmd, table), Is.True, "Setup: table should be partitioned.");

            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, WithTable(table), dropTablesRemovedFromProduct: true, productName: product),
                "A partitioned table still declared in the product must not trip the drop-by-absence guard.");
            Assert.That(ObjectExists(cmd, table), Is.True, "Table still in the product must survive untouched.");
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`{table}`;";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    private void PartitionTable(IDbCommand cmd, string table)
    {
        cmd.CommandText = $"ALTER TABLE `{_mainDb}`.`{table}` PARTITION BY RANGE (`Id`) (PARTITION p0 VALUES LESS THAN (1000000), PARTITION p1 VALUES LESS THAN MAXVALUE);";
        cmd.ExecuteNonQuery();
    }

    private bool IsPartitioned(IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.PARTITIONS
 WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND PARTITION_NAME IS NOT NULL";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static string WithTable(string table) => $$"""
[
  {
    "Name": "{{table}}",
    "Columns": [ { "Name": "`Id`", "DataType": "int", "Nullable": false } ],
    "Indexes": [ { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "`Id`" } ]
  }
]
""";

    private static string WithTables(string table, string keep) => $$"""
[
  {
    "Name": "{{table}}",
    "Columns": [ { "Name": "`Id`", "DataType": "int", "Nullable": false } ],
    "Indexes": [ { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "`Id`" } ]
  },
  {
    "Name": "{{keep}}",
    "Columns": [ { "Name": "`Id`", "DataType": "int", "Nullable": false } ],
    "Indexes": [ { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "`Id`" } ]
  }
]
""";

    private bool ObjectExists(IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
