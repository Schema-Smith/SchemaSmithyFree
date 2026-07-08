// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;

namespace SchemaQuench;

public sealed record FailureRecord(
    string Phase,
    string ScopeKey,
    string Error,
    IReadOnlyList<string> ContextTail,
    string ArtifactPath);

/// <summary>
/// A bounded, per-scope capture buffer owned by each failure-capable unit of work (product,
/// per-server script task, per-tenant work unit). Its choke-point log wrappers append lines here;
/// on failure it snapshots into an immutable <see cref="FailureRecord"/>. Ownership is explicit
/// (never ambient/thread-local) because the worker pool is hand-rolled with side threads.
/// </summary>
public sealed class FailureContext
{
    private readonly string _phase;
    private readonly string _scopeKey;
    private readonly int _capacity;
    private readonly LinkedList<string> _lines = new();
    private readonly object _lock = new();

    public FailureContext(string phase, string scopeKey, int capacity)
    {
        _phase = phase;
        _scopeKey = scopeKey;
        _capacity = capacity < 0 ? 0 : capacity;
    }

    public void Log(string line)
    {
        if (_capacity == 0) return;
        lock (_lock)
        {
            _lines.AddLast(line);
            while (_lines.Count > _capacity) _lines.RemoveFirst();
        }
    }

    public FailureRecord ToRecord(string error, string artifactPath)
    {
        lock (_lock)
            return new FailureRecord(_phase, _scopeKey, error, new List<string>(_lines), artifactPath);
    }
}
