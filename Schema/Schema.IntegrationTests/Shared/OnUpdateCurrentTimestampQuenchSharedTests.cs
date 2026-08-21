// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.IntegrationTests.Shared;

/// <summary>
/// Shared column ON UPDATE CURRENT_TIMESTAMP round-trip and drift tests for the MySQL/MariaDb family.
/// Covers the gap where the auto-refresh clause was entirely unmodelled: no domain property, never read
/// from INFORMATION_SCHEMA.COLUMNS.EXTRA, never emitted on CREATE/ALTER -- an extract-then-deploy round
/// trip silently stopped an `updated_at`-style column from refreshing itself. The MySQL and MariaDb
/// subclasses supply the platform + fixture accessors; every [Test] body here runs on both engines (no
/// MariaDb override exists for SchemaSmith_ColumnOnUpdateClause or any of the parse/quench scripts these
/// tests exercise -- the case/paren divergence is folded via the existing SchemaSmith_NormalizeColumnDefault
/// MariaDb override instead), so behavior is identical on both.
/// </summary>
public abstract class OnUpdateCurrentTimestampQuenchSharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string MainDb { get; }
    protected abstract string MainConnectionString { get; }

    private const string TableName = "on_update_quench_test";
    private const string PkIndexName = "pk_on_update_quench_test";
    private const string Product = "OnUpdateQuenchProduct";

    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDb = MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(MainConnectionString);
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
        DropTestTable();
        // ChangeAudit persists across the fixture's shared connection; scope each test to its own rows.
        Exec($"DELETE FROM SchemaSmith_ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");
    }

    [TearDown]
    public void TearDown() => DropTestTable();

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

    private void DropTestTable() => Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{TableName}`");

    private static string BuildTableJson(string dataType, string defaultValue, string onUpdate)
    {
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn
                {
                    Name = "`updated_at`", DataType = dataType, Nullable = false,
                    Default = defaultValue, OnUpdateCurrentTimestamp = onUpdate
                }
            ],
            Indexes =
            [
                new MySqlIndex { Name = $"`{PkIndexName}`", PrimaryKey = true, Unique = true, IndexColumns = "`id`" }
            ]
        };
        return "[" + JsonConvert.SerializeObject(table) + "]";
    }

    private void Deploy(string dataType, string defaultValue, string onUpdate)
    {
        var json = BuildTableJson(dataType, defaultValue, onUpdate).Replace("'", "''");
        Exec($"CALL SchemaSmith_TableQuench('{Product}', '{_testDb}', '{json}', 0, 0, 0)");
    }

    private MySqlTable Extract()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', '{TableName}')";
        var json = "";
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }
        return PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
    }

    private MySqlColumn ExtractUpdatedAtColumn() =>
        (MySqlColumn)Extract()!.Columns.Find(c => c.Name.Contains("updated_at"));

    private string LiveExtra() => ScalarStr(
        $@"SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'updated_at'");

    private long ColumnModifiedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'modified' AND ObjectName LIKE '%{TableName}.updated_at%'");

    // ---- round-trip: bare form ----------------------------------------------

    [Test]
    public void OnUpdateCurrentTimestamp_BareForm_AppliedOnCreate_AndRoundTripsThroughExtraction()
    {
        Deploy(dataType: "DATETIME", defaultValue: "CURRENT_TIMESTAMP", onUpdate: "CURRENT_TIMESTAMP");

        var extra = LiveExtra();
        Assert.That(extra, Does.Contain("on update").IgnoreCase,
            "The declared ON UPDATE CURRENT_TIMESTAMP clause must be applied on CREATE TABLE.");

        var updatedAt = ExtractUpdatedAtColumn();
        Assert.That(updatedAt, Is.Not.Null);
        Assert.That(updatedAt!.OnUpdateCurrentTimestamp, Is.EqualTo("CURRENT_TIMESTAMP"),
            "Extraction must round-trip the declared bare ON UPDATE CURRENT_TIMESTAMP clause.");
    }

    // ---- round-trip: precision must survive, not collapse to the bare form --

    [Test]
    public void OnUpdateCurrentTimestamp_WithPrecision_RoundTripsPrecision()
    {
        Deploy(dataType: "DATETIME(3)", defaultValue: "CURRENT_TIMESTAMP(3)", onUpdate: "CURRENT_TIMESTAMP(3)");

        var extra = LiveExtra();
        Assert.That(extra, Does.Contain("(3)"),
            "The declared precision must be applied on CREATE TABLE, not silently dropped.");

        var updatedAt = ExtractUpdatedAtColumn();
        Assert.That(updatedAt, Is.Not.Null);
        Assert.That(updatedAt!.OnUpdateCurrentTimestamp, Is.EqualTo("CURRENT_TIMESTAMP(3)"),
            "Extraction must preserve the declared ON UPDATE precision rather than collapsing it to the bare form.");
    }

    // ---- drift: adding the clause (nothing else differs) is detected + applied

    [Test]
    public void OnUpdateCurrentTimestamp_Added_DifferingOnlyInOnUpdate_IsDetectedAndApplied()
    {
        Deploy(dataType: "DATETIME", defaultValue: "CURRENT_TIMESTAMP", onUpdate: null);
        Assert.That(LiveExtra(), Does.Not.Contain("on update").IgnoreCase,
            "Sanity: no ON UPDATE clause declared, none should be live yet.");

        Deploy(dataType: "DATETIME", defaultValue: "CURRENT_TIMESTAMP", onUpdate: "CURRENT_TIMESTAMP");

        Assert.Multiple(() =>
        {
            Assert.That(LiveExtra(), Does.Contain("on update").IgnoreCase,
                "Adding ON UPDATE CURRENT_TIMESTAMP (nothing else differs) must be detected and applied.");
            Assert.That(ColumnModifiedAuditCount(), Is.GreaterThanOrEqualTo(1),
                "The ON UPDATE-only change must record a 'modified' manifest row.");
        });
    }

    // ---- drift: removing the clause (nothing else differs) is detected + applied

    [Test]
    public void OnUpdateCurrentTimestamp_Removed_DifferingOnlyInOnUpdate_IsDetectedAndApplied()
    {
        Deploy(dataType: "DATETIME", defaultValue: "CURRENT_TIMESTAMP", onUpdate: "CURRENT_TIMESTAMP");
        Assert.That(LiveExtra(), Does.Contain("on update").IgnoreCase,
            "Sanity: ON UPDATE clause starts declared and live.");

        Deploy(dataType: "DATETIME", defaultValue: "CURRENT_TIMESTAMP", onUpdate: null);

        Assert.Multiple(() =>
        {
            Assert.That(LiveExtra(), Does.Not.Contain("on update").IgnoreCase,
                "Removing ON UPDATE CURRENT_TIMESTAMP (nothing else differs) must be detected and applied.");
            Assert.That(ColumnModifiedAuditCount(), Is.GreaterThanOrEqualTo(1),
                "The ON UPDATE-only removal must record a 'modified' manifest row.");
        });
    }

    // ---- DEFAULT CURRENT_TIMESTAMP alone is independent of ON UPDATE, and stays idempotent

    [Test]
    public void DefaultCurrentTimestampWithoutOnUpdate_StaysUnaffected_AndRedeployIsIdempotent()
    {
        Deploy(dataType: "DATETIME", defaultValue: "CURRENT_TIMESTAMP", onUpdate: null);

        var updatedAt = ExtractUpdatedAtColumn();
        Assert.That(updatedAt, Is.Not.Null);
        Assert.That(updatedAt!.Default, Is.EqualTo("CURRENT_TIMESTAMP"),
            "Sanity: the DEFAULT CURRENT_TIMESTAMP clause is present.");
        Assert.That(updatedAt!.OnUpdateCurrentTimestamp, Is.Null,
            "A DEFAULT CURRENT_TIMESTAMP column with no declared ON UPDATE must not report one -- the two clauses are independent.");

        Exec($"DELETE FROM SchemaSmith_ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");

        Deploy(dataType: "DATETIME", defaultValue: "CURRENT_TIMESTAMP", onUpdate: null);

        Assert.Multiple(() =>
        {
            Assert.That(ColumnModifiedAuditCount(), Is.EqualTo(0),
                "A second identical deploy must not record a spurious 'modified' row (non-idempotency bug).");
            Assert.That(LiveExtra(), Does.Not.Contain("on update").IgnoreCase,
                "A redeploy must not introduce an ON UPDATE clause that was never declared.");
        });
    }
}
