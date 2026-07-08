// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Linq;
using Schema.Isolators;

namespace Schema.UnitTests.Isolators;

[TestFixture]
public class DirectoryWrapperTests
{
    private string _root;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), "schemasmith-dirwrapper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Join(_root, "sub"));
        File.WriteAllText(Path.Join(_root, "top.sql"), "x");
        File.WriteAllText(Path.Join(_root, "top.txt"), "x");
        File.WriteAllText(Path.Join(_root, "sub", "nested.sql"), "x");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void EnumerateFiles_TopDirectoryOnly_ExcludesSubdirectories()
    {
        var files = DirectoryWrapper.GetFromFactory()
            .EnumerateFiles(_root, "*.sql", SearchOption.TopDirectoryOnly)
            .ToList();

        Assert.That(files, Has.Count.EqualTo(1));
        Assert.That(files[0], Does.EndWith("top.sql"));
    }

    [Test]
    public void EnumerateFiles_AllDirectories_IncludesNested()
    {
        var files = DirectoryWrapper.GetFromFactory()
            .EnumerateFiles(_root, "*.sql", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToList();

        Assert.That(files, Is.EquivalentTo(new[] { "top.sql", "nested.sql" }));
    }

    [Test]
    public void EnumerateFileSystemEntries_ReturnsBothFilesAndDirectories()
    {
        var entries = DirectoryWrapper.GetFromFactory()
            .EnumerateFileSystemEntries(_root, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToList();

        Assert.That(entries, Is.EquivalentTo(new[] { "top.sql", "top.txt", "sub" }));
    }
}
