// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SchemaQuench;

/// <summary>
/// Bounded thread-pool dispatcher for schema-template fan-out work units (design §5.2).
///
/// <para><b>Single-use.</b> Each instance dispatches exactly one batch of work. Calling
/// <see cref="Run"/> a second time on the same instance throws — wrapping <see cref="Run"/>
/// in a retry loop is a misuse pattern, since the queues drain on the first call and a second
/// call would otherwise silently no-op. Construct a fresh dispatcher per dispatch.</para>
///
/// <para><b>TRANSITIONAL (slice 3 of schema-templates) — fail loud, abort fast.</b> If any
/// work unit's callback throws, the dispatcher stops accepting new work, lets currently-running
/// callbacks complete naturally (no aggressive cancellation mid-script — partial-transaction
/// risk too high), and surfaces the failure as an <see cref="AggregateException"/> from
/// <see cref="Run"/>. Failure isolation (the <c>ContinueOnSchemaFailure</c> /
/// <c>ContinueOnDatabaseFailure</c> settings on <c>Template</c>) lands in slice 4 along with
/// the rich per-failure logging. Slice 4 will replace the unconditional abort with per-policy
/// routing — the dispatcher itself becomes aware of which failures are isolatable and which
/// still abort. See Community roadmap: "Slice 3 transitional aids".</para>
///
/// <para><b>Scheduling model.</b> Each work unit's template name maps via the
/// <c>allowParallel</c> dictionary to one of two queues:</para>
/// <list type="bullet">
///   <item><description><b>Parallel queue (default):</b> a single shared FIFO. Up to
///   <c>maxThreads</c> workers pull from this queue concurrently.</description></item>
///   <item><description><b>Per-template serial queue:</b> templates with
///   <c>AllowParallel: false</c> get their own queue. At most one of that template's units runs
///   at a time, but parallel-eligible units from other templates may run alongside. This is the
///   "this template touches a shared resource, do not parallelize" escape hatch.</description></item>
/// </list>
///
/// <para><b>Choice rationale (vs <c>TaskQueueManager&lt;T&gt;</c>).</b> The codebase already
/// has <c>Schema.Utility.TaskQueueManager&lt;T&gt;</c>, which is used elsewhere for plain
/// bounded-concurrency work (e.g., table token resolution in <c>Template.cs</c> and database
/// fan-out in <c>ProductQuench.EnumerateWorkUnitsForServer</c>). It was considered for this
/// dispatcher and rejected for slice 3 because (a) it silently swallows worker exceptions
/// (no fail-loud path), and (b) it has no notion of per-template serial sub-queues.
/// Wrapping it to add both behaviors would be more code than implementing the focused
/// dispatcher directly, and would obscure the simpler scheduling model this slice needs.
/// Slice 4 will revisit if the dispatcher and <c>TaskQueueManager&lt;T&gt;</c> converge.</para>
/// </summary>
public sealed class WorkUnitDispatcher
{
    private readonly int _maxThreads;
    private readonly Action<WorkUnit> _callback;
    private readonly IReadOnlyDictionary<string, bool> _allowParallel;
    private readonly Queue<WorkUnit> _parallelQueue;
    private readonly Dictionary<string, Queue<WorkUnit>> _serialQueues;
    private readonly HashSet<string> _serialBusy = new();
    private readonly object _lock = new();
    private readonly List<Exception> _failures = new();
    private bool _abort;
    private bool _hasRun;

    /// <summary>
    /// Creates a dispatcher that will execute <paramref name="callback"/> for each work unit in
    /// <paramref name="units"/>, with at most <paramref name="maxThreads"/> running concurrently.
    /// </summary>
    /// <param name="units">The full flat list of work units enumerated by the caller.</param>
    /// <param name="maxThreads">Maximum concurrent workers. Coerced to a minimum of 1.</param>
    /// <param name="allowParallel">
    /// Per-template parallelism: missing templates default to <c>true</c> (parallel-eligible).
    /// Templates mapped to <c>false</c> get a per-template serial queue.
    /// </param>
    /// <param name="callback">Invoked once per work unit. Throwing aborts the dispatch (slice-3 stance).</param>
    public WorkUnitDispatcher(
        IEnumerable<WorkUnit> units,
        int maxThreads,
        IReadOnlyDictionary<string, bool> allowParallel,
        Action<WorkUnit> callback)
    {
        if (units == null) throw new ArgumentNullException(nameof(units));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _allowParallel = allowParallel ?? new Dictionary<string, bool>();
        _maxThreads = maxThreads < 1 ? 1 : maxThreads;

        _parallelQueue = new Queue<WorkUnit>();
        _serialQueues = new Dictionary<string, Queue<WorkUnit>>();

        foreach (var unit in units)
        {
            if (IsSerial(unit.TemplateName))
            {
                if (!_serialQueues.TryGetValue(unit.TemplateName, out var q))
                {
                    q = new Queue<WorkUnit>();
                    _serialQueues[unit.TemplateName] = q;
                }
                q.Enqueue(unit);
            }
            else
            {
                _parallelQueue.Enqueue(unit);
            }
        }
    }

    /// <summary>
    /// Synchronously dispatches all work units to the bounded worker pool and returns when
    /// every unit has either completed or — in the failure path — every in-flight unit has
    /// drained. Throws an <see cref="AggregateException"/> wrapping the first callback failure
    /// (and any additional in-flight failures observed while draining).
    /// <para>Single-use: a second call on the same instance throws
    /// <see cref="InvalidOperationException"/>.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Run"/> is called more than once on the same instance.</exception>
    public void Run()
    {
        lock (_lock)
        {
            if (_hasRun)
            {
                throw new InvalidOperationException(
                    "WorkUnitDispatcher is single-use; create a new instance per Run.");
            }
            _hasRun = true;
        }

        if (TotalRemaining() == 0) return;

        var tasks = new List<Task>(_maxThreads);
        for (var i = 0; i < _maxThreads; i++)
        {
            tasks.Add(Task.Run(WorkerLoop));
        }

        Task.WaitAll(tasks.ToArray());

        if (_failures.Count > 0)
        {
            throw new AggregateException(
                "One or more work units failed; dispatcher aborted (slice-3 fail-loud stance).",
                _failures);
        }
    }

    private void WorkerLoop()
    {
        while (true)
        {
            WorkUnit unit;
            string serialClaim;
            lock (_lock)
            {
                if (_abort) return;
                if (!TryDequeue(out unit, out serialClaim))
                {
                    // No work currently available — but a serial queue may unlock when a sibling
                    // unit finishes. Exit only when nothing remains anywhere.
                    if (TotalRemaining() == 0) return;
                    Monitor.Wait(_lock);
                    continue;
                }
            }

            try
            {
                _callback(unit);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _failures.Add(ex);
                    _abort = true;
                    Monitor.PulseAll(_lock);
                }
            }
            finally
            {
                lock (_lock)
                {
                    if (serialClaim != null) _serialBusy.Remove(serialClaim);
                    Monitor.PulseAll(_lock);
                }
            }
        }
    }

    private bool TryDequeue(out WorkUnit unit, out string serialClaim)
    {
        // Prefer parallel work to keep the pool saturated while serial queues unblock naturally.
        if (_parallelQueue.Count > 0)
        {
            unit = _parallelQueue.Dequeue();
            serialClaim = null;
            return true;
        }

        foreach (var kvp in _serialQueues)
        {
            if (_serialBusy.Contains(kvp.Key)) continue;
            if (kvp.Value.Count == 0) continue;
            unit = kvp.Value.Dequeue();
            serialClaim = kvp.Key;
            _serialBusy.Add(serialClaim);
            return true;
        }

        unit = default;
        serialClaim = null;
        return false;
    }

    private int TotalRemaining()
    {
        var n = _parallelQueue.Count;
        foreach (var q in _serialQueues.Values) n += q.Count;
        return n;
    }

    private bool IsSerial(string templateName)
    {
        // Missing entries default to parallel-eligible.
        return _allowParallel.TryGetValue(templateName, out var allow) && !allow;
    }
}
