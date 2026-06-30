// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.MySQL;

[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_DropForeignKeysTests : BaseTableQuenchTests
{
    // A dedicated product name so concurrent MySQL tests (which share the default product name)
    // do not reconcile this test's product-owned FKs: MySQL STEP 4 drops ALL FKs owned by a
    // product that are absent from the CURRENT quench's definition, so a sibling quench under
    // the shared product name would otherwise drop FK_FkDropSuppressed_Ref out from under us.
    private const string FkProductName = "FK Drop Tests";

    // The FKs are deployed through the product (so they are owned), then removed in a second
    // quench. RunTableQuenchProc passes DropUnknownIndexes = 0, so FkDropControl's FK dropping
    // proves the D-MYSQL-FK decouple: MySQL now cleans up orphaned foreign keys by absence
    // WITHOUT requiring DropUnknownIndexes (the ### Fixed). Non-vacuous: FkDropSuppressed sets
    // DropForeignKeysRemovedFromProduct:false and its FK survives in the same quench.
    [Test]
    public void TableQuench_ShouldSuppressForeignKeyDropWhenTableFlagIsFalse()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = '{_mainDb}' AND CONSTRAINT_NAME = 'FK_FkDropSuppressed_Ref' AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1), "FK_FkDropSuppressed_Ref should still exist (suppressed by table flag).");

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = '{_mainDb}' AND CONSTRAINT_NAME = 'FK_FkDropControl_Ref' AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "FK_FkDropControl_Ref should be gone (dropped by absence with DropUnknownIndexes=0).");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Quench 1: deploy the tables WITH their foreign keys so they become product-owned.
        var withFks = """
            [
            {
                "Name": "FkDropRef",
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false } ],
                "Indexes": [ { "Name": "PK_FkDropRef", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" } ]
            },
            {
                "Name": "FkDropSuppressed",
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false }, { "Name": "RefId", "DataType": "INT", "Nullable": true } ],
                "Indexes": [ { "Name": "PK_FkDropSuppressed", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" } ],
                "ForeignKeys": [ { "Name": "FK_FkDropSuppressed_Ref", "Columns": "RefId", "RelatedTable": "FkDropRef", "RelatedColumns": "Id" } ]
            },
            {
                "Name": "FkDropControl",
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false }, { "Name": "RefId", "DataType": "INT", "Nullable": true } ],
                "Indexes": [ { "Name": "PK_FkDropControl", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" } ],
                "ForeignKeys": [ { "Name": "FK_FkDropControl_Ref", "Columns": "RefId", "RelatedTable": "FkDropRef", "RelatedColumns": "Id" } ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, withFks, productName: FkProductName);

        // Quench 2: remove the foreign keys. FkDropSuppressed protects its own FK with the flag.
        var withoutFks = """
            [
            {
                "Name": "FkDropRef",
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false } ],
                "Indexes": [ { "Name": "PK_FkDropRef", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" } ]
            },
            {
                "Name": "FkDropSuppressed",
                "DropForeignKeysRemovedFromProduct": false,
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false }, { "Name": "RefId", "DataType": "INT", "Nullable": true } ],
                "Indexes": [ { "Name": "PK_FkDropSuppressed", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" } ]
            },
            {
                "Name": "FkDropControl",
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false }, { "Name": "RefId", "DataType": "INT", "Nullable": true } ],
                "Indexes": [ { "Name": "PK_FkDropControl", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" } ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, withoutFks, productName: FkProductName);

        conn.Close();
    }
}
