// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;

using NUnit.Framework;
using Schema.IntegrationTests.MySQL;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for column alteration scenarios during table quench.
/// Tests data type changes, nullability changes, default values, and generated columns.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
[Category("Integration")]
public class TableQuench_AlterColumnTests : BaseTableQuenchTests
{
    private const string TestSchema = "AlterColumnTests";

    [TestCase("Col1", "BIGINT")]
    [TestCase("Col2", "CHAR(20)")]
    [TestCase("Col4", "VARCHAR(20)")]
    [TestCase("Col6", "VARCHAR(100)")]
    [TestCase("Col10", "DATETIME(5)")]
    [TestCase("Col11", "DECIMAL(12,3)")]
    [TestCase("Col12", "DECIMAL(10,2)")]
    public void TableQuench_ShouldModifyColumnForChangeDataType(string colName, string expectedType)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        var actualType = GetColumnDataType(cmd, TestSchema, "ChangeType", colName);
        Assert.That(actualType, Is.EqualTo(expectedType).IgnoreCase);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldModifyColumnNullability()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ChangeNullability'
              AND COLUMN_NAME = 'Column1'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("NO"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldHandleGoingToGeneratedColumn()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ColumnToGenerated'
              AND COLUMN_NAME = 'Column2'";
        var extra = cmd.ExecuteScalar()?.ToString();
        Assert.That(extra, Does.Contain("GENERATED"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldHandleGoingFromGeneratedColumn()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ColumnFromGenerated'
              AND COLUMN_NAME = 'Column2'";
        var extra = cmd.ExecuteScalar()?.ToString() ?? "";
        Assert.That(extra, Does.Not.Contain("GENERATED"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldHandleChangeGenerationExpression()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT GENERATION_EXPRESSION FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ChangeGenerationExpression'
              AND COLUMN_NAME = 'Column2'";
        var expr = cmd.ExecuteScalar()?.ToString();
        // Check for "100" which is in the new expression but not in the original "* 2"
        Assert.That(expr, Does.Contain("100"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAlterColumnUsedInIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Column should exist
        Assert.That(ColumnExists(cmd, TestSchema, "AlterColumnInIndex", "Column2"), Is.True);
        Assert.That(ColumnExists(cmd, TestSchema, "AlterColumnInIndex", "Column1"), Is.True);

        // Index should exist
        Assert.That(IndexExists(cmd, TestSchema, "AlterColumnInIndex", "IDX_Dependency"), Is.True);
        Assert.That(IndexExists(cmd, TestSchema, "AlterColumnInIndex", "IDX_NoDependency"), Is.True);

        // Column should have new type
        Assert.That(GetColumnDataType(cmd, TestSchema, "AlterColumnInIndex", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAlterColumnWithDefault()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.That(ColumnExists(cmd, TestSchema, "AlterColumnWithDefault", "Column2"), Is.True);
        Assert.That(GetColumnDataType(cmd, TestSchema, "AlterColumnWithDefault", "Column2"), Is.EqualTo("BIGINT"));

        cmd.CommandText = $@"
            SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AlterColumnWithDefault'
              AND COLUMN_NAME = 'Column2'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("0"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAlterColumnWithTableLevelCheckConstraint()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.That(ColumnExists(cmd, TestSchema, "AlterColumnWithTableCheckConstraint", "Column2"), Is.True);
        Assert.That(GetColumnDataType(cmd, TestSchema, "AlterColumnWithTableCheckConstraint", "Column2"), Is.EqualTo("BIGINT"));

        // Check constraint should still exist
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'AlterColumnWithTableCheckConstraint'
              AND CONSTRAINT_NAME = 'CK_AlterColumnWithTableCheckConstraint_Dependency'
              AND CONSTRAINT_TYPE = 'CHECK'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThan(0));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAlterColumnWithForeignKey()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.That(ColumnExists(cmd, TestSchema, "AlterColumnWithFK", "Column2"), Is.True);
        Assert.That(GetColumnDataType(cmd, TestSchema, "AlterColumnWithFK", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void TableQuench_AlterColumnWithGenerationExpression()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        Assert.That(ColumnExists(cmd, TestSchema, "AlterColumnWithGenerated", "Column2"), Is.True);
        Assert.That(GetColumnDataType(cmd, TestSchema, "AlterColumnWithGenerated", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldModifyColumnCollation()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
            SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyColumnCollation'
              AND COLUMN_NAME = 'Column2'";
        var collation = cmd.ExecuteScalar()?.ToString();
        Assert.That(collation, Is.EqualTo("utf8mb4_bin"));

        conn.Close();
    }

    private static bool ColumnExists(IDbCommand cmd, string schema, string table, string column)
    {
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{schema}'
              AND TABLE_NAME = '{table}'
              AND COLUMN_NAME = '{column}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool IndexExists(IDbCommand cmd, string schema, string table, string index)
    {
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{schema}'
              AND TABLE_NAME = '{table}'
              AND INDEX_NAME = '{index}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static new string GetColumnDataType(IDbCommand cmd, string schema, string table, string column)
    {
        // Use COLUMN_TYPE to get full type with precision (e.g., "varchar(20)" instead of "varchar")
        cmd.CommandText = $@"
            SELECT UPPER(COLUMN_TYPE) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{schema}'
              AND TABLE_NAME = '{table}'
              AND COLUMN_NAME = '{column}'";
        return cmd.ExecuteScalar()?.ToString()?.ToUpper() ?? "UNKNOWN";
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Create test schema (database in MySQL)
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"USE `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandTimeout = 300;
        cmd.CommandText = $@"
-- TableQuench_ShouldModifyColumnForChangeDataType
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ChangeType`
  (`Col1` INT NOT NULL, `Col2` CHAR(10) NOT NULL, `Col4` VARCHAR(10) NOT NULL, `Col6` VARCHAR(10) NOT NULL,
   `Col10` DATETIME(3) NOT NULL, `Col11` DECIMAL(12, 2) NOT NULL,
   `Col12` DECIMAL(12, 2) NOT NULL);
-- TableQuench_ShouldModifyColumnNullability
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ChangeNullability` (`Column1` INT NULL);
-- TableQuench_ShouldHandleGoingToGeneratedColumn
-- NOTE: MySQL doesn't support ALTER COLUMN to change regular->generated, so we use DROP+ADD approach
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ColumnToGenerated` (`Column1` INT NOT NULL, `Column2` INT);
-- TableQuench_ShouldHandleGoingFromGeneratedColumn
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ColumnFromGenerated` (`Column1` INT NOT NULL, `Column2` INT AS (`Column1` * 2) VIRTUAL);
-- TableQuench_ShouldHandleChangeGenerationExpression
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ChangeGenerationExpression` (`Column1` INT NOT NULL, `Column2` INT AS (`Column1` * 2) VIRTUAL);
-- TableQuench_ShouldAlterColumnUsedInIndex
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AlterColumnInIndex` (`Column1` INT NOT NULL, `Column2` INT NULL);
CREATE INDEX `IDX_NoDependency` ON `{TestSchema}`.`AlterColumnInIndex` (`Column1`);
CREATE INDEX `IDX_Dependency` ON `{TestSchema}`.`AlterColumnInIndex` (`Column2`);
-- TableQuench_ShouldAlterColumnWithDefault
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AlterColumnWithDefault` (`Column1` INT NOT NULL, `Column2` INT NOT NULL DEFAULT 0);
-- TableQuench_ShouldAlterColumnWithTableLevelCheckConstraint
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AlterColumnWithTableCheckConstraint` (`Column1` INT NOT NULL, `Column2` INT, CONSTRAINT `CK_AlterColumnWithTableCheckConstraint_Dependency` CHECK (`Column2` < `Column1`));
-- TableQuench_ShouldAlterColumnWithForeignKey
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AlterColumnWithFK` (`Column1` INT NOT NULL, `Column2` INT, PRIMARY KEY (`Column1`));
CREATE UNIQUE INDEX `UQ_Column2` ON `{TestSchema}`.`AlterColumnWithFK` (`Column2`);
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AlterColumnWithFKRef` (`Column1` INT NOT NULL, `Column2` INT, PRIMARY KEY (`Column1`));
-- TableQuench_AlterColumnWithGenerationExpression
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`AlterColumnWithGenerated` (`Column1` INT NOT NULL, `Column2` INT, `Column3` INT AS (`Column2`*3) VIRTUAL);
-- TableQuench_ShouldModifyColumnCollation
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyColumnCollation` (`Column1` VARCHAR(10) COLLATE utf8mb4_unicode_ci NULL, `Column2` VARCHAR(10) NULL, `Column3` VARCHAR(10) COLLATE utf8mb4_unicode_ci NULL);
";
        cmd.ExecuteNonQuery();

        // Now run the quench with the desired schema
        var json = """
        [
            {
                "Name": "ChangeType",
                "Columns": [
                    { "Name": "Col1", "DataType": "BIGINT", "Nullable": false },
                    { "Name": "Col2", "DataType": "CHAR(20)", "Nullable": false },
                    { "Name": "Col4", "DataType": "VARCHAR(20)", "Nullable": false },
                    { "Name": "Col6", "DataType": "VARCHAR(100)", "Nullable": false },
                    { "Name": "Col10", "DataType": "DATETIME(5)", "Nullable": false },
                    { "Name": "Col11", "DataType": "DECIMAL(12, 3)", "Nullable": false },
                    { "Name": "Col12", "DataType": "DECIMAL(10, 2)", "Nullable": false }
                ]
            },
            {
                "Name": "ChangeNullability",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false }
                ]
            },
            {
                "Name": "ColumnToGenerated",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "INT", "Nullable": true, "GenerationExpression": "`Column1` * 2", "Generated": "VIRTUAL" }
                ]
            },
            {
                "Name": "ColumnFromGenerated",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "INT", "Nullable": true }
                ]
            },
            {
                "Name": "ChangeGenerationExpression",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "INT", "Nullable": true, "GenerationExpression": "(`Column1` + 100)", "Generated": "VIRTUAL" }
                ]
            },
            {
                "Name": "AlterColumnInIndex",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "BIGINT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_Dependency", "IndexColumns": "Column2" }
                ]
            },
            {
                "Name": "AlterColumnWithDefault",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "BIGINT", "Nullable": false, "Default": "0" }
                ]
            },
            {
                "Name": "AlterColumnWithTableCheckConstraint",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "BIGINT", "Nullable": false }
                ],
                "CheckConstraints": [
                    { "Name": "CK_AlterColumnWithTableCheckConstraint_Dependency", "Expression": "`Column2` < `Column1`" }
                ]
            },
            {
                "Name": "AlterColumnWithFK",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "BIGINT", "Nullable": false }
                ]
            },
            {
                "Name": "AlterColumnWithGenerated",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "BIGINT", "Nullable": false },
                    { "Name": "Column3", "DataType": "BIGINT", "Nullable": true, "GenerationExpression": "`Column2`*3", "Generated": "VIRTUAL" }
                ]
            },
            {
                "Name": "ModifyColumnCollation",
                "Columns": [
                    { "Name": "Column1", "DataType": "VARCHAR(10)", "Nullable": true },
                    { "Name": "Column2", "DataType": "VARCHAR(10)", "Nullable": true, "Collation": "utf8mb4_bin" },
                    { "Name": "Column3", "DataType": "VARCHAR(10)", "Nullable": true }
                ]
            }
        ]
        """;

        // Use direct procedure calls for alter column tests
        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingTableAndColumnQuench('{TestSchema}', 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0)";
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
        catch
        {
            // Ignore cleanup errors
        }
    }
}
