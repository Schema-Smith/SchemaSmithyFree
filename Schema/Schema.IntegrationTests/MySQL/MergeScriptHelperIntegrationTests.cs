// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using System;

using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for MergeScriptHelper against MySQL.
/// Uses dynamically created test databases via FixtureSetup.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class MergeScriptHelperIntegrationTests
{
    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [SetUp]
    public void SetUp()
    {
        _testDb = FixtureSetup.MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void BuildMergeScript_TableNameWithSingleQuote_GeneratesValidScript()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"zz't_{Guid.NewGuid():N}";

        try
        {
            command.CommandText = $@"
CREATE TABLE `{_testDb}`.`{tableName.Replace("`", "``")}` (
    id INT PRIMARY KEY,
    name VARCHAR(50) NOT NULL
)";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""name"":""Alpha""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, true, false, false, null!);

            Assert.That(script, Is.Not.Null);
            Assert.That(script, Does.Contain($"`{_testDb}`.`{tableName}`"));

            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            command.CommandText = $"SELECT name FROM `{_testDb}`.`{tableName.Replace("`", "``")}` WHERE id = 1";
            Assert.That(command.ExecuteScalar()?.ToString(), Is.EqualTo("Alpha"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName.Replace("`", "``")}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void GetKeyColumns_ReturnsCorrectPrimaryKey_SingleColumn()
    {
        // Arrange - actor table has single-column primary key (actor_id)
        using var command = _connection.CreateCommand();

        // Act
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform.MySQL, command, _testDb, "actor");

        // Assert
        Assert.That(keyColumns, Is.EqualTo("`actor_id`"));
    }

    [Test]
    public void GetKeyColumns_ReturnsCorrectPrimaryKey_CompositeKey()
    {
        // Arrange - film_actor table has composite primary key (actor_id, film_id)
        using var command = _connection.CreateCommand();

        // Act
        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform.MySQL, command, _testDb, "film_actor");

        // Assert
        Assert.That(keyColumns, Does.Contain("`actor_id`"));
        Assert.That(keyColumns, Does.Contain("`film_id`"));
    }

    [Test]
    public void GetKeyColumns_ReturnsEmpty_ForNonExistentTable()
    {
        using var command = _connection.CreateCommand();

        var keyColumns = MergeScriptHelper.GetKeyColumns(Platform.MySQL, command, _testDb, "non_existent_table");

        Assert.That(keyColumns, Is.EqualTo(""));
    }

    [Test]
    public void BuildMergeScript_ActorTable_GeneratesUpsertPlusDeleteSQL()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        var tableData = @"[{""actor_id"":1,""first_name"":""TEST"",""last_name"":""ACTOR"",""last_update"":""2024-01-01 00:00:00""}]";

        // Act
        var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, "actor",
            tableData, "`actor_id`", true, true, false, false, null!);

        // Assert — must not use REPLACE INTO (breaks ON DELETE RESTRICT FKs)
        Assert.That(script, Does.Not.Contain("REPLACE INTO"));
        Assert.That(script, Does.Contain($"INSERT INTO `{_testDb}`.`actor`"));
        Assert.That(script, Does.Contain("ON DUPLICATE KEY UPDATE"));
        Assert.That(script, Does.Contain($"DELETE Target FROM `{_testDb}`.`actor` Target"));
        Assert.That(script, Does.Contain("NOT EXISTS"));
        Assert.That(script, Does.Contain("JSON_TABLE("));
        Assert.That(script, Does.Contain("`first_name` VARCHAR(45) PATH '$.first_name'"));
        Assert.That(script, Does.Contain("`last_name` VARCHAR(45) PATH '$.last_name'"));
        // AUTO_INCREMENT columns are included so explicit ID values from data files are preserved.
        Assert.That(script, Does.Contain("`actor_id`"));
    }

    [Test]
    public void BuildMergeScript_IncludesAutoIncrementColumn()
    {
        // Arrange - country table has auto_increment country_id
        using var command = _connection.CreateCommand();
        var tableData = @"[{""country_id"":1,""country"":""Testland"",""last_update"":""2024-01-01 00:00:00""}]";

        // Act
        var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, "country",
            tableData, "`country_id`", false, false, false, false, null!);

        // Assert
        Assert.That(script, Does.Contain($"INSERT IGNORE INTO `{_testDb}`.`country`"));
        Assert.That(script, Does.Contain("`country` VARCHAR(50) PATH '$.country'"));
        // AUTO_INCREMENT columns are included to preserve original IDs
        Assert.That(script, Does.Contain("`country_id`"));
    }

    [Test]
    public void BuildMergeScript_UpsertType_GeneratesValidSQL()
    {
        // Arrange
        using var command = _connection.CreateCommand();
        var tableData = @"[{""country"":""Updated Country"",""last_update"":""2024-01-01 00:00:00""}]";

        // Act
        var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, "country",
            tableData, "", true, false, false, false, null!);

        // Assert
        Assert.That(script, Does.Contain($"INSERT INTO `{_testDb}`.`country`"));
        Assert.That(script, Does.Contain("ON DUPLICATE KEY UPDATE"));
        Assert.That(script, Does.Contain("`country` = VALUES(`country`)"));
    }

    [Test]
    public void BuildMergeScript_GeneratedScript_CanBeExecuted()
    {
        // Arrange - Create a test table with data (not temporary, as temp tables don't show in INFORMATION_SCHEMA)
        using var command = _connection.CreateCommand();
        var tableName = $"_test_merge_data_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            // Create test table
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    value DECIMAL(10,2),
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )";
            command.ExecuteNonQuery();

            // Insert initial data
            command.CommandText = $"INSERT INTO `{_testDb}`.`{tableName}` (name, value) VALUES ('initial', 10.50)";
            command.ExecuteNonQuery();

            var tableData = @"[{""name"":""test1"",""value"":20.00},{""name"":""test2"",""value"":30.00}]";

            // Generate and execute merge script
            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", false, false, false, false, null!);

            // Execute the generated script (batch execution)
            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify data was inserted
            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}`";
            var count = Convert.ToInt32(command.ExecuteScalar());

            // Assert - should have 3 rows (1 initial + 2 inserted)
            Assert.That(count, Is.EqualTo(3));
        }
        finally
        {
            // Cleanup
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void BuildMergeScript_GeometryColumn_UsesTextInJsonTableAndStGeomFromTextInSelect()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_geom_meta_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    name VARCHAR(100),
                    location POINT NOT NULL
                )";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""name"":""Test"",""location"":""POINT(0 0)""}]";

            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, false, false, false, null!);

            // JSON_TABLE should read geometry as TEXT (WKT string)
            Assert.That(script, Does.Contain("`location` TEXT PATH '$.location'"));

            // SELECT should convert WKT back to geometry
            Assert.That(script, Does.Contain("ST_GeomFromText(`location`)"));

            // INSERT INTO should have the plain column name
            Assert.That(script, Does.Contain($"INSERT INTO `{_testDb}`.`{tableName}` (`id`, `name`, `location`)"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void BuildMergeScript_GeometryColumn_CanBeExecuted()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_geom_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    name VARCHAR(100),
                    location POINT NOT NULL
                )";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""name"":""Place A"",""location"":""POINT(10.5 20.3)""},{""id"":2,""name"":""Place B"",""location"":""POINT(-73.9857 40.7484)""}]";

            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, true, false, false, null!);

            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify data was inserted
            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}`";
            var count = Convert.ToInt32(command.ExecuteScalar());
            Assert.That(count, Is.EqualTo(2));

            // Verify geometry can be read back as WKT
            command.CommandText = $"SELECT ST_AsText(location) FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            var wkt = command.ExecuteScalar()?.ToString();
            Assert.That(wkt, Does.Contain("POINT"));
            Assert.That(wkt, Does.Contain("10.5"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void BuildMergeScript_BlobColumn_UsesTextInJsonTableAndFromBase64InSelect()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_blob_meta_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    name VARCHAR(100),
                    picture BLOB
                )";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""name"":""Test"",""picture"":""SGVsbG8gV29ybGQ=""}]";

            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, false, false, false, null!);

            // JSON_TABLE should read blob as TEXT (Base64 string)
            Assert.That(script, Does.Contain("`picture` TEXT PATH '$.picture'"));

            // SELECT should convert Base64 back to binary
            Assert.That(script, Does.Contain("FROM_BASE64(`picture`)"));

            // INSERT INTO should have the plain column name
            Assert.That(script, Does.Contain($"INSERT INTO `{_testDb}`.`{tableName}` (`id`, `name`, `picture`)"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void BuildMergeScript_BlobColumn_CanBeExecuted()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_blob_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    name VARCHAR(100),
                    data BLOB
                )";
            command.ExecuteNonQuery();

            // "Hello World" = SGVsbG8gV29ybGQ=, "Test Data" = VGVzdCBEYXRh
            var tableData = @"[{""id"":1,""name"":""Item A"",""data"":""SGVsbG8gV29ybGQ=""},{""id"":2,""name"":""Item B"",""data"":""VGVzdCBEYXRh""}]";

            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, true, false, false, null!);

            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify data was inserted
            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}`";
            var count = Convert.ToInt32(command.ExecuteScalar());
            Assert.That(count, Is.EqualTo(2));

            // Verify binary data can be read back
            command.CommandText = $"SELECT CAST(data AS CHAR) FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            var value = command.ExecuteScalar()?.ToString();
            Assert.That(value, Is.EqualTo("Hello World"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void BuildMergeScript_DecimalType_GeneratesCorrectPrecision()
    {
        using var command = _connection.CreateCommand();
        var tableData = @"[{""amount"":100.50,""payment_date"":""2024-01-01 10:00:00""}]";

        // payment table has amount DECIMAL(5,2)
        var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, "payment",
            tableData, "`payment_id`", false, false, false, false, null!);

        Assert.That(script, Does.Contain("DECIMAL(5,2)"));
    }

    [Test]
    public void BuildMergeScript_DatetimeType_GeneratesCorrectFormat()
    {
        using var command = _connection.CreateCommand();
        var tableData = @"[{""first_name"":""TEST"",""last_name"":""ACTOR"",""last_update"":""2024-01-01 12:30:45""}]";

        var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, "actor",
            tableData, "", false, false, false, false, null!);

        Assert.That(script, Does.Contain("TIMESTAMP")); // last_update is TIMESTAMP
    }

    [Test]
    public void BuildMergeScript_JsonColumn_RoundTrip()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_json_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    name VARCHAR(100),
                    metadata JSON
                )";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""name"":""Test"",""metadata"":{""z_key"":""last"",""a_key"":""first"",""nested"":{""x"":10}}}]";

            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, false, false, false, null!);

            // JSON column upsert should use conditional comparison
            Assert.That(script, Does.Contain("CAST(VALUES(`metadata`) AS JSON)"));

            // Execute the generated script
            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify data was inserted
            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}`";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1));

            // Verify JSON data preserved
            command.CommandText = $"SELECT JSON_EXTRACT(metadata, '$.a_key') FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            Assert.That(command.ExecuteScalar()?.ToString(), Does.Contain("first"));

            command.CommandText = $"SELECT JSON_EXTRACT(metadata, '$.nested.x') FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            Assert.That(command.ExecuteScalar()?.ToString(), Is.EqualTo("10"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void BuildMergeScript_YearColumn_RoundTrip()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_year_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    release_year YEAR
                )";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""release_year"":2024},{""id"":2,""release_year"":1901},{""id"":3,""release_year"":0}]";

            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, true, false, false, null!);

            // Execute the generated script
            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            // Verify all rows inserted
            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}`";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(3));

            // Verify year values preserved
            command.CommandText = $"SELECT release_year FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(2024));

            command.CommandText = $"SELECT release_year FROM `{_testDb}`.`{tableName}` WHERE id = 2";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(1901));

            command.CommandText = $"SELECT release_year FROM `{_testDb}`.`{tableName}` WHERE id = 3";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(0));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void BuildMergeScript_FullSyncDelete_WithPortableMergeFilter_ScopesDeletionToFilterMatch()
    {
        // #333: a MergeFilter authored portably as `Target.<col>` must resolve on MySQL
        // (previously failed with "Unknown column 'Target.region'" — delete aliased the table `t`).
        // MergeFilter scopes which rows are even eligible for deletion (docs: schema-packages.md
        // MergeFilter) — rows outside the filter's scope are never removed, regardless of whether
        // they're present in the incoming source data.
        using var command = _connection.CreateCommand();
        var tableName = $"_test_merge_filter_{Guid.NewGuid():N}".Substring(0, 40);

        try
        {
            command.CommandText = $@"
                CREATE TABLE `{_testDb}`.`{tableName}` (
                    id INT PRIMARY KEY,
                    region VARCHAR(20) NOT NULL
                )";
            command.ExecuteNonQuery();

            command.CommandText = $@"
                INSERT INTO `{_testDb}`.`{tableName}` (id, region) VALUES
                    (1, 'KEEP'),
                    (2, 'KEEP'),
                    (3, 'OTHER')";
            command.ExecuteNonQuery();

            // Source data only carries row 1 (region 'KEEP'). Row 2 is in-scope (region 'KEEP')
            // but absent from source, so it must be deleted. Row 3 is out-of-scope (region
            // 'OTHER') so it must survive even though it's also absent from source.
            var tableData = @"[{""id"":1,""region"":""KEEP""}]";

            var script = MergeScriptHelper.BuildMergeScript(Platform.MySQL, command, _testDb, tableName,
                tableData, "`id`", true, true, false, false, "Target.region = 'KEEP'");

            foreach (var batch in script.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}`";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(2));

            command.CommandText = $"SELECT region FROM `{_testDb}`.`{tableName}` WHERE id = 1";
            Assert.That(command.ExecuteScalar()?.ToString(), Is.EqualTo("KEEP"));

            command.CommandText = $"SELECT COUNT(*) FROM `{_testDb}`.`{tableName}` WHERE id = 2";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(0));

            command.CommandText = $"SELECT region FROM `{_testDb}`.`{tableName}` WHERE id = 3";
            Assert.That(command.ExecuteScalar()?.ToString(), Is.EqualTo("OTHER"));
        }
        finally
        {
            command.CommandText = $"DROP TABLE IF EXISTS `{_testDb}`.`{tableName}`";
            command.ExecuteNonQuery();
        }
    }
}
