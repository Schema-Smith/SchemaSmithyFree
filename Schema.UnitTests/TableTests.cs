using NSubstitute;
using Schema.Isolators;
using Schema.Domain;
using System;

namespace Schema.UnitTests;

public class TableTests
{
    [Test]
    public void ShouldProvideTheFileNameWhenErrorLoadingATable()
    {
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => Table.Load("badPath"));
            Assert.That(ex!.Message, Contains.Substring("Error loading table from badPath"));

            FactoryContainer.Clear();
        }
    }
}
