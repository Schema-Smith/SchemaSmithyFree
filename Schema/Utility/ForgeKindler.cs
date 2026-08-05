// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
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

    // SQL Server legacy (XML) encoding kindle-list transforms: below the OPENJSON compat cliff the model is
    // ingested/compared as XML, so each OPENJSON/FOR JSON proc is swapped for its FOR XML PATH / .nodes() twin
    // and fn_FormatJson (used only by the JSON generate path, and itself OPENJSON-based) is dropped. The
    // {{ParseJson}}/{{TableDef}} token bodies also switch to XML — see ResolveKindleScript.
    private static readonly Dictionary<string, string> SqlServerXmlSwaps = new(StringComparer.Ordinal)
    {
        ["SchemaSmith.BootstrapTableQuench.sql"] = "SchemaSmith.BootstrapTableXmlQuench.sql",
        ["SchemaSmith.IndexOnlyQuench.sql"] = "SchemaSmith.IndexOnlyXmlQuench.sql",
        ["SchemaSmith.IndexedViewQuench.sql"] = "SchemaSmith.IndexedViewXmlQuench.sql",
        ["SchemaSmith.GenerateTableJson.sql"] = "SchemaSmith.GenerateTableXml.sql",
        ["SchemaSmith.GenerateIndexedViewJson.sql"] = "SchemaSmith.GenerateIndexedViewXml.sql",
    };

    private static readonly HashSet<string> SqlServerXmlSkips = new(StringComparer.Ordinal)
    {
        "SchemaSmith.fn_FormatJson.sql",
    };

    /// <summary>
    /// Deploy all SchemaSmith helper objects, version-gated and self-skipping. Acquires a
    /// session-scoped lock so concurrent first-arrivals serialize; if the in-DB stamp already
    /// matches the current kindle content (and not forceReKindle), returns without touching the
    /// helpers. Otherwise drops superseded PG overloads (PG only), re-kindles, and re-stamps.
    /// </summary>
    public static void KindleTheForge(IDbCommand command, Platform platform, bool forceReKindle = false,
        IngestEncoding encoding = IngestEncoding.Json, int serverMajorVersion = 0, string policy = "warn")
    {
        AcquireKindleLock(command, platform); // throws ArgumentException for unsupported platforms (before the try)
        try
        {
            var expected = ComputeKindleStamp(platform, encoding, serverMajorVersion, policy);
            var current = ReadStamp(command, platform);
            if (!forceReKindle && string.Equals(current, expected, StringComparison.Ordinal))
            {
                Log.Info($"  Kindle stamp current ({expected[..12]}…) — skipping kindle");
                return;
            }

            if (platform == Platform.PostgreSQL)
                DropSupersededPostgreSqlOverloads(command);

            KindleScripts(command, platform, encoding, serverMajorVersion, policy);
            if (platform.GetBasePlatform() == Platform.MySQL)
                CleanupMySqlStatusMessages(command);

            WriteStamp(command, platform, expected);
        }
        finally
        {
            ReleaseKindleLock(command, platform);
        }
    }

    private static void KindleScripts(IDbCommand command, Platform platform, IngestEncoding encoding,
        int serverMajorVersion, string policy)
    {
        foreach (var s in GetKindlingScripts(platform, encoding))
            KindleOneFile(command, s.FileName, platform, s.ReplaceParseJson, s.ReplaceTableDef, encoding,
                serverMajorVersion, policy);
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
    /// Load a kindling script and apply the kindle-time token substitutions. This is the single
    /// resolution path — KindleOneFile executes the result, ComputeKindleStamp hashes it. Resolved
    /// here: {{ParseJson}} and {{TableDef}} (static resources), plus {{ServerMajorVersion}} and
    /// {{UnsupportedPolicy}} (the C#-detected server version + resolved run policy baked into the two
    /// SQL Server helper functions — see the note below). No iteration/schema-scoped token (e.g.
    /// {{SchemaName}}) is ever substituted in the kindle path, so the stamp is content + server-version
    /// + policy scoped: identical across databases of the same server and policy, differing only when
    /// a kindled script body, the detected server version, or the policy changes.
    /// </summary>
    internal static string ResolveKindleScript(string fileName, Platform platform,
        bool replaceParseJson, bool replaceTableDef, IngestEncoding encoding = IngestEncoding.Json,
        int serverMajorVersion = 0, string policy = "warn")
    {
        var script = ResourceLoader.Load(fileName, platform)
            ?? throw new Exception($"Script '{fileName}' not found for platform '{platform}'.");
        if (replaceParseJson)
            script = script.Replace("{{ParseJson}}", encoding == IngestEncoding.Xml
                ? GetParseTableXmlScript(platform)
                : GetParseTableJsonScript(platform));
        if (replaceTableDef)
        {
            var tableDefJson = GetSiblingTableDefJson(fileName, platform);
            // Legacy encoding: the bootstrap proc takes a single <Table> XML element, so convert the sibling
            // JSON object rather than shipping raw JSON the OPENJSON-free bootstrap can't shred.
            script = script.Replace("{{TableDef}}", encoding == IngestEncoding.Xml
                ? ModelXmlSerializer.ToIngestXmlObject(tableDefJson, "Table")
                : tableDefJson);
        }
        // SQL Server 2008-floor helpers bake the C#-detected server major version and the resolved
        // unsupported-feature policy at kindle time rather than reading SESSION_CONTEXT (2016+, which
        // would fail to CREATE the functions on a genuine pre-2016 binary). The tokens appear only in
        // the two SQL Server helper functions, so these replaces are a no-op on every other script.
        script = script.Replace("{{ServerMajorVersion}}", serverMajorVersion.ToString(CultureInfo.InvariantCulture))
                       .Replace("{{UnsupportedPolicy}}", policy);
        return script;
    }

    /// <summary>
    /// Execute a single SQL script from embedded resources for the specified platform.
    /// Optionally substitutes {{ParseJson}} with the platform's ParseTableJson script body,
    /// and / or {{TableDef}} with the sibling .json resource (same base name as the script).
    /// </summary>
    public static void KindleOneFile(IDbCommand command, string fileName, Platform platform,
        bool replaceParseJsonToken = false, bool replaceTableDefToken = false,
        IngestEncoding encoding = IngestEncoding.Json, int serverMajorVersion = 0, string policy = "warn")
    {
        try
        {
            var script = ResolveKindleScript(fileName, platform, replaceParseJsonToken, replaceTableDefToken,
                encoding, serverMajorVersion, policy);

            if (platform.GetBasePlatform() == Platform.MySQL)
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
            else if (platform.GetBasePlatform() == Platform.SqlServer)
            {
                // Split on GO so a kindled object can use the pre-2016 idempotent create form
                // (IF OBJECT_ID(...) DROP; GO; CREATE …) instead of the 2016 SP1 CREATE OR ALTER. A script
                // with no GO yields a single batch, so this is a no-op for any script that hasn't adopted it.
                foreach (var batch in SqlServerBatchSplitter.Split(script))
                {
                    command.CommandText = batch;
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
    /// Get the XML-ingest twin of the ParseTableJson script (selected below the OPENJSON compat cliff).
    /// </summary>
    public static string GetParseTableXmlScript(Platform platform)
    {
        return ResourceLoader.Load("ParseTableXmlIntoTempTables.sql", platform)
            ?? throw new Exception($"ParseTableXmlIntoTempTables.sql not found for platform '{platform}'.");
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

    internal static KindleScript[] GetKindlingScripts(Platform platform, IngestEncoding encoding = IngestEncoding.Json)
    {
        // Route on base platform so variants (e.g. MariaDb -> MySQL) inherit the base kindling
        // list; each script still resolves through ResourceLoader's per-file variant fallback.
        KindleScript[] scripts = platform.GetBasePlatform() switch
        {
            Platform.SqlServer =>
            [
                new("Kindling_SchemaSmith_Schema.sql"),
                new("SchemaSmith.BootstrapTableQuench.sql"),
                new("Kindling_KindleStamp_Table.sql", ReplaceTableDef: true),
                new("Kindling_CompletedMigrationScripts_Table.sql", ReplaceTableDef: true),
                new("Kindling_ChangeAudit_Table.sql", ReplaceTableDef: true),
                new("SchemaSmith.fn_StripParenWrapping.sql"),
                new("SchemaSmith.fn_StripLeadingSelect.sql"),
                new("SchemaSmith.fn_StripBracketWrapping.sql"),
                new("SchemaSmith.fn_SafeBracketWrap.sql"),
                new("SchemaSmith.fn_SplitList.sql"),
                new("SchemaSmith.fn_ServerMajorVersion.sql"),
                new("SchemaSmith.UnsupportedFeaturePolicy.sql"),
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
                // TRANSITIONAL: tighten the ProductOwnership unique key to enforce one-owner-per-object
                // (runs after the table exists; BootstrapTableQuench can't reconcile a changed index).
                new("Kindling_ProductOwnership_IndexMigration.sql"),
                new("Kindling_CompletedMigrationScripts_Table.sql", ReplaceTableDef: true),
                new("Kindling_ChangeAudit_Table.sql", ReplaceTableDef: true),
                new("SchemaSmith.ExecuteOrDebug.sql"),
                new("SchemaSmith.QuoteColumnList.sql"),
                new("SchemaSmith.QuoteIndexColumnList.sql"),
                new("SchemaSmith.StripParenWrapping.sql"),
                new("SchemaSmith.StripTypeCast.sql"),
                new("SchemaSmith.ServerVersionNum.sql"),
                new("SchemaSmith.UnsupportedFeaturePolicy.sql"),
                new("SchemaSmith.IndexNullsNotDistinct.sql"),
                new("SchemaSmith.ColumnCompression.sql"),
                new("SchemaSmith.StatisticsExpressionColumns.sql"),
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
                // Scalar JSON helpers must kindle BEFORE BootstrapTableQuench: the Kindling_*_Table
                // scripts execute Bootstrap at kindle time, and Bootstrap (like ParseTableJson) calls
                // these helpers to shred its table definition.
                new("SchemaSmith_JsonScalarInt.sql"),
                new("SchemaSmith_JsonScalarStr.sql"),
                new("SchemaSmith_BootstrapTableQuench.sql"),
                new("Kindling_KindleStamp_Table.sql", ReplaceTableDef: true),
                new("Kindling_CompletedMigrationScripts_Table.sql", ReplaceTableDef: true),
                new("Kindling_ProductOwnership_Table.sql", ReplaceTableDef: true),
                new("Kindling_StatusMessages_Table.sql", ReplaceTableDef: true),
                new("Kindling_ChangeAudit_Table.sql", ReplaceTableDef: true),
                new("SchemaSmith_QuoteIdentifier.sql"),
                new("SchemaSmith_StripBacktickWrapping.sql"),
                new("SchemaSmith_SafeBacktickWrap.sql"),
                new("SchemaSmith_StripLeadingSelect.sql"),
                new("SchemaSmith_ServerVersionNum.sql"),
                new("SchemaSmith_NormalizeIndexColumns.sql"),
                new("SchemaSmith_NormalizeCheckExpression.sql"),
                new("SchemaSmith_UpperDataType.sql"),
                new("SchemaSmith_StripIntDisplayWidth.sql"),
                new("SchemaSmith_NormalizeColumnDefault.sql"),
                new("SchemaSmith_IndexIsVisible.sql"),
                new("SchemaSmith_DropCheckClause.sql"),
                new("SchemaSmith_IndexInvisibleClause.sql"),
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

        // SQL Server legacy (XML) encoding: swap the OPENJSON/FOR JSON procs for their XML twins and drop
        // the JSON-only helpers, preserving each descriptor's token-substitution flags.
        if (encoding == IngestEncoding.Xml && platform.GetBasePlatform() == Platform.SqlServer)
            scripts = scripts
                .Where(s => !SqlServerXmlSkips.Contains(s.FileName))
                .Select(s => SqlServerXmlSwaps.TryGetValue(s.FileName, out var xmlFile) ? s with { FileName = xmlFile } : s)
                .ToArray();

        return scripts;
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

        if (platform.GetBasePlatform() == Platform.MySQL)
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

        command.CommandText = platform.GetBasePlatform() switch
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

        var (deleteSql, insertSql) = platform.GetBasePlatform() switch
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
    /// Scoped to (platform, encoding, server major version, policy) — it cannot depend on
    /// iteration/schema/database scope, so the stamp is identical across databases of the same server
    /// and policy. Any change to a kindled script, a token source (e.g. ParseTableJsonIntoTempTables.sql),
    /// the detected server version, or the run policy changes the resolved text and therefore the stamp
    /// -> the next kindle re-runs automatically (alternating version/policy across runs re-kindles; cheap).
    /// </summary>
    public static string ComputeKindleStamp(Platform platform, IngestEncoding encoding = IngestEncoding.Json,
        int serverMajorVersion = 0, string policy = "warn")
    {
        var sb = new StringBuilder();
        // The XML encoding swaps in different script bodies (and drops fn_FormatJson), so the concatenated
        // resolved text — and therefore the stamp — already differs from Json; no extra discriminator needed.
        foreach (var s in GetKindlingScripts(platform, encoding))
            sb.Append(ResolveKindleScript(s.FileName, platform, s.ReplaceParseJson, s.ReplaceTableDef, encoding,
                serverMajorVersion, policy));

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
        switch (platform.GetBasePlatform())
        {
            case Platform.SqlServer:
                command.CommandText =
                    "DECLARE @r INT; " +
                    $"EXEC @r = sp_getapplock @Resource = '{KindleLockResource}', " +
                    "@LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 60000; " +
                    // RAISERROR (not THROW) so the guard CREATEs/runs on the SQL Server 2008 floor — THROW is
                    // 2012+. Severity 16 surfaces as a SqlException to the caller, matching THROW's abort intent.
                    "IF @r < 0 RAISERROR('Could not acquire the SchemaSmith kindle lock.', 16, 1);";
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
            command.CommandText = platform.GetBasePlatform() switch
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
