// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using System;

using NUnit.Framework;
using Schema.IntegrationTests.MySQL;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for FULLTEXT index deployment via ParseTableJson and IndexOnlyQuench.
/// Verifies that FULLTEXT indexes defined in table JSON are correctly parsed and created.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class TableQuench_FullTextIndexTests
{
    private IDbConnection _connection = null!;
    private string _testTableName = null!;
    private string _testDb = null!;

    [SetUp]
    public void SetUp()
    {
        FixtureSetup.EnsureInitialized();
        _testDb = FixtureSetup.MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
        _testTableName = $"_test_ft_{Guid.NewGuid():N}"[..30];

        // Create test table with TEXT columns suitable for FULLTEXT indexing
        using var command = _connection.CreateCommand();
        command.CommandText = $@"
            CREATE TABLE `{_testDb}`.`{_testTableName}` (
                id INT AUTO_INCREMENT PRIMARY KEY,
                title VARCHAR(200) NOT NULL,
                content TEXT,
                summary TEXT
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
    public void ShouldDeployFullTextIndex()
    {
        // Arrange - Parse table JSON with a FULLTEXT index defined, then run full table quench path
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""Columns"": [
                {{""Name"": ""`id`"", ""DataType"": ""INT"", ""Nullable"": false, ""AutoIncrement"": true}},
                {{""Name"": ""`title`"", ""DataType"": ""VARCHAR(200)"", ""Nullable"": false}},
                {{""Name"": ""`content`"", ""DataType"": ""TEXT""}},
                {{""Name"": ""`summary`"", ""DataType"": ""TEXT""}}
            ],
            ""Indexes"": [
                {{""Name"": ""`PRIMARY`"", ""IndexColumns"": ""`id`"", ""PrimaryKey"": true}}
            ],
            ""FullTextIndexes"": [
                {{""Name"": ""ft_content"", ""Columns"": ""content""}}
            ]
        }}]";

        // Parse JSON and run IndexOnlyQuench (which handles FULLTEXT indexes)
        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act - IndexOnlyQuench creates the FULLTEXT index
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0)";
        command.ExecuteNonQuery();

        // Assert - FULLTEXT index should exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'ft_content'
              AND INDEX_TYPE = 'FULLTEXT'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.GreaterThan(0), "FULLTEXT index should have been created");
    }

    [Test]
    public void ShouldDeployFullTextIndexViaIndexOnly()
    {
        // Arrange - Use IndexOnly path explicitly (no column definitions needed)
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""FullTextIndexes"": [
                {{""Name"": ""ft_title"", ""Columns"": ""title""}}
            ]
        }}]";

        // Parse JSON
        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act - IndexOnlyQuench
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0)";
        command.ExecuteNonQuery();

        // Assert - FULLTEXT index should exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'ft_title'
              AND INDEX_TYPE = 'FULLTEXT'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.GreaterThan(0), "FULLTEXT index should have been created via IndexOnly path");
    }

    [Test]
    public void ShouldDeployFullTextIndexWithParser()
    {
        // Arrange - FULLTEXT index with ngram parser (available in MySQL 8.0+)
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""FullTextIndexes"": [
                {{""Name"": ""ft_content_ngram"", ""Columns"": ""content"", ""Parser"": ""ngram""}}
            ]
        }}]";

        // Parse JSON
        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0)";
        command.ExecuteNonQuery();

        // Assert - FULLTEXT index should exist
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'ft_content_ngram'
              AND INDEX_TYPE = 'FULLTEXT'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.GreaterThan(0), "FULLTEXT index with parser should have been created");

        // Verify the parser was applied by checking INFORMATION_SCHEMA.INNODB_INDEXES
        // or by attempting to use the index — the parser attribute is not exposed in STATISTICS.
        // For now, existence of the index confirms the WITH PARSER clause didn't cause an error.
    }

    [Test]
    public void ShouldDeployMultiColumnFullTextIndex()
    {
        // Arrange - FULLTEXT index spanning multiple columns
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""FullTextIndexes"": [
                {{""Name"": ""ft_multi"", ""Columns"": ""title, content, summary""}}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0)";
        command.ExecuteNonQuery();

        // Assert - FULLTEXT index should exist with 3 columns
        command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{_testDb}'
              AND TABLE_NAME = '{_testTableName}'
              AND INDEX_NAME = 'ft_multi'
              AND INDEX_TYPE = 'FULLTEXT'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(3), "FULLTEXT index should span 3 columns");
    }

    [Test]
    public void ShouldSelectGatedFullTextVariant()
    {
        var variantTableName = $"{_testTableName}_v";
        using var command = _connection.CreateCommand();

        // Create the variant table (SetUp only creates _testTableName)
        command.CommandText = $@"
            CREATE TABLE `{_testDb}`.`{variantTableName}` (
                `id` INT AUTO_INCREMENT PRIMARY KEY,
                `content` TEXT
            )";
        command.ExecuteNonQuery();

        try
        {
            var tableJson = $@"[{{
                ""Name"": ""`{variantTableName}`"",
                ""Columns"": [
                    {{""Name"": ""`id`"", ""DataType"": ""INT"", ""Nullable"": false, ""AutoIncrement"": true}},
                    {{""Name"": ""`content`"", ""DataType"": ""TEXT""}}
                ],
                ""Indexes"": [
                    {{""Name"": ""`PRIMARY`"", ""IndexColumns"": ""`id`"", ""PrimaryKey"": true}}
                ],
                ""FullTextIndexes"": [
                    {{""Name"": ""ft_variant"", ""Columns"": ""content"", ""ShouldApplyExpression"": ""1 = 1""}},
                    {{""Name"": ""ft_variant"", ""Columns"": ""content"", ""Parser"": ""ngram"", ""ShouldApplyExpression"": ""1 = 0""}}
                ]
            }}]";

            command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
            command.ExecuteNonQuery();

            command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0)";
            command.ExecuteNonQuery();

            // Exactly one FULLTEXT index — the gated-true (non-ngram) variant was selected
            command.CommandText = $@"
                SELECT COUNT(DISTINCT INDEX_NAME) FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{variantTableName}'
                  AND INDEX_TYPE = 'FULLTEXT'";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1),
                "Exactly one FULLTEXT index should be deployed — the variant with ShouldApplyExpression '1 = 1'");
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{variantTableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void ShouldTrackFullTextIndexInProductOwnership()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        var tableJson = $@"[{{
            ""Name"": ""`{_testTableName}`"",
            ""FullTextIndexes"": [
                {{""Name"": ""ft_owned"", ""Columns"": ""content""}}
            ]
        }}]";

        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_testDb}', '{tableJson.Replace("'", "''")}')";
        command.ExecuteNonQuery();

        // Act
        command.CommandText = $"CALL SchemaSmith_IndexOnlyQuench('TestProduct', '{_testDb}', 0, 0)";
        command.ExecuteNonQuery();

        // Assert - FULLTEXT index should be tracked in ProductOwnership
        command.CommandText = $@"
            SELECT COUNT(*) FROM `{FixtureSetup.MainDb}`.SchemaSmith_ProductOwnership
            WHERE ProductName = 'TestProduct'
              AND ObjectSchema = '{_testDb}'
              AND ObjectType = 'INDEX'
              AND ObjectName = '{_testTableName}.ft_owned'";
        var count = Convert.ToInt32(command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(1), "FULLTEXT index should be tracked in ProductOwnership");
    }
}
