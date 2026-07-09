// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SchemaQuench.Reporting;

namespace SchemaQuench;

/// <summary>
/// Thread-safe collector of per-slot / per-database timing data for a single product run, plus
/// overall run wall-clock. <see cref="DatabaseQuench.Execute"/> calls <see cref="Record"/> once
/// per timed slot per target; <see cref="ProductQuench.QuenchProduct"/> owns <see cref="Start"/> /
/// <see cref="Stop"/> for the run-level clock. Purely passive measurement — a later slice
/// assembles the Deployment Summary Report from these aggregates. Safe under the fan-out's
/// concurrent worker pool: <see cref="Record"/> may be called from many threads at once.
/// </summary>
public class RunTiming
{
    private sealed record RecordEntry(string ScopeKey, string Database, string Slot, long Ms, int ScriptsRun);

    private readonly ConcurrentBag<RecordEntry> _entries = new();
    private readonly Stopwatch _stopwatch = new();

    /// <summary>Starts the run-level wall-clock. Call once at the start of the run.</summary>
    public void Start() => _stopwatch.Start();

    /// <summary>Stops the run-level wall-clock, freezing <see cref="TotalMs"/>.</summary>
    public void Stop() => _stopwatch.Stop();

    /// <summary>Run wall-clock elapsed milliseconds; live while running, frozen after <see cref="Stop"/>.</summary>
    public long TotalMs => _stopwatch.ElapsedMilliseconds;

    /// <param name="scopeKey">Per-target LogPrefix, e.g. <c>[primary].[TenantA] [Schema: sales]</c>. Opaque; used as the Bottlenecks scope.</param>
    /// <param name="database">The target database name, used for the <see cref="ByDatabase"/> rollup.</param>
    /// <param name="slot">The timed slot name (e.g. <c>ModifiedTables</c>).</param>
    /// <param name="ms">Elapsed milliseconds for this slot on this target.</param>
    /// <param name="scriptsRun">Number of scripts run in this slot on this target, where cheaply known; 0 otherwise.</param>
    public void Record(string scopeKey, string database, string slot, long ms, int scriptsRun) =>
        _entries.Add(new RecordEntry(scopeKey, database, slot, ms, scriptsRun));

    /// <summary>One entry per distinct slot, aggregated across all targets.</summary>
    public IReadOnlyList<SlotTiming> BySlot() =>
        _entries
            .GroupBy(e => e.Slot)
            .Select(g => new SlotTiming(g.Key, g.Sum(e => e.Ms), g.Count()))
            .ToList();

    /// <summary>One entry per distinct database, summing every slot's ms for that database.</summary>
    public IReadOnlyList<DbTiming> ByDatabase() =>
        _entries
            .GroupBy(e => e.Database)
            .Select(g => new DbTiming(g.Key, g.Sum(e => e.Ms)))
            .ToList();

    /// <summary>One entry per individual <see cref="Record"/> call whose ms is strictly greater than <paramref name="thresholdMs"/>.</summary>
    public IReadOnlyList<BottleneckEntry> Bottlenecks(long thresholdMs) =>
        _entries
            .Where(e => e.Ms > thresholdMs)
            .Select(e => new BottleneckEntry(e.ScopeKey, e.Slot, e.Ms))
            .ToList();

    /// <summary>Per-target slot timings for one scope (the target's LogPrefix), for the report's targets[].slots[].</summary>
    public IReadOnlyList<TargetSlotTiming> SlotsForScope(string scopeKey) =>
        _entries
            .Where(e => e.ScopeKey == scopeKey)
            .GroupBy(e => e.Slot)
            .Select(g => new TargetSlotTiming(g.Key, g.Sum(e => e.Ms), g.Sum(e => e.ScriptsRun)))
            .ToList();
}

public sealed record SlotTiming(string Slot, long TotalMs, int TargetCount);
public sealed record DbTiming(string Database, long TotalMs);
public sealed record BottleneckEntry(string Scope, string Slot, long DurationMs);
