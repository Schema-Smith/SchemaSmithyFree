// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// SchemaSmith_RebuildBlockedReason on MySQL. The function is deliberately always-NULL: MySQL has no
/// system-versioned tables, no application-time periods, no table-level Change Data Capture or Change
/// Tracking, and its replication streams the binlog rather than tracking per-table articles whose identity a
/// swap would break. There is no state here for a shadow-copy-and-swap to destroy, so no table is ever
/// refused -- and a future contributor who "fills in the gap" by copying the MariaDb override's body would
/// make every MySQL kindle fail outright, because INFORMATION_SCHEMA.PERIODS does not exist on MySQL and a
/// missing INFORMATION_SCHEMA table is rejected by the parser at CREATE time, not at execution.
///
/// That last point is why these tests are worth their weight despite asserting NULL: the function is kindled
/// with every other helper, so if the MySQL body ever grew a reference to a catalog object MySQL lacks, the
/// kindle would fail and this fixture would go red before any user hit it.
/// </summary>
[Category("MySQL")]
[Category("Integration")]
[TestFixture]
public class RebuildBlockedReasonTests
{
    private const string TableName = "plain_rebuild_guard";
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

    // ---- every MySQL table is rebuildable ----------------------------------

    [Test]
    public void PlainTable_IsRebuildable()
    {
        Exec($"CREATE TABLE `{_testDb}`.`{TableName}` (id INT NOT NULL PRIMARY KEY, val VARCHAR(50) NULL) ENGINE=InnoDB");

        Assert.That(BlockedReason(TableName), Is.Null,
            "MySQL has no state a shadow-copy-and-swap would destroy, so no table may be refused a rebuild. A "
            + "reason here would stop a rebuild the user is entitled to and send them to hand-written "
            + "migration scripts for nothing.");
    }

    [Test]
    public void UnknownTable_IsNotReportedAsBlocked()
    {
        Assert.That(BlockedReason("no_such_table_anywhere"), Is.Null,
            "A table that does not exist has nothing to rebuild, so the guard must not invent a blocking state "
            + "for it -- the caller decides what a missing table means.");
    }
}
