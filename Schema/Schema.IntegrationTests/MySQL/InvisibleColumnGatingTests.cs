// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// Invisible-column version-gating for MySQL -- the same feature as the shipped invisible-INDEX gate, one
/// level down (column instead of index). `ALTER TABLE t ADD c INT INVISIBLE` requires MySQL 8.0.23; below
/// that the keyword is a hard syntax error at the engine, so the emit must degrade through
/// SchemaSmith_UnsupportedFeaturePolicy: 'warn' (default) creates the column visible + records a
/// 'downgraded' manifest row (idempotent); 'fail' aborts naming the column.
///
/// These bodies run on the modern 8.0 CI container and drive the degrade LOGIC via
/// @schemasmith_version_override = 507 (forces SchemaSmith_SupportsInvisibleColumn() -> 0 on a
/// non-MariaDB server), mirroring DefaultExpressionGatingTests / CheckConstraintGatingTests. The genuine
/// MySQL 5.7 end-to-end behavior is validated out-of-band against the throwaway floor container. MariaDB
/// has its own real threshold (10.3, not "always supported" like DEFAULT expressions) -- see
/// Schema.IntegrationTests.MariaDb.InvisibleColumnGatingTests.
/// </summary>
[Category("MySQL")]
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
    public void SetUp()
    {
        SetVersionOverride(null);
        SetPolicy(null);
        DropTestTable();
        // ChangeAudit persists across the fixture's shared connection; scope each test to its own rows.
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

    private void SetVersionOverride(int? major) =>
        Exec(major.HasValue ? $"SET @schemasmith_version_override = {major.Value}" : "SET @schemasmith_version_override = NULL");

    private void SetPolicy(string policy) =>
        Exec(policy == null ? "SET @schemasmith_unsupported_policy = NULL" : $"SET @schemasmith_unsupported_policy = '{policy}'");

    private void DropTestTable() => Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{TableName}`");

    // The secret column carries a DEFAULT so the fixture is portable across both engines: MariaDB
    // rejects a NOT NULL INVISIBLE column with no DEFAULT (error 4108 -- see
    // NotNullInvisibleColumnWithoutDefault_IsRejectedByEngine in the MariaDb test file, where that
    // divergence is pinned deliberately), MySQL does not. The DEFAULT is incidental to what these tests
    // are actually about (Invisible surviving extraction / visibility drift), so it is fixed at a
    // constant value here rather than becoming part of what any individual test varies.
    private static string BuildTableJson(bool invisible)
    {
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn { Name = "`secret`", DataType = "INT", Nullable = false, Invisible = invisible, Default = "0" }
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
        Exec($"CALL SchemaSmith_TableQuench('InvisibleColGateProduct', '{_testDb}', '{json}', 0, 0, 0)");
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

    // ---- degrade path (MySQL 5.7 simulated via version override) -----------

    [Test]
    public void BelowFloor_Warn_DeploysTableCreatesColumnVisibleAndRecordsDowngraded()
    {
        SetVersionOverride(507);
        SetPolicy("warn");

        Deploy(invisible: true);

        Assert.Multiple(() =>
        {
            Assert.That(TableExistsCount(), Is.EqualTo(1), "Table must still deploy on MySQL 5.7.");
            Assert.That(ColumnExistsCount(), Is.EqualTo(1), "The column must still be created below MySQL 8.0.23 -- only its invisibility degrades.");
            Assert.That(ColumnIsInvisible(), Is.False, "Invisible column degrades to visible below MySQL 8.0.23.");
            Assert.That(DowngradedAuditCount(), Is.EqualTo(1), "The degraded invisibility must record a 'downgraded' manifest row.");
        });
    }

    [Test]
    public void BelowFloor_Warn_SecondDeployIsIdempotent()
    {
        SetVersionOverride(507);
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
        SetVersionOverride(507);
        SetPolicy("fail");

        Assert.That(() => Deploy(invisible: true), Throws.Exception.With.Message.Contains("8.0.23"),
            "Under policy 'fail' a declared invisible column below the floor must abort the deploy.");
    }

    // ---- supported path (modern binary, no override) -----------------------

    [Test]
    public void ModernServer_CreatesColumnInvisible_AndExtractionRoundTripsTrue()
    {
        // The positive path is only assertable where the engine actually supports invisible columns
        // (MySQL 8.0.23+). On a genuine below-floor target (MySQL 5.7) the column degrades, so this
        // positive test skips there.
        if (Scalar("SELECT SchemaSmith_SupportsInvisibleColumn()") == 0)
            Assert.Ignore("Target does not support invisible columns (MySQL < 8.0.23); covered by the BelowFloor_* tests.");

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
        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        var secret = (MySqlColumn)table!.Columns.Find(c => c.Name.Contains("secret"));

        Assert.That(secret, Is.Not.Null);
        Assert.That(secret!.Invisible, Is.True, "Extraction must round-trip the declared Invisible=true.");
    }

    [Test]
    public void ModernServer_DriftVisibleToInvisibleIsDetectedAndApplied()
    {
        if (Scalar("SELECT SchemaSmith_SupportsInvisibleColumn()") == 0)
            Assert.Ignore("Target does not support invisible columns (MySQL < 8.0.23); covered by the BelowFloor_* tests.");

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
            Assert.Ignore("Target does not support invisible columns (MySQL < 8.0.23); covered by the BelowFloor_* tests.");

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
