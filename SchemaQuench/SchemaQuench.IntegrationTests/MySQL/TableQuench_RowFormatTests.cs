// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;
using NUnit.Framework;
using Schema.IntegrationTests.MySQL;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for MySQL RowFormat support in table quench.
/// Verifies that RowFormat is applied on CREATE TABLE and modified on ALTER TABLE.
/// </summary>
[TestFixture]
[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_RowFormatTests : BaseTableQuenchTests
{
    private const string TestSchema = "RowFormatTests";

    [Test]
    public void ShouldCreateTableWithRowFormat()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT ROW_FORMAT FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'TableWithRowFormat'";
        var rowFormat = cmd.ExecuteScalar()?.ToString();
        Assert.That(rowFormat, Is.EqualTo("Compressed").IgnoreCase,
            "Table should have COMPRESSED row format after creation");

        conn.Close();
    }

    [Test]
    public void ShouldModifyTableRowFormat()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT ROW_FORMAT FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'TableToModifyRowFormat'";
        var rowFormat = cmd.ExecuteScalar()?.ToString();
        Assert.That(rowFormat, Is.EqualTo("Compact").IgnoreCase,
            "Table should have COMPACT row format after modification");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Drop and recreate to ensure clean state
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CREATE DATABASE `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandTimeout = 300;

        // Create existing table with default row format (DYNAMIC) for modification test
        cmd.CommandText = $@"
CREATE TABLE `{TestSchema}`.`TableToModifyRowFormat` (
    `Id` INT NOT NULL PRIMARY KEY,
    `Name` VARCHAR(100) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'TableToModifyRowFormat');
";
        cmd.ExecuteNonQuery();

        // Quench: create new table with COMPRESSED, modify existing to COMPACT
        var json = """
            [
            {
                "Name": "TableWithRowFormat",
                "RowFormat": "COMPRESSED",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Description", "DataType": "VARCHAR(200)", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "`Id`" }
                ]
            },
            {
                "Name": "TableToModifyRowFormat",
                "RowFormat": "COMPACT",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Name", "DataType": "VARCHAR(100)", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "`Id`" }
                ]
            }
            ]
            """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingTableAndColumnQuench('{TestSchema}', 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0)";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        try
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestSchema}`";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
        catch { /* Ignore cleanup errors */ }
    }
}
