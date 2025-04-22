using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NSubstitute;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.UnitTests;

public class RepositoryHelperTests
{
    [Test]
    public void UpdateOrInitRepository_ShouldCreateDirectoriesAndWriteProductFile()
    {
        var productPath = "C:/Projects/MyProduct";
        var productName = "MyProduct";
        var templateName = "MyTemplate";
        var dbName = "MyDatabase";

        var mockFileWrapper = Substitute.For<IFile>();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);
            FactoryContainer.Register(mockDirectoryWrapper);

            RepositoryHelper.UpdateOrInitRepository(productPath, productName, templateName, dbName);

            mockDirectoryWrapper.Received(2).CreateDirectory(Arg.Any<string>());
            mockDirectoryWrapper.Received(1).CreateDirectory(Path.Combine(productPath, "Templates"));
            mockDirectoryWrapper.Received(1).CreateDirectory(Path.Combine(productPath, ".json-schemas"));
            mockFileWrapper.Received(4).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWith("products.schema")), Arg.Any<string>());
            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWith("templates.schema")), Arg.Any<string>());
            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWith("tables.schema")), Arg.Any<string>());
            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Product.json")), Arg.Any<string>());
            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Product.json")), Arg.Is<string>(s => s.ContainsIgnoringCase("\"Name\": \"MyProduct\"")));

            FactoryContainer.Clear();
        }
    }

    [TestCase("C:/Projects/MyProduct", "MyProduct")]
    [TestCase("C:/Projects/MyProduct/", "MyProduct")]
    [TestCase(@"C:\Projects\MyProduct\", "MyProduct")]
    public void UpdateOrInitRepository_ShouldDefaultProductNameCorrectlyFromPathWhenBlank(string productPath, string expectedName)
    {
        var productName = "";
        var templateName = "MyTemplate";
        var dbName = "MyDatabase";
        if (Path.DirectorySeparatorChar == Path.AltDirectorySeparatorChar) productPath = productPath.Replace(@"\", "/");

        var mockFileWrapper = Substitute.For<IFile>();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);
            FactoryContainer.Register(mockDirectoryWrapper);

            RepositoryHelper.UpdateOrInitRepository(productPath, productName, templateName, dbName);

            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Product.json")), Arg.Is<string>(s => s.ContainsIgnoringCase($"\"Name\": \"{expectedName}\"")));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void UpdateOrInitRepository_ShouldLoadAndUpdateExistingProductFile()
    {
        var productPath = "C:/Projects/MyProduct";
        var productName = "ExistingProduct";
        var templateName = "MyTemplate";
        var dbName = "MyDatabase";
        var product = new Product
        {
            Name = productName,
            TemplateOrder = ["ExistingTemplate"],
            ScriptTokens = new Dictionary<string, string> { { "ExistingTemplateDb", "ExistingDb" } }
        };

        var mockFileWrapper = Substitute.For<IFile>();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Product.json"))).Returns(JsonConvert.SerializeObject(product));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);
            FactoryContainer.Register(mockDirectoryWrapper);

            RepositoryHelper.UpdateOrInitRepository(productPath, productName, templateName, dbName);

            mockDirectoryWrapper.Received(2).CreateDirectory(Arg.Any<string>());
            mockDirectoryWrapper.Received(1).CreateDirectory(Path.Combine(productPath, "Templates"));
            mockDirectoryWrapper.Received(1).CreateDirectory(Path.Combine(productPath, ".json-schemas"));
            mockFileWrapper.Received(1).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            mockFileWrapper.Received(1).ReadAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Product.json")));
            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Product.json")), Arg.Is<string>(s => s.ContainsIgnoringCase("\"Name\": \"ExistingProduct\"")));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void UpdateOrInitTemplate_ShouldCreateDirectoriesAndWriteTemplateFile()
    {
        var productPath = "C:/Projects/MyProduct";
        var templateName = "MyTemplate";
        var dbName = "MyDatabase";

        var mockFileWrapper = Substitute.For<IFile>();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);
            FactoryContainer.Register(mockDirectoryWrapper);

            var result = RepositoryHelper.UpdateOrInitTemplate(productPath, templateName, dbName);

            Assert.That(result, Is.EqualTo(Path.Combine(productPath, "Templates", templateName)));
            mockDirectoryWrapper.Received(13).CreateDirectory(Arg.Any<string>());
            mockDirectoryWrapper.Received(1).CreateDirectory(Path.Combine(productPath, "Templates", templateName));
            mockDirectoryWrapper.Received(1).CreateDirectory(Arg.Is<string>(s => s.Contains("Schemas")));
            mockDirectoryWrapper.Received(1).CreateDirectory(Arg.Is<string>(s => s.Contains("Functions")));
            mockDirectoryWrapper.Received(1).CreateDirectory(Arg.Is<string>(s => s.Contains("DataTypes")));
            mockFileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Template.json")), Arg.Is<string>(s => s.ContainsIgnoringCase("\"Name\": \"MyTemplate\"")));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void UpdateOrInitTemplate_ShouldNotOverwriteExistingTemplateFile()
    {
        var productPath = "C:/Projects/MyProduct";
        var templateName = "MyTemplate";
        var dbName = "MyDatabase";
        var template = new Template
        {
            Name = "ExistingTemplate",
            DatabaseIdentificationScript = "SELECT [Name] FROM master.sys.databases WHERE [Name] = '{{ExistingTemplateDb}}'"
        };

        var mockFileWrapper = Substitute.For<IFile>();
        var mockDirectoryWrapper = Substitute.For<IDirectory>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(true);
        mockFileWrapper.ReadAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Template.json"))).Returns(JsonConvert.SerializeObject(template));

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);
            FactoryContainer.Register(mockDirectoryWrapper);

            var result = RepositoryHelper.UpdateOrInitTemplate(productPath, templateName, dbName);

            Assert.That(result, Is.EqualTo(Path.Combine(productPath, "Templates", templateName)));
            mockDirectoryWrapper.Received(13).CreateDirectory(Arg.Any<string>());
            mockDirectoryWrapper.Received(1).CreateDirectory(Path.Combine(productPath, "Templates", templateName));
            mockFileWrapper.Received(0).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("Template.json")), Arg.Any<string>());

            FactoryContainer.Clear();
        }
    }
}
