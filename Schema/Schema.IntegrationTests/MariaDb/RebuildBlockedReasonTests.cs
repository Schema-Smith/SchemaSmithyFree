// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// SchemaSmith_RebuildBlockedReason on MariaDB -- the per-file variant override of the always-NULL MySQL
/// base definition (Schema.IntegrationTests.MySQL.RebuildBlockedReasonTests). MariaDB genuinely has two
/// states a shadow-copy-and-swap destroys: a system-versioned table's row history, and an application-time
/// period. Neither is carried by the schema package, so neither can be put back after a swap.
///
/// The catalog-availability trap this fixture pins: INFORMATION_SCHEMA.PERIODS is MariaDB 11.4, while the
/// application-time period feature itself arrived in 10.4.3. A missing INFORMATION_SCHEMA *table* is resolved
/// by the PARSER, not deferred to execution the way a missing COLUMN is (which is what lets
/// SchemaSmith_ColumnSrid / SchemaSmith_IndexIsVisible guard with an early RETURN), so a static reference
/// would be ER_UNKNOWN_TABLE at CREATE time on the 10.2 and 10.6 legs and would fail the WHOLE kindle rather
/// than one query. The override stages that read behind a /*M!110400 ... */ executable comment.
/// PlainTable_IsRebuildable is therefore load-bearing on every leg: if that staging ever stops working, the
/// function never gets created and this fixture goes red before a user meets it.
/// </summary>
[Category("MariaDb")]
[Category("Integration")]
[TestFixture]
public class RebuildBlockedReasonTests
{
    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDb = FixtureSetup.MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform.MariaDb).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
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
    public void SetUp() => DropTestTables();

    [TearDown]
    public void TearDown() => DropTestTables();

    // ---- helpers -----------------------------------------------------------

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void DropTestTables()
    {
        Exec($"DROP TABLE IF EXISTS `{_testDb}`.`plain_rebuild_guard`");
        Exec($"DROP TABLE IF EXISTS `{_testDb}`.`sysver_rebuild_guard`");
        Exec($"DROP TABLE IF EXISTS `{_testDb}`.`period_rebuild_guard`");
    }

    private string BlockedReason(string table)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT SchemaSmith_RebuildBlockedReason('{_testDb}', '{table}')";
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    private long ServerVersionNum()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT SchemaSmith_ServerVersionNum()";
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
    }

    // ---- an ordinary table is rebuildable (and the function exists at all) --

    [Test]
    public void PlainTable_IsRebuildable()
    {
        Exec($"CREATE TABLE `{_testDb}`.`plain_rebuild_guard` (id INT NOT NULL PRIMARY KEY, val VARCHAR(50) NULL) ENGINE=InnoDB");

        Assert.That(BlockedReason("plain_rebuild_guard"), Is.Null,
            "An ordinary MariaDB table holds nothing a shadow copy would lose, so it must be allowed to "
            + "rebuild. This also proves the function was created at all: the version-staged "
            + "INFORMATION_SCHEMA.PERIODS read would otherwise be a parse error at kindle time on 10.2/10.6 "
            + "and no SchemaSmith helper would exist in this database.");
    }

    [Test]
    public void UnknownTable_IsNotReportedAsBlocked()
    {
        Assert.That(BlockedReason("no_such_table_anywhere"), Is.Null,
            "A table that does not exist has nothing to rebuild, so the guard must not invent a blocking state "
            + "for it -- the caller decides what a missing table means.");
    }

    // ---- system versioning (MariaDB 10.3+) ---------------------------------

    [Test]
    public void SystemVersionedTable_IsBlocked_AndTheReasonNamesSystemVersioning()
    {
        if (ServerVersionNum() < 1003)
            Assert.Ignore("System-versioned tables are MariaDB 10.3+; the state cannot exist on this server.");

        Exec($"CREATE TABLE `{_testDb}`.`sysver_rebuild_guard` (id INT NOT NULL PRIMARY KEY, val VARCHAR(50) NULL) "
             + "ENGINE=InnoDB WITH SYSTEM VERSIONING");

        var reason = BlockedReason("sysver_rebuild_guard");

        // Asserted non-null before the text match: Does.Contain against null reports "Expected: IEnumerable
        // But was: null", which names nothing about what actually failed.
        Assert.That(reason, Is.Not.Null,
            "A system-versioned table must be refused a rebuild: the copy carries only the current rows, so "
            + "every historical version the table has accumulated is discarded with no error raised.");
        Assert.That(reason, Does.Contain("system versioning"),
            $"The refusal must name system versioning so the operator knows to write a Before/After migration "
            + $"script instead of guessing why the deploy stopped. Got: '{reason}'.");
    }

    // ---- application-time periods (feature 10.4.3+, catalog 11.4+) ----------

    [Test]
    public void ApplicationTimePeriodTable_IsBlocked_AndTheReasonNamesThePeriod()
    {
        if (ServerVersionNum() < 1104)
            Assert.Ignore("INFORMATION_SCHEMA.PERIODS is MariaDB 11.4+. Below it the catalog cannot be asked "
                          + "about application-time periods at all, so the guard reports none -- a known "
                          + "detection gap on 10.4.3-11.3, not a failure of this assertion.");

        Exec($@"CREATE TABLE `{_testDb}`.`period_rebuild_guard` (
                  id INT NOT NULL PRIMARY KEY,
                  date_start DATE NOT NULL,
                  date_end DATE NOT NULL,
                  PERIOD FOR app_time(date_start, date_end)
                ) ENGINE=InnoDB");

        var reason = BlockedReason("period_rebuild_guard");

        Assert.That(reason, Is.Not.Null,
            "A table with an application-time period must be refused a rebuild: the period is a table-level "
            + "temporal contract the schema package does not carry, so the copy comes back as an ordinary "
            + "table and FOR PORTION OF statements against it start failing.");
        Assert.That(reason, Does.Contain("application-time period"),
            $"The refusal must name the application-time period rather than system versioning -- they are "
            + $"different features with different remediation, and the SYSTEM_TIME period is deliberately "
            + $"excluded from this check so the two cannot be confused. Got: '{reason}'.");
    }
}
