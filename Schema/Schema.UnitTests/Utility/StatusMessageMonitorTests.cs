// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Utility;

namespace Schema.UnitTests.Utility;

/// <summary>
/// StatusMessageMonitor requires a real database connection (MySQL) to function.
/// These tests validate the class design and interface. Full integration tests
/// will cover actual polling behavior with a running MySQL instance.
/// </summary>
[TestFixture]
public class StatusMessageMonitorTests
{
    [Test]
    public void StatusMessageMonitor_ImplementsIDisposable()
    {
        Assert.That(typeof(StatusMessageMonitor).GetInterfaces(), Does.Contain(typeof(System.IDisposable)));
    }

    [Test]
    public void StatusMessageMonitor_HasFlushMethod()
    {
        var method = typeof(StatusMessageMonitor).GetMethod("Flush");
        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void StatusMessageMonitor_HasPollOnceMethod()
    {
        var method = typeof(StatusMessageMonitor).GetMethod("PollOnce",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
    }
}
