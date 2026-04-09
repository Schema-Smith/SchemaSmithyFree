// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using log4net;
using NSubstitute;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;

namespace SchemaTongs.UnitTests;

[TestFixture]
public class ExtractionFileIndexEncodingTests
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
    public void FindExistingPath_EncodedFileOnDisk_MatchesEncodedLookup()
    {
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "dbo.foo%3Abar.sql")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        var result = index.FindExistingPath("dbo.foo%3Abar.sql");

        Assert.That(result, Is.EqualTo(Path.Combine(basePath, "dbo.foo%3Abar.sql")));
    }

    [Test]
    public void FindExistingPath_OldUnencodedFileOnDisk_DoesNotMatchEncodedLookup()
    {
        // Old file with raw colon (valid on Linux, not Windows) becomes an orphan
        // when the new encoded name is looked up — this is the expected transition behavior.
        var basePath = ViewsPath;
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "dbo.foo:bar.sql")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        var result = index.FindExistingPath("dbo.foo%3Abar.sql");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindExistingPath_PlainFilename_StillFoundCorrectly()
    {
        var basePath = Path.Combine("C:", "pkg", "Tables");
        _directory.Exists(basePath).Returns(true);
        _directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Returns(
        [
            Path.Combine(basePath, "dbo.MyTable.json")
        ]);

        var index = new ExtractionFileIndex();
        index.BuildIndex(basePath);

        var result = index.FindExistingPath("dbo.MyTable.json");

        Assert.That(result, Is.EqualTo(Path.Combine(basePath, "dbo.MyTable.json")));
    }
}
