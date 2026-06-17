// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace SchemaQuench.IntegrationTests.PostgreSQL.Profiling;

/// <summary>
/// Thread-safe accumulator for open/close/dispose events captured by ProfilingConnection.
/// Used by the PG connection-discipline investigation (Phase 1) to disentangle test-side
/// vs engine-side connection demand.
/// </summary>
public sealed class ProfilingConnectionRecorder
{
    private readonly ConcurrentQueue<EventRecord> _events = new();
    private long _eventCounter;
    private readonly long _startTicks = Stopwatch.GetTimestamp();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    public void RecordOpen(int connId, string category, string callerFrame) =>
        Append(connId, category, callerFrame, "open");

    public void RecordClose(int connId, string category, string callerFrame) =>
        Append(connId, category, callerFrame, "close");

    public void RecordDispose(int connId) =>
        Append(connId, string.Empty, string.Empty, "dispose");

    private void Append(int connId, string category, string callerFrame, string kind)
    {
        _events.Enqueue(new EventRecord(
            EventId: Interlocked.Increment(ref _eventCounter),
            TimestampMicros: GetMicros(),
            ThreadId: Environment.CurrentManagedThreadId,
            ConnId: connId,
            Category: category,
            CallerFrame: callerFrame,
            EventKind: kind));
    }

    private long GetMicros()
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
        var elapsedMicros = (long)(elapsedTicks * 1_000_000.0 / Stopwatch.Frequency);
        return _startTime.ToUnixTimeMilliseconds() * 1000 + elapsedMicros;
    }

    public int OpenCount => _events.Count(e => e.EventKind == "open");
    public int CloseCount => _events.Count(e => e.EventKind == "close");
    public int PeakConcurrentOpens => ComputePeakConcurrentOpens();

    private int ComputePeakConcurrentOpens()
    {
        var ordered = _events.OrderBy(e => e.EventId).ToList();
        var current = 0;
        var peak = 0;
        foreach (var e in ordered)
        {
            if (e.EventKind == "open") current++;
            else if (e.EventKind == "close" || e.EventKind == "dispose") current = Math.Max(0, current - 1);
            if (current > peak) peak = current;
        }
        return peak;
    }

    public void WriteCsv(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path);
        writer.WriteLine("event_id,timestamp_micros,thread_id,conn_id,category,caller_frame,event_kind");
        foreach (var e in _events.OrderBy(x => x.EventId))
        {
            var caller = (e.CallerFrame ?? string.Empty).Replace(",", ";");
            writer.WriteLine($"{e.EventId},{e.TimestampMicros},{e.ThreadId},{e.ConnId},{e.Category},{caller},{e.EventKind}");
        }
    }

    public void Reset()
    {
        while (_events.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _eventCounter, 0);
    }

    private readonly record struct EventRecord(
        long EventId,
        long TimestampMicros,
        int ThreadId,
        int ConnId,
        string Category,
        string CallerFrame,
        string EventKind);
}
