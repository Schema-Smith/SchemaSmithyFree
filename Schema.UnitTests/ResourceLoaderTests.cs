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
}
