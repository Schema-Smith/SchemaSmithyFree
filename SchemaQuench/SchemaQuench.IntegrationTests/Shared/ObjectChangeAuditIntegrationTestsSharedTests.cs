// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

#nullable enable
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

// Drives the MySQL object-change audit weaving (#243 E5). RunTableQuenchProc calls the
// SchemaSmith_TableQuench proc directly (not via DatabaseQuench.Execute), so the audit rows are NOT
// drained by the reader — we query SchemaSmith_ChangeAudit directly on the same session to verify
// each proc emits the right (ObjectType, ObjectName, ActionType) rows as it runs DDL.
public abstract class ObjectChangeAuditIntegrationTestsSharedTests : BaseTableQuenchTests
{
    private const string Product = "ObjectChangeAudit Tests";

    [Test]
    public void TableQuench_EmitsObjectChangeAudit_ForCreateModifyDrop()
    {
        if (!TargetSupportsCheckConstraints())
            Assert.Ignore("Exercises CHECK-constraint audit rows; CHECK requires MySQL 8.0.16 — skipped below the floor.");
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        DropIfExists(cmd, "AuditChild");
        DropIfExists(cmd, "AuditParent");
        DropIfExists(cmd, "AuditDrop");
        ClearAudit(cmd);

        // Phase 1 — create parent + child + a to-be-dropped table.
        RunTableQuenchProc(cmd, CreateJson, productName: Product);
        var created = ReadAudit(cmd);

        Assert.That(created.Any(r => r is { Type: "table", Action: "created" } && r.Name.Contains("AuditParent")),
            "expected table/created for AuditParent");
        Assert.That(created.Any(r => r is { Type: "table", Action: "created" } && r.Name.Contains("AuditChild")),
            "expected table/created for AuditChild");
        Assert.That(created.Any(r => r is { Type: "index", Action: "created" } && r.Name.Contains("IX_AuditChild")),
            "expected index/created for IX_AuditChild_Name");
        Assert.That(created.Any(r => r is { Type: "constraint", Action: "created" } && r.Name.Contains("CK_AuditChild")),
            "expected constraint/created for the check constraint");
        Assert.That(created.Any(r => r is { Type: "foreignKey", Action: "created" } && r.Name.Contains("FK_AuditChild")),
            "expected foreignKey/created for FK_AuditChild_Parent");

        ClearAudit(cmd);

        // Phase 2 — widen Name (column modify); drop the index + FK by absence; drop the AuditDrop
        // table (removed from product).
        RunTableQuenchProc(cmd, ModifyJson, productName: Product, dropTablesRemovedFromProduct: true);
        var changed = ReadAudit(cmd);

        Assert.That(changed.Any(r => r is { Type: "column", Action: "modified" } && r.Name.Contains("Name")),
            "expected column/modified for Name");
        Assert.That(changed.Any(r => r is { Type: "index", Action: "dropped" } && r.Name.Contains("IX_AuditChild")),
            "expected index/dropped for IX_AuditChild_Name");
        Assert.That(changed.Any(r => r is { Type: "foreignKey", Action: "dropped" }),
            "expected foreignKey/dropped for FK_AuditChild_Parent");
        Assert.That(changed.Any(r => r is { Type: "constraint", Action: "dropped" } && r.Name.Contains("CK_AuditChild")),
            "expected constraint/dropped for the check constraint");
        Assert.That(changed.Any(r => r is { Type: "table", Action: "dropped" } && r.Name.Contains("AuditDrop")),
            "expected table/dropped for AuditDrop");
        Assert.That(changed.Any(r => r is { Type: "column", Action: "created" } && r.Name.Contains("Calc")),
            "expected column/created for the generated column Calc added to the existing table");

        DropIfExists(cmd, "AuditChild");
        DropIfExists(cmd, "AuditParent");
        DropIfExists(cmd, "AuditDrop");
        conn.Close();
    }

    [Test]
    public void TableQuench_WhatIf_EmitsWouldChangeAudit_WithoutRunningDdl()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        DropIfExists(cmd, "AuditChild");
        DropIfExists(cmd, "AuditParent");
        DropIfExists(cmd, "AuditDrop");
        DropIfExists(cmd, "AuditNew");
        ClearAudit(cmd);

        // Baseline: really create parent + child + a to-be-dropped table.
        RunTableQuenchProc(cmd, CreateJson, productName: Product);
        ClearAudit(cmd);

        // WhatIf re-quench (#363): add a new table + column (wouldCreate), widen Name (wouldModify),
        // drop the index/FK by absence + AuditDrop by absence (wouldDrop).
        RunTableQuenchProc(cmd, WhatIfJson, productName: Product, whatIf: true, dropTablesRemovedFromProduct: true);
        var wi = ReadAudit(cmd);

        Assert.Multiple(() =>
        {
            Assert.That(wi.Any(r => r is { Type: "table", Action: "wouldCreate" } && r.Name.Contains("AuditNew")),
                "expected table/wouldCreate for AuditNew");
            Assert.That(wi.Any(r => r is { Type: "column", Action: "wouldCreate" } && r.Name.Contains("Extra")),
                "expected column/wouldCreate for the new Extra column");
            Assert.That(wi.Any(r => r is { Type: "index", Action: "wouldCreate" } && r.Name.Contains("IX_AuditChild_Extra")),
                "expected index/wouldCreate for the new IX_AuditChild_Extra");
            Assert.That(wi.Any(r => r is { Type: "column", Action: "wouldModify" } && r.Name.Contains("Name")),
                "expected column/wouldModify for the widened Name column");
            Assert.That(wi.Any(r => r is { Type: "index", Action: "wouldDrop" } && r.Name.Contains("IX_AuditChild_Name")),
                "expected index/wouldDrop for IX_AuditChild_Name");
            Assert.That(wi.Any(r => r is { Type: "foreignKey", Action: "wouldDrop" }),
                "expected foreignKey/wouldDrop for FK_AuditChild_Parent");
            Assert.That(wi.Any(r => r is { Type: "table", Action: "wouldDrop" } && r.Name.Contains("AuditDrop")),
                "expected table/wouldDrop for AuditDrop");
            Assert.That(wi.All(r => r.Action is not ("created" or "modified" or "dropped")),
                "WhatIf must not emit executed-change actions");
        });

        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AuditNew'";
        Assert.That(System.Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "WhatIf must not create AuditNew");
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AuditDrop'";
        Assert.That(System.Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1), "WhatIf must not drop AuditDrop");
        cmd.CommandText = "SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AuditChild' AND COLUMN_NAME = 'Name'";
        Assert.That(System.Convert.ToInt64(cmd.ExecuteScalar()), Is.EqualTo(50), "WhatIf must not widen Name (VARCHAR(50))");

        DropIfExists(cmd, "AuditChild");
        DropIfExists(cmd, "AuditParent");
        DropIfExists(cmd, "AuditDrop");
        conn.Close();
    }

    private const string CreateJson = """
        [
        {
            "Name": "`AuditParent`",
            "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "`PK_AuditParent`", "PrimaryKey": true, "IndexColumns": "`Id`" } ]
        },
        {
            "Name": "`AuditChild`",
            "Columns": [
                { "Name": "`Id`", "DataType": "INT", "Nullable": false },
                { "Name": "`ParentId`", "DataType": "INT", "Nullable": false },
                { "Name": "`Name`", "DataType": "VARCHAR(50)", "Nullable": true }
            ],
            "Indexes": [
                { "Name": "`PK_AuditChild`", "PrimaryKey": true, "IndexColumns": "`Id`" },
                { "Name": "`IX_AuditChild_Name`", "IndexColumns": "`Name`", "Unique": false, "PrimaryKey": false }
            ],
            "ForeignKeys": [
                { "Name": "`FK_AuditChild_Parent`", "Columns": "`ParentId`", "RelatedTable": "`AuditParent`", "RelatedColumns": "`Id`" }
            ],
            "CheckConstraints": [
                { "Name": "`CK_AuditChild_Ids`", "Expression": "`Id` <> `ParentId`" }
            ]
        },
        {
            "Name": "`AuditDrop`",
            "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "`PK_AuditDrop`", "PrimaryKey": true, "IndexColumns": "`Id`" } ]
        }
        ]
        """;

    private const string ModifyJson = """
        [
        {
            "Name": "`AuditParent`",
            "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "`PK_AuditParent`", "PrimaryKey": true, "IndexColumns": "`Id`" } ]
        },
        {
            "Name": "`AuditChild`",
            "Columns": [
                { "Name": "`Id`", "DataType": "INT", "Nullable": false },
                { "Name": "`ParentId`", "DataType": "INT", "Nullable": false },
                { "Name": "`Name`", "DataType": "VARCHAR(100)", "Nullable": true },
                { "Name": "`Calc`", "DataType": "INT", "GeneratedExpression": "`Id` + `ParentId`" }
            ],
            "Indexes": [ { "Name": "`PK_AuditChild`", "PrimaryKey": true, "IndexColumns": "`Id`" } ]
        }
        ]
        """;

    private const string WhatIfJson = """
        [
        {
            "Name": "`AuditParent`",
            "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "`PK_AuditParent`", "PrimaryKey": true, "IndexColumns": "`Id`" } ]
        },
        {
            "Name": "`AuditChild`",
            "Columns": [
                { "Name": "`Id`", "DataType": "INT", "Nullable": false },
                { "Name": "`ParentId`", "DataType": "INT", "Nullable": false },
                { "Name": "`Name`", "DataType": "VARCHAR(100)", "Nullable": true },
                { "Name": "`Extra`", "DataType": "INT", "Nullable": true }
            ],
            "Indexes": [
                { "Name": "`PK_AuditChild`", "PrimaryKey": true, "IndexColumns": "`Id`" },
                { "Name": "`IX_AuditChild_Extra`", "IndexColumns": "`Extra`", "Unique": false, "PrimaryKey": false }
            ]
        },
        {
            "Name": "`AuditNew`",
            "Columns": [ { "Name": "`Id`", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "`PK_AuditNew`", "PrimaryKey": true, "IndexColumns": "`Id`" } ]
        }
        ]
        """;

    private static void DropIfExists(IDbCommand cmd, string table)
    {
        cmd.CommandText = $"DROP TABLE IF EXISTS `{table}`";
        cmd.ExecuteNonQuery();
    }

    private static void ClearAudit(IDbCommand cmd)
    {
        cmd.CommandText = "DELETE FROM SchemaSmith_ChangeAudit WHERE SessionId = CONNECTION_ID()";
        cmd.ExecuteNonQuery();
    }

    private static List<AuditRow> ReadAudit(IDbCommand cmd)
    {
        cmd.CommandText = "SELECT ObjectType, ObjectName, ActionType FROM SchemaSmith_ChangeAudit WHERE SessionId = CONNECTION_ID() ORDER BY Id";
        using var reader = cmd.ExecuteReader();
        var rows = new List<AuditRow>();
        while (reader.Read())
            rows.Add(new AuditRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return rows;
    }

    private sealed record AuditRow(string Type, string Name, string Action);
}
