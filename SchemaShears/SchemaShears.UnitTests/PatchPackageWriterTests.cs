// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class PatchPackageWriterTests
{
    private string _root;
    private string _source;
    private string _output;
    private string _ordersRel;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), "shears-" + System.Guid.NewGuid().ToString("N"));
        _source = Path.Join(_root, "product");
        _output = Path.Join(_root, "patch");
        _ordersRel = Path.Join("Templates", "Main", "Tables", "dbo.Orders.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Join(_source, _ordersRel)));
        File.WriteAllText(Path.Join(_source, _ordersRel), "{ \"Name\": \"Orders\" }");
        File.WriteAllText(Path.Join(_source, "Product.json"), "{}");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Write_CopiesFilesPreservingLayout_AndWritesReport()
    {
        var set = new Dictionary<string, IncludeReason>
        {
            [_ordersRel] = IncludeReason.Manifest,
            ["Product.json"] = IncludeReason.Scaffolding
        };

        PatchPackageWriter.Write(set, _source, _output);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Join(_output, _ordersRel)), Is.True);
            Assert.That(File.ReadAllText(Path.Join(_output, _ordersRel)), Is.EqualTo("{ \"Name\": \"Orders\" }"));
            var report = File.ReadAllText(Path.Join(_output, "patch-build-report.txt"));
            Assert.That(report, Does.Contain("dbo.Orders.json"));
            Assert.That(report, Does.Contain("Manifest"));
            Assert.That(report, Does.Contain("Scaffolding"));
        });
    }

    [Test]
    public void Write_NonEmptyOutput_Throws()
    {
        Directory.CreateDirectory(_output);
        File.WriteAllText(Path.Join(_output, "stale.txt"), "x");

        var set = new Dictionary<string, IncludeReason> { ["Product.json"] = IncludeReason.Scaffolding };

        Assert.Throws<PatchBuildException>(() => PatchPackageWriter.Write(set, _source, _output));
    }
}
