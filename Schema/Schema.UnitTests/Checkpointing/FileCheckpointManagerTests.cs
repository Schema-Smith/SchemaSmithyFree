// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Linq;
using Schema.Checkpointing;

namespace Schema.UnitTests.Checkpointing;

[TestFixture]
public class FileCheckpointManagerTests
{
    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"checkpoint_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private TrackingScope DbScope(string product = "TestProduct", string template = "Main",
        string server = "localhost", string database = "TestDB") =>
        new() { ProductName = product, TemplateName = template, Server = server, DatabaseName = database };

    private TrackingScope ProductScope(string product = "TestProduct") =>
        new() { ProductName = product };

    [Test]
    public void Track_DatabaseScope_RunsActionOnFirstCall()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var ran = false;

        mgr.Track(DbScope(), "KindleForge", () => ran = true);

        Assert.That(ran, Is.True);
    }

    [Test]
    public void Track_DatabaseScope_SkipsActionOnSecondCall()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var count = 0;

        mgr.Track(DbScope(), "KindleForge", () => count++);
        mgr.Track(DbScope(), "KindleForge", () => count++);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Track_DatabaseScope_DifferentSteps_BothRun()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var count = 0;

        mgr.Track(DbScope(), "KindleForge", () => count++);
        mgr.Track(DbScope(), "ValidateBaseline", () => count++);

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void Track_DatabaseScope_PersistsToDisk()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        mgr.Track(DbScope(), "KindleForge", () => { });

        var mgr2 = new FileCheckpointManager(_tempDir);
        var ran = false;
        mgr2.Track(DbScope(), "KindleForge", () => ran = true);

        Assert.That(ran, Is.False);
    }

    [Test]
    public void TrackScript_DatabaseScope_RunsOnFirstCall()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var ran = false;

        mgr.TrackScript(DbScope(), "Before", "scripts/init.sql", () => ran = true);

        Assert.That(ran, Is.True);
    }

    [Test]
    public void TrackScript_DatabaseScope_SkipsOnSecondCall()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var count = 0;

        mgr.TrackScript(DbScope(), "Before", "scripts/init.sql", () => count++);
        mgr.TrackScript(DbScope(), "Before", "scripts/init.sql", () => count++);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void TrackScript_DatabaseScope_DifferentSlots_BothRun()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var count = 0;

        mgr.TrackScript(DbScope(), "Before", "scripts/init.sql", () => count++);
        mgr.TrackScript(DbScope(), "After", "scripts/init.sql", () => count++);

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void TrackScript_DatabaseScope_PersistsToDisk()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        mgr.TrackScript(DbScope(), "Object", "views/MyView.sql", () => { });

        var mgr2 = new FileCheckpointManager(_tempDir);
        var ran = false;
        mgr2.TrackScript(DbScope(), "Object", "views/MyView.sql", () => ran = true);

        Assert.That(ran, Is.False);
    }

    [Test]
    public void Track_ProductScope_RunsAndSkips()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var count = 0;

        mgr.Track(ProductScope(), "MainTemplate", () => count++);
        mgr.Track(ProductScope(), "MainTemplate", () => count++);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void TrackScript_ProductScope_Before_RunsAndSkips()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var scope = new TrackingScope { ProductName = "TestProduct", Server = "server1" };
        var count = 0;

        mgr.TrackScript(scope, "Before", "scripts/before1.sql", () => count++);
        mgr.TrackScript(scope, "Before", "scripts/before1.sql", () => count++);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void TrackScript_ProductScope_After_RunsAndSkips()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var scope = new TrackingScope { ProductName = "TestProduct", Server = "server1" };
        var count = 0;

        mgr.TrackScript(scope, "After", "scripts/after1.sql", () => count++);
        mgr.TrackScript(scope, "After", "scripts/after1.sql", () => count++);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void DeleteCheckpoints_RemovesAllFilesForProduct()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        mgr.Track(DbScope(), "KindleForge", () => { });
        mgr.Track(ProductScope(), "MainTemplate", () => { });

        mgr.DeleteCheckpoints("TestProduct");

        var mgr2 = new FileCheckpointManager(_tempDir);
        var count = 0;
        mgr2.Track(DbScope(), "KindleForge", () => count++);
        mgr2.Track(ProductScope(), "MainTemplate", () => count++);

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void DatabaseCheckpoint_ParseFormat_RoundTrip()
    {
        var checkpoint = new DatabaseCheckpoint
        {
            ProductName = "MyProduct",
            TemplateName = "Main",
            Server = "localhost",
            DatabaseName = "TestDB",
            StartedAt = new DateTime(2026, 4, 11, 10, 30, 0)
        };
        checkpoint.AddStep("KindleForge");
        checkpoint.AddStep("ValidateBaseline");
        checkpoint.AddCompletedScript(DatabaseScriptSlot.Before, "scripts/init.sql");
        checkpoint.AddCompletedScript(DatabaseScriptSlot.Object, "views/MyView.sql");

        var formatted = FileCheckpointManager.FormatDatabaseCheckpoint(checkpoint);
        var parsed = FileCheckpointManager.ParseDatabaseCheckpoint(formatted, "MyProduct", "Main", "localhost", "TestDB");

        Assert.That(parsed.HasCompletedStep("KindleForge"), Is.True);
        Assert.That(parsed.HasCompletedStep("ValidateBaseline"), Is.True);
        Assert.That(parsed.HasCompletedScript(DatabaseScriptSlot.Before, "scripts/init.sql"), Is.True);
        Assert.That(parsed.HasCompletedScript(DatabaseScriptSlot.Object, "views/MyView.sql"), Is.True);
        Assert.That(parsed.StartedAt, Is.EqualTo(new DateTime(2026, 4, 11, 10, 30, 0)));
    }

    [Test]
    public void ProductCheckpoint_ParseFormat_RoundTrip()
    {
        var checkpoint = new ProductCheckpoint
        {
            ProductName = "MyProduct",
            StartedAt = new DateTime(2026, 4, 11, 10, 30, 0)
        };
        checkpoint.AddBeforeScript("server1", "scripts/before1.sql");
        checkpoint.AddTemplate("Main");
        checkpoint.AddAfterScript("server1", "scripts/after1.sql");

        var formatted = FileCheckpointManager.FormatProductCheckpoint(checkpoint);
        var parsed = FileCheckpointManager.ParseProductCheckpoint(formatted, "MyProduct");

        Assert.That(parsed.HasCompletedBeforeScript("server1", "scripts/before1.sql"), Is.True);
        Assert.That(parsed.HasCompletedTemplate("Main"), Is.True);
        Assert.That(parsed.HasCompletedAfterScript("server1", "scripts/after1.sql"), Is.True);
        Assert.That(parsed.StartedAt, Is.EqualTo(new DateTime(2026, 4, 11, 10, 30, 0)));
    }

    [Test]
    public void TrackScript_NormalizesBackslashes()
    {
        var mgr = new FileCheckpointManager(_tempDir);
        var count = 0;

        mgr.TrackScript(DbScope(), "Before", @"scripts\init.sql", () => count++);
        mgr.TrackScript(DbScope(), "Before", "scripts/init.sql", () => count++);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Track_DatabaseScope_CorruptedCheckpointFile_ReturnsFreshCheckpoint()
    {
        var scope = DbScope();

        var mgr = new FileCheckpointManager(_tempDir);
        mgr.Track(scope, "KindleForge", () => { });

        var checkpointFile = Directory.GetFiles(_tempDir, "*.checkpoint")
            .Single(f => !f.EndsWith(".product.checkpoint"));
        File.WriteAllText(checkpointFile, "THIS IS NOT VALID CHECKPOINT CONTENT @@##!!");

        var mgr2 = new FileCheckpointManager(_tempDir);
        var ran = false;
        mgr2.Track(scope, "KindleForge", () => ran = true);

        Assert.That(ran, Is.True, "Corrupted checkpoint should be silently discarded, causing the step to run again");
    }

    [Test]
    public void Track_ProductScope_CorruptedProductCheckpoint_ReturnsFreshCheckpoint()
    {
        var scope = ProductScope();

        var mgr = new FileCheckpointManager(_tempDir);
        mgr.Track(scope, "MainTemplate", () => { });

        var productCheckpointFile = Directory.GetFiles(_tempDir, "*.product.checkpoint").Single();
        File.WriteAllText(productCheckpointFile, "\x00\x01\x02 BINARY GARBAGE \xFF\xFE");

        var mgr2 = new FileCheckpointManager(_tempDir);
        var ran = false;
        mgr2.Track(scope, "MainTemplate", () => ran = true);

        Assert.That(ran, Is.True, "Corrupted product checkpoint should be silently discarded, causing the step to run again");
    }

    [Test]
    public void Complete_WhenNoExistingCheckpoints_DoesNotThrow()
    {
        var mgr = new FileCheckpointManager(_tempDir);

        Assert.DoesNotThrow(() => mgr.DeleteCheckpoints("TestProduct"));
    }

    [Test]
    public void Track_DatabaseScope_AfterComplete_RunsAgain()
    {
        var dbScope = DbScope();
        var productScope = ProductScope();

        var mgr = new FileCheckpointManager(_tempDir);
        mgr.Track(dbScope, "KindleForge", () => { });
        mgr.Track(productScope, "MainTemplate", () => { });
        mgr.DeleteCheckpoints("TestProduct");

        var mgr2 = new FileCheckpointManager(_tempDir);
        var count = 0;
        mgr2.Track(dbScope, "KindleForge", () => count++);
        mgr2.Track(productScope, "MainTemplate", () => count++);

        Assert.That(count, Is.EqualTo(2), "Both steps should run after checkpoints were cleaned up by DeleteCheckpoints");
    }

    [Test]
    public void Constructor_CreatesDirectoryIfMissing()
    {
        var newDir = Path.Combine(_tempDir, "subdir");
        Assert.That(Directory.Exists(newDir), Is.False);

        _ = new FileCheckpointManager(newDir);

        Assert.That(Directory.Exists(newDir), Is.True);
    }

    [Test]
    public void Constructor_ThrowsOnNullDirectory()
    {
        Assert.Throws<ArgumentNullException>(() => new FileCheckpointManager(null));
    }

    [Test]
    public void DefaultConstructor_UsesTempDirectory()
    {
        var mgr = new FileCheckpointManager();
        var ran = false;

        mgr.Track(new TrackingScope { ProductName = $"TestProduct_{Guid.NewGuid():N}" }, "Step1", () => ran = true);

        Assert.That(ran, Is.True);
    }

    [Test]
    public void GetFromFactory_ReturnsICheckpointingInstance()
    {
        var checkpointing = FileCheckpointManager.GetFromFactory();

        Assert.That(checkpointing, Is.Not.Null);
        Assert.That(checkpointing, Is.InstanceOf<ICheckpointing>());
    }
}
