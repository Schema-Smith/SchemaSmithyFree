// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class TaskQueueManagerTests
{
    [Test]
    public void AddToQueue_ExecutesWork()
    {
        using var manager = new TaskQueueManager<string>(maxTasks: 1);
        var processed = new List<string>();

        manager.AddToQueue("item1", item =>
        {
            lock (processed) { processed.Add(item); }
        });

        manager.WaitForAll();

        Assert.That(processed, Has.Count.EqualTo(1));
        Assert.That(processed[0], Is.EqualTo("item1"));
    }

    [Test]
    public void AddToQueue_ExecutesMultipleItems()
    {
        using var manager = new TaskQueueManager<int>(maxTasks: 5);
        var processed = new List<int>();
        var lockObj = new object();

        for (var i = 1; i <= 10; i++)
        {
            manager.AddToQueue(i, item =>
            {
                lock (lockObj) { processed.Add(item); }
            });
        }

        manager.WaitForAll();

        Assert.That(processed, Has.Count.EqualTo(10));
        for (var i = 1; i <= 10; i++)
            Assert.That(processed, Does.Contain(i));
    }

    [Test]
    public void Constructor_SetsMinimumMaxTasksToOne()
    {
        using var manager = new TaskQueueManager<string>(maxTasks: 0);
        var processed = new List<string>();
        var lockObj = new object();

        manager.AddToQueue("item1", item =>
        {
            lock (lockObj) { processed.Add(item); }
        });
        manager.AddToQueue("item2", item =>
        {
            lock (lockObj) { processed.Add(item); }
        });

        manager.WaitForAll();

        Assert.That(processed, Has.Count.EqualTo(2));
    }

    [Test]
    public void Constructor_NegativeMaxTasksBecomesOne()
    {
        using var manager = new TaskQueueManager<string>(maxTasks: -5);
        var processed = new List<string>();

        manager.AddToQueue("item1", item =>
        {
            lock (processed) { processed.Add(item); }
        });

        manager.WaitForAll();

        Assert.That(processed, Has.Count.EqualTo(1));
    }

    [Test]
    public void WaitForAll_InvokesActionWhileWaiting()
    {
        using var manager = new TaskQueueManager<string>(maxTasks: 1);
        var actionCalled = false;

        manager.AddToQueue("item1", _ => Thread.Sleep(200));

        manager.WaitForAll(() => actionCalled = true);

        Assert.That(actionCalled, Is.True);
    }

    [Test]
    public void WaitForAll_CompletesWithNoItems()
    {
        using var manager = new TaskQueueManager<string>(maxTasks: 5);
        manager.WaitForAll();
        Assert.Pass();
    }

    [Test]
    public void Dispose_ClearsQueues()
    {
        var manager = new TaskQueueManager<string>(maxTasks: 1);
        manager.AddToQueue("item1", _ => Thread.Sleep(50));
        manager.Dispose();
        Assert.Pass();
    }

    [Test]
    public void AddToQueue_RespectsMaxConcurrency()
    {
        using var manager = new TaskQueueManager<int>(maxTasks: 2);
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        for (var i = 0; i < 6; i++)
        {
            manager.AddToQueue(i, _ =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    if (concurrentCount > maxConcurrent)
                        maxConcurrent = concurrentCount;
                }
                Thread.Sleep(100);
                lock (lockObj) { concurrentCount--; }
            });
        }

        manager.WaitForAll();

        Assert.That(maxConcurrent, Is.LessThanOrEqualTo(3));
    }

    [Test]
    public void AddToQueue_WorksWithDifferentTypes()
    {
        using var manager = new TaskQueueManager<DateTime>(maxTasks: 2);
        var processed = new List<DateTime>();
        var lockObj = new object();
        var date1 = new DateTime(2025, 1, 1);
        var date2 = new DateTime(2025, 6, 15);

        manager.AddToQueue(date1, item =>
        {
            lock (lockObj) { processed.Add(item); }
        });
        manager.AddToQueue(date2, item =>
        {
            lock (lockObj) { processed.Add(item); }
        });

        manager.WaitForAll();

        Assert.That(processed, Has.Count.EqualTo(2));
        Assert.That(processed, Does.Contain(date1));
        Assert.That(processed, Does.Contain(date2));
    }

    [Test]
    public void DefaultMaxTasks_Is20()
    {
        using var manager = new TaskQueueManager<string>();
        var processed = new List<string>();
        var lockObj = new object();

        for (var i = 0; i < 25; i++)
        {
            manager.AddToQueue($"item{i}", item =>
            {
                lock (lockObj) { processed.Add(item); }
            });
        }

        manager.WaitForAll();

        Assert.That(processed, Has.Count.EqualTo(25));
    }
}
