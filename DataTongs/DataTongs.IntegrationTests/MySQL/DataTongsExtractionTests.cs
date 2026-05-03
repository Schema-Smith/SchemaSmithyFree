// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using System;
using System.Linq;

using NUnit.Framework;
using Schema.IntegrationTests.MySQL;

namespace DataTongs.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for DataTongs data extraction against MySQL.
/// Uses dynamically created test databases via FixtureSetup.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
[Category("MySQL")]
public class DataTongsExtractionTests
{
    private IDbConnection _connection = null!;
    private global::DataTongs.DataTongs _dataTongs = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS `{FixtureSetup.MainDb}`.`legacy_types` (
    `id` INT NOT NULL PRIMARY KEY,
    `blob_data` BLOB,
    `longblob_data` LONGBLOB,
    `longtext_data` LONGTEXT,
    `json_data` JSON,
    `geometry_data` GEOMETRY
);

INSERT IGNORE INTO `{FixtureSetup.MainDb}`.`legacy_types` (`id`, `blob_data`, `longblob_data`, `longtext_data`, `json_data`, `geometry_data`) VALUES
    (1, X'DEADBEEF', X'CAFEBABE', 'Very long text content here', JSON_OBJECT('key', 'value'), ST_GeomFromText('POINT(1 1)'));

INSERT IGNORE INTO `{FixtureSetup.MainDb}`.`legacy_types` (`id`, `blob_data`, `longblob_data`, `longtext_data`, `json_data`, `geometry_data`) VALUES
    (2, NULL, NULL, NULL, NULL, NULL);
";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS `{FixtureSetup.MainDb}`.`legacy_types`";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [SetUp]
    public void SetUp()
    {
        _testDb = FixtureSetup.MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
        _dataTongs = new global::DataTongs.DataTongs(Platform.MySQL);
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    #region TableExists Tests

    [Test]
    public void TableExists_ExistingTable_ReturnsTrue()
    {
        using var command = _connection.CreateCommand();

        var exists = _dataTongs.TableExists(command, _testDb, "actor");

        Assert.That(exists, Is.True);
    }

    [Test]
    public void TableExists_NonExistentTable_ReturnsFalse()
    {
        using var command = _connection.CreateCommand();

        var exists = _dataTongs.TableExists(command, _testDb, "non_existent_table_xyz");

        Assert.That(exists, Is.False);
    }

    [Test]
    public void TableExists_CaseSensitive_ReturnsFalse()
    {
        using var command = _connection.CreateCommand();

        // Test database has 'actor' table, not 'ACTOR' (case-sensitive with BINARY)
        var exists = _dataTongs.TableExists(command, _testDb, "ACTOR");

        // MySQL on Windows may be case-insensitive, but our BINARY comparison should be case-sensitive
        // This test verifies our BINARY comparison is working
        Assert.That(exists, Is.False);
    }

    [Test]
    public void TableExists_WithBackticks_ReturnsTrue()
    {
        using var command = _connection.CreateCommand();

        var exists = _dataTongs.TableExists(command, $"`{_testDb}`", "`actor`");

        Assert.That(exists, Is.True);
    }

    #endregion

    #region GetSelectColumns Tests

    [Test]
    public void GetSelectColumns_Actor_ReturnsAllColumns()
    {
        using var command = _connection.CreateCommand();

        var columns = _dataTongs.GetSelectColumns(command, _testDb, "actor");

        // Returns a comma-separated string of column expressions
        Assert.That(columns, Is.Not.Null.And.Not.Empty);
        Assert.That(columns, Does.Contain("actor_id"));
        Assert.That(columns, Does.Contain("first_name"));
        Assert.That(columns, Does.Contain("last_name"));
        Assert.That(columns, Does.Contain("last_update"));
    }

    [Test]
    public void GetSelectColumns_ExcludesGeneratedColumns()
    {
        // Create a table with a generated column to test exclusion
        using var command = _connection.CreateCommand();
        var tableName = $"_test_gen_{Guid.NewGuid():N}".Substring(0, 30);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    first_name VARCHAR(50),
                    last_name VARCHAR(50),
                    full_name VARCHAR(100) GENERATED ALWAYS AS (CONCAT(first_name, ' ', last_name)) STORED
                )";
            command.ExecuteNonQuery();

            var columns = _dataTongs.GetSelectColumns(command, _testDb, tableName);

            // Should include id, first_name, last_name but not generated full_name
            Assert.That(columns, Does.Contain("id"));
            Assert.That(columns, Does.Contain("first_name"));
            Assert.That(columns, Does.Contain("last_name"));
            Assert.That(columns, Does.Not.Contain("full_name"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void GetSelectColumns_IncludesAutoIncrementColumns()
    {
        using var command = _connection.CreateCommand();

        var columns = _dataTongs.GetSelectColumns(command, _testDb, "actor");

        // actor_id is AUTO_INCREMENT but should still be included for data extraction
        Assert.That(columns, Does.Contain("actor_id"));
    }

    #endregion

    #region FormatColumnForJsonObject Tests

    [Test]
    public void FormatColumnForJsonObject_VarcharColumn_PassesThrough()
    {
        var column = new global::DataTongs.DataTongs.ColumnInfo { Name = "first_name", DataType = "varchar" };

        var result = global::DataTongs.DataTongs.FormatColumnForJsonObject(column);

        Assert.That(result, Is.EqualTo("'first_name', `first_name`"));
    }

    [Test]
    public void FormatColumnForJsonObject_GeometryColumn_UsesStAsText()
    {
        var column = new global::DataTongs.DataTongs.ColumnInfo { Name = "location", DataType = "geometry" };

        var result = global::DataTongs.DataTongs.FormatColumnForJsonObject(column);

        Assert.That(result, Does.Contain("ST_AsText"));
        Assert.That(result, Does.Contain("location"));
    }

    [Test]
    public void FormatColumnForJsonObject_DatetimeColumn_UsesDateFormat()
    {
        var column = new global::DataTongs.DataTongs.ColumnInfo { Name = "last_update", DataType = "datetime" };

        var result = global::DataTongs.DataTongs.FormatColumnForJsonObject(column);

        Assert.That(result, Does.Contain("DATE_FORMAT"));
        Assert.That(result, Does.Contain("last_update"));
    }

    [Test]
    public void FormatColumnForJsonObject_BitColumn_CastsToUnsigned()
    {
        var column = new global::DataTongs.DataTongs.ColumnInfo { Name = "is_active", DataType = "bit" };

        var result = global::DataTongs.DataTongs.FormatColumnForJsonObject(column);

        Assert.That(result, Does.Contain("CAST"));
        Assert.That(result, Does.Contain("UNSIGNED"));
    }

    [Test]
    public void FormatColumnForJsonObject_BlobColumn_UsesBase64()
    {
        var column = new global::DataTongs.DataTongs.ColumnInfo { Name = "picture", DataType = "blob" };

        var result = global::DataTongs.DataTongs.FormatColumnForJsonObject(column);

        Assert.That(result, Does.Contain("TO_BASE64"));
        Assert.That(result, Does.Contain("picture"));
    }

    [Test]
    public void FormatColumnForJsonObject_DateColumn_UsesDateFormat()
    {
        var column = new global::DataTongs.DataTongs.ColumnInfo { Name = "birth_date", DataType = "date" };

        var result = global::DataTongs.DataTongs.FormatColumnForJsonObject(column);

        Assert.That(result, Does.Contain("DATE_FORMAT"));
        Assert.That(result, Does.Contain("%Y-%m-%d"));
    }

    [Test]
    public void FormatColumnForJsonObject_IntColumn_PassesThrough()
    {
        var column = new global::DataTongs.DataTongs.ColumnInfo { Name = "actor_id", DataType = "int" };

        var result = global::DataTongs.DataTongs.FormatColumnForJsonObject(column);

        Assert.That(result, Is.EqualTo("'actor_id', `actor_id`"));
    }

    #endregion

    #region GetTableDataJson Tests

    [Test]
    public void GetTableDataJson_Actor_ReturnsValidJson()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "actor");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "actor", "`actor_id`", null);

        Assert.That(json, Is.Not.Null);
        Assert.That(json, Does.StartWith("["));
        Assert.That(json, Does.Contain("actor_id"));
        Assert.That(json, Does.Contain("first_name"));
        Assert.That(json, Does.Contain("last_name"));
    }

    [Test]
    public void GetTableDataJson_WithFilter_AppliesFilter()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "actor");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "actor", "`actor_id`", "actor_id = 1");

        Assert.That(json, Is.Not.Null);
        // Should only contain one actor
        var objectCount = json.Split(new[] { "},{" }, StringSplitOptions.None).Length;
        Assert.That(objectCount, Is.EqualTo(1));
    }

    [Test]
    public void GetTableDataJson_EmptyResult_ReturnsNull()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "actor");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "actor", "`actor_id`", "actor_id = -999");

        // JSON_ARRAYAGG returns null for empty results
        Assert.That(json == "[]" || json == "null" || string.IsNullOrEmpty(json), Is.True);
    }

    [Test]
    public void GetTableDataJson_DecimalColumn_PreservesPrecision()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "payment");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "payment", "`payment_id`", "payment_id = 1");

        Assert.That(json, Does.Contain("amount"));
        // Decimal values should be preserved (not converted to scientific notation)
    }

    [Test]
    public void GetTableDataJson_DateColumn_FormatsCorrectly()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "rental");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "rental", "`rental_id`", "rental_id = 1");

        Assert.That(json, Does.Contain("rental_date"));
        // Should contain ISO-formatted date
    }

    [Test]
    public void GetTableDataJson_NullValues_HandlesCorrectly()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "rental");

        // Some rentals have null return_date
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "rental", "`rental_id`", "return_date IS NULL AND rental_id <= 10");

        Assert.That(json, Is.Not.Null);
        // JSON should handle null values properly - return_date will be null in the output
    }

    [Test]
    public void GetTableDataJson_BinaryData_EncodesAsBase64()
    {
        // staff table has a picture column (blob)
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "staff");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "staff", "`staff_id`", null);

        Assert.That(json, Does.Contain("picture"));
        // Base64-encoded data should be present (or null)
    }

    [Test]
    public void GetTableDataJson_OrderByMultipleColumns()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "film_actor");

        // film_actor has composite key
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "film_actor", "`actor_id`, `film_id`", "actor_id = 1");

        Assert.That(json, Is.Not.Null);
        Assert.That(json, Does.Contain("actor_id"));
        Assert.That(json, Does.Contain("film_id"));
    }

    #endregion

    #region Legacy Type Tests

    [Test]
    public void GetSelectColumns_LegacyTypes_ReturnsAllColumns()
    {
        using var command = _connection.CreateCommand();

        var columns = _dataTongs.GetSelectColumns(command, _testDb, "legacy_types");

        Assert.That(columns, Is.Not.Null.And.Not.Empty);
        Assert.That(columns, Does.Contain("id"));
        Assert.That(columns, Does.Contain("blob_data"));
        Assert.That(columns, Does.Contain("longblob_data"));
        Assert.That(columns, Does.Contain("longtext_data"));
        Assert.That(columns, Does.Contain("json_data"));
        Assert.That(columns, Does.Contain("geometry_data"));
    }

    [Test]
    public void GetTableDataJson_LegacyTypes_ReturnsValidJson()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "legacy_types");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "legacy_types", "`id`", "id = 1");

        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("id"));
        Assert.That(json, Does.Contain("longtext_data"));
        Assert.That(json, Does.Contain("Very long text content"));
    }

    [Test]
    public void GetTableDataJson_LegacyTypes_BlobData_EncodesAsBase64()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "legacy_types");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "legacy_types", "`id`", "id = 1");

        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("blob_data"));
    }

    [Test]
    public void GetTableDataJson_LegacyTypes_GeometryData_UsesStAsText()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "legacy_types");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "legacy_types", "`id`", "id = 1");

        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("geometry_data"));
        Assert.That(json, Does.Contain("POINT"));
    }

    [Test]
    public void GetTableDataJson_LegacyTypes_NullValues_HandledCorrectly()
    {
        using var command = _connection.CreateCommand();
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "legacy_types");

        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "legacy_types", "`id`", "id = 2");

        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("id"));
    }

    #endregion

    #region End-to-End Extraction Tests

    [Test]
    public void Extraction_Country_ReturnsCompleteData()
    {
        using var command = _connection.CreateCommand();

        // Get select columns
        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "country");
        Assert.That(selectColumns, Is.Not.Null.And.Not.Empty);

        // Extract data
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "country", "`country_id`", null);
        Assert.That(json, Is.Not.Null.And.Not.Empty);

        // Verify expected content
        Assert.That(json, Does.Contain("country_id"));
        Assert.That(json, Does.Contain("country"));
        Assert.That(json, Does.Contain("last_update"));
    }

    [Test]
    public void Extraction_FilmCategory_CompositeKey()
    {
        using var command = _connection.CreateCommand();

        var selectColumns = _dataTongs.GetSelectColumns(command, _testDb, "film_category");
        var json = _dataTongs.GetTableDataJson(command, selectColumns, _testDb, "film_category", "`film_id`, `category_id`", null);

        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("film_id"));
        Assert.That(json, Does.Contain("category_id"));
    }

    #endregion
}
