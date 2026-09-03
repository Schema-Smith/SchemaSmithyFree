// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// SchemaSmith_RebuildBlockedReason on MySQL. MySQL has almost none of the states a shadow-copy-and-swap
/// would destroy: no system-versioned tables, no application-time periods, no table-level Change Data
/// Capture or Change Tracking, and its replication streams the binlog rather than tracking per-table
/// articles whose identity a swap would break. PARTITIONING is the exception, and this fixture pins both
/// halves -- an ordinary table must NOT be refused, a partitioned one must be.
///
/// A future contributor who "fills in the gap" further by copying the MariaDb override's system-versioning
/// or period checks would make every MySQL kindle fail outright, because INFORMATION_SCHEMA.PERIODS does not
/// exist on MySQL and a missing INFORMATION_SCHEMA table is rejected by the parser at CREATE time, not at
/// execution.
///
/// That last point is why the always-NULL cases are worth their weight: the function is kindled with every
/// other helper, so if the MySQL body ever grew a reference to a catalog object MySQL lacks, the kindle would
/// fail and this fixture would go red before any user hit it.
/// </summary>
[Category("MySQL")]
[Category("Integration")]
[TestFixture]
public class RebuildBlockedReasonTests
{
    private const string TableName = "plain_rebuild_guard";
    private const string PartTableName = "part_rebuild_guard_mysql";
    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDb = FixtureSetup.MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
        // ForgeKindler is already deployed into MainDb by the fixture.
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [SetUp]
    public void SetUp() => DropTestTable();

    [TearDown]
    public void TearDown() => DropTestTable();

    // ---- helpers -----------------------------------------------------------

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void DropTestTable() => Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{TableName}`");

    private string BlockedReason(string table)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT SchemaSmith_RebuildBlockedReason('{_testDb}', '{table}')";
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    // ---- an ordinary MySQL table is rebuildable ------------------------------

    [Test]
    public void PlainTable_IsRebuildable()
    {
        Exec($"CREATE TABLE `{_testDb}`.`{TableName}` (id INT NOT NULL PRIMARY KEY, val VARCHAR(50) NULL) ENGINE=InnoDB");

        Assert.That(BlockedReason(TableName), Is.Null,
            "An unpartitioned MySQL table holds nothing a shadow-copy-and-swap would destroy, so it must not "
            + "be refused. A reason here would stop a rebuild the user is entitled to and send them to "
            + "hand-written migration scripts for nothing.");
    }

    [Test]
    public void UnknownTable_IsNotReportedAsBlocked()
    {
        Assert.That(BlockedReason("no_such_table_anywhere"), Is.Null,
            "A table that does not exist has nothing to rebuild, so the guard must not invent a blocking state "
            + "for it -- the caller decides what a missing table means.");
    }
    // ---- partitioning: the one state MySQL DOES have --------------------------

    [Test]
    public void PartitionedTable_IsBlocked_AndTheReasonNamesPartitioning()
    {
        // MySQL carries the whole partition definition INSIDE the table DDL, so a shadow copy built from
        // the package's column list is unpartitioned by construction. Every row survives the swap and the
        // layout does not -- and until partitioning is declarable the package has nothing to put back.
        Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{PartTableName}`");
        Exec($"CREATE TABLE `{_testDb}`.`{PartTableName}` (id INT NOT NULL, val VARCHAR(50) NULL, PRIMARY KEY(id)) "
             + "ENGINE=InnoDB PARTITION BY RANGE (id) "
             + "(PARTITION p0 VALUES LESS THAN (100), PARTITION pmax VALUES LESS THAN MAXVALUE)");

        try
        {
            var reason = BlockedReason(PartTableName);

            Assert.That(reason, Is.Not.Null,
                "A partitioned table must be refused a rebuild. This is the counterexample to the claim that "
                + "MySQL holds no state a shadow copy destroys -- PostgreSQL's twin of this function already "
                + "refuses for exactly this reason.");
            Assert.That(reason, Does.Contain("partition"),
                $"The refusal must name partitioning, or the operator cannot tell what to migrate around. "
                + $"Got: '{reason}'.");
        }
        finally
        {
            Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{PartTableName}`");
        }
    }
}
