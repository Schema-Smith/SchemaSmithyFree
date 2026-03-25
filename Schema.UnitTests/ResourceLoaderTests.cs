// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Schema.Utility;

using Schema.Utility;
using System;
using System.IO;

namespace Schema.UnitTests;

public class ResourceLoaderTests
{
    [Test]
    public void ShouldErrorOnBlankRequest()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => ResourceLoader.Load(""));
            Assert.Throws<ArgumentNullException>(() => ResourceLoader.Load(null));
        });
    }

    [Test]
    public void ShouldErrorOnMissingResource()
    {
        Assert.Throws<FileLoadException>(() => ResourceLoader.Load("MissingResource.txt"));
    }

    [Test]
    public void ShouldLoadKnownEmbeddedResource()
    {
        var content = ResourceLoader.Load("TableQuench.sql");
        Assert.That(content, Is.Not.Null.And.Not.Empty);
        Assert.That(content, Does.Contain("SchemaSmith"));
    }

    [Test]
    public void ShouldMatchCaseInsensitively()
    {
        var content = ResourceLoader.Load("tablequench.sql");
        Assert.That(content, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ShouldLoadResourceFromOtherAssembly()
    {
        var content = ResourceLoader.Load("ParseTableJsonIntoTempTables.sql");
        Assert.That(content, Is.Not.Null.And.Not.Empty);
    }
}
