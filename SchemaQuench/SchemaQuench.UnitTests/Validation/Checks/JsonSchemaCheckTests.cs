// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Isolators;
using Schema.Utility;
using SchemaQuench.Validation;
using SchemaQuench.Validation.Checks;

namespace SchemaQuench.UnitTests.Validation.Checks;

/// <summary>
/// Slice 2.4: the final `--Validate` check. Presents a fake package tree via IFile/IDirectory
/// isolator mocks (schema text + JSON text in memory — no real disk, no DB), same convention as
/// TokenCheckTests. NJsonSchema validation and SchemaGenerator run FOR REAL against the mocked
/// content, so these tests exercise the actual staleness-compare and structural-validation logic,
/// not a stand-in.
/// </summary>
[TestFixture]
public class JsonSchemaCheckTests
{
    private const string PackagePath = @"C:\pkg";
    private static readonly string SchemaDir = Path.Combine(PackagePath, ".json-schemas");
    private static readonly string TablesSchemaPath = Path.Combine(SchemaDir, "tables.sqlserver.schema");
    private static readonly string ProductsSchemaPath = Path.Join(SchemaDir, "products.sqlserver.schema");

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

        _mockDirectory.Exists(SchemaDir).Returns(true);
        _mockDirectory.GetFiles(PackagePath, "*.json", SearchOption.AllDirectories).Returns(System.Array.Empty<string>());
    }

    [TearDown]
    public void TearDown() => FactoryContainer.Clear();

    private static Product Product() => new() { Name = "Acme", Platform = Platform.SqlServer };

    private static ValidationContext Context() => new(Product(), new List<Template>(), PackagePath);

    // Mirrors JsonSchemaCheck's own private PlatformElementResolver for Platform.SqlServer — used
    // here only to build the "current model" fresh schema fixtures, exactly as the check itself
    // would generate it, so committed-vs-fresh comparisons in these tests are meaningful.
    private static System.Func<System.Type, System.Type> SqlServerElementResolver() => t =>
        t == typeof(Column) ? typeof(SqlServerColumn)
        : t == typeof(Schema.Domain.Index) ? typeof(SqlServerIndex)
        : t == typeof(ForeignKey) ? typeof(SqlServerForeignKey)
        : t; // CheckConstraint has no SqlServer subclass (PlatformDeserializer.GetCheckConstraintType)

    private static JObject FreshTablesSchema() =>
        SchemaGenerator.GenerateSchema(typeof(SqlServerTable), SqlServerElementResolver());

    // Product has no Column/Index/ForeignKey/CheckConstraint members needing platform-subclass
    // resolution, so the identity resolver mirrors what JsonSchemaCheck's own
    // PlatformElementResolver would do for it.
    private static JObject FreshProductsSchema() =>
        SchemaGenerator.GenerateSchema(typeof(Product), t => t);

    private static JObject ColumnItemsProperties(JObject tableSchema) =>
        (JObject)tableSchema["properties"]!["Columns"]!["items"]!["properties"]!;

    private void CommitTablesSchema(JObject schema) =>
        FileContent(TablesSchemaPath, schema.ToString(Formatting.None));

    private void CommitProductsSchema(JObject schema) =>
        FileContent(ProductsSchemaPath, schema.ToString(Formatting.None));

    private void JsonFiles(params string[] files) =>
        _mockDirectory.GetFiles(PackagePath, "*.json", SearchOption.AllDirectories).Returns(files);

    private void FileContent(string path, string content)
    {
        _mockFile.Exists(path).Returns(true);
        _mockFile.ReadAllText(path).Returns(content);
    }

    private static string TableFilePath(string name = "Customer.json") =>
        Path.Combine(PackagePath, "Templates", "Main", "Tables", name);

    [Test]
    public void MisnamedProperty_IsError()
    {
        CommitTablesSchema(FreshTablesSchema());
        var tableFile = TableFilePath();
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Name"": ""Customer"", ""Columns"": [ { ""Nam"": ""Id"", ""DataType"": ""int"" } ] }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Some.Matches<Finding>(f => f.Code == "SS-JSON-001" && f.Message.Contains("Nam")));
        Assert.That(findings, Has.All.Matches<Finding>(f => f.Severity == Severity.Error && f.Category == "JsonSchema" && f.Location == tableFile));
    }

    [Test]
    public void MissingRequiredProperty_IsError()
    {
        CommitTablesSchema(FreshTablesSchema());
        var tableFile = TableFilePath();
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Name"": ""Customer"", ""Columns"": [ { ""Name"": ""Id"" } ] }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Code, Is.EqualTo("SS-JSON-001"));
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Category, Is.EqualTo("JsonSchema"));
        Assert.That(findings[0].Location, Is.EqualTo(tableFile));
        Assert.That(findings[0].Message, Does.Contain("DataType"));
    }

    [Test]
    public void CustomPropertyGovernanceViolation_IsError()
    {
        // Simulates a hand-authored Extensions fragment: the committed schema (not the bare
        // generated one) constrains a custom Column-level "DataClassification" property to an
        // enum. MergeExtensionsDefinition preserves exactly this kind of fragment across
        // regeneration, so validating the JSON file against the COMMITTED schema is what makes
        // custom-property governance work automatically.
        var schema = FreshTablesSchema();
        ColumnItemsProperties(schema)["Extensions"] = new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["DataClassification"] = new JObject { ["type"] = "string", ["enum"] = new JArray("Public", "PII", "Confidential") }
            },
            ["additionalProperties"] = false
        };
        CommitTablesSchema(schema);

        var tableFile = TableFilePath();
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Name"": ""Customer"", ""Columns"": [ { ""Name"": ""Id"", ""DataType"": ""int"", ""Extensions"": { ""DataClassification"": ""Nope"" } } ] }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Code, Is.EqualTo("SS-JSON-001"));
        Assert.That(findings[0].Message, Does.Contain("DataClassification"));
    }

    [Test]
    public void ValidFileAgainstFreshSchema_NoFindings()
    {
        CommitTablesSchema(FreshTablesSchema());
        var tableFile = TableFilePath();
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Name"": ""Customer"", ""Columns"": [ { ""Name"": ""Id"", ""DataType"": ""int"" } ] }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void StaleCommittedSchema_IsError_AndSkipsStructural()
    {
        // Committed schema is deliberately out of date: the current model generates "DataType" as
        // a Column property (Required), but the committed file omits it entirely — as if the
        // domain model gained a property since --WriteSchemasOnly was last run.
        var schema = FreshTablesSchema();
        var columnProps = ColumnItemsProperties(schema);
        columnProps.Remove("DataType");
        var required = (JArray)schema["properties"]!["Columns"]!["items"]!["required"]!;
        required.Remove(required.Single(t => t.ToString() == "DataType"));
        CommitTablesSchema(schema);

        // This JSON file would ALSO fail structurally against the (correct) fresh schema — missing
        // "Name" — but staleness must short-circuit Pass 2 so no SS-JSON-001 is emitted for tables.
        var tableFile = TableFilePath();
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Columns"": [ { ""Name"": ""Id"", ""DataType"": ""int"" } ] }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Code, Is.EqualTo("SS-STALE-001"));
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Category, Is.EqualTo("Staleness"));
        Assert.That(findings[0].Location, Is.EqualTo(TablesSchemaPath));
        Assert.That(findings.Any(f => f.Code == "SS-JSON-001"), Is.False);
    }

    // ---- Regression: #326 — a table named "Product" (MySQL-style file layout: no schema
    // prefix, so the table file is just Tables/Product.json) must classify as a TABLE, never
    // as the product manifest, purely on directory context. ----

    [Test]
    public void TableNamedProductUnderTablesFolder_ClassifiesAsTable_NotProductManifest()
    {
        CommitTablesSchema(FreshTablesSchema());
        CommitProductsSchema(FreshProductsSchema());
        var tableFile = TableFilePath("Product.json");
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Name"": ""Product"", ""Columns"": [ { ""Name"": ""Id"", ""DataType"": ""int"" } ] }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        // Valid table JSON, validated against the tables schema — if misclassified as the product
        // manifest instead, this would spuriously fail with "missing required property
        // 'ValidationScript'" (a Product-only required field).
        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void RealRootProductJson_StillClassifiesAsProductManifest()
    {
        CommitProductsSchema(FreshProductsSchema());
        var productFile = Path.Join(PackagePath, "Product.json");
        JsonFiles(productFile);
        FileContent(productFile, @"{ ""Name"": ""Acme"" }"); // missing required ValidationScript

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Code, Is.EqualTo("SS-JSON-001"));
        Assert.That(findings[0].Location, Is.EqualTo(productFile));
        Assert.That(findings[0].Message, Does.Contain("ValidationScript"));
    }

    // ---- Extra coverage beyond the brief's mandatory list ----

    [Test]
    public void NoJsonSchemasDirectory_NoFindings()
    {
        _mockDirectory.Exists(SchemaDir).Returns(false);

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void NoCommittedTablesSchema_TableFilesNotValidated()
    {
        // No tables.sqlserver.schema committed at all (e.g. a package that never ran
        // --WriteSchemasOnly) — nothing to validate against, so no findings for tables either way.
        var tableFile = TableFilePath();
        JsonFiles(tableFile);
        FileContent(tableFile, @"{ ""Columns"": [ { ""Nam"": ""Id"" } ] }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void JsonSchemasDirectoryFile_ExcludedFromStructuralScan()
    {
        CommitTablesSchema(FreshTablesSchema());
        var schemaFileAsJson = Path.Combine(SchemaDir, "tables.sqlserver.schema.json");
        JsonFiles(schemaFileAsJson);
        FileContent(schemaFileAsJson, @"{ ""whatever"": true }");

        var findings = new JsonSchemaCheck().Run(Context()).ToList();

        Assert.That(findings, Is.Empty);
    }
}
