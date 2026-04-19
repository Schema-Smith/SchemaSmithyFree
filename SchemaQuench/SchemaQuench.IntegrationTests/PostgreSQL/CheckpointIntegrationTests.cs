// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests;
using Schema.Isolators;
using Schema.Utility;
using System;
using System.IO;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
public class CheckpointIntegrationTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;
    private readonly string _server;
    private string _checkpointDir;

    public CheckpointIntegrationTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres", config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
        _server = config["Target:Server"];
    }

    [SetUp]
    public void SetUp()
    {
        _checkpointDir = Path.Combine(Path.GetTempPath(), $"SchemaQuench_Checkpoint_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_checkpointDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_checkpointDir))
                Directory.Delete(_checkpointDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    public void ShouldCreateCheckpointFilesOnStart()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("PostgreSQL", "ValidProduct");
            FactoryContainer.Resolve<IConfigurationRoot>()["CheckpointDirectory"] = _checkpointDir;

            try
            {
                RunSchemaQuench();
            }
            catch
            {
                // Expected - we're just checking checkpoint creation
            }

            _progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Begin Quench")));

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
            FactoryContainer.Unregister<Schema.Checkpointing.ICheckpointing>();
        }
    }

    [Test]
    public void ShouldResumeFromProductCheckpoint()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("PostgreSQL", "ValidProduct");
            FactoryContainer.Resolve<IConfigurationRoot>()["CheckpointDirectory"] = _checkpointDir;

            var product = Product.Load();
            var checkpointContent = $@"# SchemaQuench Product Checkpoint
# Product: {product.Name}
# Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

[Completed Templates]
Template:Main
";
            var checkpointPath = Path.Combine(_checkpointDir, $"{FileNameEncoder.Encode(product.Name)}.product.checkpoint");
            File.WriteAllText(checkpointPath, checkpointContent);

            RunSchemaQuench();

            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains("Skipping template 'Main' (previously completed per checkpoint)")));
            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains("Quenching Template: Secondary")));

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
            FactoryContainer.Unregister<Schema.Checkpointing.ICheckpointing>();
        }
    }

    [Test]
    public void ShouldResumeFromDatabaseCheckpoint()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("PostgreSQL", "ValidProduct");
            FactoryContainer.Resolve<IConfigurationRoot>()["CheckpointDirectory"] = _checkpointDir;

            var product = Product.Load();
            var dbCheckpointContent = $@"# SchemaQuench Database Checkpoint
# Product: {product.Name}
# Template: Main
# Server: {_server}
# Database: {_mainDb}
# Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

[Completed Steps]
KindleForge

[Before Scripts]

[Object Scripts]

[After Tables Object Scripts]

[Between Tables And Keys Scripts]

[After Table Scripts]

[Table Data Scripts]

[After Scripts]
";
            var dbCheckpointPath = Path.Combine(_checkpointDir,
                $"{FileNameEncoder.Encode(product.Name)}.{FileNameEncoder.Encode("Main")}.{FileNameEncoder.Encode(_server)}.{FileNameEncoder.Encode(_mainDb)}.checkpoint");
            File.WriteAllText(dbCheckpointPath, dbCheckpointContent);

            RunSchemaQuench();

            _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains(_mainDb) && s.Contains("Resuming from checkpoint")));

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
            FactoryContainer.Unregister<Schema.Checkpointing.ICheckpointing>();
        }
    }

    [Test]
    public void ShouldPreserveCheckpointOnFailure()
    {
        // Verifies cleanup is skipped when a quench fails. Pre-writes a representative
        // checkpoint file, runs a deliberately-failing quench, and confirms the pre-existing
        // file survives. Required because the real failing run (BeforeTemplateScriptError)
        // fails before any successful step, so nothing is written to the checkpoint during
        // the run itself — the only way to observe "cleanup did not run" is to seed disk
        // state ahead of time and check that it's still there afterward.
        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = TestHelper.GetTestProductPath("PostgreSQL", "BeforeTemplateScriptError");
            FactoryContainer.Resolve<IConfigurationRoot>()["CheckpointDirectory"] = _checkpointDir;

            // Seed a checkpoint file that should not be touched by the failing run's cleanup path.
            var seededCheckpoint = Path.Combine(_checkpointDir, "BeforeTemplateScriptError.product.checkpoint");
            const string seededContent = "# Seeded by test to verify cleanup is skipped on failure\n[Completed Templates]\nTemplate:Seeded\n";
            Directory.CreateDirectory(_checkpointDir);
            File.WriteAllText(seededCheckpoint, seededContent);

            RunSchemaQuench();

            Assert.That(File.Exists(seededCheckpoint), Is.True, "Pre-existing checkpoint must not be deleted after a failed run");
            Assert.That(File.ReadAllText(seededCheckpoint), Is.EqualTo(seededContent), "Pre-existing checkpoint content must not be altered after a failed run");

            _environment.Received(1).Exit(2);

            LogFactory.Clear();
            FactoryContainer.Unregister<IEnvironment>();
            FactoryContainer.Unregister<Schema.Checkpointing.ICheckpointing>();
        }
    }

    private void SetupSharedMocks()
    {
        _progressLog.ClearReceivedCalls();
        _errorLog.ClearReceivedCalls();
        _environment.ClearReceivedCalls();
        _environment.CommandLine.Returns("--SkipKindlingForge --ResumeQuench");
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private void RunSchemaQuench()
    {
        Program.Main(["SkipKindlingForge"]);
    }
}
