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
/// Column-SRID version-gating for MySQL. `col POINT SRID 4326` restricts a spatial column to one
/// spatial reference system (MySQL 8.0.3, WL#8592); below that the keyword is a hard syntax error at
/// the engine, so the emit must degrade through SchemaSmith_UnsupportedFeaturePolicy: 'warn' (default)
/// creates the column unrestricted + records a 'downgraded' manifest row (idempotent); 'fail' aborts
/// naming the column.
///
/// These bodies run on the modern 8.0 CI container and drive the degrade LOGIC via
/// @schemasmith_version_override = 507 (forces SchemaSmith_SupportsColumnSrid() -> 0 on a non-MariaDB
/// server), mirroring InvisibleColumnGatingTests. MariaDB has NO equivalent attribute at any version --
/// SchemaSmith_SupportsColumnSrid() is 0 there unconditionally, not a floor it ever crosses -- see
/// Schema.IntegrationTests.MariaDb.ColumnSridGatingTests, which also pins the extraction-safety
/// regression (INFORMATION_SCHEMA.COLUMNS.SRS_ID does not exist on MariaDB at all).
/// </summary>
[Category("MySQL")]
[Category("Integration")]
[TestFixture]
public class ColumnSridGatingTests
{
    private const string TableName = "srid_col_gate_test";
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

    private long? ScalarNullableLong(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : Convert.ToInt64(result);
    }

    private void SetVersionOverride(int? major) =>
        Exec(major.HasValue ? $"SET @schemasmith_version_override = {major.Value}" : "SET @schemasmith_version_override = NULL");

    private void SetPolicy(string policy) =>
        Exec(policy == null ? "SET @schemasmith_unsupported_policy = NULL" : $"SET @schemasmith_unsupported_policy = '{policy}'");

    private void DropTestTable() => Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{TableName}`");

    // Only Srid varies between deploys in these tests -- ModifiedTableQuench re-emits the whole
    // declared ColumnScript when ANY column difference is detected, so a fixture that also toggled
    // another property would let that difference (not the SRID drift predicate) drive the MODIFY, and
    // the drift tests below would pass for the wrong reason.
    private static string BuildTableJson(int? srid)
    {
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn { Name = "`loc`", DataType = "POINT", Nullable = false, Srid = srid }
            ],
            Indexes =
            [
                new Schema.Domain.Index { Name = $"`pk_{TableName}`", PrimaryKey = true, Unique = true, IndexColumns = "`id`" }
            ]
        };
        return "[" + JsonConvert.SerializeObject(table) + "]";
    }

    private void Deploy(int? srid)
    {
        var json = BuildTableJson(srid).Replace("'", "''");
        Exec($"CALL SchemaSmith_TableQuench('SridColGateProduct', '{_testDb}', '{json}', 0, 0, 0)");
    }

    private long TableExistsCount() => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}'");

    private long ColumnExistsCount() => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'loc'");

    private long? LiveSrid() => ScalarNullableLong(
        $@"SELECT SRS_ID FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'loc'");

    private long DowngradedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'downgraded' AND ObjectName LIKE '%{TableName}.loc%'");

    private long ModifiedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'modified' AND ObjectName LIKE '%{TableName}.loc%'");

    // ---- degrade path (MySQL < 8.0.3 simulated via version override) -------

    [Test]
    public void BelowFloor_Warn_DeploysColumnUnrestricted_AndRecordsDowngraded()
    {
        SetVersionOverride(507);
        SetPolicy("warn");

        Deploy(srid: 4326);

        Assert.Multiple(() =>
        {
            Assert.That(TableExistsCount(), Is.EqualTo(1), "Table must still deploy on MySQL < 8.0.3.");
            Assert.That(ColumnExistsCount(), Is.EqualTo(1), "The column must still be created below MySQL 8.0.3 -- only its SRID restriction degrades.");
            Assert.That(LiveSrid(), Is.Null, "SRID restriction degrades to unrestricted below MySQL 8.0.3.");
            Assert.That(DowngradedAuditCount(), Is.EqualTo(1), "The degraded SRID must record a 'downgraded' manifest row.");
        });
    }

    [Test]
    public void BelowFloor_Warn_SecondDeployIsIdempotent()
    {
        SetVersionOverride(507);
        SetPolicy("warn");

        Deploy(srid: 4326);
        Assert.DoesNotThrow(() => Deploy(srid: 4326), "A second deploy below the floor must not error.");

        Assert.Multiple(() =>
        {
            Assert.That(LiveSrid(), Is.Null, "The column must remain unrestricted after a second deploy.");
            Assert.That(ModifiedAuditCount(), Is.EqualTo(0),
                "A second deploy must not record a spurious 'modified' row for the ignored SRID difference below the floor (non-idempotency bug).");
        });
    }

    [Test]
    public void BelowFloor_Fail_AbortsNamingTheOffendingColumn()
    {
        SetVersionOverride(507);
        SetPolicy("fail");

        Assert.That(() => Deploy(srid: 4326), Throws.Exception.With.Message.Contains("8.0.3"),
            "Under policy 'fail' a declared column SRID below the floor must abort the deploy.");
    }

    // ---- supported path (modern binary, no override) -----------------------

    [Test]
    public void ModernServer_DeploysColumnWithSrid_AndExtractionRoundTripsValue()
    {
        if (Scalar("SELECT SchemaSmith_SupportsColumnSrid()") == 0)
            Assert.Ignore("Target does not support column SRID (MySQL < 8.0.3); covered by the BelowFloor_* tests.");

        SetPolicy("warn");
        Deploy(srid: 4326);

        Assert.That(LiveSrid(), Is.EqualTo(4326), "The column must be created SRID-restricted on the modern binary.");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', '{TableName}')";
        var json = "";
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }
        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        var loc = (MySqlColumn)table!.Columns.Find(c => c.Name.Contains("loc"));

        Assert.That(loc, Is.Not.Null);
        Assert.That(loc!.Srid, Is.EqualTo(4326), "Extraction must round-trip the declared Srid=4326.");
    }

    [Test]
    public void ModernServer_SecondDeployWithSameSrid_IsIdempotent()
    {
        if (Scalar("SELECT SchemaSmith_SupportsColumnSrid()") == 0)
            Assert.Ignore("Target does not support column SRID (MySQL < 8.0.3); covered by the BelowFloor_* tests.");

        SetPolicy("warn");
        Deploy(srid: 4326);
        Deploy(srid: 4326);

        Assert.That(ModifiedAuditCount(), Is.EqualTo(0), "A second deploy declaring the same SRID must not record a spurious 'modified' row.");
    }

    [Test]
    public void ModernServer_DriftSridChangeIsDetectedAndApplied()
    {
        if (Scalar("SELECT SchemaSmith_SupportsColumnSrid()") == 0)
            Assert.Ignore("Target does not support column SRID (MySQL < 8.0.3); covered by the BelowFloor_* tests.");

        SetPolicy("warn");
        Deploy(srid: 4326);
        Deploy(srid: 0);

        Assert.Multiple(() =>
        {
            Assert.That(LiveSrid(), Is.EqualTo(0), "A declared SRID change (4326 -> 0) must be detected and applied.");
            Assert.That(ModifiedAuditCount(), Is.GreaterThanOrEqualTo(1), "The SRID change must record a 'modified' audit row.");
        });
    }

    [Test]
    public void ModernServer_DriftRestrictedToUnrestrictedIsDetectedAndApplied()
    {
        if (Scalar("SELECT SchemaSmith_SupportsColumnSrid()") == 0)
            Assert.Ignore("Target does not support column SRID (MySQL < 8.0.3); covered by the BelowFloor_* tests.");

        SetPolicy("warn");
        Deploy(srid: 4326);
        Deploy(srid: null);

        Assert.Multiple(() =>
        {
            Assert.That(LiveSrid(), Is.Null,
                "Removing the declared SRID (4326 -> unrestricted) must be detected and applied -- the null-safe <=> compare must not silently ignore a NULL-vs-value change.");
            Assert.That(ModifiedAuditCount(), Is.GreaterThanOrEqualTo(1), "The SRID removal must record a 'modified' audit row.");
        });
    }
}
