// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;

using NUnit.Framework;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Regression coverage for #289: dropping a table removed from the product must first drop any
/// foreign key that REFERENCES it. A kept table keeps its FK columns, but the product no longer
/// declares the FK and no longer declares the referenced table. The removed-table drop ran before
/// the FK drop, so the table drop failed while the inbound FK still referenced it.
///
/// Each test owns a UNIQUE product name so DropTablesRemovedFromProduct is scoped to its own tables
/// and never drops a sibling test's tables under parallel execution.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Parallelizable(scope: ParallelScope.All)]
[Category("Integration")]
public class TableQuench_RemovedTableInboundFkTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_DropsInboundForeignKeyBeforeDroppingRemovedTable()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var product = $"InboundFkProduct_{uniqueId}";
        var parent = $"InboundFkParent_{uniqueId}"; // referenced table, removed in the second quench
        var child = $"InboundFkChild_{uniqueId}";    // kept table that holds the inbound FK
        var fkName = $"FK_{child}_{parent}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            // Establish product ownership of both tables, with the inbound FK in place.
            RunTableQuenchProc(cmd, WithParentAndFk(parent, child, fkName), productName: product);
            Assert.That(ObjectExists(cmd, parent), Is.True, "Setup: parent table should exist after the first quench.");
            Assert.That(ForeignKeyExists(cmd, child, fkName), Is.True, "Setup: inbound FK should exist after the first quench.");

            // Remove the parent table AND the child's FK from the product, with autodrop on.
            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, ChildOnly(child), dropTablesRemovedFromProduct: true, productName: product),
                "Quench must drop the inbound FK before dropping the removed table (#289).");

            Assert.Multiple(() =>
            {
                Assert.That(ObjectExists(cmd, parent), Is.False, "Removed parent table should be dropped.");
                Assert.That(ForeignKeyExists(cmd, child, fkName), Is.False, "Inbound FK should be dropped.");
                Assert.That(ObjectExists(cmd, child), Is.True, "Kept child table must survive.");
            });
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`{child}`; DROP TABLE IF EXISTS `{_mainDb}`.`{parent}`;";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    // WhatIf must show the same picture as apply: it previews BOTH the inbound-FK drop and the
    // removed-table drop, and changes nothing.
    [Test]
    public void TableQuench_WhatIfPreviewsInboundFkDropAndTableDropWithoutApplying()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var product = $"InboundFkWiProduct_{uniqueId}";
        var parent = $"InboundFkWiParent_{uniqueId}";
        var child = $"InboundFkWiChild_{uniqueId}";
        var fkName = $"FK_{child}_{parent}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            RunTableQuenchProc(cmd, WithParentAndFk(parent, child, fkName), productName: product);

            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();

            RunTableQuenchProc(cmd, ChildOnly(child), dropTablesRemovedFromProduct: true, whatIf: true, productName: product);

            cmd.CommandText = "SELECT GROUP_CONCAT(Message SEPARATOR ' | ') FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            var preview = cmd.ExecuteScalar()?.ToString() ?? "";

            Assert.Multiple(() =>
            {
                Assert.That(preview, Does.Contain(fkName),
                    $"WhatIf must preview the inbound FK drop. Preview: {preview}");
                Assert.That(preview, Does.Contain(parent),
                    $"WhatIf must preview the removed-table drop. Preview: {preview}");
                Assert.That(ObjectExists(cmd, parent), Is.True, "WhatIf must NOT drop the table.");
                Assert.That(ForeignKeyExists(cmd, child, fkName), Is.True, "WhatIf must NOT drop the inbound FK.");
            });
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`{child}`; DROP TABLE IF EXISTS `{_mainDb}`.`{parent}`;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    private static string WithParentAndFk(string parent, string child, string fkName) => $$"""
[
  {
    "Name": "{{parent}}",
    "Columns": [ { "Name": "`Id`", "DataType": "int", "Nullable": false } ],
    "Indexes": [ { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "`Id`" } ]
  },
  {
    "Name": "{{child}}",
    "Columns": [
      { "Name": "`Id`",       "DataType": "int", "Nullable": false },
      { "Name": "`ParentId`", "DataType": "int", "Nullable": false }
    ],
    "Indexes": [
      { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "`Id`" },
      { "Name": "IX_{{child}}_ParentId", "IndexColumns": "`ParentId`" }
    ],
    "ForeignKeys": [
      { "Name": "{{fkName}}", "Columns": "`ParentId`", "RelatedTable": "`{{parent}}`", "RelatedColumns": "`Id`", "DeleteAction": "NO ACTION", "UpdateAction": "NO ACTION", "RelatedTableSchema": "" }
    ]
  }
]
""";

    private static string ChildOnly(string child) => $$"""
[
  {
    "Name": "{{child}}",
    "Columns": [
      { "Name": "`Id`",       "DataType": "int", "Nullable": false },
      { "Name": "`ParentId`", "DataType": "int", "Nullable": false }
    ],
    "Indexes": [
      { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "`Id`" },
      { "Name": "IX_{{child}}_ParentId", "IndexColumns": "`ParentId`" }
    ]
  }
]
""";

    private bool ObjectExists(IDbCommand cmd, string tableName)
    {
        cmd.CommandText = $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
 WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
