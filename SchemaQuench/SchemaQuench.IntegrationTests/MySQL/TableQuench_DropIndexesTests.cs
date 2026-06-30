// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.MySQL;

[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_DropIndexesTests : BaseTableQuenchTests
{
    // Unique product name: MySQL records index ownership in the persistent SchemaSmith_ProductOwnership
    // table keyed by product, so an isolated name keeps a concurrent sibling quench from interfering.
    private const string IndexProduct = "Index Drop Tests";

    // Two-phase: an index must be product-OWNED (recorded in ProductOwnership) to take the
    // removed-from-product path. Phase 1 quenches the index into existence; phase 2 reconciles with
    // the index removed. On MySQL the removed-from-product drop (STEP 8) is gated by DropUnknownIndexes
    // (its full decoupling is Index-B), so phase 2 runs MissingIndexesAndConstraintsQuench with
    // DropUnknownIndexes=1; the new per-table DropIndexesRemovedFromProduct:false then suppresses
    // IdxDropSuppressed while IdxDropControl (no flag) drops.
    [Test]
    public void TableQuench_ShouldSuppressIndexDropWhenTableFlagIsFalse()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = 'IdxDropSuppressed' AND INDEX_NAME = 'IX_IdxDropSuppressed_Val'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0), "IX_IdxDropSuppressed_Val should still exist (suppressed by table flag).");

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = 'IdxDropControl' AND INDEX_NAME = 'IX_IdxDropControl_Val'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "IX_IdxDropControl_Val should be gone (dropped by absence).");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Phase 1 — quench the tables WITH their secondary index (creates + records ownership).
        var withIndex = """
            [
            {
                "Name": "`IdxDropSuppressed`",
                "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false }, { "Name": "`Val`", "DataType": "INT", "Nullable": true } ],
                "Indexes": [ { "Name": "`IX_IdxDropSuppressed_Val`", "IndexColumns": "`Val`", "Unique": false, "PrimaryKey": false } ]
            },
            {
                "Name": "`IdxDropControl`",
                "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false }, { "Name": "`Val`", "DataType": "INT", "Nullable": true } ],
                "Indexes": [ { "Name": "`IX_IdxDropControl_Val`", "IndexColumns": "`Val`", "Unique": false, "PrimaryKey": false } ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, withIndex, productName: IndexProduct);

        // Phase 2 — reconcile with the secondary index removed from both. IdxDropSuppressed protects
        // its own via the per-table flag. Run MissingIndexesAndConstraintsQuench directly with
        // DropUnknownIndexes=1 so STEP 8 (removed-from-product) fires; the flag gates suppression.
        var withoutIndex = """
            [
            {
                "Name": "`IdxDropSuppressed`",
                "DropIndexesRemovedFromProduct": false,
                "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false }, { "Name": "`Val`", "DataType": "INT", "Nullable": true } ],
                "Indexes": []
            },
            {
                "Name": "`IdxDropControl`",
                "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false }, { "Name": "`Val`", "DataType": "INT", "Nullable": true } ],
                "Indexes": []
            }
            ]
            """;
        cmd.CommandTimeout = 300;
        cmd.CommandText =
            $"CALL SchemaSmith_ParseTableJson('{_mainDb}', '{withoutIndex.Replace("'", "''")}'); " +
            $"CALL SchemaSmith_MissingIndexesAndConstraintsQuench('{IndexProduct}', '{_mainDb}', 0, 1, 1, 1);";
        cmd.ExecuteNonQuery();

        conn.Close();
    }
}
