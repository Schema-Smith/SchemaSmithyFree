// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using NSubstitute;
using Schema.Domain;
using Schema.Isolators;

namespace SchemaTongs.UnitTests;

[TestFixture]
public class TableFileResolverTests
{
    private IFile _file;
    private IDirectory _directory;
    private const string TablesDir = "pkg/Tables";

    [SetUp]
    public void SetUp()
    {
        _file = Substitute.For<IFile>();
        _directory = Substitute.For<IDirectory>();
        FactoryContainer.Register(_file);
        FactoryContainer.Register(_directory);
        _directory.Exists(Arg.Any<string>()).Returns(false);
        _directory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns([]);
    }

    [TearDown]
    public void TearDown() => FactoryContainer.Clear();

    // Registers a Tables/ folder holding the given (path, json-content) files so JsonHelper.TableLoad
    // deserializes each. Mirrors the file-read isolation in OrphanHandlerTests.
    private void StubTablesFolder(params (string path, string json)[] files)
    {
        _directory.Exists(TablesDir).Returns(true);
        var paths = new string[files.Length];
        for (var i = 0; i < files.Length; i++)
        {
            paths[i] = files[i].path;
            _file.Exists(files[i].path).Returns(true);
            _file.ReadAllText(files[i].path).Returns(files[i].json);
        }
        _directory.GetFiles(TablesDir, "*.json", SearchOption.AllDirectories).Returns(paths);
    }

    private static string TableJson(string schema, string name, string variantName = "", string gate = "")
    {
        var parts = new System.Collections.Generic.List<string> { $"\"Name\":\"{name}\"" };
        if (!string.IsNullOrEmpty(schema)) parts.Add($"\"Schema\":\"{schema}\"");
        if (!string.IsNullOrEmpty(variantName)) parts.Add($"\"VariantName\":\"{variantName}\"");
        if (!string.IsNullOrEmpty(gate)) parts.Add($"\"ShouldApplyExpression\":\"{gate}\"");
        return "{" + string.Join(",", parts) + "}";
    }

    [Test]
    public void Resolve_NoExistingFileForTable_ReturnsCanonicalBarePath_RefreshTrue()
    {
        _directory.Exists(TablesDir).Returns(true);
        _directory.GetFiles(TablesDir, "*.json", SearchOption.AllDirectories).Returns([]);

        var resolver = new TableFileResolver(TablesDir, Platform.SqlServer, isSchemaTemplate: false);
        var res = resolver.Resolve("dbo", "Orders");

        Assert.That(res.RefreshStructure, Is.True);
        Assert.That(res.IsVariantSet, Is.False);
        Assert.That(Path.GetFileName(res.WritePath), Is.EqualTo("dbo.Orders.json"));
    }

    [Test]
    public void Resolve_SingleExistingMatch_NonCanonicalName_ReturnsExistingPath_RefreshTrue()
    {
        var existing = Path.Combine("pkg", "Tables", "legacy-name.json"); // deliberately non-canonical
        StubTablesFolder((existing, TableJson("dbo", "Orders")));

        var resolver = new TableFileResolver(TablesDir, Platform.SqlServer, isSchemaTemplate: false);
        var res = resolver.Resolve("dbo", "Orders");

        Assert.That(res.RefreshStructure, Is.True);
        Assert.That(res.IsVariantSet, Is.False);
        Assert.That(res.WritePath, Is.EqualTo(existing)); // refresh in place, do not duplicate
    }

    [Test]
    public void Resolve_VariantSet_RefreshFalse_IsVariantSetTrue()
    {
        StubTablesFolder(
            (Path.Combine("pkg", "Tables", "dbo.Orders.EU.json"), TableJson("dbo", "Orders", "EU", "1=1")),
            (Path.Combine("pkg", "Tables", "dbo.Orders.US.json"), TableJson("dbo", "Orders", "US", "1=0")));

        var resolver = new TableFileResolver(TablesDir, Platform.SqlServer, isSchemaTemplate: false);
        var res = resolver.Resolve("dbo", "Orders");

        Assert.That(res.RefreshStructure, Is.False);
        Assert.That(res.IsVariantSet, Is.True);
    }

    [Test]
    public void Resolve_DifferentSchemaSameName_AreDistinctIdentities()
    {
        StubTablesFolder(
            (Path.Combine("pkg", "Tables", "sales.Orders.json"), TableJson("sales", "Orders")),
            (Path.Combine("pkg", "Tables", "hr.Orders.json"), TableJson("hr", "Orders")));

        var resolver = new TableFileResolver(TablesDir, Platform.SqlServer, isSchemaTemplate: false);
        var sales = resolver.Resolve("sales", "Orders");

        Assert.That(sales.IsVariantSet, Is.False); // not a variant set — different schemas
        Assert.That(sales.WritePath, Is.EqualTo(Path.Combine("pkg", "Tables", "sales.Orders.json")));
    }

    [Test]
    public void Resolve_MySqlNoSchemaContent_MatchesByNameAlone()
    {
        var existing = Path.Combine("pkg", "Tables", "Documents.json");
        StubTablesFolder((existing, TableJson("", "Documents")));

        var resolver = new TableFileResolver(TablesDir, Platform.MySQL, isSchemaTemplate: true);
        var res = resolver.Resolve("", "Documents");

        Assert.That(res.RefreshStructure, Is.True);
        Assert.That(res.WritePath, Is.EqualTo(existing));
    }
}
