// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.Domain;

namespace SchemaQuench;

/// <summary>
/// Idempotently provisions schemas when <c>TemplateTargets.&lt;name&gt;.CreateIfMissing</c>
/// is true and the target schema doesn't yet exist on the iteration database (design §6, #257).
/// The schema axis is supported on SQL Server and PostgreSQL only; MySQL has no
/// schema/database distinction and uses <c>EnsureDatabaseExists</c> on the database axis
/// (slice 4). All per-engine DDL uses native IF-NOT-EXISTS semantics so a race between two
/// parallel quenches against the same target is safe.
/// <para>
/// The provisioner is a thin pure-DDL helper — existence checking and the broader
/// skip-missing flow live in <see cref="DatabaseQuench.EnsureSchemaExists(IDbCommand)"/>,
/// which invokes the provisioner only after determining the schema is missing AND the
/// override declared <c>CreateIfMissing: true</c>. Splitting the responsibility this way
/// keeps the DDL strings testable in isolation and avoids duplicating quoting / escaping
/// across multiple call sites.
/// </para>
/// </summary>
public class SchemaProvisioner
{
    /// <summary>
    /// Emits idempotent CREATE SCHEMA DDL on the supplied open command. Under WhatIf the
    /// DDL is NOT executed; the equivalent action renders through <paramref name="log"/>
    /// using the engine's existing <c>[WhatIf] Would &lt;verb&gt;</c> convention so the
    /// rest of the WhatIf summary accurately reflects what a real run would do.
    /// </summary>
    /// <param name="command">Open command whose connection targets the iteration's database.</param>
    /// <param name="schemaName">Schema to ensure exists. Caller is responsible for upstream
    /// identifier validation; embedded brackets / quotes are escaped here as belt-and-suspenders.</param>
    /// <param name="platform">Target engine. MySQL throws <see cref="InvalidOperationException"/>.</param>
    /// <param name="isWhatIf">When true, the DDL renders through <paramref name="log"/> and
    /// is NOT executed.</param>
    /// <param name="log">Info-level log surface for the per-create / WhatIf rendering line.</param>
    public virtual void EnsureSchemaExists(IDbCommand command, string schemaName, Platform platform,
        bool isWhatIf, Action<string> log)
    {
        switch (platform)
        {
            case Platform.SqlServer:
            {
                // CREATE SCHEMA must be the only statement in its batch on SQL Server, so the
                // IF-NOT-EXISTS gate uses EXEC() indirection. The escape of literal ']' to ']]'
                // mirrors QuoteIdentifier; the single-quote escape inside the IF condition
                // mirrors EscapeSqlLiteral.
                var quoted = schemaName.Replace("]", "]]");
                var literal = schemaName.Replace("'", "''");
                var ddl = $"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{literal}') " +
                          $"EXEC('CREATE SCHEMA [{quoted}]')";
                if (isWhatIf)
                {
                    log($"  [WhatIf] Would create schema [{schemaName}] (CreateIfMissing: true)");
                    return;
                }
                log($"  Creating schema [{schemaName}] (CreateIfMissing: true)");
                command.CommandText = ddl;
                command.ExecuteNonQuery();
                break;
            }
            case Platform.PostgreSQL:
            {
                var quoted = schemaName.Replace("\"", "\"\"");
                var ddl = $"CREATE SCHEMA IF NOT EXISTS \"{quoted}\"";
                if (isWhatIf)
                {
                    log($"  [WhatIf] Would create schema \"{schemaName}\" (CreateIfMissing: true)");
                    return;
                }
                log($"  Creating schema \"{schemaName}\" (CreateIfMissing: true)");
                command.CommandText = ddl;
                command.ExecuteNonQuery();
                break;
            }
            case Platform.MySQL:
                throw new InvalidOperationException(
                    "SchemaProvisioner.EnsureSchemaExists is not supported on MySQL (no schema/database " +
                    "distinction). Use EnsureDatabaseExists for MySQL database-axis provisioning.");
            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform,
                    $"Unsupported platform: {platform}");
        }
    }
}
