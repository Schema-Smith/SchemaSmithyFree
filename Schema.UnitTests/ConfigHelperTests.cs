// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.UnitTests;

public class ConfigHelperTests
{
    [SetUp]
    public void SetUp()
    {
        FactoryContainer.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    private static void MockCommandLine(string commandLine)
    {
        var env = Substitute.For<IEnvironment>();
        env.CommandLine.Returns(commandLine);
        FactoryContainer.Register<IEnvironment>(env);
    }

    [Test]
    public void ShouldNotThrowWhenSettingsFileMissing()
    {
        // With optional:true, missing settings file should not throw
        MockCommandLine("test.exe");

        Assert.DoesNotThrow(() => ConfigHelper.GetAppSettingsAndUserSecrets("SchemaTongs", null));
    }

    [Test]
    public void ShouldNotLoadQuenchSettingsPrefix()
    {
        // QuenchSettings_ prefix should no longer be loaded
        Environment.SetEnvironmentVariable("QuenchSettings_TestKey", "old-value");
        Environment.SetEnvironmentVariable("SmithySettings_TestKey", "new-value");
        MockCommandLine("test.exe");

        try
        {
            var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaQuench", null);
            Assert.That(config["TestKey"], Is.EqualTo("new-value"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("QuenchSettings_TestKey", null);
            Environment.SetEnvironmentVariable("SmithySettings_TestKey", null);
        }
    }

    [Test]
    public void ShouldReturnCachedConfig_WhenCalledTwice()
    {
        MockCommandLine("test.exe");

        var config1 = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaQuench", null);
        var config2 = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaQuench", null);

        Assert.That(config2, Is.SameAs(config1));
    }

    [Test]
    public void ShouldUseSmithySettingsPrefix()
    {
        Environment.SetEnvironmentVariable("SmithySettings_CustomKey", "custom-value");
        MockCommandLine("test.exe");

        try
        {
            var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaQuench", null);
            Assert.That(config["CustomKey"], Is.EqualTo("custom-value"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SmithySettings_CustomKey", null);
        }
    }

    [Test]
    public void ShouldRegisterConfigInFactoryContainer()
    {
        MockCommandLine("test.exe");

        var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaQuench", null);
        var resolved = FactoryContainer.Resolve<IConfigurationRoot>();

        Assert.That(resolved, Is.SameAs(config));
    }
}
