// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class RepositoryHelperTests
{
    private IFile _file;
    private IDirectory _directory;

    [SetUp]
    public void SetUp()
    {
        FactoryContainer.Clear();
        _file = Substitute.For<IFile>();
        _directory = Substitute.For<IDirectory>();
        FactoryContainer.Register<IFile>(_file);
        FactoryContainer.Register<IDirectory>(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    [Test]
    public void GetSchemaFileNames_MySQL_ReturnsPlatformSpecificNames()
    {
        var names = RepositoryHelper.GetSchemaFileNames(Platform.MySQL);

        Assert.That(names, Has.Length.EqualTo(3));
        Assert.That(names[0], Does.StartWith("products."));
        Assert.That(names[1], Does.StartWith("templates."));
        Assert.That(names[2], Does.StartWith("tables."));

        var platformStr = Platform.MySQL.ToCanonicalString().ToLower();
        foreach (var name in names)
        {
            Assert.That(name, Does.Contain(platformStr));
            Assert.That(name, Does.EndWith(".schema"));
        }
    }

    [Test]
    public void GetSchemaFileNames_SqlServer_IncludesIndexedViewsSchema()
    {
        var names = RepositoryHelper.GetSchemaFileNames(Platform.SqlServer);

        Assert.That(names, Has.Length.EqualTo(4));
        Assert.That(names[0], Does.StartWith("products."));
        Assert.That(names[1], Does.StartWith("templates."));
        Assert.That(names[2], Does.StartWith("tables."));
        Assert.That(names[3], Is.EqualTo("indexedviews.sqlserver.schema"));

        var platformStr = Platform.SqlServer.ToCanonicalString().ToLower();
        foreach (var name in names)
        {
            Assert.That(name, Does.Contain(platformStr));
            Assert.That(name, Does.EndWith(".schema"));
        }
    }

    [Test]
    public void GetSchemaFileNames_PostgreSQL_IncludesMaterializedViewsSchema()
    {
        var names = RepositoryHelper.GetSchemaFileNames(Platform.PostgreSQL);

        Assert.That(names, Has.Length.EqualTo(4));
        Assert.That(names[0], Does.StartWith("products."));
        Assert.That(names[1], Does.StartWith("templates."));
        Assert.That(names[2], Does.StartWith("tables."));
        Assert.That(names[3], Is.EqualTo("materializedviews.postgresql.schema"));

        var platformStr = Platform.PostgreSQL.ToCanonicalString().ToLower();
        foreach (var name in names)
        {
            Assert.That(name, Does.Contain(platformStr));
            Assert.That(name, Does.EndWith(".schema"));
        }
    }

    [Test]
    [TestCase(Platform.SqlServer)]
    [TestCase(Platform.MySQL)]
    public void GetSchemaFileNames_NonPostgreSQL_DoesNotIncludeMaterializedViews(Platform platform)
    {
        var names = RepositoryHelper.GetSchemaFileNames(platform);

        Assert.That(names, Has.None.Contain("materializedviews"));
    }

    [Test]
    [TestCase(Platform.PostgreSQL)]
    [TestCase(Platform.MySQL)]
    public void GetSchemaFileNames_NonSqlServer_DoesNotIncludeIndexedViews(Platform platform)
    {
        var names = RepositoryHelper.GetSchemaFileNames(platform);

        Assert.That(names, Has.None.Contain("indexedviews"));
    }

    [Test]
    public void GetValidationScript_SqlServer_ContainsMasterSysDatabases()
    {
        var script = RepositoryHelper.GetValidationScript("MyTemplate", Platform.SqlServer);
        Assert.That(script, Does.Contain("master.sys.databases"));
        Assert.That(script, Does.Contain("{{MyTemplateDb}}"));
    }

    [Test]
    public void GetValidationScript_PostgreSQL_ContainsPgDatabase()
    {
        var script = RepositoryHelper.GetValidationScript("MyTemplate", Platform.PostgreSQL);
        Assert.That(script, Does.Contain("pg_database"));
        Assert.That(script, Does.Contain("{{MyTemplateDb}}"));
    }

    [Test]
    public void GetValidationScript_MySQL_ContainsInformationSchema()
    {
        var script = RepositoryHelper.GetValidationScript("MyTemplate", Platform.MySQL);
        Assert.That(script, Does.Contain("information_schema"));
        Assert.That(script, Does.Contain("{{MyTemplateDb}}"));
    }

    [Test]
    public void GetDatabaseIdentificationScript_SqlServer_ContainsSelect()
    {
        var script = RepositoryHelper.GetDatabaseIdentificationScript("MyTemplate", Platform.SqlServer);
        Assert.That(script, Does.Contain("SELECT"));
        Assert.That(script, Does.Contain("{{MyTemplateDb}}"));
    }

    [Test]
    public void GetDatabaseIdentificationScript_PostgreSQL_ContainsPgDatabase()
    {
        var script = RepositoryHelper.GetDatabaseIdentificationScript("MyTemplate", Platform.PostgreSQL);
        Assert.That(script, Does.Contain("pg_database"));
        Assert.That(script, Does.Contain("{{MyTemplateDb}}"));
    }

    [Test]
    public void GetDatabaseIdentificationScript_MySQL_ContainsSchemaName()
    {
        var script = RepositoryHelper.GetDatabaseIdentificationScript("MyTemplate", Platform.MySQL);
        Assert.That(script, Does.Contain("SCHEMA_NAME"));
        Assert.That(script, Does.Contain("{{MyTemplateDb}}"));
    }

    // --- SchemaIdentificationScript stub tests (design §7.5) ---

    [Test]
    public void GetSchemaIdentificationStub_SqlServer_UsesSchemaNameAliasUnquoted()
    {
        // Design §7.5: stub should select the source schema literal as a single-row example
        // aliased as SchemaName (matches the column-name convention used in built-in
        // identification scripts elsewhere in the codebase). SQL Server identifiers are
        // case-insensitive by default; no quoting needed.
        var script = RepositoryHelper.GetSchemaIdentificationStub("tenant_seed", Platform.SqlServer);
        Assert.That(script, Does.Contain("'tenant_seed'"));
        Assert.That(script, Does.Contain("AS SchemaName"));
        Assert.That(script, Does.Not.Contain("schemaname"),
            "SQL Server stub must use the canonical SchemaName casing, not lowercase.");
    }

    [Test]
    public void GetSchemaIdentificationStub_PostgreSQL_UsesQuotedSchemaNameAlias()
    {
        // Design §7.5: PG stub must use the same SchemaName alias as the SQL Server stub
        // for spec-text consistency. PostgreSQL folds unquoted identifiers to lowercase, so
        // the alias is double-quoted to preserve the canonical CamelCase casing.
        var script = RepositoryHelper.GetSchemaIdentificationStub("tenant_seed", Platform.PostgreSQL);
        Assert.That(script, Does.Contain("'tenant_seed'"));
        Assert.That(script, Does.Contain("AS \"SchemaName\""));
        Assert.That(script, Does.Not.Contain("AS schemaname"),
            "PG stub must quote SchemaName to keep the casing — bare schemaname folds and drifts from the SQL Server stub.");
    }

    // --- On-the-fly schema generation tests ---

    [Test]
    [TestCase(Platform.SqlServer)]
    [TestCase(Platform.PostgreSQL)]
    [TestCase(Platform.MySQL)]
    public void AddMissingSchemaFiles_WritesGeneratedSchemaFromCSharpTypes(Platform platform)
    {
        var writtenFiles = new Dictionary<string, string>();
        _file.Exists(Arg.Any<string>()).Returns(false);
        _file.When(f => f.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
            .Do(ci => writtenFiles[Path.GetFileName(ci.ArgAt<string>(0))] = ci.ArgAt<string>(1));

        RepositoryHelper.AddMissingSchemaFiles("/fake/product", platform);

        Assert.That(writtenFiles, Is.Not.Empty);
        foreach (var (_, content) in writtenFiles)
        {
            var schema = JObject.Parse(content);
            Assert.That(schema["type"]?.ToString(), Is.EqualTo("object"), "Generated schema should be an object");
            Assert.That(schema["properties"], Is.Not.Null, "Generated schema should have properties");
            // No x-core-version since we use on-the-fly generation
            Assert.That(schema["x-core-version"], Is.Null, "On-the-fly schema should not have x-core-version");
        }
    }

    [Test]
    public void WriteSchemaFilesWithResults_NewFile_CreatesFromGeneratedSchema()
    {
        var writtenFiles = new Dictionary<string, string>();
        _file.Exists(Arg.Any<string>()).Returns(false);
        _file.When(f => f.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
            .Do(ci => writtenFiles[Path.GetFileName(ci.ArgAt<string>(0))] = ci.ArgAt<string>(1));

        var results = RepositoryHelper.WriteSchemaFilesWithResults("/fake/product", Platform.SqlServer);

        Assert.That(results, Has.Count.EqualTo(4)); // products, templates, tables, indexedviews
        Assert.That(results, Has.All.Matches<SchemaFileResult>(r => r.WasCreated));
        Assert.That(writtenFiles, Has.Count.EqualTo(4));

        // Verify each written file is valid JSON with schema structure
        foreach (var (_, content) in writtenFiles)
        {
            var schema = JObject.Parse(content);
            Assert.That(schema["type"]?.ToString(), Is.EqualTo("object"));
            Assert.That(schema["properties"], Is.Not.Null);
        }
    }

    [Test]
    public void WriteSchemaFilesWithResults_ExistingFile_MergesExtensionsDefinition()
    {
        var existingSchemaContent = @"{
            ""type"": ""object"",
            ""properties"": {
                ""Name"": { ""type"": ""string"" },
                ""Extensions"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""customProp"": { ""type"": ""boolean"" }
                    }
                }
            }
        }";

        var writtenFiles = new Dictionary<string, string>();

        // Only the tables file "exists" with custom Extensions
        _file.Exists(Arg.Is<string>(s => s.Contains("tables.sqlserver"))).Returns(true);
        _file.Exists(Arg.Is<string>(s => !s.Contains("tables.sqlserver"))).Returns(false);
        _file.ReadAllText(Arg.Is<string>(s => s.Contains("tables.sqlserver"))).Returns(existingSchemaContent);
        _file.When(f => f.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
            .Do(ci => writtenFiles[Path.GetFileName(ci.ArgAt<string>(0))] = ci.ArgAt<string>(1));

        var results = RepositoryHelper.WriteSchemaFilesWithResults("/fake/product", Platform.SqlServer);

        // tables.sqlserver.schema should NOT be WasCreated (it existed)
        var tablesResult = results.Find(r => r.FileName.Contains("tables"));
        Assert.That(tablesResult, Is.Not.Null);
        Assert.That(tablesResult.WasCreated, Is.False);

        // Merged file should preserve user's Extensions definition
        var mergedContent = writtenFiles["tables.sqlserver.schema"];
        var merged = JObject.Parse(mergedContent);
        var extensions = merged["properties"]?["Extensions"];
        Assert.That(extensions, Is.Not.Null, "Extensions should survive merge");
        Assert.That(extensions["properties"]?["customProp"]?["type"]?.ToString(), Is.EqualTo("boolean"),
            "User's custom Extensions property should be preserved");
    }

    [Test]
    public void WriteSchemaFilesWithResults_ExistingFileWithoutExtensions_WritesGeneratedSchema()
    {
        var existingSchemaContent = @"{
            ""type"": ""object"",
            ""properties"": {
                ""Name"": { ""type"": ""string"" }
            }
        }";

        var writtenFiles = new Dictionary<string, string>();

        _file.Exists(Arg.Is<string>(s => s.Contains("products.sqlserver"))).Returns(true);
        _file.Exists(Arg.Is<string>(s => !s.Contains("products.sqlserver"))).Returns(false);
        _file.ReadAllText(Arg.Is<string>(s => s.Contains("products.sqlserver"))).Returns(existingSchemaContent);
        _file.When(f => f.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
            .Do(ci => writtenFiles[Path.GetFileName(ci.ArgAt<string>(0))] = ci.ArgAt<string>(1));

        RepositoryHelper.WriteSchemaFilesWithResults("/fake/product", Platform.SqlServer);

        // products file was updated with full generated schema
        var content = writtenFiles["products.sqlserver.schema"];
        var schema = JObject.Parse(content);
        // Should have the full generated schema with Name, ValidationScript, etc.
        Assert.That(schema["properties"]?["Name"], Is.Not.Null);
        Assert.That(schema["properties"]?["ValidationScript"], Is.Not.Null);
    }
}
