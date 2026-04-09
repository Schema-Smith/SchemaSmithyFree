// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using log4net;
using NSubstitute;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;

namespace SchemaTongs.UnitTests;

[TestFixture]
public class ExtractionFileIndexTests
{
    private IDirectory _directory;
    private ILog _progressLog;

    [SetUp]
    public void SetUp()
    {
        _directory = Substitute.For<IDirectory>();
        _progressLog = Substitute.For<ILog>();
        FactoryContainer.Register(_directory);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
        LogFactory.Clear();
    }

    private static string ViewsPath => Path.Combine("C:", "pkg", "Views");

    [Test]
    public void BuildIndex_ScansSubfolders_BuildsCorrectDictionary()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "dbo.ViewA.sql"),
            Path.Combine(basePath, "Reporting", "dbo.ReportView.sql"),
            Path.Combine(basePath, "Archive", "dbo.OldView.sql"),
            Path.Combine(basePath, "Reporting", "dbo.Summary.json")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        Assert.That(index.FindExistingPath("dbo.ViewA.sql"), Is.EqualTo(Path.Combine(basePath, "dbo.ViewA.sql")));
        Assert.That(index.FindExistingPath("dbo.ReportView.sql"), Is.EqualTo(Path.Combine(basePath, "Reporting", "dbo.ReportView.sql")));
        Assert.That(index.FindExistingPath("dbo.OldView.sql"), Is.EqualTo(Path.Combine(basePath, "Archive", "dbo.OldView.sql")));
        Assert.That(index.FindExistingPath("dbo.Summary.json"), Is.EqualTo(Path.Combine(basePath, "Reporting", "dbo.Summary.json")));
    }

    [Test]
    public void FindExistingPath_SingleMatch_ReturnsThatPath()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "Reporting", "dbo.MyView.sql")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        var result = index.FindExistingPath("dbo.MyView.sql");

        Assert.That(result, Is.EqualTo(Path.Combine(basePath, "Reporting", "dbo.MyView.sql")));
    }

    [Test]
    public void FindExistingPath_MultipleMatches_ReturnsNullAndLogsWarning()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "Reporting", "dbo.MyView.sql"),
            Path.Combine(basePath, "Archive", "dbo.MyView.sql")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        var result = index.FindExistingPath("dbo.MyView.sql");

        Assert.That(result, Is.Null);
        _progressLog.Received(1).Warn(Arg.Is<string>(s => s.Contains("dbo.MyView.sql") && s.Contains("multiple subfolders")));
    }

    [Test]
    public void FindExistingPath_NotFound_ReturnsNull()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "dbo.OtherView.sql")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        var result = index.FindExistingPath("dbo.NewView.sql");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindExistingPath_SqlNotFound_FindsSqlerrorVariant()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "Reporting", "dbo.MyView.sqlerror")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        var result = index.FindExistingPath("dbo.MyView.sql");

        Assert.That(result, Is.EqualTo(Path.Combine(basePath, "Reporting", "dbo.MyView.sqlerror")));
    }

    [Test]
    public void MarkWritten_AndGetOrphans_ExcludesWrittenFiles()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        var writtenPath = Path.Combine(basePath, "dbo.WrittenView.sql");
        var orphanPath = Path.Combine(basePath, "dbo.OrphanView.sql");
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            writtenPath,
            orphanPath
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);
        index.MarkWritten(writtenPath);

        var orphans = index.GetOrphans();

        Assert.That(orphans, Does.Not.Contain(writtenPath));
        Assert.That(orphans, Does.Contain(orphanPath));
    }

    [Test]
    public void ExcludeFromOrphans_ExcludedFilesNotInOrphanList()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        var excludedPath = Path.Combine(basePath, "dbo.Encrypted.sql");
        var orphanPath = Path.Combine(basePath, "dbo.OrphanView.sql");
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            excludedPath,
            orphanPath
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);
        index.ExcludeFromOrphans("dbo.Encrypted.sql");

        var orphans = index.GetOrphans();

        Assert.That(orphans, Does.Not.Contain(excludedPath));
        Assert.That(orphans, Does.Contain(orphanPath));
    }

    [Test]
    public void BuildIndex_IndexesJsonFiles_ForTableFolders()
    {
        var basePath = Path.Combine("C:", "pkg", "Tables");
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "dbo.Orders.json"),
            Path.Combine(basePath, "dbo.Customers.json"),
            Path.Combine(basePath, "Archive", "dbo.OldOrders.json")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        Assert.That(index.FindExistingPath("dbo.Orders.json"), Is.EqualTo(Path.Combine(basePath, "dbo.Orders.json")));
        Assert.That(index.FindExistingPath("dbo.OldOrders.json"), Is.EqualTo(Path.Combine(basePath, "Archive", "dbo.OldOrders.json")));
    }

    [Test]
    public void BuildIndex_NonExistentDirectory_DoesNothing()
    {
        _directory.Exists(Arg.Any<string>()).Returns(false);

        var index = new ExtractionFileIndex();

        Assert.DoesNotThrow(() => index.BuildIndex(Path.Combine("C:", "nonexistent")));
        Assert.That(index.GetOrphans(), Is.Empty);
    }

    [Test]
    public void FindExistingPath_CaseInsensitiveMatch_ReturnsPath()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "dbo.MyView.sql")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        Assert.That(index.FindExistingPath("DBO.MYVIEW.SQL"), Is.EqualTo(Path.Combine(basePath, "dbo.MyView.sql")));
        Assert.That(index.FindExistingPath("dbo.myview.sql"), Is.EqualTo(Path.Combine(basePath, "dbo.MyView.sql")));
    }
}
