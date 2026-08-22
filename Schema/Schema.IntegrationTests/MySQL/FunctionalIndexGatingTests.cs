// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.MySQL;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// Functional/expression-index version-gating for the MySQL floor lowering (MySQL 5.7). A functional key
/// part (e.g. `CREATE INDEX ix ON t ((LOWER(name)))`) requires MySQL 8.0.13; below that it is a hard syntax
/// error at the engine (unlike a suppressible clause), so the emit must degrade through
/// SchemaSmith_UnsupportedFeaturePolicy: 'warn' (default) skips the whole index + records a 'downgraded'
/// manifest row (idempotent); 'fail' aborts.
///
/// These bodies run on the modern 8.0 CI container and drive the degrade LOGIC via
/// @schemasmith_version_override = 507 (which forces SchemaSmith_SupportsFunctionalIndex() -> 0), mirroring
/// DefaultExpressionGatingTests. The genuine MySQL 5.7 end-to-end behavior is validated out-of-band against
/// the throwaway floor container. MariaDB is intentionally not exercised here via override: unlike
/// SchemaSmith_SupportsDefaultExpression, SchemaSmith_SupportsFunctionalIndex() is unconditionally 0 on
/// MariaDB at every version — see Schema.IntegrationTests.MariaDb.FunctionalIndexGatingTests, which proves
/// the degrade at MariaDB's own running version, with no override needed.
/// </summary>
[Category("MySQL")]
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

    private void SetVersionOverride(int? major) =>
        Exec(major.HasValue ? $"SET @schemasmith_version_override = {major.Value}" : "SET @schemasmith_version_override = NULL");

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
        Exec($"CALL SchemaSmith_TableQuench('FuncIdxGateProduct', '{_testDb}', '{json}', 0, 0, 0)");
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

    private long CreatedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'created' AND ObjectName LIKE '%{TableName}.{IndexName}%'");

    // ---- degrade path (MySQL 5.7 simulated via version override) -----------

    [Test]
    public void BelowFloor_Warn_DeploysTableSkipsIndexAndRecordsDowngraded()
    {
        SetVersionOverride(507);
        SetPolicy("warn");

        Deploy();

        Assert.Multiple(() =>
        {
            Assert.That(TableExistsCount(), Is.EqualTo(1), "Table must still deploy on MySQL 5.7.");
            Assert.That(IndexExistsCount(), Is.EqualTo(0), "The functional index must be skipped below MySQL 8.0.13.");
            Assert.That(DowngradedAuditCount(), Is.EqualTo(1), "The skipped index must record a 'downgraded' manifest row.");
            Assert.That(CreatedAuditCount(), Is.EqualTo(0), "No 'created' audit row may be recorded for an index skipped below the floor.");
        });
    }

    [Test]
    public void BelowFloor_Warn_SecondDeployIsIdempotent()
    {
        SetVersionOverride(507);
        SetPolicy("warn");

        Deploy();
        Assert.DoesNotThrow(Deploy, "A second deploy below the floor must not error.");

        Assert.Multiple(() =>
        {
            Assert.That(IndexExistsCount(), Is.EqualTo(0), "The index must remain absent after a second deploy.");
            Assert.That(CreatedAuditCount(), Is.EqualTo(0),
                "A second deploy must not falsely record a 'created' row (non-idempotency bug).");
        });
    }

    [Test]
    public void BelowFloor_Fail_AbortsNamingTheOffendingIndex()
    {
        SetVersionOverride(507);
        SetPolicy("fail");

        Assert.That(Deploy, Throws.Exception.With.Message.Contains("8.0.13"),
            "Under policy 'fail' a declared functional index below the floor must abort the deploy.");
    }

    // ---- supported path (modern binary, no override) -----------------------

    [Test]
    public void ModernServer_CreatesFunctionalIndex()
    {
        // The positive path is only assertable where the engine actually supports functional indexes
        // (MySQL 8.0.13+). On a genuine below-floor target (MySQL 5.7) the index degrades, so this
        // positive test skips there.
        if (Scalar("SELECT SchemaSmith_SupportsFunctionalIndex()") == 0)
            Assert.Ignore("Target does not support functional indexes (MySQL < 8.0.13, or MariaDB); covered by the BelowFloor_* tests.");

        // No version override: on a real 8.0.13+ server SupportsFunctionalIndex() = 1, so the index is created.
        SetPolicy("warn");

        Deploy();

        Assert.Multiple(() =>
        {
            Assert.That(TableExistsCount(), Is.EqualTo(1), "Table must deploy on the modern binary.");
            Assert.That(IndexExistsCount(), Is.EqualTo(1), "The functional index must be created.");
            Assert.That(DowngradedAuditCount(), Is.EqualTo(0), "No downgrade may be recorded on a supported server.");
        });
    }
}
