// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using System.Linq;
using NUnit.Framework;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class AlwaysIncludeListTests
{
    private string _root;
    private string _source;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "shears-" + System.Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "product");
        var procs = Path.Combine(_source, "Templates", "Main", "Procedures");
        Directory.CreateDirectory(procs);
        File.WriteAllText(Path.Combine(procs, "SchemaSmith.CustomTableDrop.sql"), "x");
        File.WriteAllText(Path.Combine(procs, "SchemaSmith.CustomTableRestore.sql"), "x");
        File.WriteAllText(Path.Combine(_source, "Product.json"), "{}");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Expand_NullPath_ReturnsEmpty()
    {
        Assert.That(AlwaysIncludeList.Expand(null, _source), Is.Empty);
    }

    [Test]
    public void Expand_DirectoryEntry_ExpandsToAllFilesBeneath()
    {
        var cfg = Path.Combine(_root, "always.txt");
        File.WriteAllText(cfg, "Templates/Main/Procedures\n");

        var result = AlwaysIncludeList.Expand(cfg, _source);

        Assert.That(result, Is.EquivalentTo(new[]
        {
            Path.Combine("Templates", "Main", "Procedures", "SchemaSmith.CustomTableDrop.sql"),
            Path.Combine("Templates", "Main", "Procedures", "SchemaSmith.CustomTableRestore.sql")
        }));
    }

    [Test]
    public void Expand_FileEntry_IncludedAsIs()
    {
        var cfg = Path.Combine(_root, "always.txt");
        File.WriteAllText(cfg, "Templates/Main/Procedures/SchemaSmith.CustomTableDrop.sql\n");

        var result = AlwaysIncludeList.Expand(cfg, _source);

        Assert.That(result.Single(), Is.EqualTo(Path.Combine("Templates", "Main", "Procedures", "SchemaSmith.CustomTableDrop.sql")));
    }

    [Test]
    public void Expand_MissingEntry_Throws()
    {
        var cfg = Path.Combine(_root, "always.txt");
        File.WriteAllText(cfg, "Templates/Main/DoesNotExist\n");

        Assert.Throws<PatchBuildException>(() => AlwaysIncludeList.Expand(cfg, _source));
    }
}
