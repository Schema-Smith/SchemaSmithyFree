using Schema.Domain;
using Schema.Isolators;
using NSubstitute;
using System;

namespace Schema.UnitTests;

public class SqlScriptTests
{
    [Test]
    public void ShouldErrorOnBadSqlScriptPath()
    {
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => SqlScript.Load("badPath"));
            Assert.That(ex!.Message, Is.EqualTo("File badPath does not exist"));

            FactoryContainer.Clear();
        }
    }
}
