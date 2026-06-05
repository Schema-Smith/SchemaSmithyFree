// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

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
        cmd.CommandText = $"CREATE TABLE dbo.{tableName} (Id INT NOT NULL, Title VARCHAR(200) NULL); " +
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
        cmd.CommandText = "CREATE TABLE dbo.FtVariant_SwitchGate (ActiveCatalog VARCHAR(50)); " +
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

    private static string TwoVariantJson(string tableName, string matchDbName, string nonMatchDbName) =>
        GatedTwoVariantJson(tableName, $"DB_NAME() = '{matchDbName}'", $"DB_NAME() = '{nonMatchDbName}'");

    private static string GatedTwoVariantJson(string tableName, string expressionA, string expressionB) => $$"""
        [
          { "FullTextCatalog": "[FT_Catalog]", "KeyIndex": "[UDX_{{tableName}}]", "Columns": "[Title]", "ChangeTracking": "OFF", "ShouldApplyExpression": "{{expressionA}}" },
          { "FullTextCatalog": "[FT_Catalog2]", "KeyIndex": "[UDX_{{tableName}}]", "Columns": "[Title]", "ChangeTracking": "OFF", "ShouldApplyExpression": "{{expressionB}}" }
        ]
        """;
}
