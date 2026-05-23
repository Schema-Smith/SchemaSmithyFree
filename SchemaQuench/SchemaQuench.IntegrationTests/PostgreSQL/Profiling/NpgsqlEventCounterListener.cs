// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;

namespace SchemaQuench.IntegrationTests.PostgreSQL.Profiling;

/// <summary>
/// EventListener that subscribes to the Npgsql event source and captures EventCounter samples
/// (pool internals — idle-connections, busy-connections, command-wait-time, bytes-*).
/// Sample cadence is set to 100ms via the EventCounterIntervalSec argument.
///
/// <para>Cross-checked against ProfilingConnectionRecorder's wrapper-side open/close counts;
/// disagreement is itself a finding (connections held outside the factory path).</para>
/// </summary>
public sealed class NpgsqlEventCounterListener : EventListener
{
    private const string NpgsqlEventSourceName = "Npgsql";
    private readonly ConcurrentBag<CounterSample> _samples = new();
    private EventSource _npgsqlSource;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // The Npgsql event source may be created BEFORE this listener is constructed (e.g. when
        // an earlier test triggered Npgsql initialization). EventListener invokes
        // OnEventSourceCreated for every source already alive at the moment of subscription,
        // so we handle both cases identically here.
        if (eventSource.Name != NpgsqlEventSourceName) return;
        _npgsqlSource = eventSource;
        EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string>
        {
            ["EventCounterIntervalSec"] = "0.1",
        });
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource.Name != NpgsqlEventSourceName) return;
        if (eventData.EventName != "EventCounters") return;
        if (eventData.Payload == null || eventData.Payload.Count == 0) return;

        var dict = eventData.Payload[0] as IDictionary<string, object>;
        if (dict == null) return;

        var name = dict.TryGetValue("Name", out var n) ? n?.ToString() ?? string.Empty : string.Empty;
        var value = ExtractValue(dict);

        _samples.Add(new CounterSample(
            TimestampUtc: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
            Name: name,
            Value: value));
    }

    private static double ExtractValue(IDictionary<string, object> dict)
    {
        // PollingCounter / EventCounter present "Mean", IncrementingEventCounter presents "Increment".
        // Capture whichever shape this counter exposes.
        if (dict.TryGetValue("Mean", out var mean) && TryToDouble(mean, out var m)) return m;
        if (dict.TryGetValue("Increment", out var inc) && TryToDouble(inc, out var i)) return i;
        if (dict.TryGetValue("Max", out var max) && TryToDouble(max, out var x)) return x;
        if (dict.TryGetValue("Count", out var count) && TryToDouble(count, out var c)) return c;
        return double.NaN;
    }

    private static bool TryToDouble(object o, out double v)
    {
        try { v = Convert.ToDouble(o); return true; }
        catch { v = double.NaN; return false; }
    }

    public void WriteCsv(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path);
        writer.WriteLine("timestamp_micros,counter_name,value");
        foreach (var s in _samples.OrderBy(x => x.TimestampUtc).ThenBy(x => x.Name))
        {
            writer.WriteLine($"{s.TimestampUtc},{s.Name},{s.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
        }
    }

    public int SampleCount => _samples.Count;

    public void Reset()
    {
        while (_samples.TryTake(out _)) { }
    }

    private readonly record struct CounterSample(long TimestampUtc, string Name, double Value);
}
