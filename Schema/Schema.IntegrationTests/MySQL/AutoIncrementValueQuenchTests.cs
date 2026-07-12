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
/// Integration tests for AutoIncrementValue quench behavior.
/// Verifies that the declared AutoIncrementValue is applied set-if-higher on both
/// new table creation (MissingTableAndColumnQuench) and existing table modification
/// (ModifiedTableQuench), and that it is idempotent and WhatIf-aware.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class AutoIncrementValueQuenchTests
{
    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDb = FixtureSetup.MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
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
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`ai_quench_test`";
        cmd.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`ai_quench_test`";
        cmd.ExecuteNonQuery();
    }

    private string BuildTableJson(ulong? autoIncrementValue = null)
    {
        var table = new MySqlTable
        {
            Name = "`ai_quench_test`",
            Engine = "InnoDB",
            AutoIncrementValue = autoIncrementValue,
            Columns =
            [
                new MySqlColumn
                {
                    Name = "`id`",
                    DataType = "INT UNSIGNED",
                    Nullable = false,
                    AutoIncrement = true
                },
                new MySqlColumn
                {
                    Name = "`name`",
                    DataType = "VARCHAR(100)",
                    Nullable = true
                }
            ],
            Indexes =
            [
                new Schema.Domain.Index
                {
                    Name = "`pk_ai_quench_test`",
                    PrimaryKey = true,
                    Unique = true,
                    IndexColumns = "`id`"
                }
            ]
        };
        return "[" + JsonConvert.SerializeObject(table) + "]";
    }

    private ulong? GetLiveAutoIncrement()
    {
        // INFORMATION_SCHEMA.TABLES.AUTO_INCREMENT returns NULL for empty InnoDB tables
        // even after an explicit ALTER TABLE ... AUTO_INCREMENT=N. Use SHOW TABLE STATUS
        // which reads the storage-level value reliably.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SHOW TABLE STATUS FROM `{_testDb}` LIKE 'ai_quench_test'";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var ordinal = reader.GetOrdinal("Auto_increment");
        if (reader.IsDBNull(ordinal)) return null;
        return Convert.ToUInt64(reader.GetValue(ordinal));
    }

    private void RunQuench(string json, bool whatIf = false)
    {
        using var cmd = _connection.CreateCommand();
        var whatIfVal = whatIf ? 1 : 0;
        var escapedJson = json.Replace("'", "''");

        cmd.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{escapedJson}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL SchemaSmith_MissingTableAndColumnQuench('{_testDb}', {whatIfVal})";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL SchemaSmith_ModifiedTableQuench('TestProduct', '{_testDb}', {whatIfVal}, 0, 1, 1, 1, 1, 0)";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public void NewTable_WithAutoIncrementValue_AppliesValueOnCreate()
    {
        // New table declaring AutoIncrementValue=500 must start at >= 500
        var json = BuildTableJson(autoIncrementValue: 500);
        RunQuench(json, whatIf: false);

        var live = GetLiveAutoIncrement();
        Assert.That(live, Is.GreaterThanOrEqualTo(500UL),
            "New table with AutoIncrementValue=500 must have AUTO_INCREMENT >= 500 after quench.");
    }

    [Test]
    public void NewTable_WithAutoIncrementValue_IsIdempotent()
    {
        // Deploy twice with the same AutoIncrementValue — second quench must not error
        var json = BuildTableJson(autoIncrementValue: 500);
        RunQuench(json, whatIf: false);
        Assert.DoesNotThrow(() => RunQuench(json, whatIf: false),
            "Re-quenching with the same AutoIncrementValue must be idempotent (no error).");

        var live = GetLiveAutoIncrement();
        Assert.That(live, Is.GreaterThanOrEqualTo(500UL),
            "After re-quench, AUTO_INCREMENT must remain >= 500.");
    }

    [Test]
    public void ExistingTable_WithHigherAutoIncrementValue_RaisesValue()
    {
        // Create the table first without AutoIncrementValue
        var json = BuildTableJson(autoIncrementValue: null);
        RunQuench(json, whatIf: false);

        // Insert a row to ensure the table is accessed (InnoDB tracks AUTO_INCREMENT after first use)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO `{_testDb}`.`ai_quench_test` (name) VALUES ('seed_row')";
        cmd.ExecuteNonQuery();

        // Re-quench with AutoIncrementValue = 1000 (higher than current ~2)
        json = BuildTableJson(autoIncrementValue: 1000);
        RunQuench(json, whatIf: false);

        // Insert another row: its id must be >= 1000 to prove the seed was applied
        cmd.CommandText = $"INSERT INTO `{_testDb}`.`ai_quench_test` (name) VALUES ('after_seed')";
        cmd.ExecuteNonQuery();
        cmd.CommandText = $"SELECT MAX(id) FROM `{_testDb}`.`ai_quench_test`";
        var maxId = Convert.ToUInt64(cmd.ExecuteScalar());

        Assert.That(maxId, Is.GreaterThanOrEqualTo(1000UL),
            "After re-quenching with AutoIncrementValue=1000, the next inserted row must receive id >= 1000.");
    }

    [Test]
    public void ExistingTable_WithLowerAutoIncrementValue_DoesNotLowerIt()
    {
        // Create table, insert rows to drive counter above 1, then try to declare a lower value
        var json = BuildTableJson(autoIncrementValue: null);
        RunQuench(json, whatIf: false);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO `{_testDb}`.`ai_quench_test` (name) VALUES ('row1'), ('row2'), ('row3')";
        cmd.ExecuteNonQuery();

        var liveBefore = GetLiveAutoIncrement();

        // Quench with AutoIncrementValue = 1 (lower than current)
        json = BuildTableJson(autoIncrementValue: 1);
        RunQuench(json, whatIf: false);

        var liveAfter = GetLiveAutoIncrement();
        Assert.That(liveAfter, Is.GreaterThanOrEqualTo(liveBefore),
            "Declaring a lower AutoIncrementValue must not lower the live AUTO_INCREMENT (set-if-higher semantics).");
    }

    [Test]
    public void NewTable_WithoutAutoIncrementValue_DefaultBehavior()
    {
        // Table without AutoIncrementValue should deploy normally and accept inserts
        var json = BuildTableJson(autoIncrementValue: null);
        Assert.DoesNotThrow(() => RunQuench(json, whatIf: false),
            "Quenching a table without AutoIncrementValue must not error.");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO `{_testDb}`.`ai_quench_test` (name) VALUES ('test')";
        Assert.DoesNotThrow(() => cmd.ExecuteNonQuery(),
            "Insert into new table without AutoIncrementValue must work.");
    }

    [Test]
    public void WhatIf_WithHigherAutoIncrementValue_DoesNotApply()
    {
        // First deploy the table without AutoIncrementValue (WhatIf won't create it)
        var json = BuildTableJson(autoIncrementValue: null);
        RunQuench(json, whatIf: false);

        var liveBefore = GetLiveAutoIncrement();

        // WhatIf with a higher value — must NOT change the live value
        json = BuildTableJson(autoIncrementValue: 5000);
        RunQuench(json, whatIf: true);

        var liveAfter = GetLiveAutoIncrement();
        Assert.That(liveAfter, Is.EqualTo(liveBefore),
            "WhatIf mode must not apply AUTO_INCREMENT change to the live database.");
    }
}
