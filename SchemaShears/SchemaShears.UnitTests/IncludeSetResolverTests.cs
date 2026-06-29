// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SchemaShears;

namespace SchemaShears.UnitTests;

[TestFixture]
public class IncludeSetResolverTests
{
    private string _source;
    private string _ordersRel;
    private string _mainTemplateRel;
    private string _productRel;

    [SetUp]
    public void SetUp()
    {
        _source = Path.Combine(Path.GetTempPath(), "shears-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_source, "Templates", "Main", "Tables"));
        _ordersRel = Path.Combine("Templates", "Main", "Tables", "dbo.Orders.json");
        _mainTemplateRel = Path.Combine("Templates", "Main", "Template.json");
        _productRel = "Product.json";
        File.WriteAllText(Path.Combine(_source, _ordersRel), "{}");
        File.WriteAllText(Path.Combine(_source, _mainTemplateRel), "{}");
        File.WriteAllText(Path.Combine(_source, _productRel), "{}");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_source, recursive: true);

    [Test]
    public void Resolve_AddsProductAndTouchedTemplateScaffolding()
    {
        var result = IncludeSetResolver.Resolve(new[] { _ordersRel }, new List<string>(), _source);

        Assert.Multiple(() =>
        {
            Assert.That(result[_ordersRel], Is.EqualTo(IncludeReason.Manifest));
            Assert.That(result[_productRel], Is.EqualTo(IncludeReason.Scaffolding));
            Assert.That(result[_mainTemplateRel], Is.EqualTo(IncludeReason.Scaffolding));
        });
    }

    [Test]
    public void Resolve_ManifestWinsOverScaffolding()
    {
        // Product.json explicitly in the manifest keeps the Manifest reason.
        var result = IncludeSetResolver.Resolve(new[] { _productRel }, new List<string>(), _source);

        Assert.That(result[_productRel], Is.EqualTo(IncludeReason.Manifest));
    }
}
