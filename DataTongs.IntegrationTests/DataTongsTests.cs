using System;
using System.Data;
using log4net;
using NSubstitute;
using Schema.DataAccess;
using Schema.Isolators;
using Schema.Utility;

namespace DataTongs.IntegrationTests;

public class DataTongsTests
{
    private string _integrationDb = "";
    private string _connectionString;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", null);
        _connectionString = ConnectionString.Build(config["Source:Server"], "master", config["Source:User"], config["Source:Password"]);
        _integrationDb = GenerateUniqueDBName("DataTongs");

        CreateTestDatabases();
    }

    [Test]
    public void SchouldTongTableData()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_integrationDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DROP TABLE IF EXISTS [dbo].[TestTable];
CREATE TABLE [dbo].[TestTable] (
  [Id] INT NOT NULL PRIMARY KEY,
  [Name] NVARCHAR(100) NOT NULL,
  [Description] NVARCHAR(500) NULL,
  [CreatedDate] DATE NOT NULL DEFAULT GETDATE()
);

INSERT INTO [dbo].[TestTable] ([Id], [Name], Description) 
  VALUES (1, 'Test Item 1', 'This is a test item.'),
         (2, 'Test Item 2', 'This is another test item.'),
         (3, 'Test Item 3', 'This is yet another test item.');
";
        cmd.ExecuteNonQuery();

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

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", null);
            config["Source:database"] = _integrationDb;
            config["Tables:dbo.TestTable"] = "Id";

            var tongs = new DataTongs();
            tongs.CastData();

            file.Received(1).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Populate dbo.TestTable.sql")), Arg.Is<string>(s => s.ContainsIgnoringCase("\"Description\":\"This is a test item.\",\"Id\":1,\"Name\":\"Test Item 1\"},")));
            errorLog.DidNotReceive().Error(Arg.Any<string>());
            progressLog.DidNotReceive().Error(Arg.Is<string>(s => s.ContainsIgnoringCase("No match columns found")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
    }
    
    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace(" - ", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void CreateTestDatabases()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
CREATE DATABASE [{_integrationDb}];
";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    private void DropTestDatabases()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        DropOneDatabase(cmd, _integrationDb);

        conn.Close();
    }

    private static void DropOneDatabase(IDbCommand cmd, string dbName)
    {
        cmd.CommandText = @$"
IF DB_ID('{dbName}') IS NOT NULL
  ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{dbName}];
";
        cmd.ExecuteNonQuery();
    }
}