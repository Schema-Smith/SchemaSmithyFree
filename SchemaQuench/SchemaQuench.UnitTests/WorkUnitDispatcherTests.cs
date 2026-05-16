// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class WorkUnitDispatcherTests
{
    [Test]
    public void Run_AllUnitsProcessed_ExactlyOnce()
    {
        var units = new[]
        {
            new WorkUnit("s", "db1", "Core", ""),
            new WorkUnit("s", "db2", "Core", ""),
            new WorkUnit("s", "db3", "Core", "")
        };
        var processed = new ConcurrentBag<WorkUnit>();

        var dispatcher = new WorkUnitDispatcher(units, maxThreads: 2, allowParallel: new Dictionary<string, bool>(),
            unit => processed.Add(unit));

        dispatcher.Run();

        Assert.That(processed.OrderBy(u => u.DatabaseName).ToList(),
            Is.EqualTo(units.OrderBy(u => u.DatabaseName).ToList()));
    }

    [Test]
    public void Run_RespectsMaxThreads_ParallelEligibleWork()
    {
        const int maxThreads = 2;
        const int unitCount = 6;
        var units = Enumerable.Range(0, unitCount)
            .Select(i => new WorkUnit("s", $"db{i}", "Core", ""))
            .ToList();

        var inFlight = 0;
        var peak = 0;
        var lockObj = new object();
        var release = new ManualResetEventSlim(false);

        var dispatcher = new WorkUnitDispatcher(units, maxThreads, new Dictionary<string, bool>(),
            _ =>
            {
                lock (lockObj)
                {
                    inFlight++;
                    if (inFlight > peak) peak = inFlight;
                }
                // Hold each worker briefly so concurrency is observable.
                release.Wait(TimeSpan.FromSeconds(2));
                lock (lockObj) { inFlight--; }
            });

        // Release after we've had a chance to fill up the pool.
        var releaser = new Thread(() =>
        {
            Thread.Sleep(150);
            release.Set();
        }) { IsBackground = true };
        releaser.Start();

        dispatcher.Run();
        releaser.Join();

        Assert.That(peak, Is.LessThanOrEqualTo(maxThreads),
            $"Peak concurrency {peak} exceeded MaxThreads {maxThreads}.");
        Assert.That(peak, Is.GreaterThan(1), "Expected at least 2 concurrent workers to confirm parallelism is active.");
    }

    [Test]
    public void Run_SerialTemplate_TwoUnitsRunSequentially()
    {
        var units = new[]
        {
            new WorkUnit("s", "db", "Serial", "tenant_a"),
            new WorkUnit("s", "db", "Serial", "tenant_b")
        };
        var allowParallel = new Dictionary<string, bool> { ["Serial"] = false };

        var firstStarted = new ManualResetEventSlim(false);
        var firstHold = new ManualResetEventSlim(false);
        var anyOverlap = false;
        var inFlight = 0;
        var lockObj = new object();
        var first = true;

        var dispatcher = new WorkUnitDispatcher(units, maxThreads: 4, allowParallel,
            _ =>
            {
                bool isFirst;
                lock (lockObj)
                {
                    isFirst = first;
                    first = false;
                    inFlight++;
                    if (inFlight > 1) anyOverlap = true;
                }

                if (isFirst)
                {
                    firstStarted.Set();
                    firstHold.Wait(TimeSpan.FromSeconds(2));
                }

                lock (lockObj) { inFlight--; }
            });

        var releaser = new Thread(() =>
        {
            // Wait until the first serial unit has started.
            firstStarted.Wait(TimeSpan.FromSeconds(2));
            // Give the dispatcher a window to incorrectly start the second unit if it would.
            Thread.Sleep(150);
            firstHold.Set();
        }) { IsBackground = true };
        releaser.Start();

        dispatcher.Run();
        releaser.Join();

        Assert.That(anyOverlap, Is.False,
            "Two AllowParallel=false units of the same template ran concurrently.");
    }

    [Test]
    public void Run_MixedSerialAndParallel_ParallelWorkProgressesAlongsideSerial()
    {
        // One held serial unit + several parallel units. Each parallel unit also synchronizes
        // on the serial-started event before completing, so the dispatcher cannot "drain" the
        // parallel queue before the serial unit takes a slot. With maxThreads=4, we expect at
        // least one parallel unit running concurrently with the in-flight serial unit.
        var units = new List<WorkUnit>
        {
            new("s", "db", "Serial", "tenant_a"),
            new("s", "db1", "Parallel", ""),
            new("s", "db2", "Parallel", ""),
            new("s", "db3", "Parallel", "")
        };
        var allowParallel = new Dictionary<string, bool> { ["Serial"] = false, ["Parallel"] = true };

        var serialStarted = new ManualResetEventSlim(false);
        var releaseAll = new ManualResetEventSlim(false);
        var concurrent = 0;
        var peakConcurrent = 0;
        var lockObj = new object();
        var serialInFlight = false;
        var parallelStartedWhileSerialHeld = 0;

        var dispatcher = new WorkUnitDispatcher(units, maxThreads: 4, allowParallel,
            unit =>
            {
                if (unit.TemplateName == "Serial")
                {
                    lock (lockObj) { serialInFlight = true; }
                    serialStarted.Set();
                    releaseAll.Wait(TimeSpan.FromSeconds(2));
                    lock (lockObj) { serialInFlight = false; }
                }
                else
                {
                    // Wait until the serial unit is in flight before doing the concurrency check,
                    // so the test is deterministic regardless of which worker grabs which unit first.
                    serialStarted.Wait(TimeSpan.FromSeconds(2));
                    lock (lockObj)
                    {
                        concurrent++;
                        if (concurrent > peakConcurrent) peakConcurrent = concurrent;
                        if (serialInFlight) parallelStartedWhileSerialHeld++;
                    }
                    // Hold briefly so concurrent peak is observable.
                    Thread.Sleep(50);
                    lock (lockObj) { concurrent--; }
                }
            });

        var releaser = new Thread(() =>
        {
            serialStarted.Wait(TimeSpan.FromSeconds(2));
            // Give the dispatcher time to pick up parallel work alongside the held serial unit.
            Thread.Sleep(200);
            releaseAll.Set();
        }) { IsBackground = true };
        releaser.Start();

        dispatcher.Run();
        releaser.Join();

        Assert.That(parallelStartedWhileSerialHeld, Is.GreaterThan(0),
            "Parallel-eligible units should run alongside an in-flight serial unit.");
    }

    [Test]
    public void Run_CallbackThrows_PropagatesAndStopsAcceptingNewWork()
    {
        var units = new List<WorkUnit>
        {
            new("s", "db1", "Core", ""),
            new("s", "db2", "Core", ""),
            new("s", "db3", "Core", ""),
            new("s", "db4", "Core", ""),
            new("s", "db5", "Core", ""),
            new("s", "db6", "Core", "")
        };
        var startedAfterFailure = 0;
        var failureSignal = new ManualResetEventSlim(false);
        var lockObj = new object();
        var bailed = false;

        var dispatcher = new WorkUnitDispatcher(units, maxThreads: 1, new Dictionary<string, bool>(),
            unit =>
            {
                if (unit.DatabaseName == "db1")
                {
                    // safe: maxThreads=1, all callback invocations run on the single worker thread
                    bailed = true;
                    failureSignal.Set();
                    throw new InvalidOperationException("boom");
                }
                lock (lockObj)
                {
                    if (bailed) startedAfterFailure++;
                }
            });

        Assert.Throws<AggregateException>(() => dispatcher.Run());
        Assert.That(startedAfterFailure, Is.EqualTo(0),
            "Dispatcher should not start new units after a callback fails.");
        Assert.That(failureSignal.IsSet, Is.True);
    }

    [Test]
    public void Run_MissingTemplateKeyInAllowParallel_DefaultsToParallel()
    {
        // No "Core" entry in the AllowParallel map — should default to allow parallel.
        var units = Enumerable.Range(0, 4)
            .Select(i => new WorkUnit("s", $"db{i}", "Core", ""))
            .ToList();

        var peak = 0;
        var inFlight = 0;
        var lockObj = new object();
        var release = new ManualResetEventSlim(false);

        var dispatcher = new WorkUnitDispatcher(units, maxThreads: 3, allowParallel: new Dictionary<string, bool>(),
            _ =>
            {
                lock (lockObj)
                {
                    inFlight++;
                    if (inFlight > peak) peak = inFlight;
                }
                release.Wait(TimeSpan.FromSeconds(2));
                lock (lockObj) { inFlight--; }
            });

        var releaser = new Thread(() =>
        {
            Thread.Sleep(150);
            release.Set();
        }) { IsBackground = true };
        releaser.Start();

        dispatcher.Run();
        releaser.Join();

        Assert.That(peak, Is.GreaterThan(1),
            "Templates with no AllowParallel entry should default to parallel-eligible.");
    }

    [Test]
    public void Run_NoUnits_ReturnsImmediately()
    {
        var dispatcher = new WorkUnitDispatcher(
            new List<WorkUnit>(), maxThreads: 4, new Dictionary<string, bool>(),
            _ => Assert.Fail("Callback should not be invoked for an empty work set."));

        Assert.DoesNotThrow(() => dispatcher.Run());
    }

    [Test]
    public void Run_SecondCallOnSameInstance_Throws()
    {
        // Dispatcher is single-use — wrapping Run() in a retry loop would otherwise silently
        // no-op because the queues drain on the first call.
        var units = new[] { new WorkUnit("s", "db1", "Core", "") };
        var dispatcher = new WorkUnitDispatcher(units, maxThreads: 1, new Dictionary<string, bool>(),
            _ => { });

        dispatcher.Run();

        var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Run());
        Assert.That(ex!.Message, Does.Contain("single-use"));
    }

    [Test]
    public void Run_SecondCallOnSameInstance_EvenWithEmptyUnits_Throws()
    {
        // Even when the first Run is a no-op (no units), a second call still throws — the
        // single-use guard catches the caller's intent independently of whether any work
        // happened on the first run.
        var dispatcher = new WorkUnitDispatcher(
            new List<WorkUnit>(), maxThreads: 1, new Dictionary<string, bool>(),
            _ => { });

        dispatcher.Run();

        Assert.Throws<InvalidOperationException>(() => dispatcher.Run());
    }
}
