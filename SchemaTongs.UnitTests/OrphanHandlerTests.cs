// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using log4net;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;
using SchemaTongs;

namespace SchemaTongs.UnitTests;

public class OrphanHandlerTests
{
    private readonly ILog _progressLog = Substitute.For<ILog>();

    [SetUp]
    public void SetUp()
    {
        LogFactory.Register("ProgressLog", _progressLog);
    }

    [TearDown]
    public void TearDown()
    {
        LogFactory.Clear();
    }

    private static ExtractionFileIndex CreateIndexWithOrphans(string basePath, string[] orphanFiles)
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists(basePath).Returns(true);
        dir.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(orphanFiles);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build(basePath);
            FactoryContainer.Clear();
            return index;
        }
        // orphan files are in the index but MarkWritten was never called, so GetOrphans returns them all
    }

    [Test]
    public void ProcessOrphans_DetectMode_LogsButDoesNotDeleteOrWriteScripts()
    {
        var file = Substitute.For<IFile>();
        var dir = Substitute.For<IDirectory>();

        var index = CreateIndexWithOrphans("C:\\pkg\\Views", new[]
        {
            "C:\\pkg\\Views\\dbo.OldView.sql"
        });
        var folderIndexes = new Dictionary<string, ExtractionFileIndex>
        {
            ["Views"] = index
        };

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(dir);
            OrphanHandler.ProcessOrphans(folderIndexes, OrphanHandlingMode.Detect, "C:\\logs");
            FactoryContainer.Clear();
        }

        file.DidNotReceive().Delete(Arg.Any<string>());
        file.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public void ProcessOrphans_DetectWithCleanupScripts_GeneratesScriptFileButDoesNotDelete()
    {
        var file = Substitute.For<IFile>();
        var dir = Substitute.For<IDirectory>();

        var index = CreateIndexWithOrphans("C:\\pkg\\Views", new[]
        {
            "C:\\pkg\\Views\\dbo.OldView.sql"
        });
        var folderIndexes = new Dictionary<string, ExtractionFileIndex>
        {
            ["Views"] = index
        };

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(dir);
            OrphanHandler.ProcessOrphans(folderIndexes, OrphanHandlingMode.DetectWithCleanupScripts, "C:\\logs");
            FactoryContainer.Clear();
        }

        file.Received(1).WriteAllText(
            Arg.Is<string>(p => p.Contains("_OrphanCleanup_Views.sql")),
            Arg.Is<string>(s => s.Contains("DROP VIEW IF EXISTS")));
        file.DidNotReceive().Delete(Arg.Any<string>());
    }

    [Test]
    public void ProcessOrphans_DetectDeleteAndCleanup_DeletesFilesAndGeneratesScripts()
    {
        var file = Substitute.For<IFile>();
        var dir = Substitute.For<IDirectory>();

        var index = CreateIndexWithOrphans("C:\\pkg\\Views", new[]
        {
            "C:\\pkg\\Views\\dbo.OldView.sql"
        });
        var folderIndexes = new Dictionary<string, ExtractionFileIndex>
        {
            ["Views"] = index
        };

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(dir);
            OrphanHandler.ProcessOrphans(folderIndexes, OrphanHandlingMode.DetectDeleteAndCleanup, "C:\\logs");
            FactoryContainer.Clear();
        }

        file.Received(1).WriteAllText(
            Arg.Is<string>(p => p.Contains("_OrphanCleanup_Views.sql")),
            Arg.Any<string>());
        file.Received(1).Delete("C:\\pkg\\Views\\dbo.OldView.sql");
    }

    [Test]
    public void ProcessOrphans_NoOrphans_LogsCleanMessage()
    {
        var file = Substitute.For<IFile>();
        var dir = Substitute.For<IDirectory>();

        // Build an index and mark everything as written so no orphans remain
        var innerDir = Substitute.For<IDirectory>();
        innerDir.Exists("C:\\pkg\\Views").Returns(true);
        innerDir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(new[] { "C:\\pkg\\Views\\dbo.MyView.sql" });

        ExtractionFileIndex index;
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(innerDir);
            index = ExtractionFileIndex.Build("C:\\pkg\\Views");
            FactoryContainer.Clear();
        }
        index.MarkWritten("C:\\pkg\\Views\\dbo.MyView.sql");

        var folderIndexes = new Dictionary<string, ExtractionFileIndex>
        {
            ["Views"] = index
        };

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(dir);
            OrphanHandler.ProcessOrphans(folderIndexes, OrphanHandlingMode.DetectDeleteAndCleanup, "C:\\logs");
            FactoryContainer.Clear();
        }

        file.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        file.DidNotReceive().Delete(Arg.Any<string>());
        _progressLog.Received(1).Info(Arg.Is<string>(s => s.Contains("No orphaned files detected")));
    }

    [Test]
    public void ProcessOrphans_JsonOrphans_NoCleanupScriptGenerated()
    {
        var file = Substitute.For<IFile>();
        var dir = Substitute.For<IDirectory>();

        // JSON files in Tables folder — no cleanup script should be generated
        var index = CreateIndexWithOrphans("C:\\pkg\\Tables", new[]
        {
            "C:\\pkg\\Tables\\dbo.OldTable.json"
        });
        var folderIndexes = new Dictionary<string, ExtractionFileIndex>
        {
            ["Tables"] = index
        };

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(dir);
            OrphanHandler.ProcessOrphans(folderIndexes, OrphanHandlingMode.DetectDeleteAndCleanup, "C:\\logs");
            FactoryContainer.Clear();
        }

        // No cleanup script for JSON orphans
        file.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        // But the file should still be deleted
        file.Received(1).Delete("C:\\pkg\\Tables\\dbo.OldTable.json");
    }

    [Test]
    public void ArchiveExistingCleanupScripts_MovesExistingScriptsToBackupDir()
    {
        var file = Substitute.For<IFile>();
        var dir = Substitute.For<IDirectory>();

        var existingScript = "C:\\logs\\_OrphanCleanup_Views.sql";
        dir.Exists("C:\\logs").Returns(true);
        dir.GetFiles("C:\\logs", "_OrphanCleanup_*.sql", SearchOption.TopDirectoryOnly)
            .Returns(new[] { existingScript });
        dir.GetFiles("C:\\logs", "_InvalidObjectCleanup.sql", SearchOption.TopDirectoryOnly)
            .Returns(System.Array.Empty<string>());
        // First backup dir does not exist yet
        dir.Exists("C:\\logs\\SchemaTongs.0001").Returns(false);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(dir);
            OrphanHandler.ArchiveExistingCleanupScripts("C:\\logs");
            FactoryContainer.Clear();
        }

        dir.Received(1).CreateDirectory("C:\\logs\\SchemaTongs.0001");
        file.Received(1).Copy(existingScript, "C:\\logs\\SchemaTongs.0001\\_OrphanCleanup_Views.sql");
        file.Received(1).Delete(existingScript);
    }

    [Test]
    public void ArchiveExistingCleanupScripts_NoExistingScripts_DoesNothing()
    {
        var file = Substitute.For<IFile>();
        var dir = Substitute.For<IDirectory>();

        dir.Exists("C:\\logs").Returns(true);
        dir.GetFiles("C:\\logs", "_OrphanCleanup_*.sql", SearchOption.TopDirectoryOnly)
            .Returns(System.Array.Empty<string>());
        dir.GetFiles("C:\\logs", "_InvalidObjectCleanup.sql", SearchOption.TopDirectoryOnly)
            .Returns(System.Array.Empty<string>());

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IFile>(file);
            FactoryContainer.Register<IDirectory>(dir);
            OrphanHandler.ArchiveExistingCleanupScripts("C:\\logs");
            FactoryContainer.Clear();
        }

        dir.DidNotReceive().CreateDirectory(Arg.Any<string>());
        file.DidNotReceive().Copy(Arg.Any<string>(), Arg.Any<string>());
        file.DidNotReceive().Delete(Arg.Any<string>());
    }
}
