// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using NUnit.Framework;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class PatchManifestTests
{
    private string _root;
    private string _source;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "shears-" + System.Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "product");
        Directory.CreateDirectory(Path.Combine(_source, "Templates", "Main", "Tables"));
        File.WriteAllText(Path.Combine(_source, "Templates", "Main", "Tables", "dbo.Orders.json"), "{}");
        File.WriteAllText(Path.Combine(_source, "Product.json"), "{}");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Read_ValidEntries_ReturnsThem_IgnoringBlanksAndComments()
    {
        var manifest = Path.Combine(_root, "m.txt");
        File.WriteAllText(manifest, "# changed files\n\nTemplates/Main/Tables/dbo.Orders.json\n");

        var result = PatchManifest.Read(manifest, _source);

        Assert.That(result, Is.EqualTo(new[] { Path.Combine("Templates", "Main", "Tables", "dbo.Orders.json") }));
    }

    [Test]
    public void Read_PathNotUnderSource_Throws_NamingThePath()
    {
        var manifest = Path.Combine(_root, "m.txt");
        File.WriteAllText(manifest, "Templates/Main/Tables/dbo.Missing.json\n");

        var ex = Assert.Throws<PatchBuildException>(() => PatchManifest.Read(manifest, _source));
        Assert.That(ex.Message, Does.Contain("dbo.Missing.json"));
    }

    [Test]
    public void Read_EmptyManifest_Throws()
    {
        var manifest = Path.Combine(_root, "m.txt");
        File.WriteAllText(manifest, "# only comments\n\n");

        Assert.Throws<PatchBuildException>(() => PatchManifest.Read(manifest, _source));
    }

    [Test]
    public void Read_MissingManifestFile_Throws()
    {
        Assert.Throws<PatchBuildException>(() => PatchManifest.Read(Path.Combine(_root, "nope.txt"), _source));
    }
}
