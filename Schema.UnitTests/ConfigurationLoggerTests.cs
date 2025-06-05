using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Schema.Utility;

namespace Schema.UnitTests;

public class ConfigurationLoggerTests
{
    [Test]
    public void ShouldLogConfiguration()
    {
        ConfigurationLogger.LogConfiguration(GenerateConfiguration(), GetLogLine);
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var expectedOutput = new List<string>
        {
            $"Version: {FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion}",
            "Configuration:",
            "  SchemaPackagePath: packagePath",
            "  Target: ",
            "    Password: **********",
            "    Server: localhost",
            "    User: testuser",
            "",
            ""
        };
        Assert.That(_logMessages, Is.EqualTo(expectedOutput));
    }


    private static IConfigurationRoot GenerateConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();
        var configValues = new Dictionary<string, string>
        {
            ["Target:Server"] = "localhost",
            ["Target:User"] = "testuser",
            ["Target:Password"] = "testpassword",
            ["SchemaPackagePath"] = "packagePath"
        };
        configBuilder.AddInMemoryCollection(configValues);
        return configBuilder.Build();
    }

    private readonly List<string> _logMessages = [];

    private void GetLogLine(string msg)
    {
        _logMessages.Add(msg);
    }
}
