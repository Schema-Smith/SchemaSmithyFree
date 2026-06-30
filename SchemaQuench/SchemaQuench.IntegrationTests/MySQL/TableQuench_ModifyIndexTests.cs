// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;
using System;

using NUnit.Framework;
using Schema.IntegrationTests.MySQL;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for modifying indexes during table quench.
/// Tests changes to column lists, uniqueness, and index type.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
[Category("Integration")]
public class TableQuench_ModifyIndexTests : BaseTableQuenchTests
{
    private const string TestSchema = "ModifyIndexTests";

    [Test]
    public void ShouldModifyIndexWhenSortColumnListChanges()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Index should now be on Column2 instead of Column1
        cmd.CommandText = $@"
            SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',')
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyIndexColumnList'
              AND INDEX_NAME = 'IDX_ColumnList'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Column2"));

        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexWhenSortColumnListChangesIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',')
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyIndexColumnListIO'
              AND INDEX_NAME = 'IDX_ColumnListIO'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Column2"));

        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexWhenSortColumnListOrderChanges()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Index columns should now be Column2,Column1 instead of Column1,Column2
        cmd.CommandText = $@"
            SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',')
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyIndexColumnListOrder'
              AND INDEX_NAME = 'IDX_ColumnList2'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Column2,Column1"));

        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexWhenItBecomesUnique()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT NON_UNIQUE FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyIndexBecomesUnique'
              AND INDEX_NAME = 'IDX_BecomesUnique'
              AND SEQ_IN_INDEX = 1";
        var nonUnique = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.That(nonUnique, Is.EqualTo(0), "Index should be unique (NON_UNIQUE=0)");

        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexWhenItBecomesNonUnique()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT NON_UNIQUE FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyIndexBecomesNonUnique'
              AND INDEX_NAME = 'IDX_BecomesNonUnique'
              AND SEQ_IN_INDEX = 1";
        var nonUnique = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.That(nonUnique, Is.EqualTo(1), "Index should be non-unique (NON_UNIQUE=1)");

        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexWhenDescendingChanges()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Check if index is now descending (MySQL 8.0+)
        cmd.CommandText = $@"
            SELECT COLLATION FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyIndexDescending'
              AND INDEX_NAME = 'IDX_Descending'
              AND SEQ_IN_INDEX = 1";
        var collation = cmd.ExecuteScalar()?.ToString();
        Assert.That(collation, Is.EqualTo("D"), "Index should be descending (COLLATION=D)");

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
        cmd.CommandText = $@"
-- ShouldModifyIndexWhenSortColumnListChanges
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyIndexColumnList` (`Column1` INT NOT NULL, `Column2` INT NOT NULL);
CREATE INDEX `IDX_ColumnList` ON `{TestSchema}`.`ModifyIndexColumnList` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyIndexColumnList');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyIndexColumnList.IDX_ColumnList');
-- ShouldModifyIndexWhenSortColumnListOrderChanges
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyIndexColumnListOrder` (`Column1` INT NOT NULL, `Column2` INT NOT NULL);
CREATE INDEX `IDX_ColumnList2` ON `{TestSchema}`.`ModifyIndexColumnListOrder` (`Column1`, `Column2`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyIndexColumnListOrder');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyIndexColumnListOrder.IDX_ColumnList2');
-- ShouldModifyIndexWhenItBecomesUnique
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyIndexBecomesUnique` (`Column1` INT NOT NULL);
CREATE INDEX `IDX_BecomesUnique` ON `{TestSchema}`.`ModifyIndexBecomesUnique` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyIndexBecomesUnique');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyIndexBecomesUnique.IDX_BecomesUnique');
-- ShouldModifyIndexWhenItBecomesNonUnique
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyIndexBecomesNonUnique` (`Column1` INT NOT NULL);
CREATE UNIQUE INDEX `IDX_BecomesNonUnique` ON `{TestSchema}`.`ModifyIndexBecomesNonUnique` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyIndexBecomesNonUnique');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyIndexBecomesNonUnique.IDX_BecomesNonUnique');
-- ShouldModifyIndexWhenDescendingChanges
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyIndexDescending` (`Column1` INT NOT NULL);
CREATE INDEX `IDX_Descending` ON `{TestSchema}`.`ModifyIndexDescending` (`Column1` ASC);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyIndexDescending');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyIndexDescending.IDX_Descending');

-- Index Only Tests
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyIndexColumnListIO` (`Column1` INT NOT NULL, `Column2` INT NOT NULL);
CREATE INDEX `IDX_ColumnListIO` ON `{TestSchema}`.`ModifyIndexColumnListIO` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyIndexColumnListIO');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyIndexColumnListIO.IDX_ColumnListIO');
";
        cmd.ExecuteNonQuery();

        // Run quench with modified index definitions
        var json = """
        [
            {
                "Name": "ModifyIndexColumnList",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_ColumnList", "IndexColumns": "Column2" }
                ]
            },
            {
                "Name": "ModifyIndexColumnListOrder",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_ColumnList2", "IndexColumns": "Column2,Column1" }
                ]
            },
            {
                "Name": "ModifyIndexBecomesUnique",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_BecomesUnique", "IndexColumns": "Column1", "Unique": true }
                ]
            },
            {
                "Name": "ModifyIndexBecomesNonUnique",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_BecomesNonUnique", "IndexColumns": "Column1", "Unique": false },
                    { "Name": "IDX_NewUnique", "IndexColumns": "Column1", "Unique": true }
                ]
            },
            {
                "Name": "ModifyIndexDescending",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_Descending", "IndexColumns": "Column1 DESC" }
                ]
            }
        ]
        """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingTableAndColumnQuench('{TestSchema}', 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0, 1, 1)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0)";
        cmd.ExecuteNonQuery();

        // Index Only test
        var jsonIO = """
        [
            {
                "Name": "ModifyIndexColumnListIO",
                "Indexes": [
                    { "Name": "IDX_ColumnListIO", "IndexColumns": "Column2" }
                ]
            }
        ]
        """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{jsonIO.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_IndexOnlyQuench('{_productName}', '{TestSchema}', 0, 0)";
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
