// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

public abstract class TableQuench_AutoIncrementRemovalSharedTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_RemovesAutoIncrement_DataPreserving()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"RemoveAutoInc_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"USE `{_mainDb}`";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
CREATE TABLE `{tableName}` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Val` INT NOT NULL,
  PRIMARY KEY (`Id`)
);
INSERT INTO `{tableName}` (`Id`, `Val`) VALUES (1, 10), (2, 20);";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'Id'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Does.Contain("auto_increment"), "Id should be auto_increment before quench");

        // Declare WITHOUT AutoIncrement. Keep PK (auto_increment requires a key; removing AI is the only change).
        var json = $$"""
[
{
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Id",  "DataType": "INT", "Nullable": false },
        { "Name": "Val", "DataType": "INT", "Nullable": false }
    ],
    "Indexes": [
        { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" }
    ]
}
]
""";
        RunTableQuenchProc(cmd, json);

        cmd.CommandText = $@"SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'Id'";
        Assert.That(cmd.ExecuteScalar()?.ToString() ?? "", Does.Not.Contain("auto_increment"), "Id should no longer be auto_increment");

        cmd.CommandText = $"SELECT COUNT(*) FROM `{tableName}`";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(2), "Data must be preserved");

        cmd.CommandText = $"DROP TABLE `{tableName}`";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
