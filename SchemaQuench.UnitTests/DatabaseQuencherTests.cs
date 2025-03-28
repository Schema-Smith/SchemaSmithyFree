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
        command.When(c => c.ExecuteNonQuery()).Do(c => commands.Add(command.CommandText));

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

            var expectedBoostrapCommand = commands.FirstOrDefault(c => c.StartsWith("CREATE OR ALTER PROCEDURE [SchemaSmith].[TableQuench]"));
            Assert.That(expectedBoostrapCommand, Is.Not.Null);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}
