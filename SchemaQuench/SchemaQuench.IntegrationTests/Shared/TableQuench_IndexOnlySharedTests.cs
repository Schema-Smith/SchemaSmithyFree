// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using System;

using NUnit.Framework;

namespace SchemaQuench.IntegrationTests.Shared;

/// <summary>
/// Integration tests for Index Only quench functionality.
/// Tests that IndexOnlyQuench correctly handles indexes without touching table structure.
/// </summary>
[Category("Integration")]
public abstract class TableQuench_IndexOnlySharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string MainDb { get; }
    protected abstract string MainConnectionString { get; }

    private IDbConnection _connection = null!;
    private string _testTableName = null!;
    private string _testDb = null!;

    [SetUp]
    public void SetUp()
    {
        _testDb = MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(MainConnectionString);
        _connection.Open();
        _testTableName = $"_test_idx_{Guid.NewGuid():N}".Substring(0, 30);

        // Create test table with a primary key and one column
        using var command = _connection.CreateCommand();
        command.CommandText = $@"
            CREATE TABLE `{_testDb}`.`{_testTableName}` (
                id INT AUTO_INCREMENT PRIMARY KEY,
                code VARCHAR(20) NOT NULL,
                name VARCHAR(100) NOT NULL,
                value DECIMAL(10,2) DEFAULT 0.00,
                active TINYINT(1) DEFAULT 1
            )";
        command.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{_testTableName}`";
            command.ExecuteNonQuery();
        }
        catch { /* ignore cleanup errors */ }

        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void IndexOnlyQuench_ShouldAddMissingIndex()
    {
        // Arrange - Parse table JSON with an index defined
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_code`"",
                    ""IndexColumns"": ""`code`"",
                    ""Unique"": false,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        // Parse the JSON to create temp tables
        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act - Call IndexOnlyQuench
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0, 1)";
        command.ExecuteNonQuery();

        // Assert - Index should exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_code'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.GreaterThan(0), "Index should have been created");
    }

    [Test]
    public void IndexOnlyQuench_ShouldAddUniqueIndex()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_code_unique`"",
                    ""IndexColumns"": ""`code`"",
                    ""Unique"": true,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0, 1)";
        command.ExecuteNonQuery();

        // Assert - Unique index should exist
        command.CommandText = $@"
            SELECT NON_UNIQUE FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_code_unique'
              AND SEQ_IN_INDEX = 1";
        var nonUnique = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(nonUnique, Is.EqualTo(0), "Index should be unique (NON_UNIQUE=0)");
    }

    [Test]
    public void IndexOnlyQuench_ShouldNotCreateTableOrColumn()
    {
        // Arrange - Define a column that doesn't exist
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Columns"": [
                {{
                    ""Name"": ""`new_column`"",
                    ""DataType"": ""VARCHAR(50)"",
                    ""Nullable"": true
                }}
            ],
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_code`"",
                    ""IndexColumns"": ""`code`"",
                    ""Unique"": false,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act - IndexOnlyQuench should NOT add the column
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0, 1)";
        command.ExecuteNonQuery();

        // Assert - new_column should NOT exist (IndexOnly doesn't touch columns)
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND COLUMN_NAME = 'new_column'";
        var columnCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(columnCount, Is.EqualTo(0), "Column should NOT have been created by IndexOnlyQuench");

        // But the index should exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_code'";
        var indexCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(indexCount, Is.GreaterThan(0), "Index should have been created");
    }

    [Test]
    public void IndexOnlyQuench_ShouldDropUnknownIndex_WhenEnabled()
    {
        // Arrange - Create an index that won't be in the definition
        using var command = _connection.CreateCommand();
        command.CommandText = $"CREATE INDEX `idx_to_drop` ON `{_testDb}`.`{_testTableName}` (`name`)";
        command.ExecuteNonQuery();

        // Register it in ProductOwnership
        command.CommandText = $@"
            INSERT INTO `{MainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
            VALUES ('TestProduct', '', '{_testDb}', 'INDEX', '{_testTableName}.idx_to_drop')";
        command.ExecuteNonQuery();

        // Define table with only idx_code (not idx_to_drop)
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_code`"",
                    ""IndexColumns"": ""`code`"",
                    ""Unique"": false,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act - Call with DropUnknownIndexes=1
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 1, 1)";
        command.ExecuteNonQuery();

        // Assert - idx_to_drop should be gone
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_to_drop'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(0), "Unknown index should have been dropped");

        // And idx_code should exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_code'";
        var codeIndexCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(codeIndexCount, Is.GreaterThan(0), "Defined index should exist");
    }

    [Test]
    public void IndexOnlyQuench_ShouldRenameIndex()
    {
        // Arrange - Create an index with old name
        using var command = _connection.CreateCommand();
        command.CommandText = $"CREATE INDEX `idx_old_name` ON `{_testDb}`.`{_testTableName}` (`code`)";
        command.ExecuteNonQuery();

        // Register it in ProductOwnership
        command.CommandText = $@"
            INSERT INTO `{MainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
            VALUES ('TestProduct', '', '{_testDb}', 'INDEX', '{_testTableName}.idx_old_name')";
        command.ExecuteNonQuery();

        // Define table with same columns but new name
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_new_name`"",
                    ""IndexColumns"": ""`code`"",
                    ""Unique"": false,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0, 1)";
        command.ExecuteNonQuery();

        // Assert - Old name should be gone, new name should exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_old_name'";
        var oldCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(oldCount, Is.EqualTo(0), "Old index name should not exist");

        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_new_name'";
        var newCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(newCount, Is.GreaterThan(0), "New index name should exist");
    }

    [Test]
    public void IndexOnlyQuench_ShouldModifyIndex_WhenColumnsChange()
    {
        // Arrange - Create an index on just 'code'
        using var command = _connection.CreateCommand();
        command.CommandText = $"CREATE INDEX `idx_composite` ON `{_testDb}`.`{_testTableName}` (`code`)";
        command.ExecuteNonQuery();

        // Register it in ProductOwnership
        command.CommandText = $@"
            INSERT INTO `{MainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
            VALUES ('TestProduct', '', '{_testDb}', 'INDEX', '{_testTableName}.idx_composite')";
        command.ExecuteNonQuery();

        // Define table with same index name but different columns
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_composite`"",
                    ""IndexColumns"": ""`code`, `name`"",
                    ""Unique"": false,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0, 1)";
        command.ExecuteNonQuery();

        // Assert - Index should now have 2 columns
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_composite'";
        var columnCount = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(columnCount, Is.EqualTo(2), "Index should now have 2 columns");
    }

    [Test]
    public void IndexOnlyQuench_WhatIfMode_ShouldNotMakeChanges()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_whatif`"",
                    ""IndexColumns"": ""`code`"",
                    ""Unique"": false,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act - Call with WhatIf=1
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 1, 0, 1)";
        command.ExecuteNonQuery();

        // Assert - Index should NOT exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'idx_whatif'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(0), "Index should NOT have been created in WhatIf mode");
    }

    [Test]
    public void IndexOnlyQuench_ShouldTrackIndexInProductOwnership()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Indexes"": [
                {{
                    ""Name"": ""`idx_tracked`"",
                    ""IndexColumns"": ""`code`"",
                    ""Unique"": false,
                    ""PrimaryKey"": false,
                    ""IndexType"": ""BTREE""
                }}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0, 1)";
        command.ExecuteNonQuery();

        // Assert - Index should be tracked in ProductOwnership
        command.CommandText = $@"
            SELECT COUNT(*) FROM `{MainDb}`.SchemaSmith_ProductOwnership
            WHERE ProductName = 'TestProduct'
              AND ObjectSchema = '{_testDb}'
              AND ObjectType = 'INDEX'
              AND ObjectName = '{_testTableName}.idx_tracked'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(1), "Index should be tracked in ProductOwnership");
    }
}
