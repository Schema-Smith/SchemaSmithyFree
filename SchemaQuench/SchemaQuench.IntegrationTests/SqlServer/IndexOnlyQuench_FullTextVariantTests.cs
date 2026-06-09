// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

[Category("SqlServer")]
[Parallelizable(scope: ParallelScope.All)]
public class IndexOnlyQuench_FullTextVariantTests : BaseTableQuenchTests
{
    private string TableJson(string tableName, string fullTextJson) => $$"""
        [{
            "Schema": "[dbo]",
            "Name": "[{{tableName}}]",
            "Indexes": [ { "Name": "[UDX_{{tableName}}]", "IndexColumns": "[Id]", "Unique": true } ],
            "FullTextIndex": {{fullTextJson}}
        }]
        """;

    private static void CreateTestTable(DbCommand cmd, string tableName)
    {
        cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}; " +
                          $"CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Title VARCHAR(200) NULL); " +
                          $"CREATE UNIQUE INDEX UDX_{tableName} ON dbo.{tableName} (Id);";
        cmd.ExecuteNonQuery();
    }

    private void RunIndexOnlyQuench(DbCommand cmd, string tableName, string fullTextJson, bool whatIf = false)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = "EXEC SchemaSmith.IndexOnlyQuench @ProductName = @p, @TableDefinitions = @t, @WhatIf = @w";
        cmd.Parameters.Clear();
        var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = _productName; cmd.Parameters.Add(p);
        var t = cmd.CreateParameter(); t.ParameterName = "@t"; t.Value = TableJson(tableName, fullTextJson); cmd.Parameters.Add(t);
        var w = cmd.CreateParameter(); w.ParameterName = "@w"; w.Value = whatIf; cmd.Parameters.Add(w);
        cmd.ExecuteNonQuery();
        cmd.Parameters.Clear();
    }

    private static string DeployedCatalog(DbCommand cmd, string tableName)
    {
        cmd.CommandText = $@"
SELECT c.[name] FROM sys.fulltext_indexes fi WITH (NOLOCK)
  JOIN sys.fulltext_catalogs c WITH (NOLOCK) ON c.fulltext_catalog_id = fi.fulltext_catalog_id
  WHERE fi.[object_id] = OBJECT_ID('dbo.{tableName}')";
        return cmd.ExecuteScalar() as string;
    }

    [Test]
    public void FullText_SingleObjectShape_StillDeploys()
    {
        const string tableName = "FtVariant_BackCompat";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);
        RunIndexOnlyQuench(cmd, tableName,
            """{ "FullTextCatalog": "[FT_Catalog]", "KeyIndex": "[UDX_FtVariant_BackCompat]", "Columns": "[Title]", "ChangeTracking": "OFF" }""");

        Assert.That(DeployedCatalog(cmd, tableName), Is.EqualTo("FT_Catalog"));
        conn.Close();
    }

    [Test]
    public void FullText_VariantArray_SelectsTheMatchingVariant()
    {
        const string tableName = "FtVariant_Select";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);
        RunIndexOnlyQuench(cmd, tableName, TwoVariantJson(tableName, _mainDb, "FtNoSuchDb"));

        Assert.That(DeployedCatalog(cmd, tableName), Is.EqualTo("FT_Catalog"));
        conn.Close();
    }

    [Test]
    public void FullText_RequenchWithMatchingVariant_PerformsNoFullTextWork()
    {
        const string tableName = "FtVariant_NoChurn";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);
        var json = TwoVariantJson(tableName, _mainDb, "FtNoSuchDb");
        RunIndexOnlyQuench(cmd, tableName, json);
        Assert.That(DeployedCatalog(cmd, tableName), Is.EqualTo("FT_Catalog"));

        var captured = new StringBuilder();
        var sqlConn = (SqlConnection)conn;
        SqlInfoMessageEventHandler handler = (_, e) => captured.Append(e.Message);
        sqlConn.InfoMessage += handler;
        try
        {
            RunIndexOnlyQuench(cmd, tableName, json, whatIf: true);
        }
        finally
        {
            sqlConn.InfoMessage -= handler;
        }

        Assert.That(captured.ToString(), Does.Not.Contain("FULLTEXT"));
        conn.Close();
    }

    [Test]
    public void FullText_VariantSwitch_ConvergesToNewlySelectedVariant()
    {
        const string tableName = "FtVariant_Switch";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);
        cmd.CommandText = "DROP TABLE IF EXISTS dbo.FtVariant_SwitchGate; " +
                          "CREATE TABLE dbo.FtVariant_SwitchGate (ActiveCatalog VARCHAR(50)); " +
                          "INSERT INTO dbo.FtVariant_SwitchGate (ActiveCatalog) VALUES ('FT_Catalog');";
        cmd.ExecuteNonQuery();

        var json = GatedTwoVariantJson(tableName,
            "EXISTS (SELECT 1 FROM dbo.FtVariant_SwitchGate WHERE ActiveCatalog = 'FT_Catalog')",
            "EXISTS (SELECT 1 FROM dbo.FtVariant_SwitchGate WHERE ActiveCatalog = 'FT_Catalog2')");

        RunIndexOnlyQuench(cmd, tableName, json);
        Assert.That(DeployedCatalog(cmd, tableName), Is.EqualTo("FT_Catalog"));

        cmd.CommandText = "UPDATE dbo.FtVariant_SwitchGate SET ActiveCatalog = 'FT_Catalog2';";
        cmd.ExecuteNonQuery();

        RunIndexOnlyQuench(cmd, tableName, json);
        Assert.That(DeployedCatalog(cmd, tableName), Is.EqualTo("FT_Catalog2"));
        conn.Close();
    }

    [Test]
    public void FullText_NoVariantMatches_DropsExistingIndex()
    {
        const string tableName = "FtVariant_ZeroMatch";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);
        cmd.CommandText = $"CREATE FULLTEXT INDEX ON dbo.{tableName} (Title) KEY INDEX UDX_{tableName} ON FT_Catalog WITH CHANGE_TRACKING = OFF";
        cmd.ExecuteNonQuery();

        RunIndexOnlyQuench(cmd, tableName, GatedTwoVariantJson(tableName, "1 = 0", "1 = 0"));

        Assert.That(DeployedCatalog(cmd, tableName), Is.Null);
        conn.Close();
    }

    [Test]
    public void FullText_MultipleVariantsMatch_FailsWithMutuallyExclusiveError()
    {
        const string tableName = "FtVariant_DoubleMatch";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);

        var ex = Assert.Throws<SqlException>(() =>
            RunIndexOnlyQuench(cmd, tableName, GatedTwoVariantJson(tableName, "1 = 1", "1 = 1")));
        Assert.That(ex!.Message, Does.Contain("mutually exclusive"));
        conn.Close();
    }

    [Test]
    public void FullText_MultipleVariantsMatch_FailsThroughSharedParser()
    {
        // The shared JSON parser (kindled into the modular table-quench procs via the {{ParseJson}}
        // token) carries the same multi-variant THROW. Exercise it through SchemaSmith.TableQuench.
        const string tableName = "FtVariant_SharedParser";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);

        var json = $$"""
            [{
                "Schema": "[dbo]",
                "Name": "[{{tableName}}]",
                "Columns": [
                    { "Name": "[Id]", "DataType": "INT", "Nullable": false },
                    { "Name": "[Title]", "DataType": "VARCHAR(200)", "Nullable": true }
                ],
                "Indexes": [ { "Name": "[UDX_{{tableName}}]", "IndexColumns": "[Id]", "Unique": true } ],
                "FullTextIndex": {{GatedTwoVariantJson(tableName, "1 = 1", "1 = 1")}}
            }]
            """;

        var ex = Assert.Throws<SqlException>(() =>
        {
            cmd.CommandTimeout = 300;
            cmd.CommandText = "EXEC SchemaSmith.TableQuench @ProductName = @p, @TableDefinitions = @t, @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0";
            cmd.Parameters.Clear();
            var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = _productName; cmd.Parameters.Add(p);
            var t = cmd.CreateParameter(); t.ParameterName = "@t"; t.Value = json; cmd.Parameters.Add(t);
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
        });
        Assert.That(ex!.Message, Does.Contain("mutually exclusive"));
        conn.Close();
    }

    [Test]
    public void FullText_GatedOutSingleDeclaration_IsNotDeployed_IndexOnlyMode()
    {
        const string tableName = "FtVariant_GatedOut";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        CreateTestTable(cmd, tableName);
        RunIndexOnlyQuench(cmd, tableName,
            """{ "FullTextCatalog": "[FT_Catalog]", "KeyIndex": "[UDX_FtVariant_GatedOut]", "Columns": "[Title]", "ChangeTracking": "OFF", "ShouldApplyExpression": "1 = 0" }""");

        Assert.That(DeployedCatalog(cmd, tableName), Is.Null);
        conn.Close();
    }

    private static bool IndexExists(DbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*) FROM sys.indexes WITH (NOLOCK)
  WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = '{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool StatisticExists(DbCommand cmd, string tableName, string statisticName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*) FROM sys.stats WITH (NOLOCK)
  WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = '{statisticName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void RunIndexOnlyQuenchJson(DbCommand cmd, string json)
    {
        cmd.CommandTimeout = 300;
        cmd.CommandText = "EXEC SchemaSmith.IndexOnlyQuench @ProductName = @p, @TableDefinitions = @t, @WhatIf = 0";
        cmd.Parameters.Clear();
        var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = _productName; cmd.Parameters.Add(p);
        var t = cmd.CreateParameter(); t.ParameterName = "@t"; t.Value = json; cmd.Parameters.Add(t);
        cmd.ExecuteNonQuery();
        cmd.Parameters.Clear();
    }

    [Test]
    public void IndexOnly_RegularIndex_HonorsShouldApplyExpression()
    {
        const string tableName = "IxGate_Regular";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}; CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Title VARCHAR(200) NULL);";
        cmd.ExecuteNonQuery();

        var json = $$"""
            [{
                "Schema": "[dbo]",
                "Name": "[{{tableName}}]",
                "Indexes": [
                    { "Name": "[IX_Skip]", "IndexColumns": "[Id]", "ShouldApplyExpression": "1 = 0" },
                    { "Name": "[IX_Keep]", "IndexColumns": "[Title]", "ShouldApplyExpression": "1 = 1" }
                ]
            }]
            """;
        RunIndexOnlyQuenchJson(cmd, json);

        Assert.Multiple(() =>
        {
            Assert.That(IndexExists(cmd, tableName, "IX_Skip"), Is.False, "gated-out index (1=0) must not be created");
            Assert.That(IndexExists(cmd, tableName, "IX_Keep"), Is.True, "gated-in index (1=1) must be created");
        });

        cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void IndexOnly_XmlIndex_HonorsShouldApplyExpression()
    {
        const string tableName = "IxGate_Xml";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        cmd.CommandText = $@"
DROP TABLE IF EXISTS dbo.{tableName};
CREATE TABLE dbo.{tableName} (Id INT NOT NULL CONSTRAINT PK_{tableName} PRIMARY KEY CLUSTERED, Data XML NULL);";
        cmd.ExecuteNonQuery();

        var json = $$"""
            [{
                "Schema": "[dbo]",
                "Name": "[{{tableName}}]",
                "XmlIndexes": [
                    { "Name": "[XI_Skip]", "Column": "[Data]", "IsPrimary": true, "ShouldApplyExpression": "1 = 0" }
                ]
            }]
            """;
        RunIndexOnlyQuenchJson(cmd, json);

        Assert.That(IndexExists(cmd, tableName, "XI_Skip"), Is.False, "gated-out XML index (1=0) must not be created");

        var jsonKeep = $$"""
            [{
                "Schema": "[dbo]",
                "Name": "[{{tableName}}]",
                "XmlIndexes": [
                    { "Name": "[XI_Keep]", "Column": "[Data]", "IsPrimary": true, "ShouldApplyExpression": "1 = 1" }
                ]
            }]
            """;
        RunIndexOnlyQuenchJson(cmd, jsonKeep);
        Assert.That(IndexExists(cmd, tableName, "XI_Keep"), Is.True, "gated-in XML index (1=1) must be created");

        cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void IndexOnly_Statistic_HonorsShouldApplyExpression()
    {
        const string tableName = "IxGate_Stat";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}; CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Title VARCHAR(200) NULL);";
        cmd.ExecuteNonQuery();

        var json = $$"""
            [{
                "Schema": "[dbo]",
                "Name": "[{{tableName}}]",
                "Statistics": [
                    { "Name": "[ST_Skip]", "Columns": "[Id]", "ShouldApplyExpression": "1 = 0" },
                    { "Name": "[ST_Keep]", "Columns": "[Title]", "ShouldApplyExpression": "1 = 1" }
                ]
            }]
            """;
        RunIndexOnlyQuenchJson(cmd, json);

        Assert.Multiple(() =>
        {
            Assert.That(StatisticExists(cmd, tableName, "ST_Skip"), Is.False, "gated-out statistic (1=0) must not be created");
            Assert.That(StatisticExists(cmd, tableName, "ST_Keep"), Is.True, "gated-in statistic (1=1) must be created");
        });

        cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void IndexOnly_CreatingIndex_EchoesVariantNameInOperationMessage()
    {
        const string tableName = "IxVariant_Log";
        var messages = new System.Collections.Generic.List<string>();
        using var conn = (SqlConnection)DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.InfoMessage += (_, e) => { foreach (SqlError err in e.Errors) messages.Add(err.Message); };
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = (DbCommand)conn.CreateCommand();

        cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}; CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Title VARCHAR(200) NULL);";
        cmd.ExecuteNonQuery();
        try
        {
            var json = $$"""
                [{
                    "Schema": "[dbo]",
                    "Name": "[{{tableName}}]",
                    "Indexes": [ { "Name": "[IX_{{tableName}}]", "IndexColumns": "[Id]", "VariantName": "Modern engines" } ]
                }]
                """;
            cmd.CommandTimeout = 300;
            cmd.CommandText = "EXEC SchemaSmith.IndexOnlyQuench @ProductName = @p, @TableDefinitions = @t, @WhatIf = 0";
            cmd.Parameters.Clear();
            var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = _productName; cmd.Parameters.Add(p);
            var t = cmd.CreateParameter(); t.ParameterName = "@t"; t.Value = json; cmd.Parameters.Add(t);
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();

            Assert.That(messages, Has.Some.Contains("(variant: Modern engines)"));
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS dbo.{tableName}";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    private static string TwoVariantJson(string tableName, string matchDbName, string nonMatchDbName) =>
        GatedTwoVariantJson(tableName, $"DB_NAME() = '{matchDbName}'", $"DB_NAME() = '{nonMatchDbName}'");

    private static string GatedTwoVariantJson(string tableName, string expressionA, string expressionB) => $$"""
        [
          { "FullTextCatalog": "[FT_Catalog]", "KeyIndex": "[UDX_{{tableName}}]", "Columns": "[Title]", "ChangeTracking": "OFF", "ShouldApplyExpression": "{{expressionA}}" },
          { "FullTextCatalog": "[FT_Catalog2]", "KeyIndex": "[UDX_{{tableName}}]", "Columns": "[Title]", "ChangeTracking": "OFF", "ShouldApplyExpression": "{{expressionB}}" }
        ]
        """;
}
