// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using log4net;
using NSubstitute;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.UnitTests.Domain
{
    /// <summary>
    /// Slice-3 audit B4: user-facing script tokens {{TableSchema}},
    /// {{MaterializedViewSchema}}, {{IndexedViewSchema}} must reflect the post-resolver
    /// Schema values, not the pre-resolver nulls. Tests load a template whose
    /// Before-Scripts folder contains a .sql file that references the token, then
    /// inspect the resolved script body to confirm the schema name is present.
    /// </summary>
    [TestFixture]
    public class TemplateLoadTokenResolutionTests
    {
        private IFile _mockFile;
        private IDirectory _mockDirectory;
        private ILog _mockProgressLog;

        [SetUp]
        public void SetUp()
        {
            FactoryContainer.Clear();
            LogFactory.Clear();
            _mockFile = Substitute.For<IFile>();
            _mockDirectory = Substitute.For<IDirectory>();
            FactoryContainer.Register<IFile>(_mockFile);
            FactoryContainer.Register<IDirectory>(_mockDirectory);

            _mockProgressLog = Substitute.For<ILog>();
            LogFactory.Register("ProgressLog", _mockProgressLog);
        }

        [TearDown]
        public void TearDown()
        {
            FactoryContainer.Clear();
            LogFactory.Clear();
        }

        private static Product MakeProduct(Platform platform) => new()
        {
            Name = "TestProduct",
            Platform = platform,
            FilePath = Path.Combine("C:", "products", "Product.json")
        };

        private static (string TemplateFile, string TemplateDir, string TablesPath) Paths()
        {
            var dir = Path.Combine("C:", "products", "Templates", "Main");
            return (Path.Combine(dir, "Template.json"), dir, Path.Combine(dir, "Tables"));
        }

        // ---- {{TableSchema}} ----

        [Test]
        public void SchemaTemplate_TableSchemaToken_ContainsSchemaNameLiteralInResolvedScripts()
        {
            var (templateFile, templateDir, tablesPath) = Paths();
            var templateJson = @"{
                ""Name"": ""Main"",
                ""SchemaIdentificationScript"": ""SELECT 'tenant_a'""
            }";
            _mockFile.Exists(templateFile).Returns(true);
            _mockFile.ReadAllText(templateFile).Returns(templateJson);

            _mockDirectory.Exists(tablesPath).Returns(true);
            var tableFile = Path.Combine(tablesPath, "Customers.json");
            _mockDirectory.GetFiles(tablesPath, "*.json", SearchOption.AllDirectories)
                .Returns(new[] { tableFile });
            _mockFile.Exists(tableFile).Returns(true);
            _mockFile.ReadAllText(tableFile).Returns(@"{ ""Name"": ""Customers"", ""Columns"": [] }");

            // Before Scripts folder with a .sql file that references {{TableSchema}}.
            var beforeFolder = Path.Combine(templateDir, "Before Scripts");
            _mockDirectory.Exists(beforeFolder).Returns(true);
            var scriptFile = Path.Combine(beforeFolder, "Init.sql");
            _mockDirectory.GetFiles(beforeFolder, "*.sql", SearchOption.AllDirectories)
                .Returns(new[] { scriptFile });
            _mockFile.Exists(scriptFile).Returns(true);
            _mockFile.ReadAllText(scriptFile).Returns("-- TableSchema: {{TableSchema}}");

            var template = Template.Load("Main", MakeProduct(Platform.SqlServer));

            var beforeScripts = template.BeforeScripts;
            Assert.That(beforeScripts, Has.Count.EqualTo(1));
            var body = string.Join("\n", beforeScripts[0].Batches);
            Assert.That(body, Does.Contain("\"Schema\": \"{{SchemaName}}\""),
                "Schema-template {{TableSchema}} substitution must carry the {{SchemaName}} token literal " +
                $"(post-resolver). Body was: {body}");
        }

        [Test]
        public void RegularTemplate_TableSchemaToken_ContainsResolvedSqlServerDefault()
        {
            var (templateFile, templateDir, tablesPath) = Paths();
            var templateJson = @"{ ""Name"": ""Main"" }";
            _mockFile.Exists(templateFile).Returns(true);
            _mockFile.ReadAllText(templateFile).Returns(templateJson);

            _mockDirectory.Exists(tablesPath).Returns(true);
            var tableFile = Path.Combine(tablesPath, "Customers.json");
            _mockDirectory.GetFiles(tablesPath, "*.json", SearchOption.AllDirectories)
                .Returns(new[] { tableFile });
            _mockFile.Exists(tableFile).Returns(true);
            _mockFile.ReadAllText(tableFile).Returns(@"{ ""Name"": ""Customers"", ""Columns"": [] }");

            var beforeFolder = Path.Combine(templateDir, "Before Scripts");
            _mockDirectory.Exists(beforeFolder).Returns(true);
            var scriptFile = Path.Combine(beforeFolder, "Init.sql");
            _mockDirectory.GetFiles(beforeFolder, "*.sql", SearchOption.AllDirectories)
                .Returns(new[] { scriptFile });
            _mockFile.Exists(scriptFile).Returns(true);
            _mockFile.ReadAllText(scriptFile).Returns("-- TableSchema: {{TableSchema}}");

            var template = Template.Load("Main", MakeProduct(Platform.SqlServer));

            var beforeScripts = template.BeforeScripts;
            Assert.That(beforeScripts, Has.Count.EqualTo(1));
            var body = string.Join("\n", beforeScripts[0].Batches);
            Assert.That(body, Does.Contain("\"Schema\": \"dbo\""),
                "Regular SqlServer template {{TableSchema}} must carry the platform default literal. " +
                $"Body was: {body}");
        }

        [Test]
        public void RegularTemplate_TableSchemaToken_PostgreSqlDefault()
        {
            var (templateFile, templateDir, tablesPath) = Paths();
            var templateJson = @"{ ""Name"": ""Main"" }";
            _mockFile.Exists(templateFile).Returns(true);
            _mockFile.ReadAllText(templateFile).Returns(templateJson);

            _mockDirectory.Exists(tablesPath).Returns(true);
            var tableFile = Path.Combine(tablesPath, "Customers.json");
            _mockDirectory.GetFiles(tablesPath, "*.json", SearchOption.AllDirectories)
                .Returns(new[] { tableFile });
            _mockFile.Exists(tableFile).Returns(true);
            _mockFile.ReadAllText(tableFile).Returns(@"{ ""Name"": ""Customers"", ""Columns"": [] }");

            var beforeFolder = Path.Combine(templateDir, "Before Scripts");
            _mockDirectory.Exists(beforeFolder).Returns(true);
            var scriptFile = Path.Combine(beforeFolder, "Init.sql");
            _mockDirectory.GetFiles(beforeFolder, "*.sql", SearchOption.AllDirectories)
                .Returns(new[] { scriptFile });
            _mockFile.Exists(scriptFile).Returns(true);
            _mockFile.ReadAllText(scriptFile).Returns("-- TableSchema: {{TableSchema}}");

            var template = Template.Load("Main", MakeProduct(Platform.PostgreSQL));

            var beforeScripts = template.BeforeScripts;
            Assert.That(beforeScripts, Has.Count.EqualTo(1));
            var body = string.Join("\n", beforeScripts[0].Batches);
            Assert.That(body, Does.Contain("\"Schema\": \"public\""),
                "Regular PostgreSQL template {{TableSchema}} must carry the platform default literal. " +
                $"Body was: {body}");
        }

        // ---- {{MaterializedViewSchema}} (PostgreSQL only) ----

        [Test]
        public void SchemaTemplate_MaterializedViewSchemaToken_ContainsSchemaNameLiteral()
        {
            var (templateFile, templateDir, tablesPath) = Paths();
            var templateJson = @"{
                ""Name"": ""Main"",
                ""SchemaIdentificationScript"": ""SELECT 'tenant_a'""
            }";
            _mockFile.Exists(templateFile).Returns(true);
            _mockFile.ReadAllText(templateFile).Returns(templateJson);
            _mockDirectory.Exists(tablesPath).Returns(false);

            var mvFolder = Path.Combine(templateDir, "Materialized Views");
            _mockDirectory.Exists(mvFolder).Returns(true);
            var mvFile = Path.Combine(mvFolder, "mv_test.json");
            _mockDirectory.GetFiles(mvFolder, "*.json", SearchOption.AllDirectories)
                .Returns(new[] { mvFile });
            _mockFile.ReadAllText(mvFile)
                .Returns(@"{""Name"":""mv_test"",""Definition"":""SELECT 1""}");

            var beforeFolder = Path.Combine(templateDir, "Before Scripts");
            _mockDirectory.Exists(beforeFolder).Returns(true);
            var scriptFile = Path.Combine(beforeFolder, "Init.sql");
            _mockDirectory.GetFiles(beforeFolder, "*.sql", SearchOption.AllDirectories)
                .Returns(new[] { scriptFile });
            _mockFile.Exists(scriptFile).Returns(true);
            _mockFile.ReadAllText(scriptFile).Returns("-- MVSchema: {{MaterializedViewSchema}}");

            var template = Template.Load("Main", MakeProduct(Platform.PostgreSQL));

            var beforeScripts = template.BeforeScripts;
            Assert.That(beforeScripts, Has.Count.EqualTo(1));
            var body = string.Join("\n", beforeScripts[0].Batches);
            Assert.That(body, Does.Contain("\"Schema\": \"{{SchemaName}}\""),
                "Schema-template {{MaterializedViewSchema}} must carry the {{SchemaName}} token literal. " +
                $"Body was: {body}");
        }

        // ---- {{IndexedViewSchema}} (SqlServer only) ----

        [Test]
        public void SchemaTemplate_IndexedViewSchemaToken_ContainsSchemaNameLiteral()
        {
            var (templateFile, templateDir, tablesPath) = Paths();
            var templateJson = @"{
                ""Name"": ""Main"",
                ""SchemaIdentificationScript"": ""SELECT 'tenant_a'""
            }";
            _mockFile.Exists(templateFile).Returns(true);
            _mockFile.ReadAllText(templateFile).Returns(templateJson);
            _mockDirectory.Exists(tablesPath).Returns(false);

            var ivFolder = Path.Combine(templateDir, "Indexed Views");
            _mockDirectory.Exists(ivFolder).Returns(true);
            var ivFile = Path.Combine(ivFolder, "iv_test.json");
            _mockDirectory.GetFiles(ivFolder, "*.json", SearchOption.AllDirectories)
                .Returns(new[] { ivFile });
            _mockFile.ReadAllText(ivFile)
                .Returns(@"{""Name"":""iv_test"",""Definition"":""SELECT 1""}");

            var beforeFolder = Path.Combine(templateDir, "Before Scripts");
            _mockDirectory.Exists(beforeFolder).Returns(true);
            var scriptFile = Path.Combine(beforeFolder, "Init.sql");
            _mockDirectory.GetFiles(beforeFolder, "*.sql", SearchOption.AllDirectories)
                .Returns(new[] { scriptFile });
            _mockFile.Exists(scriptFile).Returns(true);
            _mockFile.ReadAllText(scriptFile).Returns("-- IVSchema: {{IndexedViewSchema}}");

            var template = Template.Load("Main", MakeProduct(Platform.SqlServer));

            var beforeScripts = template.BeforeScripts;
            Assert.That(beforeScripts, Has.Count.EqualTo(1));
            var body = string.Join("\n", beforeScripts[0].Batches);
            Assert.That(body, Does.Contain("\"Schema\": \"{{SchemaName}}\""),
                "Schema-template {{IndexedViewSchema}} must carry the {{SchemaName}} token literal. " +
                $"Body was: {body}");
        }
    }
}
