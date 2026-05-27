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
/// Platform-aware: dispatches to the correct kindling scripts based on Platform.
/// </summary>
public static class ForgeKindler
{
    private static readonly ILog Log = LogFactory.GetLogger("ProgressLog");

    /// <summary>
    /// Deploy all SchemaSmith helper objects needed for quench and extraction operations.
    /// </summary>
    public static void KindleTheForge(IDbCommand command, Platform platform)
    {
        switch (platform)
        {
            case Platform.SqlServer:
                KindleForSqlServer(command);
                break;
            case Platform.PostgreSQL:
                KindleForPostgreSQL(command);
                break;
            case Platform.MySQL:
                KindleForMySQL(command);
                break;
            default:
                throw new ArgumentException($"Unsupported platform for kindling: {platform}", nameof(platform));
        }
    }

    private static void KindleForSqlServer(IDbCommand command)
    {
        KindleOneFile(command, "Kindling_SchemaSmith_Schema.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.BootstrapTableQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "Kindling_CompletedMigrationScripts_Table.sql", Platform.SqlServer, replaceTableDefToken: true);
        KindleOneFile(command, "SchemaSmith.fn_StripParenWrapping.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.fn_StripBracketWrapping.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.fn_SafeBracketWrap.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.PrintWithNoWait.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.MissingTableAndColumnQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.ModifiedTableQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.MissingIndexesAndConstraintsQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.ForeignKeyQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.TableQuench.sql", Platform.SqlServer, replaceParseJsonToken: true);
        KindleOneFile(command, "SchemaSmith.IndexOnlyQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.fn_FormatJson.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.GenerateTableJson.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.ValidateIndexedViewOwnership.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.FixupIndexedViewOwnership.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.IndexedViewQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.GenerateIndexedViewJson.sql", Platform.SqlServer);
    }

    private static void KindleForPostgreSQL(IDbCommand command)
    {
        KindleOneFile(command, "Kindling_SchemaSmith_Schema.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.BootstrapTableQuench.sql", Platform.PostgreSQL);
        KindleOneFile(command, "Kindling_ProductOwnership_Table.sql", Platform.PostgreSQL, replaceTableDefToken: true);
        KindleOneFile(command, "Kindling_CompletedMigrationScripts_Table.sql", Platform.PostgreSQL, replaceTableDefToken: true);
        KindleOneFile(command, "SchemaSmith.ExecuteOrDebug.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.QuoteColumnList.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.QuoteIndexColumnList.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.StripParenWrapping.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.ValidateTableOwnership.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.FixupTableOwnership.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.FixupIndexOwnership.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.MissingTableAndColumnQuench.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.ModifiedTableQuench.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.MissingIndexesAndConstraintsQuench.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.ForeignKeyQuench.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.TableQuench.sql", Platform.PostgreSQL, replaceParseJsonToken: true);
        KindleOneFile(command, "SchemaSmith.IndexOnlyQuench.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.FormatJson.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.GenerateTableJson.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.ValidateMaterializedViewOwnership.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.FixupMaterializedViewOwnership.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.MissingMaterializedViewIndexesQuench.sql", Platform.PostgreSQL);
        KindleOneFile(command, "SchemaSmith.MaterializedViewQuench.sql", Platform.PostgreSQL);
    }

    private static void KindleForMySQL(IDbCommand command)
    {
        KindleOneFile(command, "SchemaSmith_BootstrapTableQuench.sql", Platform.MySQL);
        KindleOneFile(command, "Kindling_CompletedMigrationScripts_Table.sql", Platform.MySQL, replaceTableDefToken: true);
        KindleOneFile(command, "Kindling_ProductOwnership_Table.sql", Platform.MySQL, replaceTableDefToken: true);
        KindleOneFile(command, "Kindling_StatusMessages_Table.sql", Platform.MySQL, replaceTableDefToken: true);

        // Clean up orphaned status messages older than 1 hour (from crashed sessions)
        try
        {
            command.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE CreatedAt < DATE_SUB(NOW(3), INTERVAL 1 HOUR)";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignore if table doesn't exist yet
        }

        KindleOneFile(command, "SchemaSmith_QuoteIdentifier.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_StripBacktickWrapping.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_SafeBacktickWrap.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_NormalizeIndexColumns.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_GenerateTableJson.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_ParseTableJson.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_MissingTableAndColumnQuench.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_ModifiedTableQuench.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_MissingIndexesAndConstraintsQuench.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_ForeignKeyQuench.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_IndexOnlyQuench.sql", Platform.MySQL);
        KindleOneFile(command, "SchemaSmith_TableQuench.sql", Platform.MySQL);
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
            var script = ResourceLoader.Load(fileName, platform);
            if (script == null)
                throw new Exception($"Script '{fileName}' not found for platform '{platform}'.");

            if (replaceParseJsonToken)
                script = script.Replace("{{ParseJson}}", GetParseTableJsonScript(platform));

            if (replaceTableDefToken)
                script = script.Replace("{{TableDef}}", GetSiblingTableDefJson(fileName, platform));

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
