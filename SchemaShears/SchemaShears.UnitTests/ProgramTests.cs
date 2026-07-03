// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaShears.UnitTests;

[TestFixture]
public class ProgramTests
{
    private string _root;
    private string _source;
    private string _output;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), "shears-prog-" + Guid.NewGuid().ToString("N"));
        _source = Path.Join(_root, "product");
        _output = Path.Join(_root, "patch");

        var tables = Path.Join(_source, "Templates", "Main", "Tables");
        Directory.CreateDirectory(tables);
        File.WriteAllText(Path.Join(tables, "dbo.Orders.json"), "{ \"Name\": \"Orders\" }");
        File.WriteAllText(Path.Join(_source, "Templates", "Main", "Template.json"), "{}");
        File.WriteAllText(Path.Join(_source, "Product.json"), "{ \"Name\": \"Acme\" }");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void UnhandledException_LogsErrorAndExits()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var environment = Substitute.For<IEnvironment>();
            var progressLog = Substitute.For<ILog>();
            var errorLog = Substitute.For<ILog>();

            FactoryContainer.Register(environment);
            LogFactory.Register("ProgressLog", progressLog);
            LogFactory.Register("ErrorLog", errorLog);

            var exception = new Exception("Test Exception");
            Program.UnhandledException("TestApp", new UnhandledExceptionEventArgs(exception, false));

            progressLog.Received(1).Error(Arg.Is<string>(s => s.Contains("Test Exception")));
            errorLog.Received(1).Error(exception);
            environment.Received(1).Exit(3);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void Main_SuccessPath_BuildsPatchAndExitsZero()
    {
        var manifest = Path.Join(_root, "m.txt");
        File.WriteAllText(manifest, "Templates/Main/Tables/dbo.Orders.json\n");

        var environment = RunMain("SchemaShears.exe", new Dictionary<string, string>
        {
            ["SourcePath"] = _source,
            ["ManifestPath"] = manifest,
            ["OutputPath"] = _output
        });

        Assert.Multiple(() =>
        {
            environment.Received(1).Exit(0);
            Assert.That(File.Exists(Path.Join(_output, "Templates", "Main", "Tables", "dbo.Orders.json")), Is.True);
            Assert.That(File.Exists(Path.Join(_output, "patch-build-report.txt")), Is.True);
        });
    }

    [Test]
    public void Main_WithHelpSwitch_ShowsUsageThenFailsForMissingSource()
    {
        var (environment, exception) = RunMainExpectingThrow("SchemaShears.exe --help", new Dictionary<string, string>());

        Assert.Multiple(() =>
        {
            environment.Received(1).Exit(0);
            Assert.That(exception.Message, Does.Contain("Source product folder not found"));
        });
    }

    [Test]
    public void Main_NoSourceConfigured_ThrowsPatchBuildException()
    {
        var (_, exception) = RunMainExpectingThrow("SchemaShears.exe", new Dictionary<string, string>());

        Assert.That(exception.Message, Does.Contain("Source product folder not found"));
    }

    [Test]
    public void Main_ManifestNotFound_ThrowsPatchBuildException()
    {
        var (_, exception) = RunMainExpectingThrow("SchemaShears.exe", new Dictionary<string, string>
        {
            ["SourcePath"] = _source,
            ["ManifestPath"] = Path.Join(_root, "nope.txt"),
            ["OutputPath"] = _output
        });

        Assert.That(exception.Message, Does.Contain("Manifest file not found"));
    }

    [Test]
    public void Main_WithZipSwitch_ProducesZipFile()
    {
        var manifest = Path.Join(_root, "m.txt");
        File.WriteAllText(manifest, "Templates/Main/Tables/dbo.Orders.json\n");

        var environment = RunMain("SchemaShears.exe --Zip", new Dictionary<string, string>
        {
            ["SourcePath"] = _source,
            ["ManifestPath"] = manifest,
            ["OutputPath"] = _output
        });

        Assert.Multiple(() =>
        {
            environment.Received(1).Exit(0);
            Assert.That(File.Exists(_output + ".zip"), Is.True);
        });
    }

    [Test]
    public void Main_WithAllowDrops_PassesCategoriesThroughToStamp()
    {
        var manifest = Path.Join(_root, "m.txt");
        File.WriteAllText(manifest, "Templates/Main/Tables/dbo.Orders.json\n");

        var environment = RunMain("SchemaShears.exe --AllowDrops:Columns,Indexes", new Dictionary<string, string>
        {
            ["SourcePath"] = _source,
            ["ManifestPath"] = manifest,
            ["OutputPath"] = _output
        });

        environment.Received(1).Exit(0);
        var product = JObject.Parse(File.ReadAllText(Path.Join(_output, "Product.json")));
        Assert.Multiple(() =>
        {
            Assert.That(product["DropColumnsRemovedFromProduct"], Is.Null,
                "Columns is allowed, so the drop flag should not be stamped false");
            Assert.That(product["DropUnknownIndexes"], Is.Null,
                "Indexes is allowed, so the drop flag should not be stamped false");
            Assert.That(product["DropTablesRemovedFromProduct"].Value<bool>(), Is.False);
        });
    }

    /// <summary>
    /// Registers a mocked <see cref="IEnvironment"/> with the given command line, an in-memory
    /// config with the given values, and silent loggers, ready for <see cref="Program.Main"/>.
    /// </summary>
    private static IEnvironment Arrange(string commandLine, Dictionary<string, string> configValues)
    {
        FactoryContainer.Clear();
        LogFactory.Clear();

        var environment = Substitute.For<IEnvironment>();
        environment.CommandLine.Returns(commandLine);

        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        FactoryContainer.Register(environment);
        FactoryContainer.Register<IConfigurationRoot>(config);

        LogFactory.Register("ProgressLog", Substitute.For<ILog>());
        LogFactory.Register("ErrorLog", Substitute.For<ILog>());

        return environment;
    }

    private static IEnvironment RunMain(string commandLine, Dictionary<string, string> configValues)
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var environment = Arrange(commandLine, configValues);
            try
            {
                Program.Main([]);
            }
            finally
            {
                FactoryContainer.Clear();
                LogFactory.Clear();
            }
            return environment;
        }
    }

    private static (IEnvironment environment, PatchBuildException exception) RunMainExpectingThrow(
        string commandLine, Dictionary<string, string> configValues)
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var environment = Arrange(commandLine, configValues);
            PatchBuildException exception;
            try
            {
                exception = Assert.Throws<PatchBuildException>(() => Program.Main([]));
            }
            finally
            {
                FactoryContainer.Clear();
                LogFactory.Clear();
            }
            return (environment, exception);
        }
    }
}
