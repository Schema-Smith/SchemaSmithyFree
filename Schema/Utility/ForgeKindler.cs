// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Deploy all SchemaSmith helper objects needed for quench and extraction operations.
    /// </summary>
    public static void KindleTheForge(IDbCommand command, Platform platform)
    {
        if (platform is not (Platform.SqlServer or Platform.PostgreSQL or Platform.MySQL))
            throw new ArgumentException($"Unsupported platform for kindling: {platform}", nameof(platform));

        KindleScripts(command, platform);
        if (platform == Platform.MySQL)
            CleanupMySqlStatusMessages(command);
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
                new("SchemaSmith.fn_StripBracketWrapping.sql"),
                new("SchemaSmith.fn_SafeBracketWrap.sql"),
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
                new("SchemaSmith.ValidateTableOwnership.sql"),
                new("SchemaSmith.FixupTableOwnership.sql"),
                new("SchemaSmith.FixupIndexOwnership.sql"),
                new("SchemaSmith.MissingTableAndColumnQuench.sql"),
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
                new("SchemaSmith_NormalizeIndexColumns.sql"),
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
}
