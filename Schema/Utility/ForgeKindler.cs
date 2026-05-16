// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
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
        KindleOneFile(command, "SchemaSmith.fn_StripParenWrapping.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.fn_StripBracketWrapping.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.fn_SafeBracketWrap.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.PrintWithNoWait.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.MissingTableAndColumnQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.ModifiedTableQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.MissingIndexesAndConstraintsQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.ForeignKeyQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "SchemaSmith.TableQuench.sql", Platform.SqlServer, true);
        KindleOneFile(command, "SchemaSmith.IndexOnlyQuench.sql", Platform.SqlServer);
        KindleOneFile(command, "Kindling_CompletedMigrationScripts_Table.sql", Platform.SqlServer);
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
        KindleOneFile(command, "SchemaSmith.TableQuench.sql", Platform.PostgreSQL, true);
        KindleOneFile(command, "Kindling_ProductOwnership_Table.sql", Platform.PostgreSQL);
        KindleOneFile(command, "Kindling_CompletedMigrationScripts_Table.sql", Platform.PostgreSQL);
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
        KindleOneFile(command, "Kindling_CompletedMigrationScripts_Table.sql", Platform.MySQL);
        // Idempotent upgrade path for legacy tables created before the slice-2
        // schema-templates extension. MySQL's CREATE TABLE IF NOT EXISTS won't add
        // missing columns to existing tables, so we do explicit ALTERs guarded by
        // information_schema checks. No-op on tables already at the new shape.
        KindleOneFile(command, "Kindling_AlterCompletedMigrationScripts.sql", Platform.MySQL);
        KindleOneFile(command, "Kindling_ProductOwnership_Table.sql", Platform.MySQL);
        KindleOneFile(command, "Kindling_StatusMessages_Table.sql", Platform.MySQL);

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
    /// </summary>
    public static void KindleOneFile(IDbCommand command, string fileName, Platform platform, bool replaceParseJsonToken = false)
    {
        try
        {
            var script = ResourceLoader.Load(fileName, platform);
            if (script == null)
                throw new Exception($"Script '{fileName}' not found for platform '{platform}'.");

            if (replaceParseJsonToken)
                script = script.Replace("{{ParseJson}}", GetParseTableJsonScript(platform));

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
    /// Returns the list of kindling script names for the specified platform.
    /// Useful for testing and diagnostics.
    /// </summary>
    internal static string[] GetKindlingScriptNames(Platform platform)
    {
        return platform switch
        {
            Platform.SqlServer => [
                "Kindling_SchemaSmith_Schema.sql",
                "SchemaSmith.fn_StripParenWrapping.sql",
                "SchemaSmith.fn_StripBracketWrapping.sql",
                "SchemaSmith.fn_SafeBracketWrap.sql",
                "SchemaSmith.PrintWithNoWait.sql",
                "SchemaSmith.MissingTableAndColumnQuench.sql",
                "SchemaSmith.ModifiedTableQuench.sql",
                "SchemaSmith.MissingIndexesAndConstraintsQuench.sql",
                "SchemaSmith.ForeignKeyQuench.sql",
                "SchemaSmith.TableQuench.sql",
                "SchemaSmith.IndexOnlyQuench.sql",
                "Kindling_CompletedMigrationScripts_Table.sql",
                "SchemaSmith.fn_FormatJson.sql",
                "SchemaSmith.GenerateTableJson.sql",
                "SchemaSmith.ValidateIndexedViewOwnership.sql",
                "SchemaSmith.FixupIndexedViewOwnership.sql",
                "SchemaSmith.IndexedViewQuench.sql",
                "SchemaSmith.GenerateIndexedViewJson.sql"
            ],
            Platform.PostgreSQL => [
                "Kindling_SchemaSmith_Schema.sql",
                "SchemaSmith.ExecuteOrDebug.sql",
                "SchemaSmith.QuoteColumnList.sql",
                "SchemaSmith.QuoteIndexColumnList.sql",
                "SchemaSmith.StripParenWrapping.sql",
                "SchemaSmith.ValidateTableOwnership.sql",
                "SchemaSmith.FixupTableOwnership.sql",
                "SchemaSmith.FixupIndexOwnership.sql",
                "SchemaSmith.MissingTableAndColumnQuench.sql",
                "SchemaSmith.ModifiedTableQuench.sql",
                "SchemaSmith.MissingIndexesAndConstraintsQuench.sql",
                "SchemaSmith.ForeignKeyQuench.sql",
                "SchemaSmith.TableQuench.sql",
                "Kindling_ProductOwnership_Table.sql",
                "Kindling_CompletedMigrationScripts_Table.sql",
                "SchemaSmith.IndexOnlyQuench.sql",
                "SchemaSmith.FormatJson.sql",
                "SchemaSmith.GenerateTableJson.sql",
                "SchemaSmith.ValidateMaterializedViewOwnership.sql",
                "SchemaSmith.FixupMaterializedViewOwnership.sql",
                "SchemaSmith.MissingMaterializedViewIndexesQuench.sql",
                "SchemaSmith.MaterializedViewQuench.sql"
            ],
            Platform.MySQL => [
                "Kindling_CompletedMigrationScripts_Table.sql",
                "Kindling_AlterCompletedMigrationScripts.sql",
                "Kindling_ProductOwnership_Table.sql",
                "Kindling_StatusMessages_Table.sql",
                "SchemaSmith_QuoteIdentifier.sql",
                "SchemaSmith_StripBacktickWrapping.sql",
                "SchemaSmith_SafeBacktickWrap.sql",
                "SchemaSmith_NormalizeIndexColumns.sql",
                "SchemaSmith_GenerateTableJson.sql",
                "SchemaSmith_ParseTableJson.sql",
                "SchemaSmith_MissingTableAndColumnQuench.sql",
                "SchemaSmith_ModifiedTableQuench.sql",
                "SchemaSmith_MissingIndexesAndConstraintsQuench.sql",
                "SchemaSmith_ForeignKeyQuench.sql",
                "SchemaSmith_IndexOnlyQuench.sql",
                "SchemaSmith_TableQuench.sql"
            ],
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }
}
