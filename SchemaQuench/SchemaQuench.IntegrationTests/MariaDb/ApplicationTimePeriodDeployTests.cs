// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests.MariaDb;
using SchemaQuench.IntegrationTests.Shared;

namespace SchemaQuench.IntegrationTests.MariaDb;

/// <summary>
/// A declared application-time period has to reach the database.
/// <para>Extraction learned to read periods before deploy learned to write them, and that gap is the
/// dangerous shape: a package extracted from an 11.4 server carries its periods, and deploying it to a
/// fresh database would create the table without them — losing a declared part of the schema with no
/// error and nothing in the output to notice. This is the test that says the round trip closes.</para>
/// <para>MariaDB-only: MySQL has no application-time periods at any version.</para>
/// </summary>
[Category("MariaDb")]
[TestFixture]
public class ApplicationTimePeriodDeployTests : BaseTableQuenchTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDbName => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();

    private const string TableName = "PeriodDeployTarget";

    private static string TableJson() => $$"""
        [{
            "Name": "{{TableName}}",
            "Columns": [
                { "Name": "Id", "DataType": "INT", "Nullable": false },
                { "Name": "ValidFrom", "DataType": "DATE", "Nullable": false },
                { "Name": "ValidTo", "DataType": "DATE", "Nullable": false }
            ],
            "Indexes": [ { "Name": "PRIMARY", "IndexColumns": "Id", "PrimaryKey": true, "Unique": true } ],
            "Periods": [
                { "Name": "Validity", "StartColumn": "ValidFrom", "EndColumn": "ValidTo" }
            ]
        }]
        """;

    [Test]
    public void ADeclaredPeriodIsCreatedWithTheTable()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MariaDb).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith_SupportsApplicationTimePeriods()";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            Assert.Ignore("This server cannot declare an application-time period (MariaDB 10.4.3+), so the "
                          + "clause is deliberately suppressed and there is nothing to verify.");

        cmd.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        cmd.ExecuteNonQuery();

        RunTableQuenchProc(cmd, TableJson());

        // Asserted against the catalog rather than against the generated SQL: the point is that the
        // period EXISTS on the deployed table, not that a particular statement was emitted. Reading
        // PERIODS needs 11.4 even though declaring one needs only 10.4.3 -- the two thresholds are
        // genuinely different, so on 10.4.3-11.3 fall back to proving the table deployed at all.
        cmd.CommandText = "SELECT SchemaSmith_ServerVersionNum()";
        var version = Convert.ToInt32(cmd.ExecuteScalar());

        if (version < 1104)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES "
                              + $"WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{TableName}'";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1),
                "Below 11.4 the period cannot be read back, but the table carrying it must still deploy — "
                + "a period clause the engine accepts must not break the CREATE.");
            Assert.Ignore($"MariaDB {version} has no INFORMATION_SCHEMA.PERIODS (11.4+), so the period "
                          + "itself cannot be verified here. The table deployed.");
        }

        cmd.CommandText = $@"
SELECT CONCAT(PERIOD, '|', START_COLUMN_NAME, '|', END_COLUMN_NAME)
  FROM INFORMATION_SCHEMA.PERIODS
 WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{TableName}' AND PERIOD <> 'SYSTEM_TIME'";
        var actual = cmd.ExecuteScalar()?.ToString();

        Assert.That(actual, Is.EqualTo("Validity|ValidFrom|ValidTo"),
            "The declared period must reach the database with its columns in the declared order. A "
            + "transposed pair would invert every interval the table describes.");

        cmd.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    /// <summary>
    /// A period declared for a table that ALREADY exists is added to it, and declaring it again does
    /// nothing.
    /// <para>The idempotence half is the one that bites: `ADD PERIOD FOR` fails outright if the period
    /// is already there, so a convergence pass that cannot tell would break every re-deploy. That is
    /// exactly why this is gated on 11.4 rather than on 10.4.3 — declaring a period needs 10.4.3, but
    /// knowing whether one is already present needs the catalog that arrives in 11.4.</para>
    /// </summary>
    [Test]
    public void APeriodIsAddedToAnExistingTable_AndReDeployingIsANoOp()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MariaDb).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith_ServerVersionNum()";
        if (Convert.ToInt32(cmd.ExecuteScalar()) < 1104)
            Assert.Ignore("Reconciling a period on an existing table needs MariaDB 11.4 to read the "
                          + "current state; below that it is declined by design.");

        const string existing = "PeriodAddedLater";
        cmd.CommandText = $"DROP TABLE IF EXISTS `{existing}`";
        cmd.ExecuteNonQuery();

        // Deployed WITHOUT a period first, so the period genuinely has to be added by a later pass
        // rather than riding the CREATE.
        cmd.CommandText = $@"
CREATE TABLE `{existing}` (`Id` INT NOT NULL PRIMARY KEY,
                           `ValidFrom` DATE NOT NULL, `ValidTo` DATE NOT NULL) ENGINE=InnoDB";
        cmd.ExecuteNonQuery();

        var json = $$"""
            [{
                "Name": "{{existing}}",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "ValidFrom", "DataType": "DATE", "Nullable": false },
                    { "Name": "ValidTo", "DataType": "DATE", "Nullable": false }
                ],
                "Indexes": [ { "Name": "PRIMARY", "IndexColumns": "Id", "PrimaryKey": true, "Unique": true } ],
                "Periods": [
                    { "Name": "Validity", "StartColumn": "ValidFrom", "EndColumn": "ValidTo" }
                ]
            }]
            """;

        RunTableQuenchProc(cmd, json);

        cmd.CommandText = $@"
SELECT CONCAT(PERIOD, '|', START_COLUMN_NAME, '|', END_COLUMN_NAME)
  FROM INFORMATION_SCHEMA.PERIODS
 WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{existing}' AND PERIOD <> 'SYSTEM_TIME'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Validity|ValidFrom|ValidTo"),
            "A period declared for an existing table must be added to it.");

        // The second pass is the real assertion. ADD PERIOD FOR errors if the period exists, so a
        // convergence pass that cannot see the current state would throw here on every re-deploy.
        Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, json),
            "Re-deploying an unchanged package must be a no-op. ADD PERIOD FOR fails when the period is "
            + "already present, so this throwing means the existence check is not working.");

        cmd.CommandText = $@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.PERIODS
 WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{existing}' AND PERIOD <> 'SYSTEM_TIME'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1),
            "and the re-deploy must not have duplicated it.");

        cmd.CommandText = $"DROP TABLE IF EXISTS `{existing}`";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

}
