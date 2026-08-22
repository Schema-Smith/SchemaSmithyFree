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
/// Column-SRID gating for MariaDB. Unlike invisible columns (a genuine MariaDB threshold, 10.3 -- see
/// Schema.IntegrationTests.MariaDb.InvisibleColumnGatingTests), MariaDB has NO equivalent to MySQL's
/// column-level SRID attribute at any version -- verified live against MariaDB 11.4.12, the newest
/// supported release: `col POINT SRID 4326` is a hard syntax error, and INFORMATION_SCHEMA.COLUMNS
/// carries no SRS_ID column at all. So SchemaSmith_SupportsColumnSrid() is 0 on MariaDB
/// unconditionally -- no @schemasmith_version_override is needed to exercise the degrade path here,
/// unlike Schema.IntegrationTests.MySQL.ColumnSridGatingTests, which simulates a below-floor MySQL via
/// the override.
///
/// The regression this file exists to pin: SRS_ID does not exist on MariaDB's
/// INFORMATION_SCHEMA.COLUMNS at all, unlike SchemaSmith_IndexIsVisible's IS_VISIBLE/IGNORED
/// divergence where BOTH engines carry some column. A naive extraction that read c.SRS_ID directly
/// (the way GenerateTableJson reads c.EXTRA for Invisible) would throw ER_BAD_FIELD_ERROR extracting
/// EVERY table on MariaDB, not just spatial ones -- SchemaSmith_ColumnSrid isolates this (its MariaDb
/// override always returns NULL). See AnyTable_ExtractionDoesNotErrorOnMariaDb_MissingSrsIdColumn below.
/// </summary>
[Category("MariaDb")]
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

    private long TableExistsCount() => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}'");

    private long ColumnExistsCount(string columnName) => Scalar(
        $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = '{columnName}'");

    private long DowngradedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'downgraded' AND ObjectName LIKE '%{TableName}.loc%'");

    private string ExtractJson()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', '{TableName}')";
        var json = "";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            json += reader[0];
        return json;
    }

    // The name column is deliberately ordinary (non-spatial) -- it is what proves extraction does not
    // reference SRS_ID for every column indiscriminately, only ever through the isolating wrapper.
    private static string BuildTableJson(int? srid)
    {
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn { Name = "`name`", DataType = "VARCHAR(50)", Nullable = true },
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
        Exec($"CALL SchemaSmith_TableQuench('SridColGateProductMdb', '{_testDb}', '{json}', 0, 0, 0)");
    }

    // ---- the critical regression: SRS_ID must never be referenced directly on MariaDB ---------------

    [Test]
    public void AnyTable_ExtractionDoesNotErrorOnMariaDb_MissingSrsIdColumn()
    {
        // No declared SRID at all -- this alone still exercises SchemaSmith_ColumnSrid for every
        // column (spatial and non-spatial) via GenerateTableJson. A regression that referenced
        // SRS_ID directly instead of through the wrapper would break extraction outright here, on
        // MariaDB, for a table that never even mentions SRID -- not a subtle failure.
        Deploy(srid: null);

        string json = null;
        Assert.DoesNotThrow(() => json = ExtractJson(),
            "Extraction must not error on MariaDB -- SRS_ID does not exist on INFORMATION_SCHEMA.COLUMNS there.");

        // Pattern-matched rather than asserted non-null: an assertion does not narrow the variable for
        // flow analysis, so every later use still reads as a possible null dereference.
        if (PlatformDeserializer.DeserializeTable(json, Platform.MariaDb) is not MySqlTable table)
        {
            Assert.Fail("Extraction must deserialize to a MariaDB table; a null here means the JSON itself"
                        + " is wrong, which is worth failing on explicitly rather than as a"
                        + " NullReferenceException below.");
            return;
        }

        // Assert.Multiple keeps going after a failed assertion, so asserting loc non-null there would not
        // stop the dereference on the next line. Establish it first.
        if (table.Columns.Find(c => c.Name.Contains("loc")) is not MySqlColumn loc)
        {
            Assert.Fail("The spatial column must be extracted on MariaDB, only without an SRID.");
            return;
        }

        var name = table.Columns.Find(c => c.Name.Contains("name"));

        Assert.Multiple(() =>
        {
            Assert.That(name, Is.Not.Null, "Extraction of the ordinary (non-spatial) column must still succeed.");
            Assert.That(loc.Srid, Is.Null, "No SRID was declared, so extraction must report none.");
        });
    }

    // ---- declared SRID degrades unconditionally (no version threshold on MariaDB) ------------------

    [Test]
    public void DeclaredSrid_Warn_DeploysColumnUnrestricted_AndRecordsDowngraded()
    {
        SetPolicy("warn");

        Deploy(srid: 4326);

        Assert.Multiple(() =>
        {
            Assert.That(TableExistsCount(), Is.EqualTo(1), "Table must still deploy on MariaDB.");
            Assert.That(ColumnExistsCount("loc"), Is.EqualTo(1), "The column must still be created -- only its SRID restriction degrades.");
            Assert.That(DowngradedAuditCount(), Is.EqualTo(1), "The unsupported SRID must record a 'downgraded' manifest row.");
        });

        if (PlatformDeserializer.DeserializeTable(ExtractJson(), Platform.MariaDb) is not MySqlTable table)
        {
            Assert.Fail("Extraction must deserialize to a MariaDB table.");
            return;
        }

        if (table.Columns.Find(c => c.Name.Contains("loc")) is not MySqlColumn loc)
        {
            Assert.Fail("The spatial column must still be extracted, only without its SRID.");
            return;
        }

        Assert.That(loc.Srid, Is.Null, "The SRID restriction never reached the engine, so extraction must round-trip none.");
    }

    [Test]
    public void DeclaredSrid_Fail_AbortsNamingTheOffendingColumn()
    {
        SetPolicy("fail");

        Assert.That(() => Deploy(srid: 4326), Throws.Exception.With.Message.Contains("8.0.3"),
            "Under policy 'fail' a declared column SRID on MariaDB (which never supports it) must abort the deploy.");
    }
}
