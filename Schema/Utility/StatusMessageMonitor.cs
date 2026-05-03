// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.Threading;
using Schema.DataAccess;

namespace Schema.Utility;

/// <summary>
/// MySQL-specific: polls the SchemaSmith_StatusMessages table for new messages written by stored
/// procedures running on a different connection. Uses a background timer to check for messages
/// at regular intervals. Uses the CONNECTION_ID() pattern for session identification.
/// </summary>
public class StatusMessageMonitor : IDisposable
{
    private readonly string _connectionString;
    private readonly long _sessionId;
    private readonly Action<string> _callback;
    private readonly Timer _timer;
    private IDbConnection _connection;
    private long _lastId;
    private bool _disposed;
    private readonly object _pollLock = new();

    /// <summary>
    /// Creates a new monitor that polls for status messages from the given session.
    /// </summary>
    /// <param name="connectionString">Connection string for a separate polling connection.</param>
    /// <param name="sessionId">The CONNECTION_ID() of the session writing messages.</param>
    /// <param name="callback">Action invoked for each new message.</param>
    /// <param name="pollIntervalMs">Polling interval in milliseconds (default 200ms).</param>
    public StatusMessageMonitor(string connectionString, long sessionId, Action<string> callback, int pollIntervalMs = 200)
    {
        _connectionString = connectionString;
        _sessionId = sessionId;
        _callback = callback;

        _connection = DbConnectionFactory.ForPlatform(Domain.Platform.MySQL).GetDbConnection(connectionString);
        _connection.Open();

        _timer = new Timer(PollForMessages, null, pollIntervalMs, pollIntervalMs);
    }

    private void PollForMessages(object state)
    {
        if (_disposed) return;

        lock (_pollLock)
        {
            if (_disposed) return;
            try
            {
                PollOnce();
            }
            catch
            {
                // Swallow polling errors - the monitor is best-effort
                // If the connection breaks, we'll try to reconnect on next poll
                TryReconnect();
            }
        }
    }

    internal void PollOnce()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, Message FROM SchemaSmith_StatusMessages WHERE SessionId = @sid AND Id > @lastId ORDER BY Id";

        var sidParam = command.CreateParameter();
        sidParam.ParameterName = "@sid";
        sidParam.Value = _sessionId;
        sidParam.DbType = DbType.Int64;
        command.Parameters.Add(sidParam);

        var lastIdParam = command.CreateParameter();
        lastIdParam.ParameterName = "@lastId";
        lastIdParam.Value = _lastId;
        lastIdParam.DbType = DbType.Int64;
        command.Parameters.Add(lastIdParam);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            var message = reader.GetString(1);
            _lastId = id;
            _callback(message);
        }
    }

    private void TryReconnect()
    {
        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch { /* ignore */ }

        try
        {
            _connection = DbConnectionFactory.ForPlatform(Domain.Platform.MySQL).GetDbConnection(_connectionString);
            _connection.Open();
        }
        catch { /* ignore - will retry on next poll */ }
    }

    /// <summary>
    /// Performs a final poll to capture any remaining messages. Call after each procedure CALL completes.
    /// </summary>
    public void Flush()
    {
        if (_disposed) return;

        lock (_pollLock)
        {
            if (_disposed) return;
            try
            {
                PollOnce();
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Stops the monitor, performs a final flush, and cleans up session messages.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Dispose();

        // Final flush
        try
        {
            lock (_pollLock)
            {
                PollOnce();
            }
        }
        catch { /* ignore */ }

        // Clean up messages for this session
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = @sid";

            var sidParam = command.CreateParameter();
            sidParam.ParameterName = "@sid";
            sidParam.Value = _sessionId;
            sidParam.DbType = DbType.Int64;
            command.Parameters.Add(sidParam);

            command.ExecuteNonQuery();
        }
        catch { /* ignore cleanup errors */ }

        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch { /* ignore */ }
    }
}
