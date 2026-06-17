// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace SchemaQuench;

/// <summary>
/// Classifies whether an exception represents a database deadlock — the engine's
/// "rerun the transaction" signal. Recognised across the three platforms by their
/// locale-independent codes (SQL Server 1205, PostgreSQL <c>40P01</c>) with a message
/// fallback that also covers MySQL ("Deadlock found …"). Walks the inner-exception chain.
///
/// <para>Used by <see cref="DatabaseQuench"/> to retry idempotent convergence procs that lose a
/// transient deadlock race under parallel iteration — defense-in-depth alongside the SQL-level
/// prevention in the PostgreSQL convergence procs.</para>
/// </summary>
internal static class DeadlockClassifier
{
    public static bool IsDeadlock(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            // Locale-independent codes first.
            switch (e)
            {
                case SqlServerErrorException { Number: 1205 }:   // wrapped via InfoMessage
                case SqlException { Number: 1205 }:              // thrown directly
                    return true;
                case PostgresException pg when pg.SqlState == PostgresErrorCodes.DeadlockDetected:
                    return true;
            }

            // Message fallback — covers MySQL ("Deadlock found …") and any English-locale
            // message whose typed code we didn't match above.
            if (e.Message != null && e.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
