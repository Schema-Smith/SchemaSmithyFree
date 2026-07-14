// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

#nullable enable
using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

// Parity (#243 follow-up): a generated column on a BRAND-NEW table (created by SchemaSmith) must be
// created AS a generated column. MySQL excludes generated columns from CREATE TABLE and adds them
// via a deferred step (they may reference other columns) — this pins that it actually works, the
// MySQL counterpart to the SQL Server / PostgreSQL new-table generated-column tests.
public abstract class TableQuench_GeneratedColumnSharedTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_CreatesGeneratedColumn_OnNewTable()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"GenNew_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        var json = $$"""
[{
    "Name": "`{{tableName}}`",
    "Columns": [
        { "Name": "`Qty`", "DataType": "INT", "Nullable": false },
        { "Name": "`DoubleQty`", "DataType": "INT", "GenerationExpression": "`Qty` * 2" }
    ]
}]
""";
        RunTableQuenchProc(cmd, json, productName: Guid.NewGuid().ToString());

        cmd.CommandText = $@"SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
                             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DoubleQty';";
        Assert.That(cmd.ExecuteScalar()?.ToString() ?? "", Does.Contain("GENERATED"),
            "DoubleQty must be created as a GENERATED column on a brand-new table, not a plain column.");

        // Data proof: the generation must actually compute.
        cmd.CommandText = $@"INSERT INTO `{tableName}` (`Qty`) VALUES (7);
                             SELECT `DoubleQty` FROM `{tableName}` WHERE `Qty` = 7;";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(14));

        cmd.CommandText = $"DROP TABLE `{tableName}`;";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
