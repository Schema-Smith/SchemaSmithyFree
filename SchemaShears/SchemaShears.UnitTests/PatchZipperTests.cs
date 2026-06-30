// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class PatchZipperTests
{
    private string _root;
    private string _output;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), "shears-" + System.Guid.NewGuid().ToString("N"));
        _output = Path.Join(_root, "patch");
        Directory.CreateDirectory(_output);
        File.WriteAllText(Path.Join(_output, "Product.json"), "{}");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Zip_CreatesZipContainingFiles()
    {
        var zipPath = PatchZipper.Zip(_output);

        Assert.That(File.Exists(zipPath), Is.True);
        using var archive = ZipFile.OpenRead(zipPath);
        Assert.That(archive.Entries.Any(e => e.FullName.EndsWith("Product.json")), Is.True);
    }
}
