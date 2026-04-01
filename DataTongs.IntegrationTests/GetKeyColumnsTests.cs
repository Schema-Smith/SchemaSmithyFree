// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using log4net;
using NSubstitute;
using Schema.DataAccess;
using Schema.Isolators;
using Schema.Utility;

namespace DataTongs.IntegrationTests;

public class GetKeyColumnsTests
{
    private string _integrationDb = "";
    private string _connectionString;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", null);
        var connectionProperties = ConnectionString.ReadProperties(config, "Source:ConnectionProperties");
        _connectionString = ConnectionString.Build(config["Source:Server"], "master", config["Source:User"],
            config["Source:Password"], config["Source:Port"], connectionProperties);
        _integrationDb = GenerateUniqueDBName("KeyColTest");
        CreateTestDatabase();
    }

    [Test]
    public void ShouldDetectPrimaryKey()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_integrationDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[PKTable];
CREATE TABLE [dbo].[PKTable] (
    [Id] INT NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(50) NOT NULL
);
INSERT INTO [dbo].[PKTable] ([Id], [Name]) VALUES (1, 'Alice'), (2, 'Bob');
";
        cmd.ExecuteNonQuery();

        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        var progressLog = Substitute.For<ILog>();
        var errorLog = Substitute.For<ILog>();
        string capturedScript = null;
        file.WriteAllText(Arg.Any<string>(), Arg.Do<string>(s => capturedScript = s));

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            LogFactory.Register("ErrorLog", errorLog);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", null);
            config["Source:database"] = _integrationDb;
            config["Tables:0:Name"] = "dbo.PKTable";

            var tongs = new DataTongs();
            tongs.CastData();

            Assert.That(capturedScript, Is.Not.Null, "Expected a merge script to be written");
            Assert.That(capturedScript, Does.Contain("Source.[Id] = Target.[Id]"));
            Assert.That(capturedScript, Does.Not.Contain("IS NULL AND Target."));
            errorLog.DidNotReceive().Error(Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldDetectUniqueIndex_WhenNoPK()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_integrationDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[UniqueOnly];
CREATE TABLE [dbo].[UniqueOnly] (
    [Code] NVARCHAR(10) NOT NULL,
    [Val] INT NOT NULL
);
CREATE UNIQUE INDEX UX_Code ON [dbo].[UniqueOnly] ([Code]);
INSERT INTO [dbo].[UniqueOnly] ([Code], [Val]) VALUES ('A', 1), ('B', 2);
";
        cmd.ExecuteNonQuery();

        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        var progressLog = Substitute.For<ILog>();
        var errorLog = Substitute.For<ILog>();
        string capturedScript = null;
        file.WriteAllText(Arg.Any<string>(), Arg.Do<string>(s => capturedScript = s));

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            LogFactory.Register("ErrorLog", errorLog);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", null);
            config["Source:database"] = _integrationDb;
            config["Tables:0:Name"] = "dbo.UniqueOnly";

            var tongs = new DataTongs();
            tongs.CastData();

            Assert.That(capturedScript, Is.Not.Null, "Expected a merge script to be written");
            Assert.That(capturedScript, Does.Contain("Source.[Code] = Target.[Code]"));
            errorLog.DidNotReceive().Error(Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldHandleNullableColumnsInUniqueIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_integrationDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[NullableKey];
CREATE TABLE [dbo].[NullableKey] (
    [Id] INT NOT NULL,
    [Tag] NVARCHAR(50) NULL
);
CREATE UNIQUE INDEX UX_IdTag ON [dbo].[NullableKey] ([Id], [Tag]);
INSERT INTO [dbo].[NullableKey] ([Id], [Tag]) VALUES (1, 'X'), (2, NULL);
";
        cmd.ExecuteNonQuery();

        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        var progressLog = Substitute.For<ILog>();
        var errorLog = Substitute.For<ILog>();
        string capturedScript = null;
        file.WriteAllText(Arg.Any<string>(), Arg.Do<string>(s => capturedScript = s));

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            LogFactory.Register("ErrorLog", errorLog);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", null);
            config["Source:database"] = _integrationDb;
            config["Tables:0:Name"] = "dbo.NullableKey";

            var tongs = new DataTongs();
            tongs.CastData();

            Assert.That(capturedScript, Is.Not.Null, "Expected a merge script to be written");
            Assert.That(capturedScript, Does.Contain("Source.[Id] = Target.[Id]"));
            Assert.That(capturedScript, Does.Contain("Source.[Tag] IS NULL AND Target.[Tag] IS NULL"));
            errorLog.DidNotReceive().Error(Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldLogError_WhenNoKeyAndNoConfig()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_integrationDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[NoKey];
CREATE TABLE [dbo].[NoKey] (
    [Data] NVARCHAR(100) NOT NULL
);
INSERT INTO [dbo].[NoKey] ([Data]) VALUES ('test');
";
        cmd.ExecuteNonQuery();

        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        var progressLog = Substitute.For<ILog>();
        var errorLog = Substitute.For<ILog>();

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            LogFactory.Register("ErrorLog", errorLog);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", null);
            config["Source:database"] = _integrationDb;
            config["Tables:0:Name"] = "dbo.NoKey";

            var tongs = new DataTongs();
            tongs.CastData();

            progressLog.Received(1).Error(Arg.Is<string>(s => s.ContainsIgnoringCase("no primary key")));
            file.DidNotReceive().WriteAllText(
                Arg.Is<string>(s => s.ContainsIgnoringCase("NoKey")),
                Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabase();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace(" - ", "_").Substring(0, 8);
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
