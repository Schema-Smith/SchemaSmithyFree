// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.MySQL;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// Column DEFAULT-expression support on MariaDB. MDEV-10134 landed in 10.2.1 — the very first point
/// release of the 10.2 series, at/below our 10.2 floor — so SchemaSmith_SupportsDefaultExpression() is
/// unconditionally 1 for MariaDB (VERSION() LIKE '%MariaDB%' short-circuits before the MySQL-only
/// ServerVersionNum() >= 800 check). This is the MariaDB-side proof that the gate is MySQL-only: forcing
/// the same 507 override that makes MySQL degrade (see Schema.IntegrationTests.MySQL.DefaultExpressionGatingTests)
/// must have no effect on MariaDB.
/// </summary>
[Category("MariaDb")]
[Category("Integration")]
[TestFixture]
public class DefaultExpressionGatingTests
{
    private const string TableName = "def_expr_gate_test";
    private const string ExpressionDefault = "(CURRENT_DATE + INTERVAL 1 YEAR)";
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
        DropTestTable();
        Exec($"DELETE FROM SchemaSmith_ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");
    }

    [TearDown]
    public void TearDown()
    {
        SetVersionOverride(null);
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

    private void SetVersionOverride(int? major) =>
        Exec(major.HasValue ? $"SET @schemasmith_version_override = {major.Value}" : "SET @schemasmith_version_override = NULL");

    private void DropTestTable() => Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{TableName}`");

    private static string BuildTableJson()
    {
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn { Name = "`expiry_date`", DataType = "DATE", Nullable = false, Default = ExpressionDefault }
            ],
            Indexes =
            [
                new Schema.Domain.Index { Name = $"`pk_{TableName}`", PrimaryKey = true, Unique = true, IndexColumns = "`id`" }
            ]
        };
        return "[" + JsonConvert.SerializeObject(table) + "]";
    }

    private void Deploy()
    {
        var json = BuildTableJson().Replace("'", "''");
        Exec($"CALL SchemaSmith_TableQuench('DefExprGateProductMdb', '{_testDb}', '{json}', 0, 0, 0)");
    }

    private long ColumnExistsCount() => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'expiry_date'");

    [Test]
    public void SupportsDefaultExpression_IsUnaffectedByMySqlOnlyVersionOverride()
    {
        SetVersionOverride(507);
        Assert.That(Scalar("SELECT SchemaSmith_SupportsDefaultExpression()"), Is.EqualTo(1),
            "MariaDB has supported expression defaults since 10.2.1 (MDEV-10134) — at/below the 10.2 floor — so the MySQL-only version_override must not gate it.");
    }

    [Test]
    public void VersionOverride507_StillCreatesColumnWithExpressionDefault_AndRecordsNoDowngrade()
    {
        SetVersionOverride(507);

        Deploy();

        Assert.Multiple(() =>
        {
            Assert.That(ColumnExistsCount(), Is.EqualTo(1),
                "MariaDB must create the expression-default column even under the MySQL-507 override — the gate never applies to it.");
            Assert.That(Scalar($"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit WHERE ActionType = 'downgraded' AND ObjectName LIKE '%{TableName}%'"), Is.EqualTo(0),
                "No downgrade may be recorded on MariaDB, regardless of the MySQL-only version override.");
        });
    }
}
