// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using log4net;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.Utility;

/// <summary>
/// Deploys SchemaSmith helper procedures, functions, and tables to the target database.
/// This "kindles the forge" - preparing the database for schema extraction and deployment.
/// Platform-aware: uses a per-platform descriptor list (GetKindlingScripts) to drive both execution and version-stamping.
/// </summary>
public static class ForgeKindler
{
    private static readonly ILog Log = LogFactory.GetLogger("ProgressLog");
    private const string KindleLockResource = "SchemaSmith_Kindle";

    /// <summary>
    /// Deploy all SchemaSmith helper objects, version-gated and self-skipping. Acquires a
    /// session-scoped lock so concurrent first-arrivals serialize; if the in-DB stamp already
    /// matches the current kindle content (and not forceReKindle), returns without touching the
    /// helpers. Otherwise drops superseded PG overloads (PG only), re-kindles, and re-stamps.
    /// </summary>
    public static void KindleTheForge(IDbCommand command, Platform platform, bool forceReKindle = false)
    {
        AcquireKindleLock(command, platform); // throws ArgumentException for unsupported platforms (before the try)
        try
        {
            var expected = ComputeKindleStamp(platform);
            var current = ReadStamp(command, platform);
            if (!forceReKindle && string.Equals(current, expected, StringComparison.Ordinal))
            {
                Log.Info($"  Kindle stamp current ({expected[..12]}…) — skipping kindle");
                return;
            }

            if (platform == Platform.PostgreSQL)
                DropSupersededPostgreSqlOverloads(command);

            KindleScripts(command, platform);
            if (platform == Platform.MySQL)
                CleanupMySqlStatusMessages(command);

            WriteStamp(command, platform, expected);
        }
        finally
        {
            ReleaseKindleLock(command, platform);
        }
    }

    private static void KindleScripts(IDbCommand command, Platform platform)
    {
        foreach (var s in GetKindlingScripts(platform))
            KindleOneFile(command, s.FileName, platform, s.ReplaceParseJson, s.ReplaceTableDef);
    }

    // MySQL only: clear orphaned status rows from crashed sessions. Operational, not schema
    // content — runs after the table set is created and is excluded from the version stamp.
    private static void CleanupMySqlStatusMessages(IDbCommand command)
    {
        try
        {
            command.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE CreatedAt < DATE_SUB(NOW(3), INTERVAL 1 HOUR)";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignore if table doesn't exist yet
        }
    }

    /// <summary>
    /// Load a kindling script and apply the two static-resource token substitutions. This is the
    /// single resolution path — KindleOneFile executes the result, ComputeKindleStamp hashes it.
    /// IMPORTANT: only {{ParseJson}} and {{TableDef}} (both static resources) are resolved here.
    /// No runtime/iteration-scoped token (e.g. {{SchemaName}}) is ever substituted in the kindle
    /// path, which is why the stamp is content-only and identical across databases/schemas.
    /// </summary>
    internal static string ResolveKindleScript(string fileName, Platform platform,
        bool replaceParseJson, bool replaceTableDef)
    {
        var script = ResourceLoader.Load(fileName, platform)
            ?? throw new Exception($"Script '{fileName}' not found for platform '{platform}'.");
        if (replaceParseJson)
            script = script.Replace("{{ParseJson}}", GetParseTableJsonScript(platform));
        if (replaceTableDef)
            script = script.Replace("{{TableDef}}", GetSiblingTableDefJson(fileName, platform));
        return script;
    }

    /// <summary>
    /// Execute a single SQL script from embedded resources for the specified platform.
    /// Optionally substitutes {{ParseJson}} with the platform's ParseTableJson script body,
    /// and / or {{TableDef}} with the sibling .json resource (same base name as the script).
    /// </summary>
    public static void KindleOneFile(IDbCommand command, string fileName, Platform platform,
        bool replaceParseJsonToken = false, bool replaceTableDefToken = false)
    {
        try
        {
            var script = ResolveKindleScript(fileName, platform, replaceParseJsonToken, replaceTableDefToken);

            if (platform == Platform.MySQL)
            {
                foreach (var batch in BatchSplitter.Split(script))
                {
                    command.CommandText = batch;
                    command.ExecuteNonQuery();
                }
            }
            else if (platform == Platform.PostgreSQL)
            {
                foreach (var statement in PostgreSqlStatementSplitter.Split(script))
                {
                    command.CommandText = statement;
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                command.CommandText = script;
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Error occurred while kindling '{fileName}'. {e.Message}", e);
        }
    }

    /// <summary>
    /// Get the ParseTableJson script for the specified platform.
    /// </summary>
    public static string GetParseTableJsonScript(Platform platform)
    {
        return ResourceLoader.Load("ParseTableJsonIntoTempTables.sql", platform)
            ?? throw new Exception($"ParseTableJsonIntoTempTables.sql not found for platform '{platform}'.");
    }

    /// <summary>
    /// Load the sibling .json resource for a kindling _Table.sql script. Convention: the JSON
    /// file shares the script's base name. For "Kindling_CompletedMigrationScripts_Table.sql"
    /// the resolver strips the "_Table.sql" suffix and loads "Kindling_CompletedMigrationScripts.json".
    /// </summary>
    internal static string GetSiblingTableDefJson(string scriptFileName, Platform platform)
    {
        var baseName = Path.GetFileNameWithoutExtension(scriptFileName);
        const string tableSuffix = "_Table";
        if (baseName.EndsWith(tableSuffix, StringComparison.Ordinal))
            baseName = baseName.Substring(0, baseName.Length - tableSuffix.Length);
        var jsonName = baseName + ".json";

        return ResourceLoader.Load(jsonName, platform)
            ?? throw new Exception($"Sibling JSON '{jsonName}' not found for kindling script '{scriptFileName}' on platform '{platform}'.");
    }

    /// <summary>
    /// One kindling script plus the token-substitution flags it needs. Single source of truth
    /// for the kindle order — both the executor (KindleScripts) and the version-stamp
    /// (ComputeKindleStamp) iterate this list, so the deployed text and the hashed text can
    /// never drift.
    /// </summary>
    internal readonly record struct KindleScript(string FileName, bool ReplaceParseJson = false, bool ReplaceTableDef = false);

    internal static KindleScript[] GetKindlingScripts(Platform platform)
    {
        return platform switch
        {
            Platform.SqlServer =>
            [
                new("Kindling_SchemaSmith_Schema.sql"),
                new("SchemaSmith.BootstrapTableQuench.sql"),
                new("Kindling_KindleStamp_Table.sql", ReplaceTableDef: true),
                new("Kindling_CompletedMigrationScripts_Table.sql", ReplaceTableDef: true),
                new("SchemaSmith.fn_StripParenWrapping.sql"),
                new("SchemaSmith.fn_StripLeadingSelect.sql"),
                new("SchemaSmith.fn_StripBracketWrapping.sql"),
                new("SchemaSmith.fn_SafeBracketWrap.sql"),
                new("SchemaSmith.fn_ServerMajorVersion.sql"),
                new("SchemaSmith.PrintWithNoWait.sql"),
                new("SchemaSmith.MissingTableAndColumnQuench.sql"),
                new("SchemaSmith.ModifiedTableQuench.sql"),
                new("SchemaSmith.MissingIndexesAndConstraintsQuench.sql"),
                new("SchemaSmith.ForeignKeyQuench.sql"),
                new("SchemaSmith.TableQuench.sql", ReplaceParseJson: true),
                new("SchemaSmith.IndexOnlyQuench.sql"),
                new("SchemaSmith.fn_FormatJson.sql"),
                new("SchemaSmith.GenerateTableJson.sql"),
                new("SchemaSmith.ValidateIndexedViewOwnership.sql"),
                new("SchemaSmith.FixupIndexedViewOwnership.sql"),
                new("SchemaSmith.IndexedViewQuench.sql"),
                new("SchemaSmith.GenerateIndexedViewJson.sql"),
            ],
            Platform.PostgreSQL =>
            [
                new("Kindling_SchemaSmith_Schema.sql"),
                new("SchemaSmith.BootstrapTableQuench.sql"),
                new("Kindling_KindleStamp_Table.sql", ReplaceTableDef: true),
                new("Kindling_ProductOwnership_Table.sql", ReplaceTableDef: true),
                new("Kindling_CompletedMigrationScripts_Table.sql", ReplaceTableDef: true),
                new("SchemaSmith.ExecuteOrDebug.sql"),
                new("SchemaSmith.QuoteColumnList.sql"),
                new("SchemaSmith.QuoteIndexColumnList.sql"),
                new("SchemaSmith.StripParenWrapping.sql"),
                new("SchemaSmith.StripTypeCast.sql"),
                new("SchemaSmith.ServerVersionNum.sql"),
                new("SchemaSmith.StripLeadingSelect.sql"),
                new("SchemaSmith.ValidateTableOwnership.sql"),
                new("SchemaSmith.FixupTableOwnership.sql"),
                new("SchemaSmith.FixupIndexOwnership.sql"),
                new("SchemaSmith.MissingTableAndColumnQuench.sql"),
                new("SchemaSmith.BuildExistingIndexesSnapshot.sql"),
                new("SchemaSmith.ModifiedTableQuench.sql"),
                new("SchemaSmith.MissingIndexesAndConstraintsQuench.sql"),
                new("SchemaSmith.ForeignKeyQuench.sql"),
                new("SchemaSmith.TableQuench.sql", ReplaceParseJson: true),
                new("SchemaSmith.IndexOnlyQuench.sql"),
                new("SchemaSmith.FormatJson.sql"),
                new("SchemaSmith.GenerateTableJson.sql"),
                new("SchemaSmith.ValidateMaterializedViewOwnership.sql"),
                new("SchemaSmith.FixupMaterializedViewOwnership.sql"),
                new("SchemaSmith.MissingMaterializedViewIndexesQuench.sql"),
                new("SchemaSmith.MaterializedViewQuench.sql"),
            ],
            Platform.MySQL =>
            [
                new("SchemaSmith_BootstrapTableQuench.sql"),
                new("Kindling_KindleStamp_Table.sql", ReplaceTableDef: true),
                new("Kindling_CompletedMigrationScripts_Table.sql", ReplaceTableDef: true),
                new("Kindling_ProductOwnership_Table.sql", ReplaceTableDef: true),
                new("Kindling_StatusMessages_Table.sql", ReplaceTableDef: true),
                new("SchemaSmith_QuoteIdentifier.sql"),
                new("SchemaSmith_StripBacktickWrapping.sql"),
                new("SchemaSmith_SafeBacktickWrap.sql"),
                new("SchemaSmith_StripLeadingSelect.sql"),
                new("SchemaSmith_ServerVersionNum.sql"),
                new("SchemaSmith_NormalizeIndexColumns.sql"),
                new("SchemaSmith_NormalizeCheckExpression.sql"),
                new("SchemaSmith_UpperDataType.sql"),
                new("SchemaSmith_GenerateTableJson.sql"),
                new("SchemaSmith_ParseTableJson.sql"),
                new("SchemaSmith_MissingTableAndColumnQuench.sql"),
                new("SchemaSmith_ModifiedTableQuench.sql"),
                new("SchemaSmith_MissingIndexesAndConstraintsQuench.sql"),
                new("SchemaSmith_ForeignKeyQuench.sql"),
                new("SchemaSmith_IndexOnlyQuench.sql"),
                new("SchemaSmith_TableQuench.sql"),
            ],
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }

    /// <summary>
    /// Returns the list of kindling script names for the specified platform.
    /// Useful for testing and diagnostics.
    /// </summary>
    internal static string[] GetKindlingScriptNames(Platform platform)
        => GetKindlingScripts(platform).Select(s => s.FileName).ToArray();

    /// <summary>
    /// Read the current kindle stamp, or null if the marker table doesn't exist yet (fresh install)
    /// or holds no row. Uses a guard so a missing table returns null rather than raising an error.
    ///
    /// PostgreSQL note: the original CASE WHEN to_regclass(...) ELSE (SELECT ... FROM KindleStamp) END
    /// approach fails at PARSE TIME on a fresh database — PG validates all table references in the
    /// query text regardless of which CASE branch will execute. We avoid the static table reference
    /// by querying pg_class/pg_namespace instead, and only issuing the second SELECT when the table
    /// is confirmed to exist.
    /// </summary>
    internal static string ReadStamp(IDbCommand command, Platform platform)
    {
        if (platform == Platform.PostgreSQL)
        {
            // First: check existence via catalog (no static reference to KindleStamp that PG would
            // validate at parse time). Returns 1 if the table exists, 0 otherwise.
            command.CommandText =
                "SELECT COUNT(*) FROM pg_catalog.pg_class c " +
                "JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE n.nspname = 'SchemaSmith' AND c.relname = 'KindleStamp' AND c.relkind = 'r'";
            var existsScalar = command.ExecuteScalar();
            var exists = existsScalar != null && existsScalar != DBNull.Value && Convert.ToInt64(existsScalar) > 0;
            if (!exists) return null;

            // Table confirmed present — safe to reference it directly.
            command.CommandText = "SELECT \"Stamp\" FROM \"SchemaSmith\".\"KindleStamp\" LIMIT 1";
            var pgValue = command.ExecuteScalar();
            return pgValue == null || pgValue == DBNull.Value ? null : pgValue.ToString();
        }

        if (platform == Platform.MySQL)
        {
            // MySQL validates all table references at parse time — even inside a subquery behind a
            // WHERE EXISTS guard — so a single-statement existence-gated read raises 1146 on a fresh
            // install before SchemaSmith_KindleStamp has been created. Use the same two-query pattern
            // as the PostgreSQL path: confirm existence via information_schema first, then read.
            command.CommandText =
                "SELECT COUNT(*) FROM information_schema.tables " +
                "WHERE table_schema = DATABASE() AND table_name = 'SchemaSmith_KindleStamp'";
            var mysqlExistsScalar = command.ExecuteScalar();
            var mysqlExists = mysqlExistsScalar != null && mysqlExistsScalar != DBNull.Value
                              && Convert.ToInt64(mysqlExistsScalar) > 0;
            if (!mysqlExists) return null;

            command.CommandText = "SELECT Stamp FROM SchemaSmith_KindleStamp LIMIT 1";
            var mysqlValue = command.ExecuteScalar();
            return mysqlValue == null || mysqlValue == DBNull.Value ? null : mysqlValue.ToString();
        }

        command.CommandText = platform switch
        {
            Platform.SqlServer =>
                "IF OBJECT_ID('[SchemaSmith].[KindleStamp]', 'U') IS NULL SELECT CAST(NULL AS VARCHAR(64)) " +
                "ELSE SELECT TOP 1 [Stamp] FROM [SchemaSmith].[KindleStamp]",
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
        var value = command.ExecuteScalar();
        return value == null || value == DBNull.Value ? null : value.ToString();
    }

    /// <summary>
    /// Replace the single stamp row with the supplied value. Caller holds the kindle lock, so a
    /// plain DELETE + INSERT is safe (single writer). The stamp is [0-9a-f]{64} — injection-safe to inline.
    /// </summary>
    internal static void WriteStamp(IDbCommand command, Platform platform, string stamp)
    {
        if (string.IsNullOrEmpty(stamp))
            throw new ArgumentException("Stamp must be a non-empty SHA-256 hex string.", nameof(stamp));

        var (deleteSql, insertSql) = platform switch
        {
            Platform.SqlServer => (
                "DELETE FROM [SchemaSmith].[KindleStamp]",
                $"INSERT INTO [SchemaSmith].[KindleStamp] ([Stamp], [UpdatedUtc]) VALUES ('{stamp}', GETUTCDATE())"),
            Platform.PostgreSQL => (
                "DELETE FROM \"SchemaSmith\".\"KindleStamp\"",
                $"INSERT INTO \"SchemaSmith\".\"KindleStamp\" (\"Stamp\", \"UpdatedUtc\") VALUES ('{stamp}', NOW())"),
            Platform.MySQL => (
                "DELETE FROM SchemaSmith_KindleStamp",
                $"INSERT INTO SchemaSmith_KindleStamp (Stamp, UpdatedUtc) VALUES ('{stamp}', NOW())"),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
        command.CommandText = deleteSql;
        command.ExecuteNonQuery();
        command.CommandText = insertSql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// SHA-256 (lowercase hex) of the resolved kindling scripts concatenated in kindle order.
    /// Takes ONLY platform — by construction it cannot depend on iteration/schema/database scope,
    /// so the stamp is content-only and identical on every target for a given kindle-content version.
    /// Any change to a kindled script OR a token source (e.g. ParseTableJsonIntoTempTables.sql)
    /// changes the resolved text and therefore the stamp -> the next kindle re-runs automatically.
    /// </summary>
    public static string ComputeKindleStamp(Platform platform)
    {
        var sb = new StringBuilder();
        foreach (var s in GetKindlingScripts(platform))
            sb.Append(ResolveKindleScript(s.FileName, platform, s.ReplaceParseJson, s.ReplaceTableDef));

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Acquire a SESSION-scoped lock guarding the whole (multi-statement, autocommit) kindle.
    /// Session — not transaction — scope is required: the kindle is many autocommit statements and
    /// MySQL DDL forces an implicit commit, so a transaction-scoped lock would release mid-kindle.
    /// Key folds in the database name where the lock namespace is broader than one DB (PG advisory
    /// locks are cluster-global; MySQL GET_LOCK names are server-global). Released in KindleTheForge's
    /// finally on the same connection.
    /// </summary>
    internal static void AcquireKindleLock(IDbCommand command, Platform platform)
    {
        switch (platform)
        {
            case Platform.SqlServer:
                command.CommandText =
                    "DECLARE @r INT; " +
                    $"EXEC @r = sp_getapplock @Resource = '{KindleLockResource}', " +
                    "@LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 60000; " +
                    "IF @r < 0 THROW 51000, 'Could not acquire the SchemaSmith kindle lock.', 1;";
                command.ExecuteNonQuery();
                break;
            case Platform.PostgreSQL:
                // PG advisory locks have no native wait-cap argument (unlike SQL Server's
                // @LockTimeout=60000 and MySQL's GET_LOCK(...,60)). A wedged holder will block
                // this acquire indefinitely. Worth a roadmap follow-up to add a try-loop with
                // backoff; for now the kindle is fast and contention is rare, so the asymmetry is
                // acceptable. Documented so a future reader doesn't think it's an oversight.
                command.CommandText =
                    $"SELECT pg_advisory_lock(hashtext(current_database() || ':{KindleLockResource}'))";
                command.ExecuteNonQuery();
                break;
            case Platform.MySQL:
                command.CommandText = $"SELECT GET_LOCK('{GetMySqlKindleLockName(command)}', 60)";
                var got = command.ExecuteScalar();
                if (got == null || got == DBNull.Value || Convert.ToInt64(got) != 1)
                    throw new TimeoutException("Could not acquire the SchemaSmith kindle lock (MySQL GET_LOCK timed out).");
                break;
            default:
                throw new ArgumentException($"Unsupported platform for kindling: {platform}", nameof(platform));
        }
    }

    /// <summary>
    /// Drop the prior signatures of the five PostgreSQL ownership procs that gained
    /// p_TemplateName/p_SchemaName params in schema-templates. PostgreSQL overloads by signature,
    /// so CREATE OR REPLACE of the new signature would otherwise ADD a second overload on an
    /// upgraded database and make every call ambiguous (42725). IF EXISTS makes this a no-op on
    /// fresh installs. Runs once per stamp change, under the kindle lock — no "doesn't exist" window.
    /// </summary>
    internal static void DropSupersededPostgreSqlOverloads(IDbCommand command)
    {
        string[] drops =
        [
            "DROP PROCEDURE IF EXISTS \"SchemaSmith\".\"ValidateTableOwnership\"(varchar, boolean)",
            "DROP PROCEDURE IF EXISTS \"SchemaSmith\".\"FixupTableOwnership\"(varchar)",
            "DROP PROCEDURE IF EXISTS \"SchemaSmith\".\"ValidateMaterializedViewOwnership\"(varchar, boolean)",
            "DROP PROCEDURE IF EXISTS \"SchemaSmith\".\"FixupMaterializedViewOwnership\"(varchar)",
            "DROP PROCEDURE IF EXISTS \"SchemaSmith\".\"FixupIndexOwnership\"(varchar)",
        ];
        foreach (var sql in drops)
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Release the session-scoped kindle lock. Best-effort; failures here must not mask a kindle error.</summary>
    internal static void ReleaseKindleLock(IDbCommand command, Platform platform)
    {
        try
        {
            command.CommandText = platform switch
            {
                Platform.SqlServer => $"IF APPLOCK_MODE('public', '{KindleLockResource}', 'Session') <> 'NoLock' " +
                                      $"EXEC sp_releaseapplock @Resource = '{KindleLockResource}', @LockOwner = 'Session';",
                Platform.PostgreSQL => $"SELECT pg_advisory_unlock(hashtext(current_database() || ':{KindleLockResource}'))",
                Platform.MySQL => $"SELECT RELEASE_LOCK('{GetMySqlKindleLockName(command)}')",
                _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
            };
            command.ExecuteNonQuery();
        }
        catch
        {
            // The session ending releases all three lock types anyway; never let release mask a kindle failure.
        }
    }

    /// <summary>
    /// MySQL GET_LOCK names are server-global AND capped at 64 characters. Hash the current
    /// database name into a fixed-length key so long DB names (test DBs with timestamp+guid
    /// suffixes, or any user with a verbose naming convention) still fit under the cap and stay
    /// unique per database. Exposed internally so integration tests can probe the same key.
    /// </summary>
    internal static string GetMySqlKindleLockName(IDbCommand command)
    {
        var dbName = command.Connection?.Database ?? "";
        var dbHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dbName)))[..16].ToLowerInvariant();
        return $"{KindleLockResource}_{dbHash}"; // 19 + 1 + 16 = 36 chars, well under MySQL's 64
    }
}
