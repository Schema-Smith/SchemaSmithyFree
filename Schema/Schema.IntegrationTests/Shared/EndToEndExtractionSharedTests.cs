// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Newtonsoft.Json;
using NUnit.Framework;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.IntegrationTests.Shared;

/// <summary>
/// Shared end-to-end schema-extraction integration tests for the MySQL/MariaDb family. The MySQL and
/// MariaDb subclasses supply the platform + fixture accessors; every [Test] body here runs on both engines.
/// Uses dynamically created test databases via FixtureSetup.
/// </summary>
public abstract class EndToEndExtractionSharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string MainDb { get; }
    protected abstract string MainConnectionString { get; }

    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDb = MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(MainConnectionString);
        _connection.Open();

        // ForgeKindler is already deployed by FixtureSetup
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    #region Table with Many Columns Tests

    [Test]
    public void ExtractTable_FilmWithManyColumns_ExtractsAllColumns()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'film')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.Columns.Count, Is.GreaterThanOrEqualTo(10), "Film table should have many columns");

        // Verify specific columns exist
        Assert.That(table.Columns.Exists(c => c.Name.Contains("film_id")), Is.True);
        Assert.That(table.Columns.Exists(c => c.Name.Contains("title")), Is.True);
        Assert.That(table.Columns.Exists(c => c.Name.Contains("description")), Is.True);
        Assert.That(table.Columns.Exists(c => c.Name.Contains("release_year")), Is.True);
        Assert.That(table.Columns.Exists(c => c.Name.Contains("rental_duration")), Is.True);
    }

    #endregion

    #region Table with Many Indexes Tests

    [Test]
    public void ExtractTable_PaymentWithMultipleIndexes_ExtractsAllIndexes()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'payment')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.Indexes.Count, Is.GreaterThanOrEqualTo(2), "Payment table should have multiple indexes");

        // Should have PRIMARY key
        Assert.That(table.Indexes.Exists(i => i.PrimaryKey), Is.True);
    }

    #endregion

    #region Table with Foreign Keys Tests

    [Test]
    public void ExtractTable_RentalWithForeignKeys_ExtractsAllForeignKeys()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'rental')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.ForeignKeys.Count, Is.GreaterThanOrEqualTo(3), "Rental table should have multiple foreign keys");

        // Check FK to customer
        var customerFk = table.ForeignKeys.Find(fk => fk.RelatedTable.Contains("customer"));
        Assert.That(customerFk, Is.Not.Null);
        Assert.That(customerFk.Columns, Does.Contain("customer_id"));

        // Check FK to inventory
        var inventoryFk = table.ForeignKeys.Find(fk => fk.RelatedTable.Contains("inventory"));
        Assert.That(inventoryFk, Is.Not.Null);

        // Check FK to staff
        var staffFk = table.ForeignKeys.Find(fk => fk.RelatedTable.Contains("staff"));
        Assert.That(staffFk, Is.Not.Null);
    }

    #endregion

    #region Table Metadata Tests

    [Test]
    public void ExtractTable_VerifyEngineAndCollation()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'actor')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.Engine, Is.EqualTo("InnoDB"));
        Assert.That(table.CharacterSet, Is.Not.Null.And.Not.Empty);
        Assert.That(table.Collation, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ExtractTable_AutoIncrementColumn_HasCorrectProperties()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'actor')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
        var actorId = (MySqlColumn)table.Columns.Find(c => c.Name.Contains("actor_id"));

        Assert.That(actorId, Is.Not.Null);
        Assert.That(actorId.AutoIncrement, Is.True);
        Assert.That(actorId.Nullable, Is.False);
    }

    #endregion

    #region FullText Index Tests

    [Test]
    public void ExtractTable_FilmTextWithFullTextIndex_ExtractsFullTextIndex()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'film_text')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
        Assert.That(table, Is.Not.Null);
        Assert.That(table.FullTextIndexes, Is.Not.Null);
        Assert.That(table.FullTextIndexes.Count, Is.GreaterThanOrEqualTo(1));

        var ftIndex = table.FullTextIndexes[0];
        Assert.That(ftIndex.Columns, Does.Contain("title").Or.Contain("description"));
    }

    #endregion

    #region All Tables Extraction Tests

    [Test]
    public void ExtractAllTables_TestDatabase_AllSucceed()
    {
        using var command = _connection.CreateCommand();

        // Get all tables
        command.CommandText = $@"
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{_testDb}'
            AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME";

        var tables = new System.Collections.Generic.List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        Assert.That(tables.Count, Is.GreaterThan(10), "Test database should have many tables");

        var successCount = 0;
        var failedTables = new System.Collections.Generic.List<string>();

        foreach (var tableName in tables)
        {
            try
            {
                command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', '{tableName}')";

                string json = "";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        json += reader[0];
                }

                var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
                if (table != null && table.Columns != null && table.Columns.Count > 0)
                {
                    successCount++;
                }
                else
                {
                    failedTables.Add(tableName);
                }
            }
            catch (Exception ex)
            {
                failedTables.Add($"{tableName}: {ex.Message}");
            }
        }

        Assert.That(successCount, Is.EqualTo(tables.Count),
            $"All tables should extract successfully. Failed: {string.Join(", ", failedTables)}");
    }

    #endregion

    #region Column Data Type Tests

    [Test]
    public void ExtractTable_VerifyVariousDataTypes()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'film')";

        string json = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }

        var table = PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;

        // Check various data types are extracted correctly
        var filmId = table.Columns.Find(c => c.Name.Contains("film_id"));
        Assert.That(filmId.DataType.ToLower(), Does.Contain("int"));

        var title = table.Columns.Find(c => c.Name.Contains("title"));
        Assert.That(title.DataType.ToLower(), Does.Contain("varchar"));

        var description = table.Columns.Find(c => c.Name.Contains("description"));
        Assert.That(description.DataType.ToLower(), Does.Contain("text"));

        var rentalRate = table.Columns.Find(c => c.Name.Contains("rental_rate"));
        Assert.That(rentalRate.DataType.ToLower(), Does.Contain("decimal"));
    }

    #endregion

    #region JSON Roundtrip Tests

    [Test]
    public void ExtractTable_JsonRoundtrip_PreservesAllData()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', 'actor')";

        string originalJson = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                originalJson += reader[0];
        }

        // Deserialize and re-serialize
        var table = JsonConvert.DeserializeObject<MySqlTable>(originalJson);
        var reserializedJson = JsonConvert.SerializeObject(table, Formatting.Indented);

        // Deserialize again
        var table2 = JsonConvert.DeserializeObject<MySqlTable>(reserializedJson);

        // Verify key properties are preserved
        Assert.That(table2.Name, Is.EqualTo(table.Name));
        Assert.That(table2.Engine, Is.EqualTo(table.Engine));
        Assert.That(table2.Columns.Count, Is.EqualTo(table.Columns.Count));
        Assert.That(table2.Indexes.Count, Is.EqualTo(table.Indexes.Count));

        for (int i = 0; i < table.Columns.Count; i++)
        {
            Assert.That(table2.Columns[i].Name, Is.EqualTo(table.Columns[i].Name));
            Assert.That(table2.Columns[i].DataType, Is.EqualTo(table.Columns[i].DataType));
            Assert.That(table2.Columns[i].Nullable, Is.EqualTo(table.Columns[i].Nullable));
        }
    }

    #endregion
}
