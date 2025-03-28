using Schema.Domain;
using Schema.Isolators;
using Microsoft.Extensions.Configuration;
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
            Assert.That(ex.Message, Contains.Substring("Path not found badPath"));

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
}
