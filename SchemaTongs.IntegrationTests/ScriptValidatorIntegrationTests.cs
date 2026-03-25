// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Utility;

namespace SchemaTongs.IntegrationTests;

[TestFixture]
public class ScriptValidatorIntegrationTests
{
    private string _connectionString;
    private string _integrationDb;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaTongs", null);
        var connectionProperties = ConnectionString.ReadProperties(config, "Source:ConnectionProperties");
        _connectionString = ConnectionString.Build(config["Source:Server"], "master", config["Source:User"], config["Source:Password"], config["Source:Port"], connectionProperties);
        _integrationDb = GenerateUniqueDBName("ValidatorTest");
        CreateTestDatabase();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabase();
    }

    [Test]
    public void ValidateGuidRename_ValidView_ReturnsValid()
    {
        using var conn = GetConnection();
        var script = "CREATE OR ALTER VIEW [dbo].[TestView] AS SELECT 1 AS Col";

        var result = ScriptValidator.ValidateScript(conn, script, "VIEW");

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ValidateGuidRename_ValidFunction_ReturnsValid()
    {
        using var conn = GetConnection();
        var script = "CREATE OR ALTER FUNCTION [dbo].[fn_Test](@x INT) RETURNS INT AS BEGIN RETURN @x END";

        var result = ScriptValidator.ValidateScript(conn, script, "FUNCTION");

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ValidateGuidRename_ValidProcedure_ReturnsValid()
    {
        using var conn = GetConnection();
        var script = "CREATE OR ALTER PROCEDURE [dbo].[sp_Test] AS SELECT 1";

        var result = ScriptValidator.ValidateScript(conn, script, "PROCEDURE");

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ValidateGuidRename_InvalidView_ReturnsInvalidWithError()
    {
        using var conn = GetConnection();
        var script = "CREATE OR ALTER VIEW [dbo].[BadView] AS SELECT * FROM [dbo].[NonExistentTable]";

        var result = ScriptValidator.ValidateScript(conn, script, "VIEW");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void ValidateGuidRename_UnrewritableScript_ReturnsValid()
    {
        using var conn = GetConnection();
        var script = "SELECT 1";

        var result = ScriptValidator.ValidateScript(conn, script, "VIEW");

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ValidateParseOnly_ValidSelectScript_ReturnsValid()
    {
        // ParseOnly validates syntax without execution; simple DML passes cleanly
        using var conn = GetConnection();
        var script = "SELECT 1 AS Col";

        var result = ScriptValidator.ValidateScript(conn, script, "TRIGGER");

        Assert.That(result.IsValid, Is.True, $"Expected valid but got error: {result.ErrorMessage}");
    }

    [Test]
    public void ValidateParseOnly_CreateTriggerScript_ReturnsInvalid()
    {
        // SET PARSEONLY ON cannot be used with CREATE TRIGGER (SQL Server limitation);
        // ValidateParseOnly returns invalid for CREATE TRIGGER scripts
        using var conn = GetConnection();
        var script = "CREATE OR ALTER TRIGGER [dbo].[trg_Test] ON [dbo].[SomeTable] AFTER INSERT AS SELECT 1";

        var result = ScriptValidator.ValidateScript(conn, script, "TRIGGER");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("parseonly"));
        });
    }

    [Test]
    public void ValidateParseOnly_SyntaxError_ReturnsInvalid()
    {
        using var conn = GetConnection();
        var script = "CREATE TRIGGER [dbo].[trg_Bad] ON AS";

        var result = ScriptValidator.ValidateScript(conn, script, "TRIGGER");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void ValidateScript_TriggerObjectType_RoutesToParseOnly()
    {
        // ParseOnly route is proven by the error message: "Cannot set or reset the 'parseonly' option"
        // is specific to SET PARSEONLY ON wrapping. GuidRename would produce a different error
        // (object reference or execution error). This confirms TRIGGER routes to ParseOnly.
        using var conn = GetConnection();
        var script = "CREATE OR ALTER TRIGGER [dbo].[trg_Route] ON [dbo].[TableThatDoesNotExist] AFTER INSERT AS SELECT 1";

        var result = ScriptValidator.ValidateScript(conn, script, "TRIGGER");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("parseonly"));
        });
    }

    [Test]
    public void ValidateScript_ViewObjectType_RoutesToGuidRename()
    {
        // A view referencing a nonexistent table fails GuidRename (which actually executes)
        // but would pass ParseOnly (syntax-only check). This proves routing to GuidRename.
        using var conn = GetConnection();
        var script = "CREATE OR ALTER VIEW [dbo].[BadRouteView] AS SELECT * FROM [dbo].[NonExistentTable]";

        var result = ScriptValidator.ValidateScript(conn, script, "VIEW");

        Assert.That(result.IsValid, Is.False);
    }

    private IDbConnection GetConnection()
    {
        var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_integrationDb);
        return conn;
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void CreateTestDatabase()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{_integrationDb}];";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private void DropTestDatabase()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
IF DB_ID('{_integrationDb}') IS NOT NULL
  ALTER DATABASE [{_integrationDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{_integrationDb}];
";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
