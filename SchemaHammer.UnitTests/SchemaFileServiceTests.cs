// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Isolators;
using SchemaHammer.Services;
using NSubstitute;

namespace SchemaHammer.UnitTests;

public class SchemaFileServiceTests
{
    [Test]
    public void UpdateSchemaFiles_WritesFourFiles()
    {
        var mockFile = Substitute.For<IFile>();
        var mockDir = Substitute.For<IDirectory>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFile);
            FactoryContainer.Register(mockDir);

            try
            {
                var service = new SchemaFileService();
                var count = service.UpdateSchemaFiles("/test/product");

                Assert.That(count, Is.EqualTo(4));
                mockFile.Received(4).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            }
            finally
            {
                FactoryContainer.Clear();
            }
        }
    }

    [Test]
    public void UpdateSchemaFiles_CreatesSchemaDirectory()
    {
        var mockFile = Substitute.For<IFile>();
        var mockDir = Substitute.For<IDirectory>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFile);
            FactoryContainer.Register(mockDir);

            try
            {
                var service = new SchemaFileService();
                service.UpdateSchemaFiles("/test/product");

                mockDir.Received().CreateDirectory(
                    Arg.Is<string>(s => s.Contains(".json-schemas")));
            }
            finally
            {
                FactoryContainer.Clear();
            }
        }
    }

    [Test]
    public void UpdateSchemaFiles_WritesProductsSchema()
    {
        var mockFile = Substitute.For<IFile>();
        var mockDir = Substitute.For<IDirectory>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFile);
            FactoryContainer.Register(mockDir);

            try
            {
                var service = new SchemaFileService();
                service.UpdateSchemaFiles("/test/product");

                mockFile.Received().WriteAllText(
                    Arg.Is<string>(s => s.EndsWith("products.schema")),
                    Arg.Any<string>());
            }
            finally
            {
                FactoryContainer.Clear();
            }
        }
    }

    [Test]
    public void UpdateSchemaFiles_WritesIndexedViewsSchema()
    {
        var mockFile = Substitute.For<IFile>();
        var mockDir = Substitute.For<IDirectory>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFile);
            FactoryContainer.Register(mockDir);

            try
            {
                var service = new SchemaFileService();
                service.UpdateSchemaFiles("/test/product");

                mockFile.Received().WriteAllText(
                    Arg.Is<string>(s => s.EndsWith("indexedviews.schema")),
                    Arg.Any<string>());
            }
            finally
            {
                FactoryContainer.Clear();
            }
        }
    }
}
