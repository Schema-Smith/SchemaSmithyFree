// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_IndexOnlyTests : BaseTableQuenchTests
{
    [Test]
    public void IndexOnlyQuench_ShouldAddMissingIndex()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Add_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL)";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_Test]"", ""IndexColumns"": ""[Col1]""}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.{tableName}'), 'IDX_Test', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldNotCreateMissingTable()
    {
        var productName = Guid.NewGuid().ToString();

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        var json = @"[{""Schema"": ""[dbo]"", ""Name"": ""[TableThatDoesNotExist_IOTest]"", ""Columns"": [{""Name"": ""[Id]"", ""DataType"": ""INT"", ""Nullable"": false}]}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.TableThatDoesNotExist_IOTest') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False, "IndexOnly should not create missing tables");

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldModifyIndexColumns()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Mod_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL, Col2 INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_Test ON dbo.{tableName} ([Col1])";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_Test]"", ""IndexColumns"": ""[Col2]""}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
SELECT COL_NAME(ic.[object_id], ic.column_id) FROM sys.index_columns ic
JOIN sys.indexes i ON ic.[object_id] = i.[object_id] AND ic.index_id = i.index_id
WHERE i.[object_id] = OBJECT_ID('dbo.{tableName}') AND i.[name] = 'IDX_Test' AND ic.is_included_column = 0";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Col2"));

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldDropUnknownIndex()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Drop_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_Unknown ON dbo.{tableName} ([Col1])";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]""}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 1";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.{tableName}'), 'IDX_Unknown', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldModifyIncludeColumns()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_ModInc_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL, Col2 INT NOT NULL, Col3 INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_Test ON dbo.{tableName} ([Col1]) INCLUDE ([Col2])";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_Test]"", ""IndexColumns"": ""[Col1]"", ""IncludeColumns"": ""[Col3]""}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
SELECT COL_NAME(ic.[object_id], ic.column_id) FROM sys.index_columns ic
JOIN sys.indexes i ON ic.[object_id] = i.[object_id] AND ic.index_id = i.index_id
WHERE i.[object_id] = OBJECT_ID('dbo.{tableName}') AND i.[name] = 'IDX_Test' AND ic.is_included_column = 1";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Col3"));

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldAddIncludeColumnsToExistingIndex()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_AddInc_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL, Col2 INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_Test ON dbo.{tableName} ([Col1])";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_Test]"", ""IndexColumns"": ""[Col1]"", ""IncludeColumns"": ""[Col2]""}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
SELECT COL_NAME(ic.[object_id], ic.column_id) FROM sys.index_columns ic
JOIN sys.indexes i ON ic.[object_id] = i.[object_id] AND ic.index_id = i.index_id
WHERE i.[object_id] = OBJECT_ID('dbo.{tableName}') AND i.[name] = 'IDX_Test' AND ic.is_included_column = 1";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Col2"));

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldModifyFilterExpression()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Filter_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Status INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_Test ON dbo.{tableName} ([Status]) WHERE [Status]>(0)";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_Test]"", ""IndexColumns"": ""[Status]"", ""FilterExpression"": ""[Status]>(5)""}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
SELECT filter_definition FROM sys.indexes
WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = 'IDX_Test'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Does.Contain("[Status]>(5)"));

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldModifyFillFactor()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Fill_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_Test ON dbo.{tableName} ([Col1])";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""UpdateFillFactor"": true, ""Indexes"": [{{""Name"": ""[IDX_Test]"", ""IndexColumns"": ""[Col1]"", ""FillFactor"": 80}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0, @UpdateFillFactor = 1";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
SELECT fill_factor FROM sys.indexes
WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = 'IDX_Test'";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(80));

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldNotModifyFillFactorWhenDisabled()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_FillOff_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_Test ON dbo.{tableName} ([Col1])";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Pass @UpdateFillFactor = 0 — the FillFactor in JSON should be ignored
        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_Test]"", ""IndexColumns"": ""[Col1]"", ""FillFactor"": 80}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0, @UpdateFillFactor = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // fill_factor of 0 means "use server default" — it should remain 0 (unchanged)
        cmd.CommandText = $@"
SELECT fill_factor FROM sys.indexes
WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = 'IDX_Test'";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(0));

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldHandleMultipleIndexModifications()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Multi_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Col1 INT NOT NULL, Col2 INT NOT NULL, Col3 INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_One ON dbo.{tableName} ([Col1])
CREATE NONCLUSTERED INDEX IDX_Two ON dbo.{tableName} ([Col2])";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [
            {{""Name"": ""[IDX_One]"", ""IndexColumns"": ""[Col1]"", ""IncludeColumns"": ""[Col3]""}},
            {{""Name"": ""[IDX_Two]"", ""IndexColumns"": ""[Col2], [Col3]""}}
        ]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // IDX_One should have Col3 as an include column
        cmd.CommandText = $@"
SELECT COL_NAME(ic.[object_id], ic.column_id) FROM sys.index_columns ic
JOIN sys.indexes i ON ic.[object_id] = i.[object_id] AND ic.index_id = i.index_id
WHERE i.[object_id] = OBJECT_ID('dbo.{tableName}') AND i.[name] = 'IDX_One' AND ic.is_included_column = 1";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Col3"), "IDX_One should have Col3 as include column");

        // IDX_Two should have 2 key columns
        cmd.CommandText = $@"
SELECT COUNT(*) FROM sys.index_columns ic
JOIN sys.indexes i ON ic.[object_id] = i.[object_id] AND ic.index_id = i.index_id
WHERE i.[object_id] = OBJECT_ID('dbo.{tableName}') AND i.[name] = 'IDX_Two' AND ic.is_included_column = 0";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(2), "IDX_Two should have 2 key columns");

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldRenameIndex()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Rename_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_WrongName ON dbo.{tableName} (Id)";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_RightName]"", ""OldName"": ""[IDX_WrongName]"", ""IndexColumns"": ""[Id]""}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.{tableName}'), 'IDX_RightName', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True, "IDX_RightName should exist after rename");

        cmd.CommandText = $"SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.{tableName}'), 'IDX_WrongName', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False, "IDX_WrongName should no longer exist after rename");

        conn.Close();
    }

    [Test]
    public void IndexOnlyQuench_ShouldModifyIndexCompression()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IdxOnly_Compress_{uniqueId}";

        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE dbo.{tableName} (Id INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_CompressionChange ON dbo.{tableName} (Id) WITH (DATA_COMPRESSION = NONE)";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $@"[{{""Schema"": ""[dbo]"", ""Name"": ""[{tableName}]"", ""Indexes"": [{{""Name"": ""[IDX_CompressionChange]"", ""IndexColumns"": ""[Id]"", ""CompressionType"": ""PAGE""}}]}}]";
        cmd.CommandText = $"EXEC SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
SELECT p.data_compression_desc FROM sys.partitions p
JOIN sys.indexes i ON p.[object_id] = i.[object_id] AND p.index_id = i.index_id
WHERE i.[object_id] = OBJECT_ID('dbo.{tableName}') AND i.[name] = 'IDX_CompressionChange'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("PAGE"));

        conn.Close();
    }
}
