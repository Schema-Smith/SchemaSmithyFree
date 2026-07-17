// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

public abstract class TableQuench_DropCompositeFkIndexSharedTests : BaseTableQuenchTests
{
    // Unique product name: STEP 8's removed-from-product detection is keyed off
    // SchemaSmith_ProductOwnership, so an isolated name keeps a concurrent sibling quench
    // from interfering (same rationale as the single-column FK/index drop test siblings).
    private const string CompositeFkProduct = "Composite FK Index Drop Tests";

    // Regression test for STEP 8's FK-drop-before-index-drop path: when a composite FK backs a
    // composite UNIQUE index that is being dropped, the FK-drop detection join produced one row
    // PER FK COLUMN (KEY_COLUMN_USAGE has one row per column of a composite FK), so the same
    // DROP FOREIGN KEY statement was queued twice. The second execution raised MySQL error 1091
    // ("Can't DROP ...; check that column/key exists"), and the whole quench failed. The fix adds
    // SELECT DISTINCT to the FK-drop statement materialization so each FK is dropped exactly once.
    //
    // Two-phase, mirroring TableQuench_DropIndexesTests / TableQuench_DropForeignKeysTests:
    // Phase 1 (OneTimeSetUp) quenches CompFkParent (with its composite UNIQUE index uq_parent_ab)
    // and CompFkChild (with its composite FK referencing that index) into existence via the full
    // SchemaSmith_TableQuench pipeline, so the index becomes product-owned and the FK is created
    // against it (STEP 8/ForeignKeyQuench ordering: indexes/constraints are created before FKs).
    // Phase 2 reconciles CompFkParent with uq_parent_ab removed from its definition, calling
    // ParseTableJson + MissingIndexesAndConstraintsQuench directly (NOT the full TableQuench) so
    // ForeignKeyQuench does not also run in the same call and interfere with STEP 8's own
    // FK-drop-before-index-drop logic. Before the fix, phase 2 throws (1091) and OneTimeSetUp
    // fails the fixture; after the fix, it succeeds and the assertions below hold.
    [Test]
    public void TableQuench_ShouldDropCompositeForeignKeyOnceBeforeDroppingBackingUniqueIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = 'CompFkParent' AND INDEX_NAME = 'uq_parent_ab'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "uq_parent_ab should be gone (removed from product definition).");

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = '{_mainDb}' AND TABLE_NAME = 'CompFkChild' AND CONSTRAINT_NAME = 'FK_CompFkChild_Parent_AB' AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "The composite FK backed by uq_parent_ab should have been dropped exactly once (not fail with 1091 on a duplicate drop).");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Phase 1 — quench parent (with composite UNIQUE index) and child (with composite FK
        // referencing it) into existence via the full pipeline, so uq_parent_ab becomes
        // product-owned and the composite FK is created against it.
        var withIndex = """
            [
            {
                "Name": "CompFkParent",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "ColA", "DataType": "INT", "Nullable": false },
                    { "Name": "ColB", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "PK_CompFkParent", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" },
                    { "Name": "uq_parent_ab", "PrimaryKey": false, "Unique": true, "IndexColumns": "ColA,ColB" }
                ]
            },
            {
                "Name": "CompFkChild",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "ColA", "DataType": "INT", "Nullable": true },
                    { "Name": "ColB", "DataType": "INT", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PK_CompFkChild", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" }
                ],
                "ForeignKeys": [
                    { "Name": "FK_CompFkChild_Parent_AB", "Columns": "ColA,ColB", "RelatedTable": "CompFkParent", "RelatedColumns": "ColA,ColB" }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, withIndex, productName: CompositeFkProduct);

        // Phase 2 — reconcile CompFkParent with uq_parent_ab removed from its definition. Run
        // MissingIndexesAndConstraintsQuench directly (STEP 8, DropUnknownIndexes=1) so the
        // FK-drop-before-index-drop path fires WITHOUT also running ForeignKeyQuench in the same
        // call (the full TableQuench would try to reconcile/recreate the FK afterward, which is
        // not what this regression targets). CompFkChild need not be listed: STEP 8's FK-before-
        // index join reads live INFORMATION_SCHEMA.KEY_COLUMN_USAGE / TABLE_CONSTRAINTS, not the
        // current quench's table list.
        var withoutIndex = """
            [
            {
                "Name": "CompFkParent",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "ColA", "DataType": "INT", "Nullable": false },
                    { "Name": "ColB", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "PK_CompFkParent", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" }
                ]
            }
            ]
            """;
        cmd.CommandTimeout = 300;
        cmd.CommandText =
            $"CALL SchemaSmith_ParseTableJson('{_mainDb}', '{withoutIndex.Replace("'", "''")}'); " +
            $"CALL SchemaSmith_MissingIndexesAndConstraintsQuench('{CompositeFkProduct}', '{_mainDb}', 0, 1, 1, 1);";
        cmd.ExecuteNonQuery();

        conn.Close();
    }
}
