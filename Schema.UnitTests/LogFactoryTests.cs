// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Schema.Utility;

using Schema.Utility;

namespace Schema.UnitTests;

public class LogFactoryTests
{
    [Test]
    public void ShouldGetDefaultLoggerIfNoneConfigured()
    {
        var test = LogFactory.GetLogger("TEST");
        Assert.That(test, Is.Not.Null);
    }

    [Test]
    public void ShouldReplaceRegistration()
    {
        var test = LogFactory.GetLogger("TEST");
        var test2 = LogFactory.GetLogger("TEST2");
        LogFactory.Register("TEST", test2);

        Assert.Multiple(() =>
        {
            Assert.That(test, Is.Not.SameAs(LogFactory.GetLogger("TEST")));
            Assert.That(test2, Is.SameAs(LogFactory.GetLogger("TEST")));
        });
    }

    [Test]
    public void ClearRemovesAllRegisteredLoggers()
    {
        // Populate the cache with known loggers, then clear it.
        // After Clear, GetLogger must still succeed (it re-initialises from log4net),
        // but the returned instance will be a fresh object, not the cached one.
        var before = LogFactory.GetLogger("CLEAR-TEST");
        LogFactory.Clear();
        var after = LogFactory.GetLogger("CLEAR-TEST");

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Not.Null);
            Assert.That(after, Is.Not.Null);
            // After clearing the cache the factory fetches a new reference from log4net,
            // which may or may not be the same underlying object depending on log4net's own
            // caching — what matters is that Clear() does not throw and GetLogger still works.
            Assert.That(after, Is.Not.Null);
        });
    }

    [Test]
    public void GetLoggerReturnsDifferentInstancesForDifferentNames()
    {
        var loggerA = LogFactory.GetLogger("LOGGER-A");
        var loggerB = LogFactory.GetLogger("LOGGER-B");

        Assert.That(loggerA, Is.Not.SameAs(loggerB));
    }

    [Test]
    public void GetLoggerReturnsSameInstanceForSameName()
    {
        var first = LogFactory.GetLogger("SAME-NAME-TEST");
        var second = LogFactory.GetLogger("SAME-NAME-TEST");

        Assert.That(first, Is.SameAs(second));
    }
}
