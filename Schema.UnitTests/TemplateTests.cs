// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using Schema.Isolators;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Schema.UnitTests;

public class TemplateTests
{
    [Test]
    public void ShouldLoadTemplateJson()
    {
        var templateJsonFile = Path.Combine("SchemaPackagePath", "Templates", "Test", "Template.json");
        var config = SetupConfig();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(templateJsonFile).Returns(templateJson);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);

            var template = Template.Load("Test", GetTestProduct());
            mockFileWrapper.Received(1).Exists(templateJsonFile);
            mockFileWrapper.Received(1).ReadAllText(templateJsonFile);
            Assert.Multiple(() =>
            {
                Assert.That(template.FilePath, Is.EqualTo(templateJsonFile));
                Assert.That(template.ObjectScripts, Is.Not.Null);
                Assert.That(template.DatabaseIdentificationScript, Contains.Substring("Database Identification Script"));
                Assert.That(template.VersionStampScript, Contains.Substring("Version Stamp Script"));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldErrorWhenTemplateJsonNotFound()
    {
        var templateJsonFile = Path.Combine("SchemaPackagePath", "Templates", "Test", "Template.json");
        var config = SetupConfig();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => Template.Load("Test", TemplateTests.GetTestProduct()));
            Assert.That(ex!.Message, Is.EqualTo($"File {templateJsonFile} does not exist"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldReplaceScriptTokensOnLoad()
    {
        var templateJsonFile = Path.Combine("SchemaPackagePath", "Templates", "Test", "Template.json");
        var config = SetupConfig();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(templateJsonFile).Returns(templateJson);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);
            var product = GetTestProduct();
            product.ScriptTokens.Add("MyVersion", "MyVersion");
            product.ScriptTokens.Add("TestDB", "TestDB");
            var template = Template.Load("Test", product);
            mockFileWrapper.Received(1).Exists(templateJsonFile);
            mockFileWrapper.Received(1).ReadAllText(templateJsonFile);
            Assert.Multiple(() =>
            {
                Assert.That(template.FilePath, Is.EqualTo(templateJsonFile));
                Assert.That(template.DatabaseIdentificationScript, Is.EqualTo("Database Identification Script TestDB"));
                Assert.That(template.VersionStampScript, Is.EqualTo("Version Stamp Script MyVersion"));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldLoadTables()
    {
        var templateJsonFile = Path.Combine("SchemaPackagePath", "Templates", "Test", "Template.json");
        var tableFilePath = Path.Combine("SchemaPackagePath", "Templates", "Test", "Tables");
        var tableFile = Path.Combine(tableFilePath, "dbo.TestTable.json");

        var config = SetupConfig();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockDirectoryWrapper.GetFiles(tableFilePath, "*.json", SearchOption.AllDirectories).Returns([tableFile]);

        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(templateJsonFile).Returns(templateJson);
        mockFileWrapper.ReadAllText(tableFile).Returns(@"{
	""Schema"": ""dbo"",
	""Name"": ""TestTable"",
	""CompressionType"": ""NONE"",
	""Columns"": [
		{
			""Name"": ""TestID"",
			""DataType"": ""UNIQUEIDENTIFIER""
		},
		{
			""Name"": ""DateCreated"",
			""DataType"": ""DATETIME""
		},
		{
			""Name"": ""Status"",
			""DataType"": ""TINYINT"",
			""Nullable"": true
		}
	],
	""Indexes"": [
		{
			""Name"": ""CIX_DateCreated"",
			""Clustered"": true,
			""IndexColumns"": ""DateCreated""
		},
		{
			""Name"": ""PK_TestTable"",
			""PrimaryKey"": true,
			""IndexColumns"": ""TestID""
		}
	],
	""ForeignKeys"": [],
	""CheckConstraints"": [],
	""Statistics"": []
}");
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);

            var template = Template.Load("Test", GetTestProduct());
            mockFileWrapper.Received(1).Exists(tableFile);
            mockFileWrapper.Received(1).ReadAllText(tableFile);
            Assert.Multiple(() =>
            {
                Assert.That(template.Tables, Has.Count.EqualTo(1));
                Assert.That(template.Tables.First().Name, Is.EqualTo("TestTable"));
                Assert.That(template.Tables.First().Columns, Has.Count.EqualTo(3));
                Assert.That(template.Tables.First().Indexes, Has.Count.EqualTo(2));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldLoadBeforeMigrationScripts()
    {
        SqlFileLoadTest(TemplateQuenchSlot.Before, "MigrationScripts/Before");
    }

    [Test]
    public void ShouldLoadAfterMigrationScripts()
    {
        SqlFileLoadTest(TemplateQuenchSlot.After, "MigrationScripts/After");
    }

    [Test]
    public void ShouldLoadBetweenTablesAndKeysScripts()
    {
        SqlFileLoadTest(TemplateQuenchSlot.BetweenTablesAndKeys, "MigrationScripts/BetweenTablesAndKeys");
    }

    [Test]
    public void ShouldLoadAfterTablesScripts()
    {
        SqlFileLoadTest(TemplateQuenchSlot.AfterTablesScripts, "MigrationScripts/AfterTablesScripts");
    }

    [Test]
    public void ShouldLoadSchemas()
    {
        ObjectLoadTest("Schemas");
    }

    [Test]
    public void ShouldLoadDataTypes()
    {
        ObjectLoadTest("DataTypes");
    }

    [Test]
    public void ShouldLoadFullTextCatalogs()
    {
        ObjectLoadTest("FullTextCatalogs");
    }

    [Test]
    public void ShouldLoadFullTextStopLists()
    {
        ObjectLoadTest("FullTextStopLists");
    }

    [Test]
    public void ShouldLoadFunctions()
    {
        ObjectLoadTest("Functions");
    }

    [Test]
    public void ShouldLoadViews()
    {
        ObjectLoadTest("Views");
    }

    [Test]
    public void ShouldLoadProcedures()
    {
        ObjectLoadTest("Procedures");
    }

    [Test]
    public void ShouldLoadTriggers()
    {
        AfterTablesObjectLoadTest("Triggers");
    }

    [Test]
    public void ShouldMergeTemplateTokensOverProductTokens()
    {
        var templateJsonWithTokens = """
        {
          "DatabaseIdentificationScript": "SELECT '{{MainDB}}'",
          "ScriptTokens": { "MainDB": "TemplateDB" }
        }
        """;

        var templateJsonFile = Path.Combine("SchemaPackagePath", "Templates", "Test", "Template.json");
        var config = SetupConfig();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(templateJsonFile).Returns(templateJsonWithTokens);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);
            var product = GetTestProduct();
            product.ScriptTokens.Add("MainDB", "ProductDB");
            var template = Template.Load("Test", product);
            Assert.That(template.DatabaseIdentificationScript, Is.EqualTo("SELECT 'TemplateDB'"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldLoadIndexedViews()
    {
        var templateJsonFile = Path.Combine("SchemaPackagePath", "Templates", "Test", "Template.json");
        var indexedViewsPath = Path.Combine("SchemaPackagePath", "Templates", "Test", "Indexed Views");
        var viewFile = Path.Combine(indexedViewsPath, "dbo.vw_ActiveOrders.json");

        var config = SetupConfig();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockDirectoryWrapper.GetFiles(indexedViewsPath, "*.json", SearchOption.AllDirectories).Returns([viewFile]);

        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(templateJsonFile).Returns(templateJson);
        mockFileWrapper.ReadAllText(viewFile).Returns("""
        {
            "Name": "vw_ActiveOrders",
            "Schema": "dbo",
            "Definition": "SELECT OrderId FROM dbo.Orders WHERE Status = 'Active'",
            "Indexes": [
                { "Name": "CIX_vw_ActiveOrders", "Clustered": true, "Unique": true, "IndexColumns": "OrderId" }
            ]
        }
        """);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);

            var template = Template.Load("Test", GetTestProduct());
            Assert.Multiple(() =>
            {
                Assert.That(template.IndexedViews, Has.Count.EqualTo(1));
                Assert.That(template.IndexedViews[0].Name, Is.EqualTo("vw_ActiveOrders"));
                Assert.That(template.IndexedViews[0].Indexes, Has.Count.EqualTo(1));
                Assert.That(template.IndexedViewSchema, Does.Contain("vw_ActiveOrders"));
            });

            FactoryContainer.Clear();
        }
    }

    private const string templateJson = """
{
  "DatabaseIdentificationScript": "Database Identification Script {{TestDB}}",
  "VersionStampScript": "Version Stamp Script {{MyVersion}}"
}
""";

    private static void ObjectLoadTest(string routinePath)
    {
        SqlFileLoadTest(TemplateQuenchSlot.Objects, routinePath);
    }

    private static void AfterTablesObjectLoadTest(string routinePath)
    {
        SqlFileLoadTest(TemplateQuenchSlot.AfterTablesObjects, routinePath);
    }

    private static void SqlFileLoadTest(TemplateQuenchSlot slot, string filePath)
    {
        var templateJsonFile = Path.Combine("SchemaPackagePath", "Templates", "Test", "Template.json");
        var sqlFilePath = Path.Combine("SchemaPackagePath", "Templates", "Test", filePath);
        var sqlFile = Path.Combine(sqlFilePath, "AFile.sql");

        var config = SetupConfig();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockDirectoryWrapper.GetFiles(sqlFilePath, "*.sql", SearchOption.AllDirectories).Returns([sqlFile]);

        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(templateJsonFile).Returns(templateJson);
        mockFileWrapper.ReadAllText(sqlFile).Returns($"Batch One{Environment.NewLine}GO{Environment.NewLine}Batch Two");
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);

            var template = Template.Load("Test", TemplateTests.GetTestProduct());
            mockDirectoryWrapper.Received(1).Exists(sqlFilePath);
            mockFileWrapper.Received(1).ReadAllText(sqlFile);
            var sqlFiles = slot switch
            {
                TemplateQuenchSlot.Before => template.BeforeScripts,
                TemplateQuenchSlot.Objects => template.ObjectScripts,
                TemplateQuenchSlot.BetweenTablesAndKeys => template.BetweenTablesAndKeysScripts,
                TemplateQuenchSlot.AfterTablesScripts => template.AfterTablesScripts,
                TemplateQuenchSlot.AfterTablesObjects => template.AfterTablesObjectScripts,
                TemplateQuenchSlot.TableData => template.TableDataScripts,
                TemplateQuenchSlot.After => template.AfterScripts,
                _ => throw new ArgumentException($"Unexpected slot: {slot}")
            };
            Assert.Multiple(() =>
            {
                Assert.That(sqlFiles, Is.Not.Null);
                Assert.That(sqlFiles, Has.Count.EqualTo(1));
                Assert.That(sqlFiles.First().Name, Is.EqualTo("AFile.sql"));
                Assert.That(sqlFiles.First().Batches, Has.Count.EqualTo(2));
                Assert.That(sqlFiles.First().Batches.First(), Is.EqualTo($"Batch One{Environment.NewLine}"));
                Assert.That(sqlFiles.First().Batches.Last(), Is.EqualTo($"Batch Two{Environment.NewLine}"));
            });

            FactoryContainer.Clear();
        }
    }

    private static IConfigurationRoot SetupConfig()
    {
        var configValues = new Dictionary<string, string>
        {
            ["Target:Server"] = "Server",
            ["SchemaPackagePath"] = "SchemaPackagePath",
            ["ScriptTokens:TestDB"] = "TestDatabase",
            ["ScriptTokens:MyVersion"] = "MyVersion"
        };

        var configBuilder = new ConfigurationBuilder();
        _ = configBuilder.AddInMemoryCollection(configValues);
        return configBuilder.Build();
    }

    private static Product GetTestProduct()
    {
        return new Product { FilePath = "SchemaPackagePath/product.json" };
    }
}
