﻿using Schema.DataAccess;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class QuenchTables_AlterColumnTests : BaseQuenchTablesTests
{
    [Test]
    [TestCase("Col1", "BIGINT")]
    [TestCase("Col2", "CHAR(20)")]
    [TestCase("Col3", "BINARY(10)")]
    [TestCase("Col4", "VARCHAR(20)")]
    [TestCase("Col5", "VARBINARY(10)")]
    [TestCase("Col6", "VARCHAR(MAX)")]
    [TestCase("Col7", "VARBINARY(MAX)")]
    [TestCase("Col8", "VARCHAR(100)")]
    [TestCase("Col9", "VARBINARY(100)")]
    [TestCase("Col10", "DATETIME2(5)")]
    [TestCase("Col11", "DECIMAL(12, 3)")]
    [TestCase("Col12", "DECIMAL(10, 2)")]
    [TestCase("Col13", "NUMERIC(12, 3)")]
    [TestCase("Col14", "NUMERIC(10, 2)")]
    public void QuenchTables_ShouldModifyColumnForChangeDataType(string colName, string expectedType)
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        Assert.That(GetColumnDataType(cmd, "ChangeType", colName), Is.EqualTo(expectedType));
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldModifyColumnNullability()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.ChangeNullability'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleGoingToComputedColumn()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.ColumnToComputed'), 'Column2', 'IsComputed') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleGoingFromComputedColumn()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.ColumnFromComputed'), 'Column2', 'IsComputed') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleChangeComputedExpression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.ChangeComputedExpression'), 'Column2', 'IsComputed') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT [definition] FROM sys.computed_columns WHERE [object_id] = OBJECT_ID('dbo.ChangeComputedExpression') AND [name] = 'Column2'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("('NewResult')"));
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleChangingComputedColumnPersistence()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.MakePersisted'), 'Column2', 'IsComputed') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT [is_persisted] FROM sys.computed_columns WHERE [object_id] = OBJECT_ID('dbo.MakePersisted') AND [name] = 'Column2'";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnUsedInIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInIndex'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInIndex'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.AlterColumnInIndex'), 'IDX_Dependency', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.AlterColumnInIndex'), 'IDX_NoDependency', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnInIndex", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnUsedInIndexInclude()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInIndexInclude'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInIndexInclude'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.AlterColumnInIndexInclude'), 'IDX_Dependency', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.AlterColumnInIndexInclude'), 'IDX_NoDependency', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnInIndexInclude", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnUsedInUniqueIndexReferencedByFullTextIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInUniqueIndexInFTIndex'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInUniqueIndexInFTIndex'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInUniqueIndexInFTIndex'), 'Column3', 'AllowsNull') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.AlterColumnInUniqueIndexInFTIndex'), 'IDX_Dependency', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN EXISTS (SELECT * FROM sys.fulltext_indexes fi WITH (NOLOCK) WHERE fi.[object_id] = OBJECT_ID('dbo.AlterColumnInUniqueIndexInFTIndex')) THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnUsedInUniqueIndexReferencedByForeignKey()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInUniqueIndexInFK'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnInUniqueIndexInFK'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.AlterColumnInUniqueIndexInFK'), 'IDX_Dependency', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.FK_AlterColumnInUniqueIndexInFK_SelfRef') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnWithDefault()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithDefault'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithDefault'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithDefault", "Column2"), Is.EqualTo("BIGINT"));

        cmd.CommandText = "SELECT SchemaSmith.fn_StripParenWrapping(COLUMN_DEFAULT) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'AlterColumnWithDefault' AND COLUMN_NAME = 'Column2'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("0"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnWithColumnLevelCheckConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithCheckConstraint'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithCheckConstraint'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithCheckConstraint", "Column2"), Is.EqualTo("BIGINT"));

        cmd.CommandText = "SELECT SchemaSmith.fn_StripParenWrapping([definition]) FROM sys.check_constraints ck WITH (NOLOCK) WHERE ck.[parent_object_id] = OBJECT_ID('dbo.AlterColumnWithCheckConstraint') AND COL_NAME(ck.parent_object_id, ck.parent_column_id) = 'Column2'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("[Column2]<(50)"));
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnWithTableLevelCheckConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithTableCheckConstraint'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithTableCheckConstraint'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithTableCheckConstraint", "Column2"), Is.EqualTo("BIGINT"));

        cmd.CommandText = "SELECT ck.[Name] FROM sys.check_constraints ck WITH (NOLOCK) WHERE ck.[parent_object_id] = OBJECT_ID('dbo.AlterColumnWithTableCheckConstraint') AND ck.parent_column_id = 0";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("CK_AlterColumnWithTableCheckConstraint_Dependency"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_AlterColumnWithStatistics()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithStatistics'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithStatistics'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithStatistics", "Column2"), Is.EqualTo("BIGINT"));

        cmd.CommandText = "SELECT [Name] FROM sys.stats ss  WITH (NOLOCK) WHERE ss.[object_id] = OBJECT_ID('dbo.AlterColumnWithStatistics') AND [Name] = 'ST_Dependency'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("ST_Dependency"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnWithStatisticsFilterExpression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithStatisticsFilter'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithStatisticsFilter'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithStatisticsFilter", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnWithIndexFilterExpression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithIndexFilter'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithIndexFilter'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithIndexFilter", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnWithForeignKey()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithFK'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithFK'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithFK", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_AlterColumnWithComputedExpression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithComputed'), 'Column2', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithComputed'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithComputed", "Column2"), Is.EqualTo("BIGINT"));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldAlterColumnWithFullTextIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AlterColumnWithFTIndex'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        Assert.That(GetColumnDataType(cmd, "AlterColumnWithFTIndex", "Column2"), Is.EqualTo("VARCHAR(2000)"));

        cmd.CommandText = "SELECT CAST(CASE WHEN EXISTS (SELECT * FROM sys.fulltext_indexes fi WITH (NOLOCK) WHERE fi.[object_id] = OBJECT_ID('dbo.AlterColumnWithFTIndex')) THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
--QuenchTables_ShouldModifyColumnForChangeDataType
CREATE TABLE dbo.ChangeType (Col1 INT NOT NULL, Col2 CHAR(10) NOT NULL, Col3 BINARY(20) NOT NULL, Col4 VARCHAR(10) NOT NULL, Col5 VARBINARY(20) NOT NULL, Col6 VARCHAR(10) NOT NULL, Col7 VARBINARY(10) NOT NULL, Col8 VARBINARY(10) NOT NULL, Col9 VARBINARY(MAX) NOT NULL, Col10 DATETIME2(3) NOT NULL, Col11 DECIMAL(12, 2) NOT NULL, Col12 DECIMAL(12, 2) NOT NULL, Col13 NUMERIC(12, 2) NOT NULL, Col14 NUMERIC(12, 2) NOT NULL)
--QuenchTables_ShouldModifyColumnNullability
CREATE TABLE dbo.ChangeNullability (Column1 INT NULL)
--QuenchTables_ShouldHandleGoingToComputedColumn
CREATE TABLE dbo.ColumnToComputed (Column1 INT NOT NULL, Column2 INT NULL)
--QuenchTables_ShouldHandleGoingFromComputedColumn
CREATE TABLE dbo.ColumnFromComputed (Column1 INT NOT NULL, Column2 AS ([Column1] * 2))
--QuenchTables_ShouldHandleChangeComputedExpression
CREATE TABLE dbo.ChangeComputedExpression (Column1 INT NOT NULL, Column2 AS ('OldResult'))
--QuenchTables_ShouldHandleChangingComputedColumnPersistence
CREATE TABLE dbo.MakePersisted (Column1 INT NOT NULL, Column2 AS ([Column1] * 2))
--QuenchTables_ShouldAlterColumnUsedInIndex
CREATE TABLE dbo.AlterColumnInIndex (Column1 INT NOT NULL, Column2 INT NULL)
CREATE INDEX IDX_NoDependency ON dbo.AlterColumnInIndex ([Column1])
CREATE INDEX IDX_Dependency ON dbo.AlterColumnInIndex ([Column2])
--QuenchTables_ShouldAlterColumnUsedInIndexInclude
CREATE TABLE dbo.AlterColumnInIndexInclude (Column1 INT NOT NULL, Column2 INT)
CREATE INDEX IDX_NoDependency ON dbo.AlterColumnInIndexInclude ([Column1])
CREATE INDEX IDX_Dependency ON dbo.AlterColumnInIndexInclude ([Column1]) INCLUDE ([Column2])
--QuenchTables_ShouldAlterColumnUsedInUniqueIndexReferencedByFullTextIndex
CREATE TABLE dbo.AlterColumnInUniqueIndexInFTIndex (Column1 INT NOT NULL, Column2 INT NOT NULL, Column3 VARCHAR(200) NULL)
CREATE UNIQUE INDEX IDX_Dependency ON dbo.AlterColumnInUniqueIndexInFTIndex ([Column2])
CREATE FULLTEXT INDEX ON dbo.AlterColumnInUniqueIndexInFTIndex (Column3) KEY INDEX IDX_Dependency ON FT_Catalog WITH CHANGE_TRACKING = OFF
--QuenchTables_ShouldAlterColumnUsedInUniqueIndexReferencedByForeignKey
CREATE TABLE dbo.AlterColumnInUniqueIndexInFK (Column1 INT NOT NULL, Column2 INT NOT NULL)
CREATE UNIQUE INDEX IDX_Dependency ON dbo.AlterColumnInUniqueIndexInFK ([Column2])
ALTER TABLE dbo.AlterColumnInUniqueIndexInFK ADD CONSTRAINT FK_AlterColumnInUniqueIndexInFK_SelfRef FOREIGN KEY (Column1) REFERENCES dbo.AlterColumnInUniqueIndexInFK (Column2)
--QuenchTables_ShouldAlterColumnWithDefault
CREATE TABLE dbo.AlterColumnWithDefault (Column1 INT NOT NULL, Column2 INT NOT NULL DEFAULT 0)
--QuenchTables_ShouldAlterColumnWithColumnLevelCheckConstraint
CREATE TABLE dbo.AlterColumnWithCheckConstraint (Column1 INT NOT NULL, Column2 INT CHECK ([Column2] < 50))
--QuenchTables_ShouldAlterColumnWithTableLevelCheckConstraint
CREATE TABLE dbo.AlterColumnWithTableCheckConstraint (Column1 INT NOT NULL, Column2 INT, CONSTRAINT CK_AlterColumnWithTableCheckConstraint_Dependency CHECK (Column2 < Column1))
--QuenchTables_AlterColumnWithStatistics
CREATE TABLE dbo.AlterColumnWithStatistics (Column1 INT NOT NULL, Column2 INT)
CREATE STATISTICS ST_Dependency ON dbo.AlterColumnWithStatistics (Column2)
--QuenchTables_ShouldAlterColumnWithStatisticsFilterExpression
CREATE TABLE dbo.AlterColumnWithStatisticsFilter (Column1 INT NOT NULL, Column2 INT)
CREATE STATISTICS ST_Dependency ON dbo.AlterColumnWithStatisticsFilter (Column1) WHERE Column2 < 100
--QuenchTables_ShouldAlterColumnWithIndexFilterExpression
CREATE TABLE dbo.AlterColumnWithIndexFilter (Column1 INT NOT NULL, Column2 INT)
CREATE INDEX IDX_Dependency ON dbo.AlterColumnWithIndexFilter (Column1) WHERE Column2 < 100
--QuenchTables_ShouldAlterColumnWithForeignKey
CREATE TABLE dbo.AlterColumnWithFK (Column1 INT NOT NULL, Column2 INT, CONSTRAINT PK_AlterColumnWithFK PRIMARY KEY (Column1))
CREATE UNIQUE INDEX UQ_Colum2 ON dbo.AlterColumnWithFK (Column2)
CREATE TABLE dbo.AlterColumnWithFKRef (Column1 INT NOT NULL, Column2 INT, CONSTRAINT PK_AlterColumnWithFKRef PRIMARY KEY (Column1))
ALTER TABLE dbo.AlterColumnWithFK ADD CONSTRAINT FK_AlterColumnWithFK_SelfRef FOREIGN KEY (Column2) REFERENCES dbo.AlterColumnWithFK (Column1)
ALTER TABLE dbo.AlterColumnWithFKRef ADD CONSTRAINT FK_AlterColumnWithFK_Referenced FOREIGN KEY (Column2) REFERENCES dbo.AlterColumnWithFK (Column2)
ALTER TABLE dbo.AlterColumnWithFK ADD CONSTRAINT FK_AlterColumnWithFK_Referencing FOREIGN KEY (Column2) REFERENCES dbo.AlterColumnWithFKRef (Column1)
--QuenchTables_AlterColumnWithComputedExpression
CREATE TABLE dbo.AlterColumnWithComputed (Column1 INT NOT NULL, Column2 INT, Column3 AS (Column2*3))
--QuenchTables_ShouldAlterColumnWithFullTextIndex
CREATE TABLE dbo.AlterColumnWithFTIndex (Column1 INT NOT NULL, Column2 VARCHAR(1000) NULL, CONSTRAINT PK_AlterColumnWithFTIndex PRIMARY KEY (Column1))
CREATE FULLTEXT INDEX ON dbo.AlterColumnWithFTIndex (Column2) KEY INDEX PK_AlterColumnWithFTIndex ON FT_Catalog WITH CHANGE_TRACKING = OFF
";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = """
        [
            {
                "Schema": "[dbo]",
                "Name": "[ChangeType]",
                "Columns": [
                    {
                      "Name": "[Col1]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col2]",
                      "DataType": "CHAR(20)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col3]",
                      "DataType": "BINARY(10)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col4]",
                      "DataType": "VARCHAR(20)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col5]",
                      "DataType": "VARBINARY(10)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col6]",
                      "DataType": "VARCHAR(MAX)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col7]",
                      "DataType": "VARBINARY(MAX)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col8]",
                      "DataType": "VARCHAR(100)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col9]",
                      "DataType": "VARBINARY(100)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col10]",
                      "DataType": "DATETIME2(5)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col11]",
                      "DataType": "DECIMAL(12, 3)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col12]",
                      "DataType": "DECIMAL(10, 2)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col13]",
                      "DataType": "NUMERIC(12, 3)",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col14]",
                      "DataType": "NUMERIC(10, 2)",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[ChangeNullability]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[ColumnToComputed]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "ComputedExpression": "[Column1] * 2"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[ColumnFromComputed]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[ChangeComputedExpression]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "ComputedExpression": "'NewResult'"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[MakePersisted]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "ComputedExpression": "[Column1] * 2",
                      "Persisted": true
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnInIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                       "Name": "[IDX_Dependency]",
                       "IndexColumns": "[Column2]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnInIndexInclude]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                       "Name": "[IDX_Dependency]",
                       "IndexColumns": "[Column1]",
                       "IncludeColumns": "[Column2]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnInUniqueIndexInFTIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column3]",
                      "DataType": "VARCHAR(200)",
                      "Nullable": true
                    }
                ],
                "Indexes": [
                    {
                       "Name": "[IDX_Dependency]",
                       "IndexColumns": "[Column2]",
                       "Unique": true
                    }
                ],
                "FullTextIndex": {
                    "FullTextCatalog": "FT_Catalog",
                    "KeyIndex": "IDX_Dependency",
                    "ChangeTracking": "OFF",
                    "Columns": "[Column3]"
                }
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnInUniqueIndexInFK]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                       "Name": "[IDX_Dependency]",
                       "IndexColumns": "[Column2]",
                       "Unique": true
                    }
                ],
                "ForeignKeys": [
                    {
                      "Name": "[FK_AlterColumnInUniqueIndexInFK_SelfRef]",
                      "Columns": "[Column1]",
                      "RelatedTableSchema": "dbo",
                      "RelatedTable": "[AlterColumnInUniqueIndexInFK]",
                      "RelatedColumns": "[Column2]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithDefault]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false,
                      "Default": "0"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithCheckConstraint]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false,
                      "CheckExpression": "[Column2] < 50"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithTableCheckConstraint]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ],
                "CheckConstraints": [
                    {
                       "Name": "CK_AlterColumnWithTableCheckConstraint_Dependency",
                       "Expression": "Column2 < Column1"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithStatistics]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ],
                "Statistics": [
                    {
                       "Name": "ST_Dependency",
                       "Columns": "[Column2]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithStatisticsFilter]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithIndexFilter]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithFK]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithComputed]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column3]",
                      "ComputedExpression": "Column2*3"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterColumnWithFTIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "VARCHAR(2000)",
                      "Nullable": true
                    }
                ],
                "FullTextIndex": {
                    "FullTextCatalog": "FT_Catalog",
                    "KeyIndex": "PK_AlterColumnWithFTIndex",
                    "ChangeTracking": "OFF",
                    "Columns": "[Column2]"
                }
            }
        ]
        """;
        RunTableQuenchProc(cmd, json);

        conn.Close();
    }
}
