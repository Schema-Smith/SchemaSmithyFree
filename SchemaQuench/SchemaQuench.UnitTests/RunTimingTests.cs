// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class RunTimingTests
{
    [Test]
    public void Record_AccumulatesPerSlotTotalsAndTargetCount_AcrossMultipleTargets()
    {
        var timing = new RunTiming();

        timing.Record("[srv].[dbA]", "dbA", "ModifiedTables", 100, 0);
        timing.Record("[srv].[dbB]", "dbB", "ModifiedTables", 250, 0);
        timing.Record("[srv].[dbA]", "dbA", "ForeignKeys", 50, 0);

        var bySlot = timing.BySlot().ToList();

        var modifiedTables = bySlot.Single(s => s.Slot == "ModifiedTables");
        Assert.That(modifiedTables.TotalMs, Is.EqualTo(350));
        Assert.That(modifiedTables.TargetCount, Is.EqualTo(2));

        var foreignKeys = bySlot.Single(s => s.Slot == "ForeignKeys");
        Assert.That(foreignKeys.TotalMs, Is.EqualTo(50));
        Assert.That(foreignKeys.TargetCount, Is.EqualTo(1));
    }

    [Test]
    public void Record_AccumulatesPerDatabaseTotals()
    {
        var timing = new RunTiming();

        timing.Record("[srv].[dbA]", "dbA", "ModifiedTables", 100, 0);
        timing.Record("[srv].[dbA]", "dbA", "ForeignKeys", 40, 0);
        timing.Record("[srv].[dbB]", "dbB", "ModifiedTables", 250, 0);

        var byDatabase = timing.ByDatabase().ToList();

        Assert.That(byDatabase.Single(d => d.Database == "dbA").TotalMs, Is.EqualTo(140));
        Assert.That(byDatabase.Single(d => d.Database == "dbB").TotalMs, Is.EqualTo(250));
    }

    [Test]
    public void Bottlenecks_ReturnsOnlyEntriesStrictlyOverThreshold()
    {
        var timing = new RunTiming();

        timing.Record("[srv].[dbA] [Schema: sales]", "dbA", "IndexesAndConstraints", 5000, 0);
        timing.Record("[srv].[dbB]", "dbB", "ModifiedTables", 5001, 0);
        timing.Record("[srv].[dbC]", "dbC", "ForeignKeys", 4999, 0);

        var bottlenecks = timing.Bottlenecks(5000).ToList();

        Assert.That(bottlenecks.Count, Is.EqualTo(1));
        Assert.That(bottlenecks[0].Scope, Is.EqualTo("[srv].[dbB]"));
        Assert.That(bottlenecks[0].Slot, Is.EqualTo("ModifiedTables"));
        Assert.That(bottlenecks[0].DurationMs, Is.EqualTo(5001));
    }

    [Test]
    public void TotalMs_StopFreezesElapsedValue()
    {
        var timing = new RunTiming();

        timing.Start();
        Thread.Sleep(5);
        timing.Stop();

        var stopped = timing.TotalMs;
        Thread.Sleep(5);

        Assert.That(timing.TotalMs, Is.EqualTo(stopped));
        Assert.That(stopped, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void Record_IsThreadSafe_UnderConcurrentCalls()
    {
        var timing = new RunTiming();
        const int callCount = 1000;

        Parallel.For(0, callCount, i =>
        {
            timing.Record($"[srv].[db{i % 10}]", $"db{i % 10}", "ModifiedTables", 1, 0);
        });

        var bySlot = timing.BySlot().Single();
        Assert.That(bySlot.TargetCount, Is.EqualTo(callCount));
        Assert.That(bySlot.TotalMs, Is.EqualTo(callCount));

        var byDatabase = timing.ByDatabase().ToList();
        Assert.That(byDatabase.Count, Is.EqualTo(10));
        Assert.That(byDatabase.Sum(d => d.TotalMs), Is.EqualTo(callCount));
    }
}
