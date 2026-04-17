// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests.MySQL;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs.IntegrationTests.MySQL;

[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class SchemaTongsTests
{
    private string _integrationDb = "";
    private string _connectionString = "";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        FixtureSetup.EnsureInitialized();
        _connectionString = FixtureSetup.ConnectionString;
        _integrationDb = GenerateUniqueDBName("TongsTest");
        CreateTestDatabase();
    }

    [Test]
    public void ShouldCastTables()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Tables"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            // Product.json, Template.json, json-schemas, and table JSON
            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Tables") && s.EndsWithIgnoringCase(".json")), Arg.Any<string>());

            config["ShouldCast:Tables"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastViews()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Views"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Views") && s.EndsWithIgnoringCase("TestView.sql")), Arg.Any<string>());

            config["ShouldCast:Views"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastStoredProcedures()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Procedures"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Procedures") && s.EndsWithIgnoringCase("TestProcedure.sql")), Arg.Any<string>());

            config["ShouldCast:Procedures"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastUserDefinedFunctions()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Functions"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Functions") && s.EndsWithIgnoringCase("TestFunction.sql")), Arg.Any<string>());

            config["ShouldCast:Functions"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastTableTriggers()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:TableTriggers"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Triggers") && s.EndsWithIgnoringCase("TestTrigger.sql")), Arg.Any<string>());

            config["ShouldCast:TableTriggers"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastEvents()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Events"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Events") && s.EndsWithIgnoringCase("TestEvent.sql")), Arg.Any<string>());

            config["ShouldCast:Events"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldExtractMultipleObjectTypes()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Tables"] = "true";
            config["ShouldCast:Views"] = "true";
            config["ShouldCast:Procedures"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            // Should have extracted tables, views, and procedures
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Tables") && s.EndsWithIgnoringCase(".json")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Views") && s.EndsWithIgnoringCase(".sql")), Arg.Any<string>());
            file.Received().WriteAllText(Arg.Is<string>(s => s.Contains("Procedures") && s.EndsWithIgnoringCase(".sql")), Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldLogProgressMessages()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Tables"] = "true";

            var tongs = new SchemaTongs(Platform.MySQL);
            tongs.CastTemplate();

            // Verify progress was logged
            progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Kindling The Forge")));
            progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Casting Table Structures")));
            progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Casting Summary")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabase();
    }

    private IConfigurationRoot SetupConfig()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);

        // Map MySQL-specific config to Source:* keys used by SchemaTongs.GetConnection
        config["Source:Server"] = config["MySQL:Server"] ?? "127.0.0.1";
        config["Source:Port"] = config["MySQL:Port"];
        config["Source:User"] = config["MySQL:User"];
        config["Source:Password"] = config["MySQL:Password"];
        config["Source:Schema"] = _integrationDb;
        var connProps = ConnectionString.ReadProperties(config, "MySQL:ConnectionProperties");
        foreach (var prop in connProps)
            config[$"Source:ConnectionProperties:{prop.Key}"] = prop.Value;

        config["ShouldCast:Tables"] = "false";
        config["ShouldCast:Functions"] = "false";
        config["ShouldCast:Views"] = "false";
        config["ShouldCast:Procedures"] = "false";
        config["ShouldCast:TableTriggers"] = "false";
        config["ShouldCast:Events"] = "false";

        FactoryContainer.Register<IConfigurationRoot>(config);
        return config;
    }

    private void CreateTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString + "Database=information_schema;");
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_integrationDb}`;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"USE `{_integrationDb}`;";
        cmd.ExecuteNonQuery();

        ForgeKindler.KindleTheForge(cmd, Platform.MySQL);

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS TestTable (
    Column1 INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Column2 VARCHAR(200) NULL,
    Column3 BIT DEFAULT 0
);";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE INDEX idx_TestTable_Column2 ON TestTable (Column2);";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
DROP VIEW IF EXISTS TestView;
CREATE VIEW TestView AS SELECT * FROM TestTable;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
DROP FUNCTION IF EXISTS TestFunction;
CREATE FUNCTION TestFunction(param INT) RETURNS INT
DETERMINISTIC
RETURN param;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
DROP PROCEDURE IF EXISTS TestProcedure;
CREATE PROCEDURE TestProcedure(IN param INT)
BEGIN
    SELECT param;
END;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
DROP TRIGGER IF EXISTS TestTrigger;
CREATE TRIGGER TestTrigger AFTER INSERT ON TestTable
FOR EACH ROW
BEGIN
    SET @dummy = 1;
END;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"
DROP EVENT IF EXISTS TestEvent;
CREATE EVENT TestEvent
  ON SCHEDULE EVERY 1 DAY
  ON COMPLETION PRESERVE
  DISABLE
  COMMENT 'Test event for integration tests'
  DO BEGIN
    SET @dummy = 1;
  END;";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabase()
    {
        try
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString + "Database=information_schema;");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{_integrationDb}`;";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
