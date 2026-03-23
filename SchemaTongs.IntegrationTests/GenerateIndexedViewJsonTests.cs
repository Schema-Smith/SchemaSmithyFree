// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Newtonsoft.Json.Linq;
using Schema.DataAccess;
using Schema.Utility;

namespace SchemaTongs.IntegrationTests;

public class GenerateIndexedViewJsonTests
{
    private string _connectionString;
    private string _testDb;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaTongs", null);
        _connectionString = ConnectionString.Build(config["Source:Server"], "master", config["Source:User"], config["Source:Password"]);
        _testDb = GenerateUniqueDBName("GenIVJsonTest");
        CreateTestDatabase();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        DropTestDatabase();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForIndexedView()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_testDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.GenerateIndexedViewJson('dbo', 'vw_JsonTest')";
        var json = cmd.ExecuteScalar()?.ToString();

        Assert.That(json, Is.Not.Null.And.Not.Empty, "Function should return JSON");

        var obj = JObject.Parse(json);
        Assert.Multiple(() =>
        {
            // fn_SafeBracketWrap wraps schema and name in square brackets
            Assert.That(obj["Schema"]?.ToString(), Is.EqualTo("[dbo]"));
            Assert.That(obj["Name"]?.ToString(), Is.EqualTo("[vw_JsonTest]"));
            Assert.That(obj["Definition"]?.ToString(), Does.Not.Contain("CREATE VIEW"), "Definition should not contain CREATE VIEW");
            Assert.That(obj["Definition"]?.ToString(), Does.Contain("SELECT"), "Definition should contain SELECT");

            var indexes = obj["Indexes"] as JArray;
            Assert.That(indexes, Is.Not.Null);
            Assert.That(indexes, Has.Count.GreaterThanOrEqualTo(1));

            // Clustered index should come first (ORDER BY type = 1 first)
            var firstIndex = indexes[0];
            Assert.That(firstIndex["Clustered"]?.Value<bool>(), Is.True, "First index should be clustered");
            // fn_SafeBracketWrap wraps index name in square brackets
            Assert.That(firstIndex["Name"]?.ToString(), Is.EqualTo("[CIX_vw_JsonTest]"));
        });

        conn.Close();
    }

    [Test]
    public void ShouldHandleIndexedViewWithNoNonclusteredIndexes()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_testDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.GenerateIndexedViewJson('dbo', 'vw_ClusteredOnly')";
        var json = cmd.ExecuteScalar()?.ToString();

        Assert.That(json, Is.Not.Null.And.Not.Empty, "Function should return JSON for clustered-only view");

        var obj = JObject.Parse(json);
        var indexes = obj["Indexes"] as JArray;
        Assert.That(indexes, Has.Count.EqualTo(1), "Should have only the clustered index");
        Assert.That(indexes[0]["Clustered"]?.Value<bool>(), Is.True);

        conn.Close();
    }

    [Test]
    public void ShouldHandleMultipleIndexedViews()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_testDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.GenerateIndexedViewJson('dbo', 'vw_JsonTest')";
        var json1 = cmd.ExecuteScalar()?.ToString();
        Assert.That(json1, Is.Not.Null);

        cmd.CommandText = "SELECT SchemaSmith.GenerateIndexedViewJson('dbo', 'vw_ClusteredOnly')";
        var json2 = cmd.ExecuteScalar()?.ToString();
        Assert.That(json2, Is.Not.Null);

        var obj1 = JObject.Parse(json1);
        var obj2 = JObject.Parse(json2);
        Assert.That(obj1["Name"]?.ToString(), Is.Not.EqualTo(obj2["Name"]?.ToString()), "Views should be independent");

        conn.Close();
    }

    [Test]
    public void ShouldReturnNullForNonExistentView()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_testDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.GenerateIndexedViewJson('dbo', 'vw_DoesNotExist')";
        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.Null.Or.EqualTo(DBNull.Value), "Function should return NULL for non-existent view");

        conn.Close();
    }

    [Test]
    public void ShouldReturnNullForNonIndexedView()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_testDb);
        using var cmd = conn.CreateCommand();

        // vw_Plain is a regular (non-indexed) view
        cmd.CommandText = "SELECT SchemaSmith.GenerateIndexedViewJson('dbo', 'vw_Plain')";
        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.Null.Or.EqualTo(DBNull.Value), "Function should return NULL for a view without a clustered index");

        conn.Close();
    }

    [Test]
    public void ShouldIncludeNonClusteredIndexesInOutput()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_testDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.GenerateIndexedViewJson('dbo', 'vw_JsonTest')";
        var json = cmd.ExecuteScalar()?.ToString();

        Assert.That(json, Is.Not.Null.And.Not.Empty);

        var obj = JObject.Parse(json);
        var indexes = obj["Indexes"] as JArray;

        Assert.That(indexes, Is.Not.Null);
        Assert.That(indexes, Has.Count.EqualTo(2), "Should have clustered and nonclustered index");

        var nonClustered = indexes[1];
        Assert.That(nonClustered["Clustered"]?.Value<bool>(), Is.False, "Second index should be nonclustered");
        Assert.That(nonClustered["Name"]?.ToString(), Is.EqualTo("[IX_vw_JsonTest_Category]"));

        conn.Close();
    }

    private void CreateTestDatabase()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{_testDb}]";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_testDb);
        ForgeKindler.KindleTheForge(cmd);

        cmd.CommandText = @"
CREATE TABLE dbo.IVJsonTestTable (Id INT NOT NULL, Category NVARCHAR(50), Amount DECIMAL(10,2))";
        cmd.ExecuteNonQuery();

        // Indexed view with both clustered and nonclustered indexes.
        // SUM must reference a non-nullable expression for SQL Server to allow a clustered index.
        cmd.CommandText = @"
CREATE VIEW dbo.vw_JsonTest WITH SCHEMABINDING
AS SELECT Id, Category, SUM(ISNULL(Amount, 0)) AS TotalAmount, COUNT_BIG(*) AS Cnt
FROM dbo.IVJsonTestTable GROUP BY Id, Category";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE UNIQUE CLUSTERED INDEX CIX_vw_JsonTest ON dbo.vw_JsonTest (Id, Category)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE NONCLUSTERED INDEX IX_vw_JsonTest_Category ON dbo.vw_JsonTest (Category)";
        cmd.ExecuteNonQuery();

        // Indexed view with only a clustered index
        cmd.CommandText = @"
CREATE VIEW dbo.vw_ClusteredOnly WITH SCHEMABINDING
AS SELECT Id, COUNT_BIG(*) AS Cnt
FROM dbo.IVJsonTestTable GROUP BY Id";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE UNIQUE CLUSTERED INDEX CIX_vw_ClusteredOnly ON dbo.vw_ClusteredOnly (Id)";
        cmd.ExecuteNonQuery();

        // Plain (non-indexed) view — used to verify function returns NULL for non-indexed views
        cmd.CommandText = @"
CREATE VIEW dbo.vw_Plain
AS SELECT Id, Category FROM dbo.IVJsonTestTable";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    private void DropTestDatabase()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
IF DB_ID('{_testDb}') IS NOT NULL
    ALTER DATABASE [{_testDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{_testDb}];";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private static string GenerateUniqueDBName(string baseName)
    {
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_")[..8];
        return $"{baseName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }
}
