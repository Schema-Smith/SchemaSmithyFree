// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Npgsql;
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

    /// <summary>
    /// Emits idempotent CREATE DATABASE DDL on the supplied admin-database command (design §6, #257
    /// slice 4). The caller is responsible for opening <paramref name="adminCommand"/> against the
    /// per-engine admin database (<c>master</c> / <c>postgres</c> / <c>information_schema</c>) via
    /// <see cref="Schema.DataAccess.ConnectionString.RetargetDatabase"/> — this method only emits
    /// the DDL on the command it's given. Under WhatIf the DDL is NOT executed; the equivalent
    /// action renders through <paramref name="log"/> using the engine's existing
    /// <c>[WhatIf] Would &lt;verb&gt;</c> convention so the rest of the WhatIf summary accurately
    /// reflects what a real run would do.
    /// <para>
    /// Per-engine differences: SQL Server's <c>IF NOT EXISTS ... CREATE DATABASE</c> is single-
    /// statement idempotent; MySQL's <c>CREATE DATABASE IF NOT EXISTS</c> is native idempotent;
    /// PostgreSQL has no IF-NOT-EXISTS variant, so the provisioner runs an explicit
    /// <c>pg_database.datname</c> existence check first and only CREATEs when missing. PG's
    /// <c>CREATE DATABASE</c> cannot run inside a transaction block, but the engine's default
    /// autocommit-per-statement behavior on the supplied <see cref="IDbCommand"/> handles that
    /// — the provisioner does not open a transaction.
    /// </para>
    /// </summary>
    /// <param name="adminCommand">Open command whose connection targets the engine's admin database.
    /// Caller responsibility to compose via <see cref="Schema.DataAccess.ConnectionString.RetargetDatabase"/>.</param>
    /// <param name="databaseName">Database to ensure exists. Caller is responsible for upstream
    /// identifier validation; embedded brackets / quotes / backticks are escaped here as
    /// belt-and-suspenders.</param>
    /// <param name="platform">Target engine.</param>
    /// <param name="isWhatIf">When true, the DDL renders through <paramref name="log"/> and
    /// is NOT executed.</param>
    /// <param name="log">Info-level log surface for the per-create / WhatIf rendering line.</param>
    /// <exception cref="TemplateTargetProvisioningException">Wraps any engine-level failure
    /// (most commonly a CREATE DATABASE permission denial) with an actionable diagnostic
    /// naming the target DB + the required privilege. The original exception is preserved as
    /// <see cref="Exception.InnerException"/>.</exception>
    public virtual void EnsureDatabaseExists(IDbCommand adminCommand, string databaseName, Platform platform,
        bool isWhatIf, Action<string> log)
    {
        if (isWhatIf)
        {
            // Symmetric to EnsureSchemaExists — log the rendering using per-engine quoting style so
            // the WhatIf output mirrors what a real run would emit.
            var rendered = platform switch
            {
                Platform.SqlServer => $"[{databaseName}]",
                Platform.PostgreSQL => $"\"{databaseName}\"",
                Platform.MySQL => $"`{databaseName}`",
                _ => databaseName
            };
            log($"  [WhatIf] Would create database {rendered} (CreateIfMissing: true)");
            return;
        }

        try
        {
            switch (platform)
            {
                case Platform.SqlServer:
                {
                    var quoted = databaseName.Replace("]", "]]");
                    var literal = databaseName.Replace("'", "''");
                    log($"  Creating database [{databaseName}] (CreateIfMissing: true)");
                    adminCommand.Parameters.Clear();
                    adminCommand.CommandText =
                        $"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '{literal}') " +
                        $"CREATE DATABASE [{quoted}]";
                    adminCommand.ExecuteNonQuery();
                    break;
                }
                case Platform.PostgreSQL:
                {
                    // PG has no `CREATE DATABASE IF NOT EXISTS`. Existence check first, then create
                    // only when missing. Both run on the admin command (targets the `postgres` DB).
                    var quoted = databaseName.Replace("\"", "\"\"");
                    var literal = databaseName.Replace("'", "''");
                    adminCommand.Parameters.Clear();
                    adminCommand.CommandText =
                        $"SELECT COUNT(*) FROM pg_database WHERE datname = '{literal}'";
                    var existsCount = Convert.ToInt64(adminCommand.ExecuteScalar());
                    if (existsCount > 0) return;

                    log($"  Creating database \"{databaseName}\" (CreateIfMissing: true)");
                    adminCommand.Parameters.Clear();
                    adminCommand.CommandText = $"CREATE DATABASE \"{quoted}\"";
                    try
                    {
                        adminCommand.ExecuteNonQuery();
                    }
                    catch (PostgresException ex) when (ex.SqlState == "42P04")
                    {
                        // Race-loser path: another concurrent quench created the database between
                        // our pg_database existence check and our CREATE DATABASE. PG raises
                        // SqlState 42P04 ("database already exists"). The idempotency contract is
                        // preserved — the database now exists; treat as success rather than letting
                        // the outer catch re-wrap as a misleading "CREATE DATABASE permission" error.
                        return;
                    }
                    break;
                }
                case Platform.MySQL:
                {
                    var quoted = databaseName.Replace("`", "``");
                    log($"  Creating database `{databaseName}` (CreateIfMissing: true)");
                    adminCommand.Parameters.Clear();
                    adminCommand.CommandText = $"CREATE DATABASE IF NOT EXISTS `{quoted}`";
                    adminCommand.ExecuteNonQuery();
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform,
                        $"Unsupported platform: {platform}");
            }
        }
        catch (Exception ex) when (ex is not TemplateTargetProvisioningException
                                    && ex is not ArgumentOutOfRangeException)
        {
            // CREATE DATABASE is a high-privilege operation; permission denial is the most likely
            // failure mode. Wrap the engine exception in a TemplateTargetProvisioningException
            // carrying an actionable diagnostic naming the target DB + the required privilege.
            // The original exception is preserved as InnerException so the underlying engine
            // error (SqlException error 262, PostgresException SqlState 42501, MySqlException
            // code 1044 / 1142) stays attached for the caller to inspect or log.
            var adminDb = platform switch
            {
                Platform.SqlServer => "master",
                Platform.PostgreSQL => "postgres",
                Platform.MySQL => "information_schema",
                _ => "<admin>"
            };
            throw new TemplateTargetProvisioningException(
                $"Failed to provision database '{databaseName}'. The connecting account must have " +
                $"CREATE DATABASE permission on the admin database ({adminDb}). Underlying error: {ex.Message}",
                ex);
        }
    }
}
