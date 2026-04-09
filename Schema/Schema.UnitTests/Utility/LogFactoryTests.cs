// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using NSubstitute;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class LogFactoryTests
{
    [SetUp]
    public void SetUp()
    {
        LogFactory.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        LogFactory.Clear();
        LogFactory.LogInitializer = null;
    }

    [Test]
    public void GetLogger_ReturnsLoggerByName()
    {
        var logger = LogFactory.GetLogger("TestLogger");

        Assert.That(logger, Is.Not.Null);
        Assert.That(logger.Logger.Name, Is.EqualTo("TestLogger"));
    }

    [Test]
    public void GetLogger_ReturnsSameInstanceForSameName()
    {
        var logger1 = LogFactory.GetLogger("TestLogger");
        var logger2 = LogFactory.GetLogger("TestLogger");

        Assert.That(logger2, Is.SameAs(logger1));
    }

    [Test]
    public void GetLogger_ReturnsDifferentInstancesForDifferentNames()
    {
        var logger1 = LogFactory.GetLogger("Logger1");
        var logger2 = LogFactory.GetLogger("Logger2");

        Assert.That(logger2, Is.Not.SameAs(logger1));
    }

    [Test]
    public void Register_OverridesExistingLogger()
    {
        var originalLogger = LogFactory.GetLogger("TestLogger");
        var mockLogger = Substitute.For<ILog>();

        LogFactory.Register("TestLogger", mockLogger);
        var result = LogFactory.GetLogger("TestLogger");

        Assert.That(result, Is.SameAs(mockLogger));
        Assert.That(result, Is.Not.SameAs(originalLogger));
    }

    [Test]
    public void Register_AddsNewLogger()
    {
        var mockLogger = Substitute.For<ILog>();

        LogFactory.Register("NewLogger", mockLogger);
        var result = LogFactory.GetLogger("NewLogger");

        Assert.That(result, Is.SameAs(mockLogger));
    }

    [Test]
    public void Clear_RemovesAllLoggers()
    {
        var mockLogger = Substitute.For<ILog>();
        LogFactory.Register("TestLogger", mockLogger);

        LogFactory.Clear();
        var result = LogFactory.GetLogger("TestLogger");

        Assert.That(result, Is.Not.SameAs(mockLogger));
    }

    [Test]
    public void LogInitializer_CalledOnFirstGetLogger()
    {
        var initializerCalled = false;
        LogFactory.LogInitializer = () => initializerCalled = true;

        LogFactory.GetLogger("TestLogger");

        Assert.That(initializerCalled, Is.True);
    }

    [Test]
    public void LogInitializer_CalledOnlyOnce()
    {
        var callCount = 0;
        LogFactory.LogInitializer = () => callCount++;

        LogFactory.GetLogger("Logger1");
        LogFactory.GetLogger("Logger2");

        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void LogInitializer_CalledAgainAfterClear()
    {
        var callCount = 0;
        LogFactory.LogInitializer = () => callCount++;

        LogFactory.GetLogger("Logger1");
        LogFactory.Clear();
        LogFactory.GetLogger("Logger2");

        Assert.That(callCount, Is.EqualTo(2));
    }

    [Test]
    public void LogInitializer_NotCalledWhenNull()
    {
        LogFactory.LogInitializer = null;

        // Should not throw
        Assert.DoesNotThrow(() => LogFactory.GetLogger("TestLogger"));
    }
}
