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
}
