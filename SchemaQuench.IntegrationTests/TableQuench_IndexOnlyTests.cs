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
}
