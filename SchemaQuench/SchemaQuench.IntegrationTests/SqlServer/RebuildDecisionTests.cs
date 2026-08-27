// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.Threading;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// The rebuild DECISION point on SQL Server: which tables ModifiedTableQuench elects for a rebuild, and
/// -- far more important -- which it must not. RebuildTableTests covers the rebuild engine itself; this
/// fixture covers the one thing that decides whether that engine is ever pointed at a user's table.
///
/// Everything here runs the whole SchemaSmith.TableQuench path (parse -> missing -> modified -> indexes ->
/// foreign keys) rather than calling RebuildTable directly, because the decision only exists inside that
/// path and a test that reached around it would prove nothing about it.
///
/// ANTI-VACUITY. "Nothing happened" is the passing state for half of these tests, so every one of them has
/// to be able to tell "nothing happened" apart from "the test asserted nothing". Two devices do that:
///   * a LIVE-ONLY MARKER INDEX that the package never declares. RebuildTable drops the old table whole and
///     re-creates nothing but columns, and no downstream pass re-adds an index the package does not carry,
///     so the marker surviving is proof the table was NOT replaced and the marker being gone is proof it was.
///   * OBJECT_ID, captured before and compared after -- a rebuild swaps in a different object.
/// And every test asserts its preconditions, so a setup that silently failed to produce the drift under
/// test cannot read as a pass.
/// </summary>
[Category("SqlServer")]
[NonParallelizable]
public class RebuildDecisionTests : BaseTableQuenchTests
{
    private const string MarkerIndex = "ix_rebuild_decision_marker";

    // ---- harness ------------------------------------------------------------

    private IDbConnection OpenMainDb()
    {
        var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        return conn;
    }

    private static string Lit(string value) => "'" + value.Replace("'", "''") + "'";

    private static string Uid() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Runs the real TableQuench with the resolved upper-tier policy attached. Passing no mode leaves the
    /// procedure's own defaults in place, which is exactly the shape every caller that predates
    /// RebuildPolicy produces -- so the "no policy anywhere" test really is the untouched code path.
    /// </summary>
    private void Quench(IDbCommand cmd, string json, string mode = null, int? threshold = null, bool whatIf = false)
    {
        var policy = mode == null
            ? ""
            : $", @RebuildPolicyMode = N'{mode}', @RebuildPolicyThreshold = {(threshold.HasValue ? threshold.Value.ToString() : "NULL")}";
        var sql = $"EXEC SchemaSmith.TableQuench @ProductName = {Lit(_productName)}, "
                  + $"@TableDefinitions = {Lit(json)}, @WhatIf = {(whatIf ? 1 : 0)}, "
                  + $"@DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0{policy}";
        ExecWithDeadlockRetry(cmd, sql);
    }

    private static void Exec(IDbCommand cmd, string sql)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Sibling table fixtures do DDL in the same database and SQL Server picks a deadlock victim; the
    /// existing quench fixtures retry for the same reason. Retrying beats narrowing the run to the mode
    /// that happens to pass.
    /// </summary>
    private static void ExecWithDeadlockRetry(IDbCommand cmd, string sql)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Exec(cmd, sql);
                return;
            }
            catch (Exception e) when (e.Message.ContainsIgnoringCase("deadlock victim"))
            {
                Thread.Sleep(1000);
            }
        }

        // Out of retries -- run unguarded so the real error surfaces instead of a synthesized one.
        Exec(cmd, sql);
    }

    private static object Scalar(IDbCommand cmd, string sql)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private static int Int(IDbCommand cmd, string sql)
    {
        var v = Scalar(cmd, sql);
        return v == null || v == DBNull.Value ? -1 : Convert.ToInt32(v);
    }

    private static string Str(IDbCommand cmd, string sql)
    {
        var v = Scalar(cmd, sql);
        return v == null || v == DBNull.Value ? null : v.ToString();
    }

    private static int ObjectId(IDbCommand cmd, string table) => Int(cmd, $"SELECT OBJECT_ID('dbo.{table}')");

    private static int MarkerCount(IDbCommand cmd, string table)
        => Int(cmd, $"SELECT COUNT(*) FROM sys.indexes WHERE [object_id] = OBJECT_ID('dbo.{table}') AND [name] = {Lit(MarkerIndex)}");

    private static string ColumnType(IDbCommand cmd, string table, string column)
        => Str(cmd, "SELECT DATA_TYPE + ISNULL('(' + CONVERT(NVARCHAR(10), CHARACTER_MAXIMUM_LENGTH) + ')', '') "
                    + $"FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = {Lit(table)} AND COLUMN_NAME = {Lit(column)}");

    private static int RowCount(IDbCommand cmd, string table) => Int(cmd, $"SELECT COUNT(*) FROM dbo.[{table}]");

    private static int AuditCount(IDbCommand cmd, string table, string action)
        => Int(cmd, "SELECT COUNT(*) FROM SchemaSmith.ChangeAudit "
                    + $"WHERE ObjectName = {Lit("[dbo].[" + table + "]")} AND ActionType = {Lit(action)}");

    private static int OwnershipPropertyCount(IDbCommand cmd, string table)
        => Int(cmd, $"SELECT COUNT(*) FROM fn_listextendedproperty(N'ProductName', N'Schema', 'dbo', N'Table', {Lit(table)}, default, default)");

    // ---- package shapes -----------------------------------------------------

    /// <summary>
    /// Three NVARCHAR columns whose declared width is the only thing that varies, so the number of
    /// column-modification passes a deploy produces is exactly the number of widths that differ from the
    /// deployed 10. Marker is never declared narrower or wider -- it must stay out of the change count so
    /// the index on it is never dropped as a dependent of a changing column.
    /// </summary>
    private static string Package(string table, int aWidth = 10, int bWidth = 10, int cWidth = 10,
        string rebuildPolicy = null)
    {
        var policy = rebuildPolicy == null ? "" : $"\"RebuildPolicy\": {rebuildPolicy},";
        return $$"""
            [{
                "Schema": "[dbo]",
                "Name": "[{{table}}]",
                {{policy}}
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Marker]", "DataType": "INT", "Nullable": true},
                    {"Name": "[A]", "DataType": "NVARCHAR({{aWidth}})", "Nullable": true},
                    {"Name": "[B]", "DataType": "NVARCHAR({{bWidth}})", "Nullable": true},
                    {"Name": "[C]", "DataType": "NVARCHAR({{cWidth}})", "Nullable": true}
                ]
            }]
            """;
    }

    /// <summary>
    /// Deploys the table at its baseline shape through the real quench (so it carries the ownership
    /// extended property a live table would have), gives it rows and the live-only marker index, and
    /// asserts that starting state. Every test begins here so a later "nothing changed" assertion is
    /// measured against a state that was verified, not assumed.
    /// </summary>
    private string Arrange(IDbCommand cmd)
    {
        var table = "RebuildDecision_" + Uid();
        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        Quench(cmd, Package(table));
        Exec(cmd, $"INSERT INTO dbo.[{table}] (Id, Marker, A, B, C) VALUES (1, 10, 'a1', 'b1', 'c1'), (2, 20, 'a2', 'b2', 'c2')");
        Exec(cmd, $"CREATE INDEX [{MarkerIndex}] ON dbo.[{table}] ([Marker])");

        Assert.That(MarkerCount(cmd, table), Is.EqualTo(1),
            "Setup precondition: the live-only marker index must exist, or its absence later proves nothing.");
        Assert.That(ColumnType(cmd, table, "A"), Is.EqualTo("nvarchar(10)"),
            "Setup precondition: column A must start at the narrow width the later package widens.");
        Assert.That(RowCount(cmd, table), Is.EqualTo(2), "Setup precondition: the table must hold rows to lose.");
        Assert.That(OwnershipPropertyCount(cmd, table), Is.EqualTo(1),
            "Setup precondition: the baseline quench must have stamped the ProductName extended property.");
        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.Zero,
            "Setup precondition: the baseline deploy must not itself have rebuilt anything.");
        return table;
    }

    // ---- THE SAFETY PROPERTY ------------------------------------------------

    [Test]
    public void NoPolicyAnywhere_AltersInPlace_AndNeverRebuilds()
    {
        // THE test this whole slice exists to satisfy. A rebuild moves user data, so an ordinary table --
        // one with real column changes and no RebuildPolicy at any level -- must behave exactly as it did
        // before the decision point existed. Not "does not happen to be rebuilt": cannot be.
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        // Three column modifications -- comfortably more than any threshold a default would plausibly use,
        // so this is not passing merely because there was too little change to trip anything.
        Quench(cmd, Package(table, aWidth: 50, bWidth: 50, cWidth: 50));

        Assert.That(ColumnType(cmd, table, "A"), Is.EqualTo("nvarchar(50)"),
            "The in-place alter must actually have happened. Without this the no-rebuild assertions below "
            + "would pass just as happily on a quench that did nothing at all.");
        Assert.That(MarkerCount(cmd, table), Is.EqualTo(1),
            "The live-only marker index must survive. A rebuild drops the old table whole and re-creates no "
            + "index the package does not declare, so a missing marker is a replaced table.");
        Assert.That(ObjectId(cmd, table), Is.EqualTo(objectIdBefore),
            "The table must be the SAME object. A rebuild swaps in a new one.");
        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.Zero,
            "No 'rebuilt' audit row may exist for a table that never asked to be rebuilt.");
        Assert.That(AuditCount(cmd, table, "wouldRebuild"), Is.Zero,
            "Nor a 'wouldRebuild' one -- the decision must not even have considered this table elected.");
        Assert.That(RowCount(cmd, table), Is.EqualTo(2), "Rows are untouched by an in-place alter.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    [Test]
    public void ModeNever_Explicitly_AltersInPlace_AndNeverRebuilds()
    {
        // The same outcome reached by SAYING NEVER rather than by saying nothing. Both have to work: the
        // default is what protects packages that predate the feature, the explicit value is what lets a
        // table opt OUT of a product- or environment-level policy that would otherwise catch it.
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        Quench(cmd, Package(table, aWidth: 50, bWidth: 50, cWidth: 50, rebuildPolicy: """{"Mode": "NEVER"}"""));

        Assert.That(ColumnType(cmd, table, "A"), Is.EqualTo("nvarchar(50)"), "The in-place alter must have happened.");
        Assert.That(MarkerCount(cmd, table), Is.EqualTo(1), "NEVER must leave the live-only marker index standing.");
        Assert.That(ObjectId(cmd, table), Is.EqualTo(objectIdBefore), "NEVER must leave the same object in place.");
        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.Zero, "NEVER must record no rebuild.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    [Test]
    public void TableLevelNever_OverridesAnAlwaysProductPolicy()
    {
        // The opt-out that matters operationally: one table inside a product that rebuilds everything.
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        Quench(cmd, Package(table, aWidth: 50, rebuildPolicy: """{"Mode": "NEVER"}"""), mode: "ALWAYS");

        Assert.That(ColumnType(cmd, table, "A"), Is.EqualTo("nvarchar(50)"), "The in-place alter must have happened.");
        Assert.That(MarkerCount(cmd, table), Is.EqualTo(1),
            "The table's own NEVER outranks the product's ALWAYS -- the marker index must survive.");
        Assert.That(ObjectId(cmd, table), Is.EqualTo(objectIdBefore), "Same object: no rebuild.");
        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.Zero, "No rebuild may be recorded.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    // ---- ALWAYS -------------------------------------------------------------

    [Test]
    public void ModeAlways_RebuildsOnAnyChange_AndKeepsRowsAndOwnership()
    {
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        // ONE modification. ALWAYS means any change is enough, so a single widened column has to be.
        Quench(cmd, Package(table, aWidth: 50, rebuildPolicy: """{"Mode": "ALWAYS"}"""));

        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.EqualTo(1),
            "ALWAYS with a detected change must elect a rebuild and the run manifest must say so.");
        Assert.That(ObjectId(cmd, table), Is.Not.EqualTo(objectIdBefore),
            "The table must actually have been replaced -- a new object id is the only proof.");
        Assert.That(MarkerCount(cmd, table), Is.Zero,
            "The live-only marker index went with the old table, which is what a rebuild does with anything "
            + "the package does not declare.");
        Assert.That(ColumnType(cmd, table, "A"), Is.EqualTo("nvarchar(50)"),
            "The rebuilt table must be built to the DECLARED definition, not the old one.");
        Assert.That(RowCount(cmd, table), Is.EqualTo(2),
            "Carrying the rows across is the whole point; a rebuild that loses them is data destruction with "
            + "a successful exit code.");
        Assert.That(Str(cmd, $"SELECT A FROM dbo.[{table}] WHERE Id = 2"), Is.EqualTo("a2"),
            "Values must arrive intact and paired with the same keys -- a copy that shifted columns would "
            + "still produce two rows.");
        Assert.That(OwnershipPropertyCount(cmd, table), Is.EqualTo(1),
            "The rebuild drops the old table and its extended properties with it, so the ownership stamp has "
            + "to be re-applied in the same run. A rebuilt table that comes back unowned is invisible to the "
            + "next deploy's ownership checks.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    [Test]
    public void ModeAlways_WithNoChangeAtAll_DoesNotRebuild()
    {
        // ALWAYS is "always instead of altering in place", not "always". An idempotent re-deploy of an
        // unchanged package must stay a no-op, or every deploy of a stable schema moves every row.
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        Quench(cmd, Package(table, rebuildPolicy: """{"Mode": "ALWAYS"}"""));

        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.Zero,
            "No change was detected, so there was nothing for a rebuild to deliver.");
        Assert.That(ObjectId(cmd, table), Is.EqualTo(objectIdBefore), "Same object: no rebuild.");
        Assert.That(MarkerCount(cmd, table), Is.EqualTo(1), "The marker index must survive an unchanged deploy.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    // ---- THRESHOLD ----------------------------------------------------------

    [Test]
    public void ModeThreshold_BelowTheBoundary_AltersInPlace()
    {
        // The low side of the boundary. Without this test a threshold implementation that fires on ANY
        // change (or never fires) still passes the high-side test.
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        // Two modified columns against a threshold of three.
        Quench(cmd, Package(table, aWidth: 50, bWidth: 50, rebuildPolicy: """{"Mode": "THRESHOLD", "Threshold": 3}"""));

        Assert.That(ColumnType(cmd, table, "A"), Is.EqualTo("nvarchar(50)"),
            "The in-place alters must have happened -- otherwise this asserts nothing about the threshold.");
        Assert.That(ColumnType(cmd, table, "B"), Is.EqualTo("nvarchar(50)"), "Both modifications must have landed.");
        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.Zero, "Two modifications is below a threshold of three.");
        Assert.That(ObjectId(cmd, table), Is.EqualTo(objectIdBefore), "Same object: no rebuild.");
        Assert.That(MarkerCount(cmd, table), Is.EqualTo(1), "The marker index must survive an in-place alter.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    [Test]
    public void ModeThreshold_AtTheBoundary_Rebuilds()
    {
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        // Three modified columns against a threshold of three: the comparison is >=, so the boundary itself
        // fires. Paired with the below-boundary test, this pins WHERE the boundary is, not just that one exists.
        Quench(cmd, Package(table, aWidth: 50, bWidth: 50, cWidth: 50,
            rebuildPolicy: """{"Mode": "THRESHOLD", "Threshold": 3}"""));

        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.EqualTo(1), "Three modifications reaches a threshold of three.");
        Assert.That(ObjectId(cmd, table), Is.Not.EqualTo(objectIdBefore), "The table must actually have been replaced.");
        Assert.That(MarkerCount(cmd, table), Is.Zero, "A rebuilt table does not carry the live-only marker index.");
        Assert.That(ColumnType(cmd, table, "C"), Is.EqualTo("nvarchar(50)"), "The replacement is built to the declared definition.");
        Assert.That(RowCount(cmd, table), Is.EqualTo(2), "Rows survive the rebuild.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    // ---- whole-object resolution -------------------------------------------

    [Test]
    public void TableLevelPolicy_WinsWhole_AndDoesNotInheritTheProductThreshold()
    {
        // The options-blender guard. The table declares ONLY a Mode; the product declares THRESHOLD 5. If
        // resolution were a per-field COALESCE the table would come out as ALWAYS *with Threshold 5*, and a
        // single-modification deploy would not rebuild. The table asked for ALWAYS and gets ALWAYS, whole.
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        Quench(cmd, Package(table, aWidth: 50, rebuildPolicy: """{"Mode": "ALWAYS"}"""),
            mode: "THRESHOLD", threshold: 5);

        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.EqualTo(1),
            "The table's whole policy is ALWAYS. Grafting the product's Threshold of 5 onto it would leave "
            + "one modification below the bar and skip the rebuild the package asked for.");
        Assert.That(ObjectId(cmd, table), Is.Not.EqualTo(objectIdBefore), "The table must actually have been replaced.");
        Assert.That(MarkerCount(cmd, table), Is.Zero, "A rebuilt table does not carry the live-only marker index.");
        Assert.That(RowCount(cmd, table), Is.EqualTo(2), "Rows survive the rebuild.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    [Test]
    public void ProductLevelPolicy_AppliesToATableThatDeclaredNone()
    {
        // The other half of the cascade: a table with no policy of its own inherits the resolved upper tier
        // WHOLE. Without this the "table wins" test above would be satisfied by a build that simply ignored
        // the product tier entirely.
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        Quench(cmd, Package(table, aWidth: 50), mode: "ALWAYS");

        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.EqualTo(1),
            "The table declared no policy, so the product-level ALWAYS applies to it.");
        Assert.That(ObjectId(cmd, table), Is.Not.EqualTo(objectIdBefore), "The table must actually have been replaced.");
        Assert.That(MarkerCount(cmd, table), Is.Zero, "A rebuilt table does not carry the live-only marker index.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }

    // ---- WhatIf -------------------------------------------------------------

    [Test]
    public void WhatIf_WithARebuildElected_RecordsWouldRebuild_AndChangesNothing()
    {
        using var conn = OpenMainDb();
        using var cmd = conn.CreateCommand();

        var table = Arrange(cmd);
        var objectIdBefore = ObjectId(cmd, table);

        Quench(cmd, Package(table, aWidth: 50, rebuildPolicy: """{"Mode": "ALWAYS"}"""), whatIf: true);

        Assert.That(AuditCount(cmd, table, "wouldRebuild"), Is.EqualTo(1),
            "A preview has to say the rebuild would happen, or an operator reads a preview that hides the one "
            + "operation in SchemaSmith that destroys data.");
        Assert.That(AuditCount(cmd, table, "rebuilt"), Is.Zero, "WhatIf must record no actual rebuild.");
        Assert.That(ObjectId(cmd, table), Is.EqualTo(objectIdBefore), "WhatIf must leave the same object in place.");
        Assert.That(MarkerCount(cmd, table), Is.EqualTo(1), "WhatIf must not drop the live-only marker index.");
        Assert.That(ColumnType(cmd, table, "A"), Is.EqualTo("nvarchar(10)"),
            "WhatIf must not widen the column either -- the elected rebuild must not have been quietly "
            + "replaced by an in-place alter.");
        Assert.That(RowCount(cmd, table), Is.EqualTo(2), "WhatIf touches no rows.");

        Exec(cmd, $"DROP TABLE IF EXISTS dbo.[{table}]");
        conn.Close();
    }
}
