// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using log4net;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SchemaQuench.UnitTests;

public class DatabaseQuencherTests
{
    [Test]
    public void ShouldCallKindlingForge()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var connectionFactory = Substitute.For<ISqlConnectionFactory>();
        var connection = Substitute.For<IDbConnection>();
        var command = Substitute.For<IDbCommand>();

        var configValues = new Dictionary<string, string>
        {
            ["SchemaPackagePath"] = "badPath"
        };

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();

        connectionFactory.GetSqlConnection(Arg.Any<string>()).Returns(connection);
        connection.CreateCommand().Returns(command);
        var commands = new List<string>();
        command.When(c => c.ExecuteNonQuery()).Do(_ => commands.Add(command.CommandText));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(connectionFactory);
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);

            var template = new Template
            {
                Name = "TestKindlingForge",
                FilePath = "TestFilePath",
                DatabaseIdentificationScript = "",
                VersionStampScript = "",
                TableSchema = "[]"
            };

            var quencher = new DatabaseQuencher("TestKindlingForge", template, "StrapMe", false, "0", "0");
            quencher.Quench();

            var expectedBoostrapCommand = commands.FirstOrDefault(c => c.Contains("CREATE OR ALTER PROCEDURE SchemaSmith.TableQuench"));
            Assert.That(expectedBoostrapCommand, Is.Not.Null);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void TrackRunOnceMigrations_WhenFalse_ShouldNotInsertCompletedEntries()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var connectionFactory = Substitute.For<ISqlConnectionFactory>();
        var connection = Substitute.For<IDbConnection>();
        var command = Substitute.For<IDbCommand>();

        var configValues = new Dictionary<string, string>
        {
            ["SchemaPackagePath"] = "badPath"
        };

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();

        connectionFactory.GetSqlConnection(Arg.Any<string>()).Returns(connection);
        connection.CreateCommand().Returns(command);

        var reader = Substitute.For<IDataReader>();
        reader.Read().Returns(false);
        command.ExecuteReader().Returns(reader);

        var commands = new List<string>();
        command.When(c => c.ExecuteNonQuery()).Do(_ => commands.Add(command.CommandText));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(connectionFactory);
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);

            var template = new Template
            {
                Name = "TestTracking",
                FilePath = "TestFilePath",
                DatabaseIdentificationScript = "",
                VersionStampScript = "",
                TableSchema = "[]"
            };

            var quencher = new DatabaseQuencher("TestProduct", template, "TestDB", true, "0", "0",
                trackRunOnceMigrations: false);
            quencher.Quench();

            var insertCommands = commands.Where(c => c.Contains("INSERT SchemaSmith.CompletedMigrationScripts")).ToList();
            Assert.That(insertCommands, Is.Empty, "Should not insert tracking entries when TrackRunOnceMigrations is false");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void TrackRunOnceMigrations_WhenFalse_ShouldNotPruneObsoleteEntries()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var connectionFactory = Substitute.For<ISqlConnectionFactory>();
        var connection = Substitute.For<IDbConnection>();
        var command = Substitute.For<IDbCommand>();

        var configValues = new Dictionary<string, string>
        {
            ["SchemaPackagePath"] = "badPath"
        };

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();

        connectionFactory.GetSqlConnection(Arg.Any<string>()).Returns(connection);
        connection.CreateCommand().Returns(command);

        var reader = Substitute.For<IDataReader>();
        reader.Read().Returns(false);
        command.ExecuteReader().Returns(reader);

        var commands = new List<string>();
        command.When(c => c.ExecuteNonQuery()).Do(_ => commands.Add(command.CommandText));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(connectionFactory);
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);

            var template = new Template
            {
                Name = "TestPruning",
                FilePath = "TestFilePath",
                DatabaseIdentificationScript = "",
                VersionStampScript = "",
                TableSchema = "[]"
            };

            var quencher = new DatabaseQuencher("TestProduct", template, "TestDB", true, "0", "0",
                trackRunOnceMigrations: false);
            quencher.Quench();

            var deleteCommands = commands.Where(c => c.Contains("DELETE SchemaSmith.CompletedMigrationScripts")).ToList();
            Assert.That(deleteCommands, Is.Empty, "Should not prune obsolete entries when TrackRunOnceMigrations is false");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void PruneObsoleteMigrationTracking_WhenFalse_ShouldNotDeleteObsoleteEntries()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var connectionFactory = Substitute.For<ISqlConnectionFactory>();
        var connection = Substitute.For<IDbConnection>();
        var command = Substitute.For<IDbCommand>();

        var configValues = new Dictionary<string, string>
        {
            ["SchemaPackagePath"] = "badPath"
        };

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();

        connectionFactory.GetSqlConnection(Arg.Any<string>()).Returns(connection);
        connection.CreateCommand().Returns(command);

        var reader = Substitute.For<IDataReader>();
        reader.Read().Returns(false);
        command.ExecuteReader().Returns(reader);

        var commands = new List<string>();
        command.When(c => c.ExecuteNonQuery()).Do(_ => commands.Add(command.CommandText));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(connectionFactory);
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);

            var template = new Template
            {
                Name = "TestPruningOff",
                FilePath = "TestFilePath",
                DatabaseIdentificationScript = "",
                VersionStampScript = "",
                TableSchema = "[]"
            };

            var quencher = new DatabaseQuencher("TestProduct", template, "TestDB", true, "0", "0",
                trackRunOnceMigrations: true, pruneObsoleteMigrationTracking: false);
            quencher.Quench();

            var deleteCommands = commands.Where(c => c.Contains("DELETE SchemaSmith.CompletedMigrationScripts")).ToList();
            Assert.That(deleteCommands, Is.Empty, "Should not prune obsolete entries when PruneObsoleteMigrationTracking is false");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}
