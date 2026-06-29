// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class PatchBuilderTests
{
    private string _root;
    private string _source;
    private string _output;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "shears-" + System.Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "product");
        _output = Path.Combine(_root, "patch");
        var tables = Path.Combine(_source, "Templates", "Main", "Tables");
        Directory.CreateDirectory(tables);
        File.WriteAllText(Path.Combine(tables, "dbo.Orders.json"), "{ \"Name\": \"Orders\" }");
        File.WriteAllText(Path.Combine(_source, "Templates", "Main", "Template.json"), "{}");
        File.WriteAllText(Path.Combine(_source, "Product.json"), "{ \"Name\": \"Acme\" }");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Build_ProducesSubsetWithScaffoldingAndLatch()
    {
        var manifest = Path.Combine(_root, "m.txt");
        File.WriteAllText(manifest, "Templates/Main/Tables/dbo.Orders.json\n");

        new PatchBuilder().Build(new PatchBuildRequest
        {
            SourcePath = _source, ManifestPath = manifest, OutputPath = _output, Zip = false
        });

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_output, "Templates", "Main", "Tables", "dbo.Orders.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_output, "Templates", "Main", "Template.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_output, "patch-build-report.txt")), Is.True);
            var product = JObject.Parse(File.ReadAllText(Path.Combine(_output, "Product.json")));
            Assert.That(product["DropTablesRemovedFromProduct"].Value<bool>(), Is.False);
        });
    }

    [Test]
    public void Build_SourceMissingProductJson_Throws()
    {
        var bareSource = Path.Combine(_root, "bare");
        Directory.CreateDirectory(bareSource);
        var manifest = Path.Combine(_root, "m.txt");
        File.WriteAllText(manifest, "# empty-ish\n");

        Assert.Throws<PatchBuildException>(() => new PatchBuilder().Build(new PatchBuildRequest
        {
            SourcePath = bareSource, ManifestPath = manifest, OutputPath = _output
        }));
    }
}
