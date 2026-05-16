// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.Domain;

namespace SchemaQuench;

/// <summary>
/// Runs a schema-template's <c>SchemaIdentificationScript</c> against an open database
/// command, reads back the single-column result set of schema names, and validates each
/// returned name against the platform-specific reserved-name reject list (design §5.4).
///
/// <para>
/// Mirrors the existing call-site pattern for <c>DatabaseIdentificationScript</c> in
/// <see cref="ProductQuench"/> — caller supplies an <see cref="IDbCommand"/> with a live
/// <see cref="IDbConnection"/>; this class sets the command text, executes the reader,
/// and projects column 0 to <see cref="string"/>.
/// </para>
///
/// <para><b>Reserved-name guards (design §5.4).</b> Discovery hard-rejects platform-owned
/// schema namespaces: SQL Server's <c>dbo</c>, <c>sys</c>, <c>INFORMATION_SCHEMA</c>,
/// <c>guest</c>, and the <c>db_*</c> built-in roles; PostgreSQL's <c>public</c>,
/// <c>pg_catalog</c>, <c>pg_toast</c>, <c>pg_temp_*</c>, <c>pg_toast_temp_*</c>,
/// <c>information_schema</c>. These names remain valid as <c>RelatedTableSchema</c>
/// literals (cross-schema FK references are a feature, not a bug) and as explicit
/// schema references in free-form SQL — the guard only fires when a name is presented as
/// an <i>iteration target</i>. Shared content belongs in a regular template that runs earlier
/// in <c>TemplateOrder</c>, not in a fan-out iteration.</para>
///
/// <para><b>MySQL:</b> schema templates are intentionally not offered on MySQL (no
/// namespace-inside-database concept — see design §2). Load-time validation already
/// rejects schema-template-on-MySQL upstream; the defensive throw here catches direct
/// misuse from a calling test or programmatic invocation that bypasses the load path.</para>
/// </summary>
public static class SchemaDiscovery
{
    // SQL Server hard-rejects: built-in dbo/sys/INFORMATION_SCHEMA/guest plus the
    // db_* fixed database roles (which double as schemas in SQL Server).
    private static readonly HashSet<string> SqlServerReserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "dbo",
        "sys",
        "INFORMATION_SCHEMA",
        "guest",
        "db_owner",
        "db_accessadmin",
        "db_securityadmin",
        "db_ddladmin",
        "db_backupoperator",
        "db_datareader",
        "db_datawriter",
        "db_denydatareader",
        "db_denydatawriter"
    };

    // PostgreSQL fixed-name reserved set; pg_temp_* and pg_toast_temp_* are wildcard-handled below.
    private static readonly HashSet<string> PostgreSqlReservedFixed = new(StringComparer.OrdinalIgnoreCase)
    {
        "public",
        "pg_catalog",
        "pg_toast",
        "information_schema"
    };

    /// <summary>
    /// Runs <paramref name="template"/>'s <c>SchemaIdentificationScript</c> against
    /// <paramref name="command"/>, validates each returned name against the platform's
    /// reject list, and returns the schema names in discovery order.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the platform is MySQL (schema templates are SQL-Server / PostgreSQL only),
    /// or when discovery returns a reserved schema name. The thrown message names the
    /// offending schema and points the user at the "shared content in a regular template" pattern.
    /// </exception>
    public static List<string> Discover(IDbCommand command, Template template)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (command == null) throw new ArgumentNullException(nameof(command));

        var platform = template.Product?.Platform ?? Platform.Unknown;
        if (platform == Platform.MySQL)
        {
            throw new InvalidOperationException(
                $"Schema-template discovery is not supported on MySQL (template '{template.Name}'). " +
                "MySQL conflates schemas and databases; use DatabaseIdentificationScript instead. " +
                "See design §2 for rationale.");
        }

        command.CommandText = template.SchemaIdentificationScript;
        var results = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var raw = reader[0];
            if (raw == null || raw == DBNull.Value) continue;
            var name = raw.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            ValidateNotReserved(name, platform, template.Name);
            results.Add(name);
        }

        return results;
    }

    private static void ValidateNotReserved(string name, Platform platform, string templateName)
    {
        if (IsReserved(name, platform))
        {
            throw new InvalidOperationException(
                $"SchemaIdentificationScript for template '{templateName}' returned reserved schema name '{name}'. " +
                "Platform-owned namespaces (dbo/sys/public/pg_catalog/etc.) cannot be iteration targets — " +
                "put shared content in a regular template that runs earlier in TemplateOrder. " +
                "(The same name remains valid as a RelatedTableSchema literal or as an explicit reference in free-form SQL.)");
        }
    }

    private static bool IsReserved(string name, Platform platform) => platform switch
    {
        Platform.SqlServer => SqlServerReserved.Contains(name),
        Platform.PostgreSQL => IsPostgreSqlReserved(name),
        _ => false
    };

    private static bool IsPostgreSqlReserved(string name)
    {
        if (PostgreSqlReservedFixed.Contains(name)) return true;
        if (name.StartsWith("pg_temp_", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("pg_toast_temp_", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
