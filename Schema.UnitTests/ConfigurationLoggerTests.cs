// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
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
        var version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                      ?? assembly.GetName().Version?.ToString()
                      ?? "unknown";
        var expectedOutput = new List<string>
        {
            $"Version: {version}",
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

    [Test]
    public void ShouldLogVersionLine()
    {
        var config = BuildConfig(new Dictionary<string, string> { ["SomeKey"] = "SomeValue" });
        var logLines = new List<string>();

        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.Some.Matches<string>(s => s.StartsWith("Version:")));
    }

    [Test]
    public void ShouldLogConfigurationHeaderLine()
    {
        var config = BuildConfig(new Dictionary<string, string> { ["Key1"] = "Value1" });
        var logLines = new List<string>();

        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.Some.EqualTo("Configuration:"));
    }

    [Test]
    public void ShouldMaskPasswordValues()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Target:Password"] = "secret123"
        });
        var logLines = new List<string>();

        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.None.Matches<string>(s => s.Contains("secret123")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("**********")));
    }

    [Test]
    public void ShouldMaskPwdValues()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["ConnectionPwd"] = "hidden"
        });
        var logLines = new List<string>();

        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.None.Matches<string>(s => s.Contains("hidden")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("**********")));
    }

    [Test]
    public void ShouldSkipDescriptionSection()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Description"] = "Should be skipped",
            ["Other"] = "Should appear"
        });
        var logLines = new List<string>();

        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.None.Matches<string>(s => s.Contains("Should be skipped")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("Should appear")));
    }

    [Test]
    public void ShouldIndentNestedConfigValues()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Target:Server"] = "localhost",
            ["Target:Database"] = "mydb"
        });
        var logLines = new List<string>();

        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        // Nested keys get extra indentation (2 levels = 4 spaces)
        Assert.That(logLines, Has.Some.Matches<string>(s => s.StartsWith("    ") && s.Contains("localhost")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.StartsWith("    ") && s.Contains("mydb")));
    }

    [Test]
    public void ShouldHandleNullLogLineWithoutException()
    {
        var config = BuildConfig(new Dictionary<string, string> { ["Key"] = "Value" });

        Assert.DoesNotThrow(() => ConfigurationLogger.LogConfiguration(config, null));
    }

    [Test]
    public void ShouldHandleArrayConfigValues()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Items:0:Name"] = "First",
            ["Items:0:Value"] = "V1",
            ["Items:1:Name"] = "Second",
            ["Items:1:Value"] = "V2"
        });
        var logLines = new List<string>();

        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        // Array items with a Name sub-key are displayed using the Name as the key label
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("First")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("Second")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("V1")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("V2")));
    }

    private static IConfigurationRoot BuildConfig(Dictionary<string, string> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
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
