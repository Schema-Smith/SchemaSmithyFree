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
/// Functional/expression-index support on MariaDB. Unlike SchemaSmith_SupportsDefaultExpression (always
/// supported on MariaDB, at/below the 10.2 floor), SchemaSmith_SupportsFunctionalIndex() is unconditionally
/// 0 for MariaDB at EVERY version — MariaDB has no equivalent in this form at all. This is the case a naive
/// implementation gets wrong: MariaDB's real major*100+minor comparable (e.g. 1002 for 10.2, 1104 for 11.4)
/// comfortably clears the MySQL 800 threshold, so a version-only check without the engine branch would
/// wrongly treat MariaDB as supporting it. These tests therefore run with NO @schemasmith_version_override —
/// the degrade must fire at whatever version this fixture's real MariaDB container happens to be, proving
/// the gate is unconditional rather than a threshold MariaDB simply never reaches in CI. See
/// Schema.IntegrationTests.MySQL.FunctionalIndexGatingTests for the MySQL-side version-boundary coverage.
/// </summary>
[Category("MariaDb")]
[Category("Integration")]
[TestFixture]
public class FunctionalIndexGatingTests
{
    private const string TableName = "func_idx_gate_test";
    private const string IndexName = "ix_lower_full_name";
    private const string FunctionalIndexColumns = "(lower(`full_name`))";
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
        SetPolicy(null);
        DropTestTable();
        Exec($"DELETE FROM SchemaSmith_ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");
    }

    [TearDown]
    public void TearDown()
    {
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

    private void SetPolicy(string policy) =>
        Exec(policy == null ? "SET @schemasmith_unsupported_policy = NULL" : $"SET @schemasmith_unsupported_policy = '{policy}'");

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
                new MySqlColumn { Name = "`full_name`", DataType = "VARCHAR(100)", Nullable = false }
            ],
            Indexes =
            [
                new Schema.Domain.Index { Name = $"`pk_{TableName}`", PrimaryKey = true, Unique = true, IndexColumns = "`id`" },
                new Schema.Domain.Index { Name = $"`{IndexName}`", IndexColumns = FunctionalIndexColumns }
            ]
        };
        return "[" + JsonConvert.SerializeObject(table) + "]";
    }

    private void Deploy()
    {
        var json = BuildTableJson().Replace("'", "''");
        Exec($"CALL SchemaSmith_TableQuench('FuncIdxGateProductMdb', '{_testDb}', '{json}', 0, 0, 0)");
    }

    private long TableExistsCount() => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}'");

    private long IndexExistsCount() => Scalar(
        $@"SELECT COUNT(DISTINCT INDEX_NAME) FROM INFORMATION_SCHEMA.STATISTICS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND INDEX_NAME = '{IndexName}'");

    private long DowngradedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'downgraded' AND ObjectName LIKE '%{TableName}.{IndexName}%'");

    [Test]
    public void SupportsFunctionalIndex_IsUnconditionallyZero()
    {
        Assert.That(Scalar("SELECT SchemaSmith_SupportsFunctionalIndex()"), Is.EqualTo(0),
            "MariaDB has no equivalent to a functional/expression index in this form at any version, so the predicate must be 0 with no version override involved.");
    }

    [Test]
    public void Warn_DeploysTableSkipsIndexAndRecordsDowngraded()
    {
        SetPolicy("warn");

        Deploy();

        Assert.Multiple(() =>
        {
            Assert.That(TableExistsCount(), Is.EqualTo(1), "Table must still deploy on MariaDB.");
            Assert.That(IndexExistsCount(), Is.EqualTo(0), "MariaDB must skip a declared functional index — it has no equivalent at any version.");
            Assert.That(DowngradedAuditCount(), Is.EqualTo(1), "The skipped index must record a 'downgraded' manifest row.");
        });
    }

    [Test]
    public void Warn_SecondDeployIsIdempotent()
    {
        SetPolicy("warn");

        Deploy();
        Assert.DoesNotThrow(Deploy, "A second deploy must not error.");

        Assert.That(IndexExistsCount(), Is.EqualTo(0), "The index must remain absent after a second deploy.");
    }

    [Test]
    public void Fail_AbortsNamingTheOffendingIndex()
    {
        SetPolicy("fail");

        Assert.That(Deploy, Throws.Exception.With.Message.Contains("8.0.13"),
            "Under policy 'fail' a declared functional index on MariaDB must abort the deploy, naming the MySQL version it would need.");
    }
}
