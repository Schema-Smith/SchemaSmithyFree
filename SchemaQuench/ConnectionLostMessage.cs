// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace SchemaQuench;

/// <summary>
/// Builds the user-facing message for a mid-deploy server disconnect: names the server and
/// phase, explains the server dropped (restart/crash/OOM), and directs the user to the
/// environment fix + re-run. Pure — no I/O. The raw provider exception is logged separately
/// to the error log by the caller, so the full stack is never lost.
/// </summary>
internal static class ConnectionLostMessage
{
    public static string Build(string server, string phase)
    {
        var who = string.IsNullOrWhiteSpace(server) ? "the target server" : server;
        var where = string.IsNullOrWhiteSpace(phase) ? "" : $" during {phase}";
        return $"Lost connection to {who}{where} — the server stopped responding mid-deploy " +
               "(it may have restarted, crashed, or run out of memory). This is an environment " +
               "problem, not a schema error: check the server/container logs and available memory, " +
               "then re-run — SchemaQuench is idempotent and will converge cleanly.";
    }
}
