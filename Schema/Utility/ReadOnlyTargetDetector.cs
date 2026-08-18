// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.Domain;

namespace Schema.Utility
{
    /// <summary>
    /// Reports whether a target database is read-only, so a template marked
    /// <see cref="Domain.Template.SkipIfReadOnly"/> can be skipped rather than fail against it.
    /// <para>The motivating case is a SQL Server Availability Group readable secondary, but a
    /// PostgreSQL hot standby and a MySQL/MariaDB replica are the same situation, so all four
    /// engines are covered.</para>
    /// </summary>
    public static class ReadOnlyTargetDetector
    {
        public static string GetReadOnlyQuery(Platform platform) => platform switch
        {
            // Covers an Availability Group readable secondary and a database explicitly SET READ_ONLY.
            Platform.SqlServer =>
                "SELECT CASE WHEN DATABASEPROPERTYEX(DB_NAME(), 'Updateability') = 'READ_ONLY' THEN 1 ELSE 0 END",
            // pg_is_in_recovery() is a standby; transaction_read_only also reflects
            // default_transaction_read_only on a primary deliberately held read-only.
            Platform.PostgreSQL =>
                "SELECT CASE WHEN pg_is_in_recovery() OR current_setting('transaction_read_only') = 'on' THEN 1 ELSE 0 END",
            Platform.MySQL =>
                "SELECT CASE WHEN @@read_only = 1 OR @@super_read_only = 1 THEN 1 ELSE 0 END",
            // MariaDB has no super_read_only — referencing it is "Unknown system variable" and would
            // fail the whole check, so MariaDB reads @@read_only alone.
            Platform.MariaDb =>
                "SELECT CASE WHEN @@read_only = 1 THEN 1 ELSE 0 END",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform for read-only detection")
        };

        public static bool IsReadOnly(IDbCommand command, Platform platform)
        {
            command.CommandText = GetReadOnlyQuery(platform);
            var raw = command.ExecuteScalar();
            return raw != null && raw != DBNull.Value && Convert.ToInt32(raw) == 1;
        }
    }
}
