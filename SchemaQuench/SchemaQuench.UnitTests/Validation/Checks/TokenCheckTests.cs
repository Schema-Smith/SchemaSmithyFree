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

/// <summary>
/// Slice 2.3: text-level {{Token}} lint over the RAW package files, not the loaded domain model —
/// Template.Load resolves known tokens IN PLACE during load (Table.
/// ResolveScriptTokensInTableComponentScripts etc.), so a post-load scan can no longer see
/// definitions vs. references. Uses IFile/IDirectory isolator mocks to present a fake package
/// tree (no real disk, no DB), matching the convention Schema.UnitTests already uses for
/// Template.Load token-resolution tests.
/// </summary>
[TestFixture]
public class TokenCheckTests
{
    private const string PackagePath = @"C:\pkg";

    private IFile _mockFile;
    private IDirectory _mockDirectory;

    [SetUp]
    public void SetUp()
    {
        FactoryContainer.Clear();
        _mockFile = Substitute.For<IFile>();
        _mockDirectory = Substitute.For<IDirectory>();
        FactoryContainer.Register(_mockFile);
        FactoryContainer.Register(_mockDirectory);

        _mockDirectory.Exists(PackagePath).Returns(true);
        _mockDirectory.GetFiles(PackagePath, "*.sql", SearchOption.AllDirectories).Returns(System.Array.Empty<string>());
        _mockDirectory.GetFiles(PackagePath, "*.json", SearchOption.AllDirectories).Returns(System.Array.Empty<string>());
    }

    [TearDown]
    public void TearDown() => FactoryContainer.Clear();

    private static Product Product() => new() { Name = "Acme", Platform = Platform.SqlServer };

    private static ValidationContext Context() => new(Product(), new List<Template>(), PackagePath);

    private void SqlFiles(params string[] files) =>
        _mockDirectory.GetFiles(PackagePath, "*.sql", SearchOption.AllDirectories).Returns(files);

    private void JsonFiles(params string[] files) =>
        _mockDirectory.GetFiles(PackagePath, "*.json", SearchOption.AllDirectories).Returns(files);

    private void FileContent(string path, string content)
    {
        _mockFile.Exists(path).Returns(true);
        _mockFile.ReadAllText(path).Returns(content);
    }

    [Test]
    public void UndefinedTokenInColumnDefault_IsError()
    {
        var tableFile = Path.Combine(PackagePath, "Templates", "Main", "Tables", "Customer.json");
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Name"": ""Customer"", ""Columns"": [ { ""Name"": ""Id"", ""Default"": ""{{Nope}}"" } ] }");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Code, Is.EqualTo("SS-TOK-001"));
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Category, Is.EqualTo("Token"));
        Assert.That(findings[0].Location, Is.EqualTo(tableFile));
    }

    [Test]
    public void DefinedProductScriptToken_Referenced_NoFinding()
    {
        var productFile = Path.Combine(PackagePath, "Product.json");
        var scriptFile = Path.Combine(PackagePath, "Templates", "Main", "Before Scripts", "Init.sql");
        JsonFiles(productFile);
        SqlFiles(scriptFile);
        FileContent(productFile, @"{ ""Name"": ""Acme"", ""ScriptTokens"": { ""Env"": ""prod"" } }");
        FileContent(scriptFile, "SELECT '{{Env}}'");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void MalformedToken_UnmatchedBraces_IsError()
    {
        var scriptFile = Path.Combine(PackagePath, "Templates", "Main", "Before Scripts", "Bad.sql");
        SqlFiles(scriptFile);
        FileContent(scriptFile, "SELECT '{{Foo'");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Code, Is.EqualTo("SS-TOK-002"));
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Location, Is.EqualTo(scriptFile));
    }

    [Test]
    public void UnusedDefinedToken_IsWarning()
    {
        var productFile = Path.Combine(PackagePath, "Product.json");
        JsonFiles(productFile);
        FileContent(productFile, @"{ ""Name"": ""Acme"", ""ScriptTokens"": { ""Unused"": ""x"" } }");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Code, Is.EqualTo("SS-TOK-003"));
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Warning));
    }

    [Test]
    public void SchemaNameToken_NotFlagged()
    {
        var scriptFile = Path.Combine(PackagePath, "Templates", "Main", "Before Scripts", "Init.sql");
        SqlFiles(scriptFile);
        FileContent(scriptFile, "SELECT '{{SchemaName}}'");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void BuiltInProductNameToken_NotFlagged()
    {
        var scriptFile = Path.Combine(PackagePath, "Templates", "Main", "Before Scripts", "Init.sql");
        SqlFiles(scriptFile);
        FileContent(scriptFile, "SELECT '{{ProductName}}'");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void CleanPackage_NoFindings()
    {
        var productFile = Path.Combine(PackagePath, "Product.json");
        var scriptFile = Path.Combine(PackagePath, "Templates", "Main", "Before Scripts", "Init.sql");
        var tableFile = Path.Combine(PackagePath, "Templates", "Main", "Tables", "Customer.json");
        JsonFiles(productFile, tableFile);
        SqlFiles(scriptFile);
        FileContent(productFile, @"{ ""Name"": ""Acme"", ""ScriptTokens"": { ""Env"": ""prod"" } }");
        FileContent(scriptFile, "SELECT '{{Env}}', '{{ProductName}}', '{{SchemaName}}'");
        FileContent(tableFile, @"{ ""Name"": ""Customer"", ""Columns"": [ { ""Name"": ""Id"" } ] }");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    // ---- Extra coverage beyond the brief's mandatory list ----

    [Test]
    public void ExtensionsDerivedToken_ReferencedBareOrPrefixed_NotFlagged()
    {
        // Safety valve: Table.GetCustomTokens' prefixing ("Table."/"MaterializedView."/
        // "IndexedView.") depends on which object owns the Extensions block — this raw-text
        // check can't fully re-derive that, so both bare and every known-prefix form of an
        // Extensions-derived name must be treated as defined.
        var tableFile = Path.Combine(PackagePath, "Templates", "Main", "Tables", "Customer.json");
        var scriptFile = Path.Combine(PackagePath, "Templates", "Main", "Before Scripts", "Init.sql");
        JsonFiles(tableFile);
        SqlFiles(scriptFile);
        FileContent(tableFile, @"{ ""Name"": ""Customer"", ""Columns"": [], ""Extensions"": { ""Classification"": ""PII"" } }");
        FileContent(scriptFile, "SELECT '{{Classification}}', '{{Table.Classification}}'");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void JsonSchemasDirectory_FilesExcludedFromScan()
    {
        var schemaFile = Path.Combine(PackagePath, ".json-schemas", "tables.sqlserver.schema");
        JsonFiles(schemaFile);
        FileContent(schemaFile, @"{ ""Description"": ""{{WhateverUndefinedToken}}"" }");

        var findings = new TokenCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }
}
