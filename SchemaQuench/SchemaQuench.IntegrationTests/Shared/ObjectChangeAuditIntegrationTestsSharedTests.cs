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
