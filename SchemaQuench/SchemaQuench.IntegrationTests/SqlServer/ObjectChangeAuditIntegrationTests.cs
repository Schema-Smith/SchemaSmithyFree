// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Data;
using System.Linq;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

// Drives the SQL Server object-change audit weaving (#243 E5). RunTableQuenchProc calls the
// SchemaSmith.TableQuench proc directly (not via DatabaseQuench.Execute), so the audit rows are NOT
// drained by the reader — we query SchemaSmith.ChangeAudit directly on the same session to verify
// each proc emits the right (ObjectType, ObjectName, ActionType) rows as it runs DDL.
[Category("SqlServer")]
public class ObjectChangeAuditIntegrationTests : BaseTableQuenchTests
{
    private const string Product = "ObjectChangeAudit Tests";

    [Test]
    public void TableQuench_EmitsObjectChangeAudit_ForCreateModifyDrop()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        DropIfExists(cmd, "dbo.AuditChild");
        DropIfExists(cmd, "dbo.AuditParent");
        DropIfExists(cmd, "dbo.AuditDrop");
        ClearAudit(cmd);

        // Phase 1 — create parent + child + a to-be-dropped table: exercises table create, column
        // create, index create, PK/constraint create, check-constraint create, and FK create across
        // all four table procs.
        RunTableQuenchProc(cmd, CreateJson, productName: Product);
        var created = ReadAudit(cmd);

        Assert.That(created.Any(r => r is { Type: "table", Action: "created" } && r.Name.Contains("AuditParent")),
            "expected table/created for AuditParent");
        Assert.That(created.Any(r => r is { Type: "table", Action: "created" } && r.Name.Contains("AuditChild")),
            "expected table/created for AuditChild");
        Assert.That(created.Any(r => r is { Type: "index", Action: "created" } && r.Name.Contains("IX_AuditChild")),
            "expected index/created for IX_AuditChild_Name");
        Assert.That(created.Any(r => r is { Type: "constraint", Action: "created" } && r.Name.Contains("PK_AuditChild")),
            "expected constraint/created for the primary key");
        Assert.That(created.Any(r => r is { Type: "constraint", Action: "created" } && r.Name.Contains("CK_AuditChild")),
            "expected constraint/created for the check constraint");
        Assert.That(created.Any(r => r is { Type: "foreignKey", Action: "created" } && r.Name.Contains("FK_AuditChild")),
            "expected foreignKey/created for FK_AuditChild_Parent");
        Assert.That(created.Any(r => r is { Type: "column", Action: "created" } && r.Name.Contains("Calc")),
            "expected column/created for the computed column Calc");
        Assert.That(created.Any(r => r is { Type: "statistic", Action: "created" } && r.Name.Contains("ST_AuditChild")),
            "expected statistic/created for ST_AuditChild_Id");

        ClearAudit(cmd);

        // Phase 2 — widen Name (column modify); drop the index, FK, and check by absence; drop the
        // AuditDrop table (removed from product).
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

        DropIfExists(cmd, "dbo.AuditChild");
        DropIfExists(cmd, "dbo.AuditParent");
        DropIfExists(cmd, "dbo.AuditDrop");
        conn.Close();
    }

    private const string CreateJson = """
        [
        {
            "Schema": "[dbo]", "Name": "[AuditParent]",
            "Columns": [ { "Name": "[Id]", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "[PK_AuditParent]", "PrimaryKey": true, "IndexColumns": "[Id]" } ]
        },
        {
            "Schema": "[dbo]", "Name": "[AuditChild]",
            "Columns": [
                { "Name": "[Id]", "DataType": "INT", "Nullable": false },
                { "Name": "[ParentId]", "DataType": "INT", "Nullable": false },
                { "Name": "[Name]", "DataType": "NVARCHAR(50)", "Nullable": true },
                { "Name": "[Calc]", "DataType": "INT", "ComputedExpression": "[Id]+[ParentId]" }
            ],
            "Indexes": [
                { "Name": "[PK_AuditChild]", "PrimaryKey": true, "IndexColumns": "[Id]" },
                { "Name": "[IX_AuditChild_Name]", "IndexColumns": "[Name]" }
            ],
            "ForeignKeys": [
                { "Name": "[FK_AuditChild_Parent]", "Columns": "[ParentId]", "RelatedTable": "[AuditParent]", "RelatedTableSchema": "[dbo]", "RelatedColumns": "[Id]" }
            ],
            "CheckConstraints": [
                { "Name": "CK_AuditChild_Ids", "Expression": "[Id]<>[ParentId]" }
            ],
            "Statistics": [
                { "Name": "ST_AuditChild_Id", "Columns": "[Id]" }
            ]
        },
        {
            "Schema": "[dbo]", "Name": "[AuditDrop]",
            "Columns": [ { "Name": "[Id]", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "[PK_AuditDrop]", "PrimaryKey": true, "IndexColumns": "[Id]" } ]
        }
        ]
        """;

    private const string ModifyJson = """
        [
        {
            "Schema": "[dbo]", "Name": "[AuditParent]",
            "Columns": [ { "Name": "[Id]", "DataType": "INT", "Nullable": false } ],
            "Indexes": [ { "Name": "[PK_AuditParent]", "PrimaryKey": true, "IndexColumns": "[Id]" } ]
        },
        {
            "Schema": "[dbo]", "Name": "[AuditChild]",
            "Columns": [
                { "Name": "[Id]", "DataType": "INT", "Nullable": false },
                { "Name": "[ParentId]", "DataType": "INT", "Nullable": false },
                { "Name": "[Name]", "DataType": "NVARCHAR(100)", "Nullable": true },
                { "Name": "[Calc]", "DataType": "INT", "ComputedExpression": "[Id]+[ParentId]" }
            ],
            "Indexes": [ { "Name": "[PK_AuditChild]", "PrimaryKey": true, "IndexColumns": "[Id]" } ],
            "Statistics": [
                { "Name": "ST_AuditChild_Id", "Columns": "[Id]" }
            ]
        }
        ]
        """;

    private static void DropIfExists(IDbCommand cmd, string qualifiedName)
    {
        cmd.CommandText = $"IF OBJECT_ID('{qualifiedName}') IS NOT NULL DROP TABLE {qualifiedName}";
        cmd.ExecuteNonQuery();
    }

    private static void ClearAudit(IDbCommand cmd)
    {
        cmd.CommandText = "DELETE FROM SchemaSmith.ChangeAudit WHERE SessionId = @@SPID";
        cmd.ExecuteNonQuery();
    }

    private static List<AuditRow> ReadAudit(IDbCommand cmd)
    {
        cmd.CommandText = "SELECT ObjectType, ObjectName, ActionType FROM SchemaSmith.ChangeAudit WHERE SessionId = @@SPID ORDER BY Id";
        using var reader = cmd.ExecuteReader();
        var rows = new List<AuditRow>();
        while (reader.Read())
            rows.Add(new AuditRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return rows;
    }

    private sealed record AuditRow(string Type, string Name, string Action);
}
