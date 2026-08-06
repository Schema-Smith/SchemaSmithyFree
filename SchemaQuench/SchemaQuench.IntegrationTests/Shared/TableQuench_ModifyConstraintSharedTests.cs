// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;
using System;

using NUnit.Framework;

namespace SchemaQuench.IntegrationTests.Shared;

/// <summary>
/// Integration tests for modifying constraints during table quench.
/// Tests default value changes, check constraint changes, and foreign key modifications.
/// </summary>
[Category("Integration")]
public abstract class TableQuench_ModifyConstraintSharedTests : BaseTableQuenchTests
{
    private const string TestSchema = "ModifyConstraintTests";

    [Test]
    public void TableQuench_ShouldModifyDefault()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyMyDefault'
              AND COLUMN_NAME = 'Id'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("0"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldModifyTableLevelCheckConstraint()
    {
        if (!TargetSupportsCheckConstraints())
            Assert.Ignore("CHECK constraints require MySQL 8.0.16; skipped below the floor.");
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Check constraint should exist with new expression
        cmd.CommandText = $@"
            SELECT CHECK_CLAUSE FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = '{TestSchema}'
              AND CONSTRAINT_NAME = 'CHK_ModifyMyTableCheck_MyCheck'";
        var checkClause = cmd.ExecuteScalar()?.ToString();
        Assert.That(checkClause, Does.Contain("100"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldModifyForeignKeyForColumnChange()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // FK should reference Col2 now instead of Col3
        cmd.CommandText = $@"
            SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyFKColumn'
              AND CONSTRAINT_NAME = 'FK_ModifyFKColumn_ModifyFKColumnRef'
              AND REFERENCED_TABLE_NAME IS NOT NULL";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Col2"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldModifyForeignKeyForReferenceTableChange()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // FK should now reference ModifyFKRefTblRefNew instead of ModifyFKRefTblRef
        cmd.CommandText = $@"
            SELECT REFERENCED_TABLE_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyFKRefTbl'
              AND CONSTRAINT_NAME = 'FK_ModifyFKRefTbl_Ref'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("ModifyFKRefTblRefNew"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldModifyForeignKeyForCascadeDeleteChange()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // FK should no longer have CASCADE delete (changed to NO ACTION)
        cmd.CommandText = $@"
            SELECT DELETE_RULE FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = '{TestSchema}'
              AND CONSTRAINT_NAME = 'FK_ModFKCascDel_Ref'";
        var deleteRule = cmd.ExecuteScalar()?.ToString();
        Assert.That(deleteRule, Is.EqualTo("NO ACTION").Or.EqualTo("RESTRICT"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldModifyForeignKeyForCascadeUpdateChange()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // FK should no longer have CASCADE update (changed to NO ACTION)
        cmd.CommandText = $@"
            SELECT UPDATE_RULE FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = '{TestSchema}'
              AND CONSTRAINT_NAME = 'FK_ModFKCascUpd_Ref'";
        var updateRule = cmd.ExecuteScalar()?.ToString();
        Assert.That(updateRule, Is.EqualTo("NO ACTION").Or.EqualTo("RESTRICT"));

        conn.Close();
    }

    [OneTimeSetUp]
    public void SetUp()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Drop and recreate to ensure clean state
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CREATE DATABASE `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandTimeout = 300;
        cmd.CommandText = $@"
-- TableQuench_ShouldModifyDefault
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyMyDefault` (`Id` INT NOT NULL DEFAULT 10);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyMyDefault');
-- TableQuench_ShouldModifyTableLevelCheckConstraint
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyMyTableCheck` (`Id` INT NOT NULL, `Col2` INT);
ALTER TABLE `{TestSchema}`.`ModifyMyTableCheck` ADD CONSTRAINT `CHK_ModifyMyTableCheck_MyCheck` CHECK (`Id` > 0);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyMyTableCheck');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'CHECK CONSTRAINT', 'ModifyMyTableCheck.CHK_ModifyMyTableCheck_MyCheck');
-- TableQuench_ShouldModifyForeignKeyForColumnChange
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyFKColumn` (`Id` INT NOT NULL PRIMARY KEY, `Col2` INT, `Col3` INT);
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyFKColumnRef` (`Id` INT NOT NULL PRIMARY KEY);
ALTER TABLE `{TestSchema}`.`ModifyFKColumn` ADD CONSTRAINT `FK_ModifyFKColumn_ModifyFKColumnRef` FOREIGN KEY (`Col3`) REFERENCES `{TestSchema}`.`ModifyFKColumnRef` (`Id`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyFKColumn');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyFKColumnRef');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'FOREIGN KEY', 'ModifyFKColumn.FK_ModifyFKColumn_ModifyFKColumnRef');
-- TableQuench_ShouldModifyForeignKeyForReferenceTableChange
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyFKRefTbl` (`Id` INT NOT NULL PRIMARY KEY, `Col2` INT, `Col3` INT);
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyFKRefTblRef` (`Id` INT NOT NULL PRIMARY KEY);
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyFKRefTblRefNew` (`Id` INT NOT NULL PRIMARY KEY);
ALTER TABLE `{TestSchema}`.`ModifyFKRefTbl` ADD CONSTRAINT `FK_ModifyFKRefTbl_Ref` FOREIGN KEY (`Col3`) REFERENCES `{TestSchema}`.`ModifyFKRefTblRef` (`Id`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyFKRefTbl');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyFKRefTblRef');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyFKRefTblRefNew');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'FOREIGN KEY', 'ModifyFKRefTbl.FK_ModifyFKRefTbl_Ref');
-- TableQuench_ShouldModifyForeignKeyForCascadeDeleteChange
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModFKCascDel` (`Id` INT NOT NULL PRIMARY KEY, `Col2` INT, `Col3` INT);
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModFKCascDelRef` (`Id` INT NOT NULL PRIMARY KEY);
ALTER TABLE `{TestSchema}`.`ModFKCascDel` ADD CONSTRAINT `FK_ModFKCascDel_Ref` FOREIGN KEY (`Col3`) REFERENCES `{TestSchema}`.`ModFKCascDelRef` (`Id`) ON DELETE CASCADE;
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModFKCascDel');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModFKCascDelRef');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'FOREIGN KEY', 'ModFKCascDel.FK_ModFKCascDel_Ref');
-- TableQuench_ShouldModifyForeignKeyForCascadeUpdateChange
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModFKCascUpd` (`Id` INT NOT NULL PRIMARY KEY, `Col2` INT, `Col3` INT);
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModFKCascUpdRef` (`Id` INT NOT NULL PRIMARY KEY);
ALTER TABLE `{TestSchema}`.`ModFKCascUpd` ADD CONSTRAINT `FK_ModFKCascUpd_Ref` FOREIGN KEY (`Col3`) REFERENCES `{TestSchema}`.`ModFKCascUpdRef` (`Id`) ON UPDATE CASCADE;
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModFKCascUpd');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModFKCascUpdRef');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'FOREIGN KEY', 'ModFKCascUpd.FK_ModFKCascUpd_Ref');
";
        cmd.ExecuteNonQuery();

        var json = """
        [
            {
                "Name": "ModifyMyDefault",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false, "Default": "0" }
                ]
            },
            {
                "Name": "ModifyMyTableCheck",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Col2", "DataType": "INT", "Nullable": true }
                ],
                "CheckConstraints": [
                    { "Name": "CHK_ModifyMyTableCheck_MyCheck", "Expression": "`Id` > 100" }
                ]
            },
            {
                "Name": "ModifyFKColumn",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Col2", "DataType": "INT", "Nullable": true },
                    { "Name": "Col3", "DataType": "INT", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "Id" }
                ],
                "ForeignKeys": [
                    {
                        "Name": "FK_ModifyFKColumn_ModifyFKColumnRef",
                        "Columns": "Col2",
                        "RelatedTable": "ModifyFKColumnRef",
                        "RelatedColumns": "Id"
                    }
                ]
            },
            {
                "Name": "ModifyFKRefTbl",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Col2", "DataType": "INT", "Nullable": true },
                    { "Name": "Col3", "DataType": "INT", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "Id" }
                ],
                "ForeignKeys": [
                    {
                        "Name": "FK_ModifyFKRefTbl_Ref",
                        "Columns": "Col3",
                        "RelatedTable": "ModifyFKRefTblRefNew",
                        "RelatedColumns": "Id"
                    }
                ]
            },
            {
                "Name": "ModFKCascDel",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Col2", "DataType": "INT", "Nullable": true },
                    { "Name": "Col3", "DataType": "INT", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "Id" }
                ],
                "ForeignKeys": [
                    {
                        "Name": "FK_ModFKCascDel_Ref",
                        "Columns": "Col2",
                        "RelatedTable": "ModFKCascDelRef",
                        "RelatedColumns": "Id",
                        "DeleteAction": "NO ACTION"
                    }
                ]
            },
            {
                "Name": "ModFKCascUpd",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Col2", "DataType": "INT", "Nullable": true },
                    { "Name": "Col3", "DataType": "INT", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "Id" }
                ],
                "ForeignKeys": [
                    {
                        "Name": "FK_ModFKCascUpd_Ref",
                        "Columns": "Col2",
                        "RelatedTable": "ModFKCascUpdRef",
                        "RelatedColumns": "Id",
                        "UpdateAction": "NO ACTION"
                    }
                ]
            }
        ]
        """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingTableAndColumnQuench('{TestSchema}', 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0, 1, 1, 1, 1, 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0, 1, 1)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ForeignKeyQuench('{_productName}', '{TestSchema}', 0, 0, 1)";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        try
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestSchema}`";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
        catch { /* Ignore cleanup errors */ }
    }
}
