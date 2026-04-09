// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;
using System;
using MySqlConnector;

using Schema.IntegrationTests.MySQL;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for miscellaneous table quench scenarios.
/// Tests renaming, dropping, ownership checks, and edge cases.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
[Category("Integration")]
public class TableQuench_MiscellaneousTests : BaseTableQuenchTests
{
    private const string TestSchema = "MiscellaneousTests";

    [Test]
    public void TableQuench_ShouldRenameIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // New name should exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'DebugRename'
              AND INDEX_NAME = 'IDX_RightName'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0), "IDX_RightName should exist after rename");

        // Old name should not exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'DebugRename'
              AND INDEX_NAME = 'IDX_WrongName'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "IDX_WrongName should not exist after rename");

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldRenameIndexIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'DebugRenameIO'
              AND INDEX_NAME = 'IDX_RightNameIO'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'DebugRenameIO'
              AND INDEX_NAME = 'IDX_WrongNameIO'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldRenameUniqueConstraint()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'RenameMyUniqueConstraint'
              AND INDEX_NAME = 'UQ_NewName'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'RenameMyUniqueConstraint'
              AND INDEX_NAME = 'UQ_OldName'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldHandleRemovingForeignKey()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // FK_DropFK_SelfRef should be removed
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'DropFK'
              AND CONSTRAINT_NAME = 'FK_DropFK_SelfRef'
              AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));

        // FK_DropFK_SelfRef2 should still exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'DropFK'
              AND CONSTRAINT_NAME = 'FK_DropFK_SelfRef2'
              AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        conn.Close();
    }

    [Test]
    public void ShouldErrorWhenUpdatingWrongProductTable()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        var json = """
            [{
                "Name": "TableOwnedByOtherProduct",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false }
                ]
            }]
            """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        var ex = Assert.Throws<MySqlException>(() =>
        {
            cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0)";
            cmd.ExecuteNonQuery();
        });
        Assert.That(ex!.Message, Does.Contain("already owned by another product").IgnoreCase);

        conn.Close();
    }

    [Test]
    public void ShouldDropIndexNoLongerPartOfProduct()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // IDX_DropMe should be dropped
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'IndexNoLongerInProduct'
              AND INDEX_NAME = 'IDX_DropMe'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));

        // IDX_Custom should still exist (not owned by product)
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'IndexNoLongerInProduct'
              AND INDEX_NAME = 'IDX_Custom'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        conn.Close();
    }

    [Test]
    public void ShouldDropIndexNoLongerPartOfProductIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'IndexNoLongerInProductIO'
              AND INDEX_NAME = 'IDX_DropMeIO'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));

        conn.Close();
    }

    [Test]
    public void ShouldDropTableNoLongerPartOfProduct()
    {
        var uniqueProductName = Guid.NewGuid().ToString();
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Create a table and register it with the unique product
        cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`TableToBeDropped` (`Column1` INT NOT NULL);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{uniqueProductName}', '', '{TestSchema}', 'TABLE', 'TableToBeDropped');
";
        cmd.ExecuteNonQuery();

        // JSON without the table (it should be dropped)
        var json = """
            [{
                "Name": "TableInProduct",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false }
                ]
            }]
            """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingTableAndColumnQuench('{TestSchema}', 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{uniqueProductName}', '{TestSchema}', 0, 1)"; // DropTables=1
        cmd.ExecuteNonQuery();

        // Table should be dropped
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'TableToBeDropped'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));

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
-- TableQuench_ShouldRenameIndex
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`DebugRename` (`Id` INT NOT NULL);
CREATE INDEX `IDX_WrongName` ON `{TestSchema}`.`DebugRename` (`Id`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'DebugRename');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'DebugRename.IDX_WrongName');
-- TableQuench_ShouldRenameUniqueConstraint
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`RenameMyUniqueConstraint` (`Id` INT NOT NULL);
CREATE UNIQUE INDEX `UQ_OldName` ON `{TestSchema}`.`RenameMyUniqueConstraint` (`Id`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'RenameMyUniqueConstraint');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'RenameMyUniqueConstraint.UQ_OldName');
-- TableQuench_ShouldHandleRemovingForeignKey
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`DropFK` (`Column1` INT NOT NULL PRIMARY KEY, `Column2` INT NOT NULL);
CREATE UNIQUE INDEX `UQ_Column2` ON `{TestSchema}`.`DropFK` (`Column2`);
ALTER TABLE `{TestSchema}`.`DropFK` ADD CONSTRAINT `FK_DropFK_SelfRef` FOREIGN KEY (`Column2`) REFERENCES `{TestSchema}`.`DropFK` (`Column1`);
ALTER TABLE `{TestSchema}`.`DropFK` ADD CONSTRAINT `FK_DropFK_SelfRef2` FOREIGN KEY (`Column2`) REFERENCES `{TestSchema}`.`DropFK` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'DropFK');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'FOREIGN KEY', 'DropFK.FK_DropFK_SelfRef');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'FOREIGN KEY', 'DropFK.FK_DropFK_SelfRef2');
-- ShouldDropIndexNoLongerPartOfProduct
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`IndexNoLongerInProduct` (`Column1` INT NOT NULL);
CREATE INDEX `IDX_DropMe` ON `{TestSchema}`.`IndexNoLongerInProduct` (`Column1`);
CREATE INDEX `IDX_Custom` ON `{TestSchema}`.`IndexNoLongerInProduct` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'IndexNoLongerInProduct');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'IndexNoLongerInProduct.IDX_DropMe');

-- Index Only
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`DebugRenameIO` (`Id` INT NOT NULL);
CREATE INDEX `IDX_WrongNameIO` ON `{TestSchema}`.`DebugRenameIO` (`Id`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'DebugRenameIO');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'DebugRenameIO.IDX_WrongNameIO');
-- ShouldDropIndexNoLongerPartOfProductIndexOnly
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`IndexNoLongerInProductIO` (`Column1` INT NOT NULL);
CREATE INDEX `IDX_DropMeIO` ON `{TestSchema}`.`IndexNoLongerInProductIO` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'IndexNoLongerInProductIO');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'IndexNoLongerInProductIO.IDX_DropMeIO');

-- Exception Cases
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`TableOwnedByOtherProduct` (`Column1` INT NOT NULL);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('OtherProduct', '', '{TestSchema}', 'TABLE', 'TableOwnedByOtherProduct');
";
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Name": "DebugRename",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_RightName", "IndexColumns": "Id" }
                ]
            },
            {
                "Name": "RenameMyUniqueConstraint",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "UQ_NewName", "IndexColumns": "Id", "Unique": true }
                ]
            },
            {
                "Name": "DropFK",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "Column1" }
                ],
                "ForeignKeys": [
                    {
                        "Name": "FK_DropFK_SelfRef2",
                        "Columns": "Column2",
                        "RelatedTable": "DropFK",
                        "RelatedColumns": "Column1"
                    }
                ]
            },
            {
                "Name": "IndexNoLongerInProduct",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false }
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

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 1)"; // DropUnknown=1
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ForeignKeyQuench('{_productName}', '{TestSchema}', 0, 1)"; // DropUnknown=1
        cmd.ExecuteNonQuery();

        // Index Only
        var jsonIO = """
            [
            {
                "Name": "DebugRenameIO",
                "Indexes": [
                    { "Name": "IDX_RightNameIO", "IndexColumns": "Id" }
                ]
            },
            {
                "Name": "IndexNoLongerInProductIO"
            }
            ]
            """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{jsonIO.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_IndexOnlyQuench('{_productName}', '{TestSchema}', 0, 1)"; // DropUnknown=1
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

    [Test]
    public void ShouldCreateGeneratedColumnsInDependencyOrder()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"gen_dep_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Create table with only the base column
        cmd.CommandText = $@"
CREATE TABLE `{_mainDb}`.`{tableName}` (id INT NOT NULL, price DECIMAL(10,2) NOT NULL, PRIMARY KEY (id));
INSERT INTO `{_mainDb}`.`{tableName}` (id, price) VALUES (1, 100.00);
";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Quench with chained generated columns: price → tax → total
        // tax depends on price (level 1), total depends on tax (level 2)
        var json = $$"""
        [{
            "Name": "{{tableName}}",
            "Columns": [
                {"Name": "id", "DataType": "INT", "Nullable": false},
                {"Name": "price", "DataType": "DECIMAL(10,2)", "Nullable": false},
                {"Name": "tax", "DataType": "DECIMAL(10,2)", "GenerationExpression": "price * 0.10", "Generated": "STORED"},
                {"Name": "total", "DataType": "DECIMAL(10,2)", "GenerationExpression": "price + tax", "Generated": "STORED"}
            ],
            "Indexes": [
                {"Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "id"}
            ]
        }]
        """;

        cmd.CommandText = $"CALL SchemaSmith_TableQuench('{Guid.NewGuid()}', '{_mainDb}', '{json.Replace("'", "''")}', 0, 0, 0)";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Verify both generated columns exist
        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'tax' AND GENERATION_EXPRESSION != ''";
        Assert.That(Convert.ToInt64(cmd.ExecuteScalar()), Is.EqualTo(1), "Generated column 'tax' should exist");

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'total' AND GENERATION_EXPRESSION != ''";
        Assert.That(Convert.ToInt64(cmd.ExecuteScalar()), Is.EqualTo(1), "Generated column 'total' should exist");

        // Verify computed values are correct
        cmd.CommandText = $"SELECT tax, total FROM `{_mainDb}`.`{tableName}` WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetDecimal(0), Is.EqualTo(10.00m), "tax should be 10% of price");
        Assert.That(reader.GetDecimal(1), Is.EqualTo(110.00m), "total should be price + tax");
        reader.Close();

        conn.Close();
    }

    [Test]
    public void ShouldNotCreateTableInWhatIfMode()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var productName = $"WhatIfProduct_{uniqueId}";
        var tableName = $"WhatIfTable_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        var json = $$"""
        [{
            "Name": "{{tableName}}",
            "Columns": [
                {"Name": "Id", "DataType": "INT", "Nullable": false},
                {"Name": "Name", "DataType": "VARCHAR(100)", "Nullable": true}
            ],
            "Indexes": [
                {"Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id"}
            ]
        }]
        """;

        // Call SchemaSmith_TableQuench with p_WhatIf = 1
        cmd.CommandText = $"CALL SchemaSmith_TableQuench('{productName}', '{_mainDb}', '{json.Replace("'", "''")}', 1, 0, 0)";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Verify the table was NOT created
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{_mainDb}'
              AND TABLE_NAME = '{tableName}'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "Table should not be created in WhatIf mode");

        conn.Close();
    }
}
