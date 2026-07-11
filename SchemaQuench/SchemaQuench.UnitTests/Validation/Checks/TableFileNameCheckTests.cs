// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NSubstitute;
using Schema.Domain;
using Schema.Isolators;
using SchemaQuench.Validation;
using SchemaQuench.Validation.Checks;

namespace SchemaQuench.UnitTests.Validation.Checks;

[TestFixture]
public class TableFileNameCheckTests
{
    private const string PackagePath = @"C:\pkg";
    private static readonly string TablesDir = Path.Join(PackagePath, "Templates", "Main", "Tables");

    private IFile _file;
    private IDirectory _directory;

    [SetUp]
    public void SetUp()
    {
        FactoryContainer.Clear();
        _file = Substitute.For<IFile>();
        _directory = Substitute.For<IDirectory>();
        FactoryContainer.Register(_file);
        FactoryContainer.Register(_directory);
        _directory.Exists(PackagePath).Returns(true);
    }

    [TearDown]
    public void TearDown() => FactoryContainer.Clear();

    private static ValidationContext Context() =>
        new(new Product { Name = "Acme", Platform = Platform.SqlServer }, new List<Template>(), PackagePath);

    private void StubTableFiles(params (string path, string json)[] files)
    {
        foreach (var (path, json) in files)
        {
            _file.Exists(path).Returns(true);
            _file.ReadAllText(path).Returns(json);
        }
        _directory.GetFiles(PackagePath, "*.json", SearchOption.AllDirectories).Returns(files.Select(f => f.path).ToArray());
    }

    private static string TableJson(string schema, string name, string variantName = "", string gate = "")
    {
        var parts = new List<string> { $"\"Name\":\"{name}\"" };
        if (!string.IsNullOrEmpty(schema)) parts.Add($"\"Schema\":\"{schema}\"");
        if (!string.IsNullOrEmpty(variantName)) parts.Add($"\"VariantName\":\"{variantName}\"");
        if (!string.IsNullOrEmpty(gate)) parts.Add($"\"ShouldApplyExpression\":\"{gate}\"");
        return "{" + string.Join(",", parts) + "}";
    }

    [Test]
    public void CanonicalName_NoFinding()
    {
        StubTableFiles((Path.Join(TablesDir, "dbo.Orders.json"), TableJson("dbo", "Orders")));

        var findings = new TableFileNameCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void QuotedContentIdentifiers_CanonicalFile_NoFinding()
    {
        // Content loads quoted ([dbo], [Widget]); the canonical filename is the bare identifier.
        StubTableFiles((Path.Join(TablesDir, "dbo.Widget.json"), TableJson("[dbo]", "[Widget]")));

        var findings = new TableFileNameCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void CanonicalVariantName_NoFinding()
    {
        StubTableFiles((Path.Join(TablesDir, "dbo.Orders.EU.json"), TableJson("dbo", "Orders", "EU", "region='EU'")));

        var findings = new TableFileNameCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void NonCanonicalVariantFile_WarnsWithCanonicalName_NeverErrors()
    {
        StubTableFiles((Path.Join(TablesDir, "Orders-EU.json"), TableJson("dbo", "Orders", "EU", "region='EU'")));

        var findings = new TableFileNameCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Warning));
        Assert.That(findings[0].Code, Is.EqualTo("SS-FILE-NAME-003"));
        Assert.That(findings[0].Message, Does.Contain("dbo.Orders.EU.json"));
    }

    [Test]
    public void NonTableJsonOutsideTablesFolder_Ignored()
    {
        StubTableFiles((Path.Join(PackagePath, "Templates", "Main", "TableData", "weird-name.json"), TableJson("dbo", "Orders")));

        var findings = new TableFileNameCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty); // not under a Tables/ folder
    }
}
