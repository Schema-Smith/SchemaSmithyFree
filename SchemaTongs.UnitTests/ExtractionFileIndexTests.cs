// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using log4net;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;
using SchemaTongs;

namespace SchemaTongs.UnitTests;

public class ExtractionFileIndexTests
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

    [Test]
    public void Build_FindsFilesRecursively_IndexesByFileName()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\Views").Returns(true);
        dir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(new[]
            {
                "C:\\pkg\\Views\\dbo.MyView.sql",
                "C:\\pkg\\Views\\Reporting\\dbo.SalesReport.sql"
            });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\Views");
            Assert.That(index.ResolvePath("dbo.MyView.sql", "C:\\pkg\\Views"),
                Is.EqualTo("C:\\pkg\\Views\\dbo.MyView.sql"));
            Assert.That(index.ResolvePath("dbo.SalesReport.sql", "C:\\pkg\\Views"),
                Is.EqualTo("C:\\pkg\\Views\\Reporting\\dbo.SalesReport.sql"));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ResolvePath_NewFile_ReturnsBaseFolderPath()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\Views").Returns(true);
        dir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(System.Array.Empty<string>());

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\Views");
            var result = index.ResolvePath("dbo.NewView.sql", "C:\\pkg\\Views");
            Assert.That(result, Is.EqualTo(Path.Combine("C:\\pkg\\Views", "dbo.NewView.sql")));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ResolvePath_MultipleMatches_ReturnsBaseFolderPath()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\Views").Returns(true);
        dir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(new[]
            {
                "C:\\pkg\\Views\\dbo.MyView.sql",
                "C:\\pkg\\Views\\Reporting\\dbo.MyView.sql"
            });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\Views");
            var result = index.ResolvePath("dbo.MyView.sql", "C:\\pkg\\Views");
            Assert.That(result, Is.EqualTo(Path.Combine("C:\\pkg\\Views", "dbo.MyView.sql")));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ResolvePath_MultipleMatches_LogsWarning()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\Views").Returns(true);
        dir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(new[]
            {
                "C:\\pkg\\Views\\dbo.MyView.sql",
                "C:\\pkg\\Views\\Reporting\\dbo.MyView.sql"
            });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\Views");
            index.ResolvePath("dbo.MyView.sql", "C:\\pkg\\Views");
            _progressLog.Received(1).Warn(Arg.Is<string>(s => s.Contains("dbo.MyView.sql") && s.Contains("multiple subfolders")));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void GetOrphans_ReturnsFilesNotWrittenTo()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\Views").Returns(true);
        dir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(new[]
            {
                "C:\\pkg\\Views\\dbo.ViewA.sql",
                "C:\\pkg\\Views\\dbo.ViewB.sql"
            });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\Views");
            index.MarkWritten("C:\\pkg\\Views\\dbo.ViewA.sql");
            var orphans = index.GetOrphans();
            Assert.That(orphans, Has.Count.EqualTo(1));
            Assert.That(orphans[0], Is.EqualTo("C:\\pkg\\Views\\dbo.ViewB.sql"));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ExcludeFromOrphans_PreventsOrphanDetection()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\Views").Returns(true);
        dir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(new[]
            {
                "C:\\pkg\\Views\\dbo.EncryptedView.sql"
            });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\Views");
            index.ExcludeFromOrphans("dbo.EncryptedView.sql");
            var orphans = index.GetOrphans();
            Assert.That(orphans, Is.Empty);
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void Build_IndexesSqlSqlerrorAndJsonFiles()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\Views").Returns(true);
        dir.GetFiles("C:\\pkg\\Views", "*.*", SearchOption.AllDirectories)
            .Returns(new[]
            {
                "C:\\pkg\\Views\\dbo.MyView.sql",
                "C:\\pkg\\Views\\dbo.MyView.sqlerror",
                "C:\\pkg\\Views\\schema.json",
                "C:\\pkg\\Views\\notes.txt"
            });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\Views");

            // Indexed files resolve to their paths
            Assert.That(index.ResolvePath("dbo.MyView.sql", "C:\\pkg\\Views"),
                Is.EqualTo("C:\\pkg\\Views\\dbo.MyView.sql"));
            Assert.That(index.ResolvePath("dbo.MyView.sqlerror", "C:\\pkg\\Views"),
                Is.EqualTo("C:\\pkg\\Views\\dbo.MyView.sqlerror"));
            Assert.That(index.ResolvePath("schema.json", "C:\\pkg\\Views"),
                Is.EqualTo("C:\\pkg\\Views\\schema.json"));

            // .txt is not indexed — falls back to base folder
            Assert.That(index.ResolvePath("notes.txt", "C:\\pkg\\Views"),
                Is.EqualTo(Path.Combine("C:\\pkg\\Views", "notes.txt")));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void Build_NonExistentFolder_ReturnsEmptyIndex()
    {
        var dir = Substitute.For<IDirectory>();
        dir.Exists("C:\\pkg\\NoSuchFolder").Returns(false);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build("C:\\pkg\\NoSuchFolder");
            var result = index.ResolvePath("dbo.MyView.sql", "C:\\pkg\\NoSuchFolder");
            Assert.That(result, Is.EqualTo(Path.Combine("C:\\pkg\\NoSuchFolder", "dbo.MyView.sql")));
            dir.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
            FactoryContainer.Clear();
        }
    }
}
