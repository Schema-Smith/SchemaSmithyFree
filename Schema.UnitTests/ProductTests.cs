// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Schema.Domain;
using Schema.Isolators;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;

namespace Schema.UnitTests;

public class ProductTests
{
    [Test]
    public void ShouldErrorOnBadProductPath()
    {
        var configValues = new Dictionary<string, string>
        {
            ["SchemaPackagePath"] = "badPath"
        };

        var configBuilder = new ConfigurationBuilder();
        _ = configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();

        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);

            var ex = Assert.Throws<Exception>(() => Product.Load());
            Assert.That(ex!.Message, Contains.Substring("Path not found badPath"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldDeserializeProductProperly()
    {
        var productJson = """
            {
                "Name": "TestProduct",
                "ValidationScript": "SELECT CAST(CASE WHEN EXISTS(SELECT * FROM master.dbo.sysdatabases WHERE [name] = '{{PrimaryDB}}') THEN 1 ELSE 0 END AS BIT)",
                "TemplateOrder": [
                    "Utility",
                    "Application"
                ],
                "ScriptTokens": {
                    "PrimaryDB": "",
                    "ReleaseVersion": ""
                }
            }
            """;

        var configValues = new Dictionary<string, string>
        {
            ["SchemaPackagePath"] = "SchemaPackagePath",
            ["ScriptTokens:PrimaryDB"] = "PrimaryDB",
            ["ScriptTokens:ReleaseVersion"] = "ReleaseVersion"
        };
        var configBuilder = new ConfigurationBuilder();
        _ = configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();
        var productJsonFile = Path.Combine("SchemaPackagePath", "Product.json");
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(productJsonFile).Returns(productJson);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);

            var product = Product.Load();
            mockFileWrapper.Received(1).Exists(productJsonFile);
            mockFileWrapper.Received(1).ReadAllText(productJsonFile);
            Assert.Multiple(() =>
            {
                Assert.That(product.FilePath, Is.EqualTo(productJsonFile));
                Assert.That(product.TemplateOrder, Has.Count.EqualTo(2));
                Assert.That(product.ScriptTokens, Has.Count.EqualTo(3));
                Assert.That(product.ScriptTokens["PrimaryDB"], Is.EqualTo("PrimaryDB"));
                Assert.That(product.ScriptTokens["ReleaseVersion"], Is.EqualTo("ReleaseVersion"));
                Assert.That(product.ScriptTokens["ProductName"], Is.EqualTo("TestProduct"));
                Assert.That(product.ValidationScript, Is.EqualTo("SELECT CAST(CASE WHEN EXISTS(SELECT * FROM master.dbo.sysdatabases WHERE [name] = 'PrimaryDB') THEN 1 ELSE 0 END AS BIT)"));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void NewProductHasExpectedDefaults()
    {
        var product = new Product();

        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.Null);
            Assert.That(product.ValidationScript, Is.Null);
            Assert.That(product.DropUnknownIndexes, Is.False);
            Assert.That(product.TemplateOrder, Is.Not.Null);
            Assert.That(product.TemplateOrder, Is.Empty);
            Assert.That(product.ScriptTokens, Is.Not.Null);
            Assert.That(product.ScriptTokens, Is.Empty);
            Assert.That(product.BaselineValidationScript, Is.Null);
            Assert.That(product.VersionStampScript, Is.Null);
            Assert.That(product.Platform, Is.EqualTo("MSSQL"));
            Assert.That(product.FilePath, Is.Null);
        });
    }

    [Test]
    public void JsonRoundTripPreservesAllSerializableProperties()
    {
        var original = new Product
        {
            Name = "RoundTripProduct",
            ValidationScript = "SELECT 1",
            DropUnknownIndexes = true,
            TemplateOrder = ["Alpha", "Beta"],
            ScriptTokens = new Dictionary<string, string> { ["Env"] = "prod", ["DB"] = "MyDB" },
            BaselineValidationScript = "SELECT 2",
            VersionStampScript = "SELECT 3",
            Platform = "MSSQL",
            FilePath = "ignored/because/json-ignore.json"
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<Product>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Name, Is.EqualTo("RoundTripProduct"));
            Assert.That(deserialized.ValidationScript, Is.EqualTo("SELECT 1"));
            Assert.That(deserialized.DropUnknownIndexes, Is.True);
            Assert.That(deserialized.TemplateOrder, Is.EqualTo(new[] { "Alpha", "Beta" }));
            Assert.That(deserialized.ScriptTokens["Env"], Is.EqualTo("prod"));
            Assert.That(deserialized.ScriptTokens["DB"], Is.EqualTo("MyDB"));
            Assert.That(deserialized.BaselineValidationScript, Is.EqualTo("SELECT 2"));
            Assert.That(deserialized.VersionStampScript, Is.EqualTo("SELECT 3"));
            Assert.That(deserialized.Platform, Is.EqualTo("MSSQL"));
            // FilePath is [JsonIgnore] — must not round-trip
            Assert.That(deserialized.FilePath, Is.Null);
        });
    }

    [Test]
    public void FilePathIsExcludedFromJsonSerialization()
    {
        var product = new Product { FilePath = "some/path/Product.json", Name = "MyProduct" };

        var json = JsonConvert.SerializeObject(product);

        Assert.That(json, Does.Not.Contain("FilePath"));
        Assert.That(json, Does.Not.Contain("some/path/Product.json"));
    }

    [Test]
    public void ScriptTokensDictionarySerializesAndDeserializesCorrectly()
    {
        var product = new Product
        {
            ScriptTokens = new Dictionary<string, string>
            {
                ["Server"] = "SQLPROD01",
                ["Database"] = "AppDB",
                ["Schema"] = "dbo"
            }
        };

        var json = JsonConvert.SerializeObject(product);
        var deserialized = JsonConvert.DeserializeObject<Product>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.ScriptTokens, Has.Count.EqualTo(3));
            Assert.That(deserialized.ScriptTokens["Server"], Is.EqualTo("SQLPROD01"));
            Assert.That(deserialized.ScriptTokens["Database"], Is.EqualTo("AppDB"));
            Assert.That(deserialized.ScriptTokens["Schema"], Is.EqualTo("dbo"));
        });
    }

    [Test]
    public void EmptyScriptTokensDeserializesToEmptyDictionary()
    {
        var json = """{"Name": "NoTokens"}""";

        var product = JsonConvert.DeserializeObject<Product>(json);

        Assert.That(product.ScriptTokens, Is.Not.Null);
        Assert.That(product.ScriptTokens, Is.Empty);
    }

    [Test]
    public void TokenReplaceSubstitutesTokensInScript()
    {
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("PrimaryDB", "ApplicationDB"),
            new("Server", "SQLPROD01")
        };
        var script = "USE {{PrimaryDB}} ON {{Server}}";

        var result = Product.TokenReplace(script, tokens);

        Assert.That(result, Is.EqualTo("USE ApplicationDB ON SQLPROD01"));
    }

    [Test]
    public void TokenReplaceIsCaseInsensitive()
    {
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("MyToken", "ReplacedValue")
        };

        var lower = Product.TokenReplace("{{mytoken}}", tokens);
        var upper = Product.TokenReplace("{{MYTOKEN}}", tokens);
        var mixed = Product.TokenReplace("{{MyToken}}", tokens);

        Assert.Multiple(() =>
        {
            Assert.That(lower, Is.EqualTo("ReplacedValue"));
            Assert.That(upper, Is.EqualTo("ReplacedValue"));
            Assert.That(mixed, Is.EqualTo("ReplacedValue"));
        });
    }

    [Test]
    public void TokenReplaceReturnsNullWhenScriptIsNull()
    {
        var tokens = new List<KeyValuePair<string, string>> { new("Key", "Value") };

        var result = Product.TokenReplace(null, tokens);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TokenReplaceReturnsEmptyStringWhenScriptIsEmpty()
    {
        var tokens = new List<KeyValuePair<string, string>> { new("Key", "Value") };

        var result = Product.TokenReplace(string.Empty, tokens);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TemplateOrderDefaultsToEmptyList()
    {
        var json = """{"Name": "NoTemplateOrder"}""";

        var product = JsonConvert.DeserializeObject<Product>(json);

        Assert.That(product.TemplateOrder, Is.Not.Null);
        Assert.That(product.TemplateOrder, Is.Empty);
    }

    [Test]
    public void PlatformDefaultsToMSSQLWhenNotSpecifiedInJson()
    {
        var json = """{"Name": "NoPlatform"}""";

        var product = JsonConvert.DeserializeObject<Product>(json);

        Assert.That(product.Platform, Is.EqualTo("MSSQL"));
    }

    [Test]
    public void ShouldErrorWhenSchemaPackagePathNotConfigured()
    {
        var configValues = new Dictionary<string, string>();
        var configBuilder = new ConfigurationBuilder();
        _ = configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);

            var ex = Assert.Throws<Exception>(() => Product.Load());
            Assert.That(ex!.Message, Contains.Substring("SchemaPackagePath is not configured"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldApplyScriptTokensFromConfigOverrideOnLoad()
    {
        var productJson = """
            {
                "Name": "OverrideProduct",
                "ValidationScript": "USE {{DbName}}",
                "ScriptTokens": {
                    "DbName": "DefaultDB"
                }
            }
            """;

        var configValues = new Dictionary<string, string>
        {
            ["SchemaPackagePath"] = "SchemaPackagePath",
            ["ScriptTokens:DbName"] = "OverriddenDB"
        };
        var configBuilder = new ConfigurationBuilder();
        _ = configBuilder.AddInMemoryCollection(configValues);
        var config = configBuilder.Build();
        var productJsonFile = Path.Combine("SchemaPackagePath", "Product.json");
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockDirectoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(productJsonFile).Returns(productJson);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(config);
            FactoryContainer.Register(mockDirectoryWrapper);
            FactoryContainer.Register(mockFileWrapper);

            var product = Product.Load();

            Assert.Multiple(() =>
            {
                Assert.That(product.ScriptTokens["DbName"], Is.EqualTo("OverriddenDB"));
                Assert.That(product.ValidationScript, Is.EqualTo("USE OverriddenDB"));
            });

            FactoryContainer.Clear();
        }
    }
}
