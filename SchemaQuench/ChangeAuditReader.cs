// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Data;
using Schema.Domain;

namespace SchemaQuench;

/// <summary>
/// End-of-work-unit drain of the session-scoped ChangeAudit table (#243 E5). Runs on the SAME
/// connection the 4 table procs wrote on (tableConnection for SQL Server / PostgreSQL, the single
/// connection for MySQL), so the session filter is exact and uncommitted same-session rows are
/// visible. Returns null for an engine whose procs do not yet emit — the caller then leaves the
/// run honestly not-instrumented. Deletes its own session's rows after reading (self-cleaning,
/// mirrors StatusMessageMonitor). Per-engine reads land in their respective slices (SQL Server /
/// PostgreSQL / MySQL).
/// </summary>
public static class ChangeAuditReader
{
    public static IReadOnlyList<ChangeAuditRow> ReadAndDrain(Platform platform, IDbCommand command) =>
        platform switch
        {
            // Slice C: Platform.SqlServer => ReadSqlServer(command),
            // Slice D: Platform.PostgreSQL => ReadPostgreSql(command),
            // Slice E: Platform.MySQL => ReadMySql(command),
            _ => null
        };
}
