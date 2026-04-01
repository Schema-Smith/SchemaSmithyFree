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
        var basePath = "C:\\pkg\\Views";
        var viewFile = Path.Combine(basePath, "dbo.MyView.sql");
        var reportFile = Path.Combine(basePath, "Reporting", "dbo.SalesReport.sql");

        var dir = Substitute.For<IDirectory>();
        dir.Exists(basePath).Returns(true);
        dir.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
            .Returns(new[] { viewFile, reportFile });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build(basePath);
            Assert.That(index.ResolvePath("dbo.MyView.sql", basePath), Is.EqualTo(viewFile));
            Assert.That(index.ResolvePath("dbo.SalesReport.sql", basePath), Is.EqualTo(reportFile));
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
        var basePath = "C:\\pkg\\Views";
        var file1 = Path.Combine(basePath, "dbo.MyView.sql");
        var file2 = Path.Combine(basePath, "Reporting", "dbo.MyView.sql");

        var dir = Substitute.For<IDirectory>();
        dir.Exists(basePath).Returns(true);
        dir.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
            .Returns(new[] { file1, file2 });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build(basePath);
            var result = index.ResolvePath("dbo.MyView.sql", basePath);
            Assert.That(result, Is.EqualTo(Path.Combine(basePath, "dbo.MyView.sql")));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ResolvePath_MultipleMatches_LogsWarning()
    {
        var basePath = "C:\\pkg\\Views";
        var file1 = Path.Combine(basePath, "dbo.MyView.sql");
        var file2 = Path.Combine(basePath, "Reporting", "dbo.MyView.sql");

        var dir = Substitute.For<IDirectory>();
        dir.Exists(basePath).Returns(true);
        dir.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
            .Returns(new[] { file1, file2 });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build(basePath);
            index.ResolvePath("dbo.MyView.sql", basePath);
            _progressLog.Received(1).Warn(Arg.Is<string>(s => s.Contains("dbo.MyView.sql") && s.Contains("multiple subfolders")));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void GetOrphans_ReturnsFilesNotWrittenTo()
    {
        var basePath = "C:\\pkg\\Views";
        var fileA = Path.Combine(basePath, "dbo.ViewA.sql");
        var fileB = Path.Combine(basePath, "dbo.ViewB.sql");

        var dir = Substitute.For<IDirectory>();
        dir.Exists(basePath).Returns(true);
        dir.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
            .Returns(new[] { fileA, fileB });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build(basePath);
            index.MarkWritten(fileA);
            var orphans = index.GetOrphans();
            Assert.That(orphans, Has.Count.EqualTo(1));
            Assert.That(orphans[0], Is.EqualTo(fileB));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ExcludeFromOrphans_PreventsOrphanDetection()
    {
        var basePath = "C:\\pkg\\Views";
        var file = Path.Combine(basePath, "dbo.EncryptedView.sql");

        var dir = Substitute.For<IDirectory>();
        dir.Exists(basePath).Returns(true);
        dir.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
            .Returns(new[] { file });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build(basePath);
            index.ExcludeFromOrphans("dbo.EncryptedView.sql");
            var orphans = index.GetOrphans();
            Assert.That(orphans, Is.Empty);
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void Build_IndexesSqlSqlerrorAndJsonFiles()
    {
        var basePath = "C:\\pkg\\Views";
        var sqlFile = Path.Combine(basePath, "dbo.MyView.sql");
        var sqlerrorFile = Path.Combine(basePath, "dbo.MyView.sqlerror");
        var jsonFile = Path.Combine(basePath, "schema.json");
        var txtFile = Path.Combine(basePath, "notes.txt");

        var dir = Substitute.For<IDirectory>();
        dir.Exists(basePath).Returns(true);
        dir.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
            .Returns(new[] { sqlFile, sqlerrorFile, jsonFile, txtFile });

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IDirectory>(dir);
            var index = ExtractionFileIndex.Build(basePath);

            // Indexed files resolve to their paths
            Assert.That(index.ResolvePath("dbo.MyView.sql", basePath), Is.EqualTo(sqlFile));
            Assert.That(index.ResolvePath("dbo.MyView.sqlerror", basePath), Is.EqualTo(sqlerrorFile));
            Assert.That(index.ResolvePath("schema.json", basePath), Is.EqualTo(jsonFile));

            // .txt is not indexed — falls back to base folder
            Assert.That(index.ResolvePath("notes.txt", basePath),
                Is.EqualTo(Path.Combine(basePath, "notes.txt")));

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
