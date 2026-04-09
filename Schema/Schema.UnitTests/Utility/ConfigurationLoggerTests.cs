// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class ConfigurationLoggerTests
{
    [Test]
    public void LogConfiguration_LogsVersionLine()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "SomeKey", "SomeValue" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("Version:")));
    }

    [Test]
    public void LogConfiguration_LogsConfigurationHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Key1", "Value1" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.Some.EqualTo("Configuration:"));
    }

    [Test]
    public void LogConfiguration_LogsConfigValues()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "MyKey", "MyValue" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("MyKey") && s.Contains("MyValue")));
    }

    [Test]
    public void LogConfiguration_MasksPasswordValues()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Password", "secret123" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.None.Matches<string>(s => s.Contains("secret123")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("**********")));
    }

    [Test]
    public void LogConfiguration_MasksPwdValues()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ConnectionPwd", "hidden" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.None.Matches<string>(s => s.Contains("hidden")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("**********")));
    }

    [Test]
    public void LogConfiguration_SkipsDescriptionSection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Description", "Should be skipped" },
                { "Other", "Should appear" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.None.Matches<string>(s => s.Contains("Should be skipped")));
    }

    [Test]
    public void LogConfiguration_HandlesNestedConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Target:Server", "localhost" },
                { "Target:Database", "mydb" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("localhost")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("mydb")));
    }

    [Test]
    public void LogConfiguration_HandlesNullLogLine()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Key", "Value" }
            })
            .Build();

        Assert.DoesNotThrow(() => ConfigurationLogger.LogConfiguration(config, null));
    }

    [Test]
    public void LogConfiguration_HandlesArrayConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Items:0:Name", "First" },
                { "Items:0:Value", "V1" },
                { "Items:1:Name", "Second" },
                { "Items:1:Value", "V2" }
            })
            .Build();

        var logLines = new List<string>();
        ConfigurationLogger.LogConfiguration(config, s => logLines.Add(s));

        // Array items with Name sub-keys should use the name as the display key
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("First")));
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("Second")));
    }
}
