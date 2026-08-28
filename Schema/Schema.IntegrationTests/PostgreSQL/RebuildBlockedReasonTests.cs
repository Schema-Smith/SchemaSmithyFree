// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// "SchemaSmith"."RebuildBlockedReason" on PostgreSQL: the guard that decides whether a table may be replaced
/// by a shadow copy, and names the live state that forbids it when it may not.
///
/// The three guarded states are relationships the table participates in rather than data it holds, and a
/// copy-and-swap severs all of them silently: a publication loses the article and every subscriber's stream
/// stops without an error, an inheritance or partition edge simply disappears, and a partitioned parent has
/// no storage of its own to copy in the first place. Every assertion pins the NAMED state, because a bare
/// "cannot be rebuilt" tells the operator nothing about what to write instead.
///
/// Everything read by the function exists at the PostgreSQL 12 floor, so unlike the SQL Server twin there is
/// no version gate to prove -- these run identically on every matrix leg.
/// </summary>
[Category("PostgreSQL")]
[Category("Integration")]
[TestFixture]
public class RebuildBlockedReasonTests
{
    private IDbConnection _connection = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
        // ForgeKindler is already deployed into the main test database by FixtureSetup.
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    // ---- helpers -----------------------------------------------------------

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private string BlockedReason(string table, string schema = "public")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT \"SchemaSmith\".\"RebuildBlockedReason\"('{schema}', '{table}')";
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    // ---- a table with no special state is rebuildable -----------------------

    [Test]
    public void PlainTable_IsRebuildable()
    {
        Exec("CREATE TABLE plain_rebuild_guard (id INT PRIMARY KEY, val TEXT)");
        try
        {
            Assert.That(BlockedReason("plain_rebuild_guard"), Is.Null,
                "An ordinary table participates in nothing a shadow copy would sever, so it must be allowed to "
                + "rebuild. A false block here sends the user to hand-written migration scripts for a table "
                + "that never needed them.");
        }
        finally
        {
            Exec("DROP TABLE IF EXISTS plain_rebuild_guard");
        }
    }

    [Test]
    public void UnknownTable_IsNotReportedAsBlocked()
    {
        Assert.That(BlockedReason("no_such_table_anywhere"), Is.Null,
            "A table that does not exist has nothing to rebuild, so the guard must not invent a blocking state "
            + "for it -- the caller decides what a missing table means.");
    }

    // ---- logical replication publication ------------------------------------

    [Test]
    public void PublishedTable_IsBlocked_AndTheReasonNamesThePublication()
    {
        Exec("CREATE TABLE published_rebuild_guard (id INT PRIMARY KEY, val TEXT)");
        Exec("CREATE PUBLICATION rebuild_guard_pub FOR TABLE published_rebuild_guard");
        try
        {
            var reason = BlockedReason("published_rebuild_guard");

            // Asserted non-null before the text match: Does.Contain against null reports "Expected:
            // IEnumerable But was: null", which names nothing about what actually went wrong.
            Assert.That(reason, Is.Not.Null,
                "A published table must be refused a rebuild: dropping the table drops the article, and every "
                + "logical subscriber stops receiving its changes with nothing raised on the publisher side.");
            Assert.That(reason, Does.Contain("publication"),
                $"The refusal must name the publication so the operator knows the fix is a migration script "
                + $"plus a publication change, not a package edit. Got: '{reason}'.");
        }
        finally
        {
            Exec("DROP PUBLICATION IF EXISTS rebuild_guard_pub");
            Exec("DROP TABLE IF EXISTS published_rebuild_guard");
        }
    }

    // ---- inheritance --------------------------------------------------------

    [Test]
    public void InheritanceChild_IsBlocked_AndTheReasonNamesTheInheritedParent()
    {
        Exec("CREATE TABLE inherit_parent_guard (id INT PRIMARY KEY, val TEXT)");
        Exec("CREATE TABLE inherit_child_guard (extra TEXT) INHERITS (inherit_parent_guard)");
        try
        {
            var reason = BlockedReason("inherit_child_guard");

            Assert.That(reason, Is.Not.Null,
                "An inheritance child must be refused a rebuild: the replacement table is not a child of "
                + "anything, so queries against the parent silently stop returning the child's rows.");
            Assert.That(reason, Does.Contain("inherits"),
                $"The refusal must say the table inherits from a parent -- the operator has to re-establish "
                + $"that edge by hand, and cannot if the message does not mention it. Got: '{reason}'.");
        }
        finally
        {
            Exec("DROP TABLE IF EXISTS inherit_child_guard");
            Exec("DROP TABLE IF EXISTS inherit_parent_guard");
        }
    }

    [Test]
    public void InheritanceParent_IsBlocked_AndTheReasonNamesItsChildren()
    {
        Exec("CREATE TABLE inherit_parent_guard (id INT PRIMARY KEY, val TEXT)");
        Exec("CREATE TABLE inherit_child_guard (extra TEXT) INHERITS (inherit_parent_guard)");
        try
        {
            var reason = BlockedReason("inherit_parent_guard");

            Assert.That(reason, Is.Not.Null,
                "An inheritance parent must be refused a rebuild: swapping it orphans every child, so a query "
                + "that used to span the hierarchy quietly returns only the parent's own rows.");
            Assert.That(reason, Does.Contain("child tables"),
                $"The refusal must name the children, not just say the table is 'in an inheritance "
                + $"relationship' -- parent and child need different remediation. Got: '{reason}'.");
        }
        finally
        {
            Exec("DROP TABLE IF EXISTS inherit_child_guard");
            Exec("DROP TABLE IF EXISTS inherit_parent_guard");
        }
    }

    // ---- declarative partitioning -------------------------------------------

    [Test]
    public void PartitionedParent_IsBlocked_AndTheReasonNamesPartitioning()
    {
        Exec("CREATE TABLE partitioned_rebuild_guard (id INT NOT NULL, val TEXT) PARTITION BY RANGE (id)");
        Exec("CREATE TABLE partitioned_rebuild_guard_p1 PARTITION OF partitioned_rebuild_guard FOR VALUES FROM (1) TO (100)");
        try
        {
            var reason = BlockedReason("partitioned_rebuild_guard");

            Assert.That(reason, Is.Not.Null,
                "A partitioned parent must be refused a rebuild: it stores no rows itself, so a copy-and-swap "
                + "produces an empty ordinary table and every partition's data becomes unreachable through it.");
            Assert.That(reason, Does.Contain("partitioned table"),
                $"The refusal must name partitioning so the operator knows the parent's shape, not its data, "
                + $"is what needs migrating. Got: '{reason}'.");
        }
        finally
        {
            Exec("DROP TABLE IF EXISTS partitioned_rebuild_guard_p1");
            Exec("DROP TABLE IF EXISTS partitioned_rebuild_guard");
        }
    }

    [Test]
    public void PartitionLeaf_IsBlocked_AndTheReasonNamesItAsAPartition_NotAPlainInheritanceChild()
    {
        Exec("CREATE TABLE partitioned_rebuild_guard (id INT NOT NULL, val TEXT) PARTITION BY RANGE (id)");
        Exec("CREATE TABLE partitioned_rebuild_guard_p1 PARTITION OF partitioned_rebuild_guard FOR VALUES FROM (1) TO (100)");
        try
        {
            var reason = BlockedReason("partitioned_rebuild_guard_p1");

            Assert.That(reason, Is.Not.Null,
                "A leaf partition must be refused a rebuild: the replacement is a detached standalone table, so "
                + "rows written through the parent stop landing in it and reads through the parent stop seeing it.");
            Assert.That(reason, Does.Contain("partition of"),
                $"A partition is also a pg_inherits child, so the naive check order would report it as a plain "
                + $"inheritance child and point the operator at ALTER TABLE ... INHERIT, which is the wrong "
                + $"repair for a partition. Got: '{reason}'.");
        }
        finally
        {
            Exec("DROP TABLE IF EXISTS partitioned_rebuild_guard_p1");
            Exec("DROP TABLE IF EXISTS partitioned_rebuild_guard");
        }
    }
}
