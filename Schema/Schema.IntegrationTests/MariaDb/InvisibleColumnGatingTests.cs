// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// Invisible-column version-gating for MariaDB. Unlike column DEFAULT expressions (always supported at
/// the 10.2 floor -- see Schema.IntegrationTests.MariaDb.DefaultExpressionGatingTests), invisible columns
/// have a genuine MariaDB threshold: 10.3.0. Below that the INVISIBLE keyword is a hard syntax error, so
/// the emit must degrade through SchemaSmith_UnsupportedFeaturePolicy exactly as it does on MySQL below
/// 8.0.23 -- see Schema.IntegrationTests.MySQL.InvisibleColumnGatingTests.
///
/// SchemaSmith_SupportsInvisibleColumn()'s MariaDB branch calls SchemaSmith_ServerVersionNum() (unlike
/// the DEFAULT-expression gate, whose MariaDB branch short-circuits on VERSION() LIKE '%MariaDB%' alone),
/// so @schemasmith_version_override DOES drive it here: overriding to 1002 simulates MariaDB below 10.3
/// on the modern CI container.
/// </summary>
[Category("MariaDb")]
[Category("Integration")]
[TestFixture]
public class InvisibleColumnGatingTests
{
    private const string TableName = "invisible_col_gate_test";
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
    public void SetUp()
    {
        SetVersionOverride(null);
        SetPolicy(null);
        DropTestTable();
        Exec($"DELETE FROM SchemaSmith_ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");
    }

    [TearDown]
    public void TearDown()
    {
        SetVersionOverride(null);
        SetPolicy(null);
        DropTestTable();
    }

    // ---- helpers -----------------------------------------------------------

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
    }

    private string ScalarStr(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    private void SetVersionOverride(int? majorMinor) =>
        Exec(majorMinor.HasValue ? $"SET @schemasmith_version_override = {majorMinor.Value}" : "SET @schemasmith_version_override = NULL");

    private void SetPolicy(string policy) =>
        Exec(policy == null ? "SET @schemasmith_unsupported_policy = NULL" : $"SET @schemasmith_unsupported_policy = '{policy}'");

    private void DropTestTable() => Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{TableName}`");

    private static string BuildTableJson(bool invisible)
    {
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn { Name = "`secret`", DataType = "INT", Nullable = false, Invisible = invisible }
            ],
            Indexes =
            [
                new Schema.Domain.Index { Name = $"`pk_{TableName}`", PrimaryKey = true, Unique = true, IndexColumns = "`id`" }
            ]
        };
        return "[" + JsonConvert.SerializeObject(table) + "]";
    }

    private void Deploy(bool invisible)
    {
        var json = BuildTableJson(invisible).Replace("'", "''");
        Exec($"CALL SchemaSmith_TableQuench('InvisibleColGateProductMdb', '{_testDb}', '{json}', 0, 0, 0)");
    }

    private long TableExistsCount() => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}'");

    private long ColumnExistsCount() => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'secret'");

    private bool ColumnIsInvisible() =>
        (ScalarStr($@"SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'secret'") ?? "")
        .Contains("INVISIBLE", StringComparison.OrdinalIgnoreCase);

    private long DowngradedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'downgraded' AND ObjectName LIKE '%{TableName}.secret%'");

    private long ModifiedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'modified' AND ObjectName LIKE '%{TableName}.secret%'");

    // ---- degrade path (MariaDB < 10.3 simulated via version override) ------

    [Test]
    public void BelowFloor_Warn_DeploysTableCreatesColumnVisibleAndRecordsDowngraded()
    {
        SetVersionOverride(1002);
        SetPolicy("warn");

        Deploy(invisible: true);

        Assert.Multiple(() =>
        {
            Assert.That(TableExistsCount(), Is.EqualTo(1), "Table must still deploy on MariaDB < 10.3.");
            Assert.That(ColumnExistsCount(), Is.EqualTo(1), "The column must still be created below MariaDB 10.3 -- only its invisibility degrades.");
            Assert.That(ColumnIsInvisible(), Is.False, "Invisible column degrades to visible below MariaDB 10.3.");
            Assert.That(DowngradedAuditCount(), Is.EqualTo(1), "The degraded invisibility must record a 'downgraded' manifest row.");
        });
    }

    [Test]
    public void BelowFloor_Warn_SecondDeployIsIdempotent()
    {
        SetVersionOverride(1002);
        SetPolicy("warn");

        Deploy(invisible: true);
        Assert.DoesNotThrow(() => Deploy(invisible: true), "A second deploy below the floor must not error.");

        Assert.Multiple(() =>
        {
            Assert.That(ColumnIsInvisible(), Is.False, "The column must remain visible after a second deploy.");
            Assert.That(ModifiedAuditCount(), Is.EqualTo(0),
                "A second deploy must not record a spurious 'modified' row for the ignored visibility difference below the floor (non-idempotency bug).");
        });
    }

    [Test]
    public void BelowFloor_Fail_AbortsNamingTheOffendingColumn()
    {
        SetVersionOverride(1002);
        SetPolicy("fail");

        Assert.That(() => Deploy(invisible: true), Throws.Exception.With.Message.Contains("10.3"),
            "Under policy 'fail' a declared invisible column below the floor must abort the deploy.");
    }

    // ---- supported path (modern binary, no override) -----------------------

    [Test]
    public void ModernServer_CreatesColumnInvisible_AndExtractionRoundTripsTrue()
    {
        if (Scalar("SELECT SchemaSmith_SupportsInvisibleColumn()") == 0)
            Assert.Ignore("Target does not support invisible columns (MariaDB < 10.3); covered by the BelowFloor_* tests.");

        SetPolicy("warn");
        Deploy(invisible: true);

        Assert.That(ColumnIsInvisible(), Is.True, "The column must be created invisible on the modern binary.");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', '{TableName}')";
        var json = "";
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }
        var table = PlatformDeserializer.DeserializeTable(json, Platform.MariaDb) as MySqlTable;
        var secret = (MySqlColumn)table!.Columns.Find(c => c.Name.Contains("secret"));

        Assert.That(secret, Is.Not.Null);
        Assert.That(secret!.Invisible, Is.True, "Extraction must round-trip the declared Invisible=true.");
    }

    [Test]
    public void ModernServer_DriftVisibleToInvisibleIsDetectedAndApplied()
    {
        if (Scalar("SELECT SchemaSmith_SupportsInvisibleColumn()") == 0)
            Assert.Ignore("Target does not support invisible columns (MariaDB < 10.3); covered by the BelowFloor_* tests.");

        SetPolicy("warn");
        Deploy(invisible: false);
        Assert.That(ColumnIsInvisible(), Is.False, "Sanity: column starts visible.");

        Deploy(invisible: true);

        Assert.Multiple(() =>
        {
            Assert.That(ColumnIsInvisible(), Is.True, "A visible -> invisible change must be detected and applied.");
            Assert.That(ModifiedAuditCount(), Is.GreaterThanOrEqualTo(1), "The visibility change must record a 'modified' manifest row.");
        });
    }

    [Test]
    public void ModernServer_DriftInvisibleToVisibleIsDetectedAndApplied()
    {
        // The reverse direction: a naive implementation that only checks "declared invisible but
        // currently visible" misses this side.
        if (Scalar("SELECT SchemaSmith_SupportsInvisibleColumn()") == 0)
            Assert.Ignore("Target does not support invisible columns (MariaDB < 10.3); covered by the BelowFloor_* tests.");

        SetPolicy("warn");
        Deploy(invisible: true);
        Assert.That(ColumnIsInvisible(), Is.True, "Sanity: column starts invisible.");

        Deploy(invisible: false);

        Assert.Multiple(() =>
        {
            Assert.That(ColumnIsInvisible(), Is.False, "An invisible -> visible change must be detected and applied.");
            Assert.That(ModifiedAuditCount(), Is.GreaterThanOrEqualTo(1), "The visibility change must record a 'modified' manifest row.");
        });
    }
}
