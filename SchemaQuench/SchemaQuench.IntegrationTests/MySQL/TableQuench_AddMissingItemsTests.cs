// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;
using System;

using NUnit.Framework;
using Schema.IntegrationTests.MySQL;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for adding missing items during table quench.
/// Tests adding indexes, columns, defaults, check constraints, and foreign keys.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
[Category("Integration")]
public class TableQuench_AddMissingItemsTests : BaseTableQuenchTests
{
    private const string TestSchema = "AddMissingItemsTests";

    [Test]
    public void TableQuench_ShouldAddMissingIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Index should exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyIndex'
              AND INDEX_NAME = 'IDX_NewIndex'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        // Make sure the index ownership is recorded
        cmd.CommandText = $@"
            SELECT ProductName FROM `{_mainDb}`.SchemaSmith_ProductOwnership
            WHERE ObjectSchema = '{TestSchema}'
              AND ObjectName = 'AddMyIndex.IDX_NewIndex'
              AND ObjectType = 'INDEX'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo(_productName));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingIndexForIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyIndexIO'
              AND INDEX_NAME = 'IDX_NewIndexIO'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        // Make sure the index ownership is recorded
        cmd.CommandText = $@"
            SELECT ProductName FROM `{_mainDb}`.SchemaSmith_ProductOwnership
            WHERE ObjectSchema = '{TestSchema}'
              AND ObjectName = 'AddMyIndexIO.IDX_NewIndexIO'
              AND ObjectType = 'INDEX'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo(_productName));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingColumns()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // NewColumn should exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyColumn'
              AND COLUMN_NAME = 'NewColumn'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));

        // CollatedColumn should exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyColumn'
              AND COLUMN_NAME = 'CollatedColumn'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));

        // DontApply should NOT exist (ShouldApplyExpression = false)
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyColumn'
              AND COLUMN_NAME = 'DontApply'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingDefault()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyDefault'
              AND COLUMN_NAME = 'Id'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("0"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingTableWithEnumDefault()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // ENUM column should have correct default (UPPER due to ParseTableJson UPPER(DataType))
        cmd.CommandText = $@"
            SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyEnumDefault'
              AND COLUMN_NAME = 'Status'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Active").IgnoreCase);

        // VARCHAR column should have correct default
        cmd.CommandText = $@"
            SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyEnumDefault'
              AND COLUMN_NAME = 'Label'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("unknown").IgnoreCase);

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingColumnWithStringDefault()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Column should exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyStringDefault'
              AND COLUMN_NAME = 'NewStatus'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));

        // ENUM column should have correct default
        cmd.CommandText = $@"
            SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyStringDefault'
              AND COLUMN_NAME = 'NewStatus'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Pending").IgnoreCase);

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingTableLevelCheckConstraint()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyTableCheck'
              AND CONSTRAINT_NAME = 'CHK_AddMyTableCheck_MyCheck'
              AND CONSTRAINT_TYPE = 'CHECK'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingForeignKey()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyFK'
              AND CONSTRAINT_NAME = 'FK_AddMyFK_SelfRef'
              AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingUniqueIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT NON_UNIQUE FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyUniqueIndex'
              AND INDEX_NAME = 'IDX_UniqueColumn'
              AND SEQ_IN_INDEX = 1";
        var nonUnique = cmd.ExecuteScalar();
        Assert.That(nonUnique, Is.Not.Null);
        Assert.That(Convert.ToInt32(nonUnique), Is.EqualTo(0), "Index should be unique");

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldKeepOneVariantWhenTwoSameNameColumnsHaveMutuallyExclusiveShouldApply()
    {
        // Regression test for the silent-divergence bug surfaced 2026-06-01: when a table JSON
        // contained two same-named column entries with mutually exclusive ShouldApplyExpression,
        // both rows were silently dropped during JSON parsing and the column never landed. The
        // generated per-row UPDATE-then-DELETE statements matched on column name only, so any
        // one row whose expression evaluated false would zero the ShouldApply flag on every row
        // sharing the name -- including the sibling that was supposed to survive.
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // The variant whose ShouldApplyExpression evaluates true ("true") should survive
        // with its declared type (INT). The other variant (VARCHAR(50), gated on "false") is skipped.
        cmd.CommandText = $@"
            SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyVariantColumn'
              AND COLUMN_NAME = 'payload'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("int"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldKeepOneVariantWhenTwoSameNameIndexesHaveMutuallyExclusiveShouldApply()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // The surviving variant indexes col1; the skipped variant targeted col2.
        cmd.CommandText = $@"
            SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyVariantIndex'
              AND INDEX_NAME = 'IDX_Variant'
            ORDER BY SEQ_IN_INDEX";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("col1"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldKeepOneVariantWhenTwoSameNameFKsHaveMutuallyExclusiveShouldApply()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE CONSTRAINT_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AddMyVariantFK'
              AND CONSTRAINT_NAME = 'FK_AddMyVariantFK_Variant'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("col1"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldKeepOneVariantWhenTwoSameNameCheckConstraintsHaveMutuallyExclusiveShouldApply()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT CHECK_CLAUSE FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = '{TestSchema}'
              AND CONSTRAINT_NAME = 'CHK_AddMyVariantCheck_Variant'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Does.Contain("`col1` > 0"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldEchoVariantNameInOperationMessages()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS `{TestSchema}`.`VariantLogTest` (`Id` INT NOT NULL, `col1` INT NOT NULL, `col2` INT NOT NULL)";
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Name": "VariantLogTest",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "col1", "DataType": "INT", "Nullable": false },
                    { "Name": "col2", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_VariantLog", "IndexColumns": "col1", "ShouldApplyExpression": "1=1", "VariantName": "Modern engines" },
                    { "Name": "IDX_VariantLog", "IndexColumns": "col2", "ShouldApplyExpression": "1=0", "VariantName": "Legacy engines" }
                ]
            }
            ]
            """;

        try
        {
            cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0)";
            cmd.ExecuteNonQuery();

            // SchemaSmith_StatusMessages is keyed by CONNECTION_ID(); query on the SAME connection.
            cmd.CommandText = "SELECT COUNT(*) FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID() AND Message LIKE '%(variant: Modern engines)%'";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThanOrEqualTo(1));

            cmd.CommandText = "SELECT COUNT(*) FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID() AND Message LIKE '%Legacy engines%'";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{TestSchema}`.`VariantLogTest`";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();
        }
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
-- TableQuench_ShouldAddMissingIndex
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyIndex` (`Id` INT NOT NULL);
-- TableQuench_ShouldAddMissingColumn
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyColumn` (`Id` INT NOT NULL);
-- TableQuench_ShouldAddMissingDefault
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyDefault` (`Id` INT NOT NULL);
-- TableQuench_ShouldAddMissingTableLevelCheckConstraint
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyTableCheck` (`Id` INT NOT NULL, `Col2` INT);
-- TableQuench_ShouldAddMissingForeignKey
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyFK` (`Id` INT NOT NULL PRIMARY KEY, `Col2` INT);
-- TableQuench_ShouldAddMissingUniqueIndex
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyUniqueIndex` (`Id` INT NOT NULL, `UniqueColumn` VARCHAR(50) NOT NULL);

-- TableQuench_ShouldAddMissingColumnWithStringDefault
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyStringDefault` (`Id` INT NOT NULL);
-- TableQuench_ShouldKeepOneVariantWhenTwoSameNameColumnsHaveMutuallyExclusiveShouldApply
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyVariantColumn` (`Id` INT NOT NULL);
-- TableQuench_ShouldKeepOneVariantWhenTwoSameNameIndexesHaveMutuallyExclusiveShouldApply
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyVariantIndex` (`Id` INT NOT NULL, `col1` INT NOT NULL, `col2` INT NOT NULL);
-- TableQuench_ShouldKeepOneVariantWhenTwoSameNameFKsHaveMutuallyExclusiveShouldApply
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyVariantFK` (`Id` INT NOT NULL PRIMARY KEY, `col1` INT NULL, `col2` INT NULL, KEY ix_variant_col1 (`col1`), KEY ix_variant_col2 (`col2`));
-- TableQuench_ShouldKeepOneVariantWhenTwoSameNameCheckConstraintsHaveMutuallyExclusiveShouldApply
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyVariantCheck` (`Id` INT NOT NULL, `col1` INT NULL);

-- Index Only
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AddMyIndexIO` (`Id` INT NOT NULL);
";
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Name": "AddMyIndex",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_NewIndex", "IndexColumns": "Id" }
                ]
            },
            {
                "Name": "AddMyColumn",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "NewColumn", "DataType": "VARCHAR(10)", "Nullable": true },
                    { "Name": "CollatedColumn", "DataType": "VARCHAR(10)", "Nullable": true, "Collation": "utf8mb4_unicode_ci" },
                    { "Name": "DontApply", "DataType": "INT", "Nullable": true, "ShouldApplyExpression": "false" }
                ]
            },
            {
                "Name": "AddMyDefault",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false, "Default": "0" }
                ]
            },
            {
                "Name": "AddMyTableCheck",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Col2", "DataType": "INT", "Nullable": true }
                ],
                "CheckConstraints": [
                    { "Name": "CHK_AddMyTableCheck_MyCheck", "Expression": "`Id`<`Col2`" }
                ]
            },
            {
                "Name": "AddMyFK",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Col2", "DataType": "INT", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "Id" }
                ],
                "ForeignKeys": [
                    {
                        "Name": "FK_AddMyFK_SelfRef",
                        "Columns": "Col2",
                        "RelatedTable": "AddMyFK",
                        "RelatedColumns": "Id"
                    }
                ]
            },
            {
                "Name": "AddMyEnumDefault",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "Status", "DataType": "ENUM('Active','Inactive','Pending')", "Nullable": false, "Default": "'Active'" },
                    { "Name": "Label", "DataType": "VARCHAR(50)", "Nullable": true, "Default": "'unknown'" }
                ]
            },
            {
                "Name": "AddMyStringDefault",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "NewStatus", "DataType": "ENUM('Active','Inactive','Pending')", "Nullable": true, "Default": "'Pending'" }
                ]
            },
            {
                "Name": "AddMyUniqueIndex",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "UniqueColumn", "DataType": "VARCHAR(50)", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_UniqueColumn", "IndexColumns": "UniqueColumn", "Unique": true }
                ]
            },
            {
                "Name": "AddMyVariantColumn",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "payload", "DataType": "INT", "Nullable": true, "ShouldApplyExpression": "true" },
                    { "Name": "payload", "DataType": "VARCHAR(50)", "Nullable": true, "ShouldApplyExpression": "false" }
                ]
            },
            {
                "Name": "AddMyVariantIndex",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "col1", "DataType": "INT", "Nullable": false },
                    { "Name": "col2", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_Variant", "IndexColumns": "col1", "ShouldApplyExpression": "true" },
                    { "Name": "IDX_Variant", "IndexColumns": "col2", "ShouldApplyExpression": "false" }
                ]
            },
            {
                "Name": "AddMyVariantFK",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "col1", "DataType": "INT", "Nullable": true },
                    { "Name": "col2", "DataType": "INT", "Nullable": true }
                ],
                "Indexes": [
                    { "Name": "PRIMARY", "PrimaryKey": true, "IndexColumns": "Id" },
                    { "Name": "ix_variant_col1", "IndexColumns": "col1" },
                    { "Name": "ix_variant_col2", "IndexColumns": "col2" }
                ],
                "ForeignKeys": [
                    {
                      "Name": "FK_AddMyVariantFK_Variant",
                      "Columns": "col1",
                      "RelatedTable": "AddMyVariantFK",
                      "RelatedColumns": "Id",
                      "ShouldApplyExpression": "true"
                    },
                    {
                      "Name": "FK_AddMyVariantFK_Variant",
                      "Columns": "col2",
                      "RelatedTable": "AddMyVariantFK",
                      "RelatedColumns": "Id",
                      "ShouldApplyExpression": "false"
                    }
                ]
            },
            {
                "Name": "AddMyVariantCheck",
                "Columns": [
                    { "Name": "Id", "DataType": "INT", "Nullable": false },
                    { "Name": "col1", "DataType": "INT", "Nullable": true }
                ],
                "CheckConstraints": [
                    {
                      "Name": "CHK_AddMyVariantCheck_Variant",
                      "Expression": "`col1` > 0",
                      "ShouldApplyExpression": "true"
                    },
                    {
                      "Name": "CHK_AddMyVariantCheck_Variant",
                      "Expression": "`col1` < 0",
                      "ShouldApplyExpression": "false"
                    }
                ]
            }
            ]
            """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingTableAndColumnQuench('{TestSchema}', 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0, 1)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ForeignKeyQuench('{_productName}', '{TestSchema}', 0, 0, 1)";
        cmd.ExecuteNonQuery();

        // Index Only
        var jsonIO = """
            [
            {
                "Name": "AddMyIndexIO",
                "Indexes": [
                    { "Name": "IDX_NewIndexIO", "IndexColumns": "Id" }
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

