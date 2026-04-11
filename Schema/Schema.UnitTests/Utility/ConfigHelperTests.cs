// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NSubstitute;
using Microsoft.Extensions.Configuration;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class ConfigHelperTests
{
    private IEnvironment _mockEnvironment;

    [SetUp]
    public void SetUp()
    {
        _mockEnvironment = Substitute.For<IEnvironment>();
        _mockEnvironment.CommandLine.Returns("app.exe");
        FactoryContainer.Register<IEnvironment>(_mockEnvironment);
    }

    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_ReturnsConfigurationRoot()
    {
        var logLines = new List<string>();
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", s => logLines.Add(s));

        Assert.That(config, Is.Not.Null);
        Assert.That(config, Is.InstanceOf<IConfigurationRoot>());
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_RegistersConfigInFactoryContainer()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });

        var resolved = FactoryContainer.Resolve<IConfigurationRoot>();
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved, Is.SameAs(config));
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_ReturnsCachedConfig_WhenCalledTwice()
    {
        var config1 = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });
        var config2 = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });

        Assert.That(config2, Is.SameAs(config1));
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_ReturnsPreviouslyRegisteredConfig()
    {
        var mockConfig = Substitute.For<IConfigurationRoot>();
        FactoryContainer.Register(mockConfig);

        var result = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });

        Assert.That(result, Is.SameAs(mockConfig));
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_LogsAppAndLicense()
    {
        // The app name is logged on its own line, followed by the license display text
        // (potentially multi-line when a Pro package is loaded). We assert the app name
        // appears and a Community identifier appears in the log stream, without pinning
        // the exact format or verbiage.
        var logLines = new List<string>();
        ConfigHelper.GetAppSettingsAndUserSecrets("MyTool", s => logLines.Add(s));

        Assert.That(logLines, Has.Some.Matches<string>(s => s == "MyTool"));
        // License text varies: "Community License" (no license) or "Licensed to: ..." (licensed).
        Assert.That(logLines, Has.Some.Matches<string>(s => s.Contains("Community") || s.Contains("Licensed")));
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_AppLogLine_DoesNotContainHardcodedPlatform()
    {
        // Platform is read from Product.json at runtime, not hardcoded in the startup banner.
        // The log line containing the app name must not carry a hardcoded engine string.
        var logLines = new List<string>();
        ConfigHelper.GetAppSettingsAndUserSecrets("MyTool", s => logLines.Add(s));

        var appLine = logLines.FirstOrDefault(s => s.Contains("MyTool"));
        Assert.That(appLine, Is.Not.Null);
        Assert.That(appLine, Does.Not.Contain("SqlServer"));
        Assert.That(appLine, Does.Not.Contain("PostgreSQL"));
        Assert.That(appLine, Does.Not.Contain("MySQL"));
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_UsesSmithySettingsPrefix()
    {
        // Set an environment variable with SmithySettings_ prefix and verify it's picked up
        _mockEnvironment.CommandLine.Returns("app.exe");
        Environment.SetEnvironmentVariable("SmithySettings_TestKey", "TestValue");
        try
        {
            var config = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });
            var value = config["TestKey"];
            Assert.That(value, Is.EqualTo("TestValue"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SmithySettings_TestKey", null);
        }
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_DoesNotUseQuenchSettingsPrefix()
    {
        // Set an environment variable with QuenchSettings_ prefix and verify it's NOT picked up
        // This is the key behavioral change - QuenchSettings_ prefix has been removed
        _mockEnvironment.CommandLine.Returns("app.exe");
        Environment.SetEnvironmentVariable("QuenchSettings_TestKey2", "ShouldNotAppear");
        try
        {
            var config = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });
            var value = config["TestKey2"];
            Assert.That(value, Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("QuenchSettings_TestKey2", null);
        }
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_UsesConfigFileSwitch()
    {
        // Create a temp config file with a known value
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(tempFile, """{"CustomKey": "CustomValue"}""");
            _mockEnvironment.CommandLine.Returns($"app.exe --ConfigFile:{tempFile}");

            var config = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });
            Assert.That(config["CustomKey"], Is.EqualTo("CustomValue"));
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_HandlesNullLogLine()
    {
        // Should not throw when logLine is null
        Assert.DoesNotThrow(() => ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", null));
    }

    [Test]
    public void GetAppSettingsAndUserSecrets_UsesToolSpecificSettingsFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var settingsFile = Path.Combine(tempDir, "TestApp.settings.json");
        try
        {
            File.WriteAllText(settingsFile, """{"ToolSpecificKey": "FoundIt"}""");
            var originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempDir);
            try
            {
                var config = ConfigHelper.GetAppSettingsAndUserSecrets("TestApp", _ => { });
                Assert.That(config["ToolSpecificKey"], Is.EqualTo("FoundIt"));
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ConfigHelper_DoesNotHavePlatformConstant()
    {
        // Verify via reflection that ConfigHelper no longer has a Platform constant
        var field = typeof(ConfigHelper).GetField("Platform",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.That(field, Is.Null, "ConfigHelper should not have a Platform constant - platform is read from Product");
    }
}
