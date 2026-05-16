// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using log4net;
using NSubstitute;
using Schema.Checkpointing;
using Schema.IntegrationTests.MySQL;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// CheckpointManager-style unit/integration tests that exercise FileCheckpointManager's
/// on-disk persistence directly — file naming (FileNameEncoder), section layout, and
/// delete semantics. Uses a temp checkpoint directory per test.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class CheckpointIntegrationTests
{
    private ILog _errorLog = null!;
    private ILog _progressLog = null!;
    private IEnvironment _environment = null!;
    private string _checkpointDir = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        FixtureSetup.EnsureInitialized();

        _errorLog = Substitute.For<ILog>();
        _progressLog = Substitute.For<ILog>();
        _environment = Substitute.For<IEnvironment>();
    }

    [SetUp]
    public void SetUp()
    {
        _checkpointDir = Path.Combine(Path.GetTempPath(), $"SchemaQuench_Checkpoint_Test_{Guid.NewGuid():N}");

        _errorLog.ClearReceivedCalls();
        _progressLog.ClearReceivedCalls();
        _environment.ClearReceivedCalls();
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
    public void CheckpointManager_ShouldCreateDirectory()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var nestedDir = Path.Combine(_checkpointDir, "nested", "checkpoint");

            var manager = new FileCheckpointManager(nestedDir);

            Assert.That(Directory.Exists(nestedDir), Is.True);
            Assert.That(manager, Is.Not.Null);
        }
    }

    [Test]
    public void CheckpointManager_ShouldSaveAndLoadProductCheckpoint()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var manager = new FileCheckpointManager(_checkpointDir);
            var scope = new TrackingScope { ProductName = "TestProduct" };

            manager.MarkScriptCompleted(scope, "Before", "Scripts/Before1.sql");
            manager.MarkStepCompleted(scope, "Template:Main");
            manager.MarkScriptCompleted(scope, "After", "Scripts/After1.sql");

            var filePath = Path.Combine(_checkpointDir, "TestProduct.product.checkpoint");
            Assert.That(File.Exists(filePath), Is.True, "Product checkpoint file should exist on disk");

            var content = File.ReadAllText(filePath);
            Assert.That(content, Does.Contain("# Product: TestProduct"));
            Assert.That(content, Does.Contain("[Before Product Scripts - default]"));
            Assert.That(content, Does.Contain("Scripts/Before1.sql"));
            Assert.That(content, Does.Contain("[Completed Templates]"));
            Assert.That(content, Does.Contain("Template:Main"));
            Assert.That(content, Does.Contain("[After Product Scripts - default]"));
            Assert.That(content, Does.Contain("Scripts/After1.sql"));

            Assert.That(manager.HasCompleted(scope, "Template:Main"), Is.True);
            Assert.That(manager.HasCompletedScript(scope, "Before", "Scripts/Before1.sql"), Is.True);
            Assert.That(manager.HasCompletedScript(scope, "After", "Scripts/After1.sql"), Is.True);
        }
    }

    [Test]
    public void CheckpointManager_ShouldSaveAndLoadDatabaseCheckpoint()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var manager = new FileCheckpointManager(_checkpointDir);
            var scope = new TrackingScope
            {
                ProductName = "TestProduct",
                TemplateName = "Main",
                Server = "localhost",
                DatabaseName = "TestDB"
            };

            manager.MarkStepCompleted(scope, "KindleForge");
            manager.MarkStepCompleted(scope, "ValidateBaseline");
            manager.MarkScriptCompleted(scope, "Before", "MigrationScripts/Script1.sql");
            manager.MarkScriptCompleted(scope, "Object", "Functions/Func1.sql");

            // Slice 2: filename includes a 5th segment for SchemaName (empty for regular templates).
            var filePath = Path.Combine(_checkpointDir, "TestProduct.Main.localhost.TestDB..checkpoint");
            Assert.That(File.Exists(filePath), Is.True, "Database checkpoint file should exist on disk");

            var content = File.ReadAllText(filePath);
            Assert.That(content, Does.Contain("# Product: TestProduct"));
            Assert.That(content, Does.Contain("# Template: Main"));
            Assert.That(content, Does.Contain("# Server: localhost"));
            Assert.That(content, Does.Contain("# Database: TestDB"));
            Assert.That(content, Does.Contain("[Completed Steps]"));
            Assert.That(content, Does.Contain("KindleForge"));
            Assert.That(content, Does.Contain("ValidateBaseline"));
            Assert.That(content, Does.Contain("[Before Scripts]"));
            Assert.That(content, Does.Contain("MigrationScripts/Script1.sql"));
            Assert.That(content, Does.Contain("[Object Scripts]"));
            Assert.That(content, Does.Contain("Functions/Func1.sql"));

            Assert.That(manager.HasCompleted(scope, "KindleForge"), Is.True);
            Assert.That(manager.HasCompletedScript(scope, "Before", "MigrationScripts/Script1.sql"), Is.True);
        }
    }

    [Test]
    public void CheckpointManager_ShouldDeleteProductCheckpoint()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var manager = new FileCheckpointManager(_checkpointDir);
            var scope = new TrackingScope { ProductName = "TestProduct" };
            manager.MarkStepCompleted(scope, "Template:Main");

            var filePath = Path.Combine(_checkpointDir, "TestProduct.product.checkpoint");
            Assert.That(File.Exists(filePath), Is.True);

            manager.DeleteCheckpoints("TestProduct");

            Assert.That(File.Exists(filePath), Is.False, "Product checkpoint file should be deleted");
        }
    }

    [Test]
    public void CheckpointManager_ShouldDeleteDatabaseCheckpoints()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var manager = new FileCheckpointManager(_checkpointDir);

            var scope1 = new TrackingScope { ProductName = "TestProduct", TemplateName = "Main", Server = "localhost", DatabaseName = "DB1" };
            var scope2 = new TrackingScope { ProductName = "TestProduct", TemplateName = "Main", Server = "localhost", DatabaseName = "DB2" };
            manager.MarkStepCompleted(scope1, "KindleForge");
            manager.MarkStepCompleted(scope2, "KindleForge");

            // Slice 2: filename includes a 5th segment for SchemaName (empty for regular templates).
            var path1 = Path.Combine(_checkpointDir, "TestProduct.Main.localhost.DB1..checkpoint");
            var path2 = Path.Combine(_checkpointDir, "TestProduct.Main.localhost.DB2..checkpoint");
            Assert.That(File.Exists(path1), Is.True);
            Assert.That(File.Exists(path2), Is.True);

            manager.DeleteCheckpoints("TestProduct");

            Assert.That(File.Exists(path1), Is.False, "DB1 checkpoint should be deleted");
            Assert.That(File.Exists(path2), Is.False, "DB2 checkpoint should be deleted");
        }
    }

    [Test]
    public void CheckpointManager_ShouldSanitizeServerName()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var manager = new FileCheckpointManager(_checkpointDir);
            var scope = new TrackingScope
            {
                ProductName = "TestProduct",
                TemplateName = "Main",
                Server = "server:3306/instance",
                DatabaseName = "TestDB"
            };

            manager.MarkStepCompleted(scope, "KindleForge");

            var files = Directory.GetFiles(_checkpointDir, "*.checkpoint");
            Assert.That(files.Length, Is.EqualTo(1));

            var fileName = Path.GetFileName(files[0]);
            Assert.That(fileName, Does.Contain("server%3A3306%2Finstance"),
                "Server name with illegal filename characters should be percent-encoded");
        }
    }
}
