// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using System.Runtime.CompilerServices;

namespace SchemaQuench.IntegrationTests.PostgreSQL.Profiling;

/// <summary>
/// IDbConnection decorator used by the PG connection-discipline investigation (Phase 1).
/// Forwards every member to the inner Npgsql connection unchanged; intercepts Open/Close/Dispose
/// to record events with caller-tag categorization at the recorder. Caller tag is derived from
/// a stack walk at Open/Close time — see ConnectionCallerClassifier.
/// </summary>
public sealed class ProfilingConnection : IDbConnection
{
    private readonly IDbConnection _inner;
    private readonly ProfilingConnectionRecorder _recorder;
    private readonly int _connId;

    public ProfilingConnection(IDbConnection inner, ProfilingConnectionRecorder recorder)
    {
        _inner = inner;
        _recorder = recorder;
        _connId = RuntimeHelpers.GetHashCode(inner);
    }

    public void Open()
    {
        var category = ConnectionCallerClassifier.Classify(out var frame);
        _inner.Open();
        _recorder.RecordOpen(_connId, category, frame);
    }

    public void Close()
    {
        var category = ConnectionCallerClassifier.Classify(out var frame);
        _inner.Close();
        _recorder.RecordClose(_connId, category, frame);
    }

    public void Dispose()
    {
        _recorder.RecordDispose(_connId);
        _inner.Dispose();
    }

    public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
    public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
    public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
    public IDbCommand CreateCommand() => _inner.CreateCommand();

    public string ConnectionString
    {
        get => _inner.ConnectionString;
        set => _inner.ConnectionString = value;
    }

    public int ConnectionTimeout => _inner.ConnectionTimeout;
    public string Database => _inner.Database;
    public ConnectionState State => _inner.State;
}
