using Schema.Isolators;
using Schema.Utility;
using Newtonsoft.Json;
using NSubstitute;
using System;
using System.Collections.Generic;

namespace Schema.UnitTests;

public class JsonHelperTests
{
    private const string TestFilePath = "some/path/data.json";

    // ------------------------------------------------------------------
    // Load<T>
    // ------------------------------------------------------------------

    [Test]
    public void LoadThrowsWhenFileDoesNotExist()
    {
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(TestFilePath).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => JsonHelper.Load<TestData>(TestFilePath));
            Assert.That(ex!.Message, Is.EqualTo($"File {TestFilePath} does not exist"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void LoadDeserializesValidJsonFile()
    {
        var json = """{"Name":"LoadedObject","Value":42}""";
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(TestFilePath).Returns(true);
        mockFileWrapper.ReadAllText(TestFilePath).Returns(json);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var result = JsonHelper.Load<TestData>(TestFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(result.Name, Is.EqualTo("LoadedObject"));
                Assert.That(result.Value, Is.EqualTo(42));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void LoadUsesFileWrapperFromFactoryContainer()
    {
        var json = """{"Name":"FactoryCheck","Value":1}""";
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(TestFilePath).Returns(true);
        mockFileWrapper.ReadAllText(TestFilePath).Returns(json);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            _ = JsonHelper.Load<TestData>(TestFilePath);

            mockFileWrapper.Received(1).Exists(TestFilePath);
            mockFileWrapper.Received(1).ReadAllText(TestFilePath);

            FactoryContainer.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Write<T>
    // ------------------------------------------------------------------

    [Test]
    public void WriteSerializesObjectToFile()
    {
        var obj = new TestData { Name = "Written", Value = 99 };
        string writtenContent = null;
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper
            .When(f => f.WriteAllText(TestFilePath, Arg.Any<string>()))
            .Do(ci => writtenContent = ci.ArgAt<string>(1));
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            JsonHelper.Write(TestFilePath, obj);

            Assert.That(writtenContent, Is.Not.Null);
            var deserialized = JsonConvert.DeserializeObject<TestData>(writtenContent);
            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Name, Is.EqualTo("Written"));
                Assert.That(deserialized.Value, Is.EqualTo(99));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void WriteUsesIndentedFormatting()
    {
        var obj = new TestData { Name = "Indented", Value = 7 };
        string writtenContent = null;
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper
            .When(f => f.WriteAllText(TestFilePath, Arg.Any<string>()))
            .Do(ci => writtenContent = ci.ArgAt<string>(1));
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            JsonHelper.Write(TestFilePath, obj);

            // Indented JSON contains newlines and leading whitespace
            Assert.That(writtenContent, Does.Contain(Environment.NewLine));
            Assert.That(writtenContent, Does.Contain("  "));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void WriteOmitsNullValues()
    {
        var obj = new TestDataWithNullable { Name = "NoNulls", NullableField = null };
        string writtenContent = null;
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper
            .When(f => f.WriteAllText(TestFilePath, Arg.Any<string>()))
            .Do(ci => writtenContent = ci.ArgAt<string>(1));
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            JsonHelper.Write(TestFilePath, obj);

            Assert.That(writtenContent, Does.Not.Contain("NullableField"));
            Assert.That(writtenContent, Does.Not.Contain("null"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void WriteUsesFileWrapperFromFactoryContainer()
    {
        var obj = new TestData { Name = "FactoryWrite", Value = 5 };
        var mockFileWrapper = Substitute.For<IFile>();
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            JsonHelper.Write(TestFilePath, obj);

            mockFileWrapper.Received(1).WriteAllText(TestFilePath, Arg.Any<string>());

            FactoryContainer.Clear();
        }
    }

    // ------------------------------------------------------------------
    // ProductLoad<T>
    // ------------------------------------------------------------------

    [Test]
    public void ProductLoadThrowsWhenFileDoesNotExist()
    {
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(TestFilePath).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => JsonHelper.ProductLoad<TestData>(TestFilePath));
            Assert.That(ex!.Message, Is.EqualTo($"File {TestFilePath} does not exist"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ProductLoadDeserializesProductJsonCorrectly()
    {
        var json = """{"Name":"ProductObject","Value":77}""";
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(TestFilePath).Returns(true);
        mockFileWrapper.ReadAllText(TestFilePath).Returns(json);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var result = JsonHelper.ProductLoad<TestData>(TestFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(result.Name, Is.EqualTo("ProductObject"));
                Assert.That(result.Value, Is.EqualTo(77));
            });

            mockFileWrapper.Received(1).Exists(TestFilePath);
            mockFileWrapper.Received(1).ReadAllText(TestFilePath);

            FactoryContainer.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Helper types
    // ------------------------------------------------------------------

    private class TestData
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    private class TestDataWithNullable
    {
        public string Name { get; set; }
        public string NullableField { get; set; }
    }
}
