// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using System;

using Newtonsoft.Json;
using NUnit.Framework;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for GenerateTableJSON stored procedure.
/// Uses dynamically created test databases via FixtureSetup.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class GenerateTableJsonIntegrationTests
{
    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDb = FixtureSetup.MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();

        // ForgeKindler is already deployed by FixtureSetup
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void GenerateTableJSON_ActorTable_ReturnsValidJson()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'actor')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        Assert.That(json, Is.Not.Empty);
        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.Name, Does.Contain("actor"));
    }

    [Test]
    public void GenerateTableJSON_ActorTable_HasExpectedColumns()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'actor')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        Assert.That(table.Columns, Has.Count.EqualTo(4));

        // Verify actor_id column
        var actorId = (MySqlColumn)table.Columns.Find(c => c.Name.Contains("actor_id"));
        Assert.That(actorId, Is.Not.Null);
        Assert.That(actorId.AutoIncrement, Is.True);
        Assert.That(actorId.Nullable, Is.False);

        // Verify first_name column
        var firstName = table.Columns.Find(c => c.Name.Contains("first_name"));
        Assert.That(firstName, Is.Not.Null);
        Assert.That(firstName.DataType, Does.Contain("varchar"));
        Assert.That(firstName.Nullable, Is.False);

        // Verify last_update column
        var lastUpdate = table.Columns.Find(c => c.Name.Contains("last_update"));
        Assert.That(lastUpdate, Is.Not.Null);
        Assert.That(lastUpdate.DataType, Does.Contain("timestamp"));
    }

    [Test]
    public void GenerateTableJSON_ActorTable_HasPrimaryKey()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'actor')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        var primaryKey = table.Indexes.Find(i => i.PrimaryKey);
        Assert.That(primaryKey, Is.Not.Null);
        Assert.That(primaryKey.IndexColumns, Does.Contain("actor_id"));
    }

    [Test]
    public void GenerateTableJSON_FilmTable_HasForeignKeys()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'film')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        Assert.That(table.ForeignKeys, Is.Not.Null);
        Assert.That(table.ForeignKeys.Count, Is.GreaterThanOrEqualTo(1));

        // Film should have FK to language table
        var languageFk = table.ForeignKeys.Find(fk => fk.RelatedTable.Contains("language"));
        Assert.That(languageFk, Is.Not.Null);
    }

    [Test]
    public void GenerateTableJSON_FilmTable_HasMultipleIndexes()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'film')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        Assert.That(table.Indexes, Is.Not.Null);
        Assert.That(table.Indexes.Count, Is.GreaterThanOrEqualTo(2)); // PK + at least one FK index
    }

    [Test]
    public void GenerateTableJSON_FilmTextTable_HasFullTextIndex()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'film_text')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        Assert.That(table.FullTextIndexes, Is.Not.Null);
        Assert.That(table.FullTextIndexes.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void GenerateTableJSON_PaymentTable_ReturnsTableMetadata()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'payment')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.Engine, Is.EqualTo("InnoDB"));
        Assert.That(table.CharacterSet, Is.Not.Null.And.Not.Empty);
        Assert.That(table.Collation, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GenerateTableJSON_FilmTable_EnumDefaultIsQuoted()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'film')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        var rating = table.Columns.Find(c => c.Name.Contains("rating"));
        Assert.That(rating, Is.Not.Null, "Film table should have a rating column");
        Assert.That(rating.DataType, Does.Contain("enum").IgnoreCase);
        Assert.That(rating.Default, Is.EqualTo("'G'"), "ENUM default should be single-quoted");
    }

    [Test]
    public void GenerateTableJSON_ActorTable_TimestampDefaultIsNotQuoted()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'actor')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
        var lastUpdate = table.Columns.Find(c => c.Name.Contains("last_update"));
        Assert.That(lastUpdate, Is.Not.Null);
        Assert.That(lastUpdate.Default, Does.Contain("CURRENT_TIMESTAMP").IgnoreCase,
            "CURRENT_TIMESTAMP defaults should not be quoted");
        Assert.That(lastUpdate.Default, Does.Not.StartWith("'"),
            "Function defaults should not be wrapped in quotes");
    }

    [Test]
    public void GenerateTableJSON_AllTestTables_ProduceValidJson()
    {
        using var command = _connection.CreateCommand();

        // Get all tables in test database
        command.CommandText = $@"
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{_testDb}'
            AND TABLE_TYPE = 'BASE TABLE'";

        var tables = new System.Collections.Generic.List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        Assert.That(tables.Count, Is.GreaterThan(0), "Test database should have tables");

        foreach (var tableName in tables)
        {
            command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', '{tableName}')";

            string json = "";
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    json += reader[0];
            }

            Assert.That(json, Is.Not.Empty, $"Table {tableName} should return JSON");

            var table = PlatformDeserializer.DeserializeTable(json, Platform.MySQL) as MySqlTable;
            Assert.That(table, Is.Not.Null, $"Table {tableName} JSON should deserialize");
            Assert.That(table.Name, Does.Contain(tableName), $"Table name should match for {tableName}");
            Assert.That(table.Columns, Is.Not.Null.And.Count.GreaterThan(0), $"Table {tableName} should have columns");
        }
    }
}
