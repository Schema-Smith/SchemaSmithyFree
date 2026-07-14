// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Schema.Domain;

namespace SchemaQuench;

/// <summary>
/// End-of-work-unit drain of the session-scoped ChangeAudit table (#243 E5). Runs on the SAME
/// connection the 4 table procs wrote on (tableConnection for SQL Server / PostgreSQL, the single
/// connection for MySQL), so the session filter is exact and uncommitted same-session rows are
/// visible. Returns null for an engine whose procs do not yet emit, or when the audit table is
/// absent (kindling suppressed) — the caller then leaves the run honestly not-instrumented. Deletes
/// its own session's rows after reading (self-cleaning, mirrors StatusMessageMonitor).
/// </summary>
public static class ChangeAuditReader
{
    public static IReadOnlyList<ChangeAuditRow> ReadAndDrain(Platform platform, IDbCommand command) =>
        platform.GetBasePlatform() switch
        {
            Platform.SqlServer => ReadRows(command,
                "SELECT ObjectType, ObjectName, ActionType FROM SchemaSmith.ChangeAudit WHERE SessionId = @@SPID ORDER BY Id",
                "DELETE FROM SchemaSmith.ChangeAudit WHERE SessionId = @@SPID"),
            Platform.PostgreSQL => ReadRows(command,
                "SELECT \"ObjectType\", \"ObjectName\", \"ActionType\" FROM \"SchemaSmith\".\"ChangeAudit\" WHERE \"SessionId\" = pg_backend_pid() ORDER BY \"Id\"",
                "DELETE FROM \"SchemaSmith\".\"ChangeAudit\" WHERE \"SessionId\" = pg_backend_pid()"),
            Platform.MySQL => ReadRows(command,
                "SELECT ObjectType, ObjectName, ActionType FROM SchemaSmith_ChangeAudit WHERE SessionId = CONNECTION_ID() ORDER BY Id",
                "DELETE FROM SchemaSmith_ChangeAudit WHERE SessionId = CONNECTION_ID()"),
            _ => null
        };

    private static IReadOnlyList<ChangeAuditRow> ReadRows(IDbCommand command, string selectSql, string deleteSql)
    {
        try
        {
            var rows = new List<ChangeAuditRow>();
            command.Parameters.Clear();
            command.CommandText = selectSql;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    rows.Add(new ChangeAuditRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }

            command.CommandText = deleteSql;
            command.ExecuteNonQuery();
            return rows;
        }
        catch (DbException)
        {
            // Audit table absent (kindling suppressed) or unreadable — treat as not instrumented.
            return null;
        }
    }
}
