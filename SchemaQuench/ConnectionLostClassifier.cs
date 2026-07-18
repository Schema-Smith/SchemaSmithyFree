// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace SchemaQuench;

/// <summary>
/// Classifies whether an exception means the target server went away mid-deploy — restarted,
/// crashed, or was OOM-killed — as opposed to a schema/script error or an initial connect
/// failure (the latter is caught up-front by <see cref="ProductQuench"/>'s connection test).
/// Recognised across the three engines by transport/shutdown signals, with an inner
/// <see cref="SocketException"/>/<see cref="IOException"/> fallback — the literal
/// "SocketException: Success" surface seen when SQL Server is torn down mid-command
/// (SchemaSmith#353). Walks the inner-exception chain.
///
/// <para>Only invoked at DB-operation catch sites (the quench work loop and the top-level
/// deploy net), so a transport <see cref="IOException"/> in the chain is unambiguous there.</para>
/// </summary>
internal static class ConnectionLostClassifier
{
    public static bool IsConnectionLost(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            switch (e)
            {
                // Unambiguous transport failure anywhere in the chain. IOException also covers
                // EndOfStreamException (derived) — how Npgsql/MySqlConnector surface a torn stream.
                case SocketException:
                case IOException:
                    return true;

                // SQL Server transport / server-gone numbers (deliberately NOT -2 timeout, and
                // NOT schema-error numbers).
                case SqlException { Number: 233 or 64 or 20 or 10053 or 10054 }:
                    return true;

                // PostgreSQL: connection-exception class (08*) + admin/crash shutdown. String
                // literals avoid depending on Npgsql constant names.
                case PostgresException pg when pg.SqlState != null &&
                    (pg.SqlState.StartsWith("08", StringComparison.Ordinal)
                     || pg.SqlState == "57P01"   // admin_shutdown
                     || pg.SqlState == "57P02"):  // crash_shutdown
                    return true;

                // MySQL: server shutting down mid-run, or the server can no longer be reached.
                // A "lost connection during query" drop surfaces as an inner IOException (above).
                case MySqlException { ErrorCode: MySqlErrorCode.ServerShutdown
                                              or MySqlErrorCode.UnableToConnectToHost }:
                    return true;
            }

            // Narrow message fallback: SQL Server tears the session down before the socket error
            // surfaces ("Cannot continue the execution because the session is in the kill state.").
            // Scoped tightly so ordinary errors are not misclassified as drops.
            if (e.Message != null &&
                e.Message.Contains("the session is in the kill state", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
