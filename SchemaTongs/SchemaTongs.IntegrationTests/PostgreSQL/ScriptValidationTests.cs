// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.IO;
using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;
using log4net;
using NSubstitute;

namespace SchemaTongs.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[Category("Validation")]
public class ScriptValidationTests
{
    private string _integrationDb = "";
    private string _connectionString;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var connProps = ConnectionString.ReadProperties(config, "PostgreSQL:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["PostgreSQL:Server"], "postgres",
            config["PostgreSQL:User"], config["PostgreSQL:Password"], config["PostgreSQL:Port"], connProps);
        _integrationDb = GenerateUniqueDBName("TongsValidation");

        CreateTestDatabase();
    }

    [Test]
    public void ValidScript_ProducesSqlFile()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        file.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Functions"] = "true";
            config["ShouldCast:ValidateScripts"] = "true";

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            tongs.CastTemplate();

            // Valid function produces a .sql file
            file.Received().WriteAllText(
                Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Functions", "public.fn_validtest.sql"))),
                Arg.Any<string>());

            // No .sqlerror for the valid function
            file.DidNotReceive().WriteAllText(
                Arg.Is<string>(s => s.EndsWithIgnoringCase("fn_validtest.sqlerror")),
                Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void InvalidScript_ProducesSqulerrorFile()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        file.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Functions"] = "true";
            config["ShouldCast:ValidateScripts"] = "true";
            config["ShouldCast:SaveInvalidScripts"] = "true";

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            tongs.CastTemplate();

            // Invalid function produces a .sqlerror file
            file.Received().WriteAllText(
                Arg.Is<string>(s => s.EndsWithIgnoringCase("public.fn_invalidtest.sqlerror")),
                Arg.Any<string>());

            // The .sql file is deleted after validation fails
            file.Received().Delete(
                Arg.Is<string>(s => s.EndsWithIgnoringCase("public.fn_invalidtest.sql")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void InvalidScript_GeneratesCleanupScript()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        file.Exists(Arg.Any<string>()).Returns(false);
        directory.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Functions"] = "true";
            config["ShouldCast:ValidateScripts"] = "true";

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            tongs.CastTemplate();

            // Cleanup script contains DROP FUNCTION with PostgreSQL quoting
            file.Received().WriteAllText(
                Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Logs", "_InvalidObjectCleanup.sql"))),
                Arg.Is<string>(s => s.Contains("DROP FUNCTION") && s.Contains("\"public\".\"fn_invalidtest\"")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
    }

    private IConfigurationRoot SetupConfig()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);

        config["Source:Server"] = config["PostgreSQL:Server"] ?? "127.0.0.1";
        config["Source:Port"] = config["PostgreSQL:Port"];
        config["Source:User"] = config["PostgreSQL:User"];
        config["Source:Password"] = config["PostgreSQL:Password"];
        config["Source:database"] = _integrationDb;
        var connProps = ConnectionString.ReadProperties(config, "PostgreSQL:ConnectionProperties");
        foreach (var prop in connProps)
            config[$"Source:ConnectionProperties:{prop.Key}"] = prop.Value;

        config["ShouldCast:Tables"] = "false";
        config["ShouldCast:Schemas"] = "false";
        config["ShouldCast:DomainTypes"] = "false";
        config["ShouldCast:EnumTypes"] = "false";
        config["ShouldCast:CompositeTypes"] = "false";
        config["ShouldCast:Functions"] = "false";
        config["ShouldCast:Aggregates"] = "false";
        config["ShouldCast:Procedures"] = "false";
        config["ShouldCast:Sequences"] = "false";
        config["ShouldCast:Rules"] = "false";
        config["ShouldCast:TableTriggers"] = "false";
        config["ShouldCast:Views"] = "false";
        FactoryContainer.Register<IConfigurationRoot>(config);
        return config;
    }

    private void CreateTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"CREATE DATABASE ""{_integrationDb}"";";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationDb);
        ForgeKindler.KindleTheForge(cmd, Platform.PostgreSQL);

        // Create a table for the invalid function to reference, then a valid and invalid SQL-language function.
        // SQL-language functions do not register OID dependencies on referenced tables, so the table can be
        // dropped independently — leaving the function in pg_proc but broken (fails at creation time
        // when the validator tries to recreate it with a temp name).
        cmd.CommandText = @"
CREATE TABLE ""public"".""reftable"" (""id"" INT NOT NULL, ""name"" VARCHAR(100) NULL);

CREATE FUNCTION ""public"".""fn_validtest""() RETURNS int LANGUAGE sql AS 'SELECT 1';

CREATE FUNCTION ""public"".""fn_invalidtest""() RETURNS TABLE(""id"" int, ""name"" varchar) LANGUAGE sql AS
    'SELECT ""id"", ""name"" FROM ""public"".""reftable""';

DROP TABLE ""public"".""reftable"";
";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace(" - ", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabases()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        DropOneDatabase(cmd, _integrationDb);
        conn.Close();
    }

    private static void DropOneDatabase(IDbCommand cmd, string dbName)
    {
        cmd.CommandText = $@"DROP DATABASE IF EXISTS ""{dbName}"" WITH (FORCE);";
        cmd.ExecuteNonQuery();
    }
}
