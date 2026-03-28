// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench;

public class DatabaseQuencher(string productName, Template template, string dbName, bool suppressKindlingForgeForTesting, string dropUnknownIndexes, string whatIfOnly, bool updateTables = true, bool dropTablesRemovedFromProduct = true, bool runScriptsTwice = false, bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true)
{
    public bool QuenchSuccessful { get; private set; }

    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private readonly ILog _errorLog = LogFactory.GetLogger("ErrorLog");
    private string _debugFileLocation = "";

    public void Quench()
    {
        ProgressLog("Begin Quench");
        try
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandTimeout = 0;
            using var objectsConnection = GetConnection(false);
            using var objectsCommand = objectsConnection.CreateCommand();
            objectsCommand.CommandTimeout = 0;

            template.ResetScripts();

            try
            {
                // Step 1: Kindle Forge
                if (!suppressKindlingForgeForTesting)
                {
                    using var kindlingConnection = GetConnection(ignoreInfoMessages: true);
                    using var kindlingCommand = kindlingConnection.CreateCommand();
                    kindlingCommand.CommandTimeout = 0;
                    ProgressLog("  Kindling Forge");
                    ForgeKindler.KindleTheForge(kindlingCommand);
                }

                // Step 2: Validate Baseline
                if (!string.IsNullOrWhiteSpace(template.BaselineValidationScript))
                {
                    _progressLog.Info("  Validate Baseline");
                    command.CommandText = template.BaselineValidationScript;
                    if (!((bool?)command.ExecuteScalar() ?? false))
                        throw new Exception("Invalid baseline for this release");
                }

                // Step 3: Object Quench (first pass — Objects slot only)
                if (whatIfOnly != "1")
                {
                    ProgressLog("  Quenching object scripts");
                    QuenchDatabaseObjects(objectsCommand, template.ObjectScripts, false);

                    if (runScriptsTwice)
                    {
                        ProgressLog("  Quenching object scripts (second pass)");
                        template.ObjectScripts.ForEach(s => s.HasBeenQuenched = false);
                        QuenchDatabaseObjects(objectsCommand, template.ObjectScripts, false);
                    }
                }
                else
                {
                    WhatIfLogScripts(template.ObjectScripts, "object scripts");
                }

                if (updateTables)
                {
                    var tableJson = template.TableSchema.Replace("'", "''");
                    var updateFillFactor = template.UpdateFillFactor ? "1" : "0";

                    if (template.IndexOnlyTableQuenches)
                    {
                        // Index-only mode: skip table/column/FK changes, only manage indexes
                        ProgressLog("  Quenching indexes only (IndexOnlyTableQuenches mode)");
                        command.CommandText = $"EXEC [{dbName}].SchemaSmith.IndexOnlyQuench @ProductName = '{productName}', @TableDefinitions = '{tableJson}', @WhatIf = {whatIfOnly}, @DropUnknownIndexes = {dropUnknownIndexes}, @UpdateFillFactor = {updateFillFactor}";
                        _debugFileLocation = $"SchemaQuench - IndexOnlyQuench {dbName}.sql";
                        LogSqlScript(_debugFileLocation, command.CommandText);
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                        _debugFileLocation = "";
                    }
                    else
                    {
                        // Step 4: Parse JSON into temp tables once
                        ProgressLog("  Parsing table JSON into temp tables");
                        var parseJsonScript = ForgeKindler.GetParseTableJsonScript();
                        command.CommandText = $"DECLARE @TableDefinitions NVARCHAR(MAX) = '{tableJson}', @UpdateFillFactor BIT = {updateFillFactor}\r\n{parseJsonScript}";
                        _debugFileLocation = $"SchemaQuench - ParseJson {dbName}.sql";
                        LogSqlScript(_debugFileLocation, command.CommandText);
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                        _debugFileLocation = "";

                        // Step 5: MissingTableAndColumnQuench
                        ProgressLog("  Quenching missing tables and columns");
                        command.CommandText = $"EXEC [{dbName}].SchemaSmith.MissingTableAndColumnQuench @WhatIf = {whatIfOnly}";
                        _debugFileLocation = $"SchemaQuench - MissingTableAndColumnQuench {dbName}.sql";
                        LogSqlScript(_debugFileLocation, command.CommandText);
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                        _debugFileLocation = "";

                        // Step 6: Object Quench (second opportunity — Objects slot only)
                        if (whatIfOnly != "1")
                        {
                            if (template.ObjectScripts.Any(s => !s.HasBeenQuenched))
                            {
                                ProgressLog("  Quenching object scripts (post missing tables)");
                                QuenchDatabaseObjects(objectsCommand, template.ObjectScripts, false);
                            }
                        }

                        // Step 7: Template Before Scripts (migration scripts)
                        if (whatIfOnly != "1")
                        {
                            ProgressLog("  Quenching before database scripts");
                            QuenchTemplateScripts(command, "Before", template.BeforeScripts);
                        }
                        else
                        {
                            WhatIfLogTemplateScripts(command, "Before", template.BeforeScripts);
                        }

                        // Step 8: ModifiedTableQuench
                        ProgressLog("  Quenching modified tables");
                        command.CommandText = $"EXEC [{dbName}].SchemaSmith.ModifiedTableQuench @ProductName = '{productName}', @WhatIf = {whatIfOnly}, @DropUnknownIndexes = {dropUnknownIndexes}, @DropTablesRemovedFromProduct = {(dropTablesRemovedFromProduct ? "1" : "0")}";
                        _debugFileLocation = $"SchemaQuench - ModifiedTableQuench {dbName}.sql";
                        LogSqlScript(_debugFileLocation, command.CommandText);
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                        _debugFileLocation = "";
                    }

                    // Step 9: Object Quench (third opportunity — Objects slot only)
                    if (whatIfOnly != "1")
                    {
                        if (template.ObjectScripts.Any(s => !s.HasBeenQuenched))
                        {
                            ProgressLog("  Quenching object scripts (post modified tables)");
                            QuenchDatabaseObjects(objectsCommand, template.ObjectScripts, false);
                        }
                    }

                    // Step 10: BetweenTablesAndKeys Scripts (migration scripts)
                    if (whatIfOnly != "1")
                    {
                        if (template.BetweenTablesAndKeysScripts.Any())
                        {
                            ProgressLog("  Quenching between-tables-and-keys scripts");
                            QuenchTemplateScripts(command, "BetweenTablesAndKeys", template.BetweenTablesAndKeysScripts);
                        }
                    }
                    else
                    {
                        WhatIfLogTemplateScripts(command, "BetweenTablesAndKeys", template.BetweenTablesAndKeysScripts);
                    }

                    // Step 11: MissingIndexesAndConstraintsQuench
                    if (!template.IndexOnlyTableQuenches)
                    {
                        ProgressLog("  Quenching missing indexes and constraints");
                        command.CommandText = $"EXEC [{dbName}].SchemaSmith.MissingIndexesAndConstraintsQuench @ProductName = '{productName}', @WhatIf = {whatIfOnly}";
                        _debugFileLocation = $"SchemaQuench - MissingIndexesAndConstraintsQuench {dbName}.sql";
                        LogSqlScript(_debugFileLocation, command.CommandText);
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                        _debugFileLocation = "";
                    }

                    // Step 12: AfterTablesScripts (migration scripts)
                    if (whatIfOnly != "1")
                    {
                        if (template.AfterTablesScripts.Any())
                        {
                            ProgressLog("  Quenching after-tables scripts");
                            QuenchTemplateScripts(command, "AfterTablesScripts", template.AfterTablesScripts);
                        }
                    }
                    else
                    {
                        WhatIfLogTemplateScripts(command, "AfterTablesScripts", template.AfterTablesScripts);
                    }

                    // Step 13: AfterTablesObjects (Objects + AfterTablesObjects combined — final retry, show errors)
                    if (whatIfOnly != "1")
                    {
                        if (template.AfterTablesObjectScripts.Any(s => !s.HasBeenQuenched))
                        {
                            ProgressLog("  Quenching remaining Objects and AfterTableObjects scripts");
                            QuenchDatabaseObjects(objectsCommand, template.AfterTablesObjectScripts);
                        }
                    }
                    else
                    {
                        var unquenched = template.AfterTablesObjectScripts.Where(s => !s.HasBeenQuenched).ToList();
                        WhatIfLogScripts(unquenched, "remaining Objects and AfterTableObjects scripts");
                    }

                    // Step 14: TableData Scripts
                    if (whatIfOnly != "1")
                    {
                        if (template.TableDataScripts.Any(s => !s.HasBeenQuenched))
                        {
                            ProgressLog("  Quenching table data scripts");
                            QuenchDatabaseObjects(objectsCommand, template.TableDataScripts);
                        }
                    }
                    else
                    {
                        WhatIfLogScripts(template.TableDataScripts, "table data scripts");
                    }

                    // Step 15: ForeignKeyQuench
                    if (!template.IndexOnlyTableQuenches)
                    {
                        ProgressLog("  Quenching foreign keys");
                        command.CommandText = $"EXEC [{dbName}].SchemaSmith.ForeignKeyQuench @ProductName = '{productName}', @WhatIf = {whatIfOnly}";
                        _debugFileLocation = $"SchemaQuench - ForeignKeyQuench {dbName}.sql";
                        LogSqlScript(_debugFileLocation, command.CommandText);
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                        _debugFileLocation = "";
                    }

                    // Step 16: Indexed Views
                    if (template.IndexedViews.Count > 0)
                    {
                        QuenchIndexedViews(command);
                    }
                }
                else
                {
                    ProgressLog("  Skipping table updates (UpdateTables=false)");
                }

                // Step 17: Template After Scripts (migration scripts)
                if (whatIfOnly != "1")
                {
                    ProgressLog("  Quenching after database scripts");
                    QuenchTemplateScripts(command, "After", template.AfterScripts);
                }
                else
                {
                    WhatIfLogTemplateScripts(command, "After", template.AfterScripts);
                }

                // Step 18: Stamp Version
                if (whatIfOnly != "1")
                {
                    if (!string.IsNullOrWhiteSpace(template.VersionStampScript))
                    {
                        ProgressLog("  Stamp version");
                        command.CommandText = template.VersionStampScript;
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(template.VersionStampScript))
                        ProgressLog("  [WhatIf] Would stamp version");
                }
            }
            finally
            {
                connection.Close();
                objectsConnection.Close();
            }

            ProgressLog(whatIfOnly != "1" ? "Successfully Quenched" : "What If Analysis Complete");

            QuenchSuccessful = true;
        }
        catch (Exception e)
        {
            ProgressLogError($"FAILED to quench:\r\n{e.Message}");
        }
    }

    private void WhatIfLogScripts(List<SqlScript> scripts, string description)
    {
        if (!scripts.Any()) return;
        ProgressLog($"  [WhatIf] Would quench {description}:");
        foreach (var script in scripts)
            ProgressLog($"    Would APPLY: {script.LogPath}");
    }

    private void WhatIfLogTemplateScripts(IDbCommand destCmd, string slot, List<SqlScript> scripts)
    {
        if (!scripts.Any()) return;
        ProgressLog($"  [WhatIf] Would quench {slot.ToLower()} scripts:");
        var alreadyRan = trackRunOnceMigrations ? GetCompletedEntriesBySlot(destCmd, slot) : [];
        foreach (var script in scripts)
        {
            if (ShouldAlwaysRun(script.Name) || !alreadyRan.Contains(GetRelativeScriptPath(script.LogPath)))
                ProgressLog($"    Would APPLY: {script.LogPath}");
            else
                ProgressLog($"    Would SKIP (previously quenched): {script.LogPath}");
        }
    }

    private void ExecuteNonQueryAndRethrowInfoMessageError(IDbCommand command)
    {
        _infoMessageException = null;
        command.ExecuteNonQuery();
        if (_infoMessageException != null) throw _infoMessageException;
    }

    private IDbConnection GetConnection(bool fireInfoMessageEventOnUserErrors = true, bool ignoreInfoMessages = false)
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var connectionStringOverride = CommandLineParser.ValueOfSwitch("ConnectionString", null);
        var connectionProperties = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        var connectionString = string.IsNullOrEmpty(connectionStringOverride)
            ? ConnectionString.Build(config["Target:Server"], dbName, config["Target:User"], config["Target:Password"], config["Target:Port"], connectionProperties)
            : connectionStringOverride;

        var connection = SqlConnectionFactory.GetFromFactory().GetSqlConnection(connectionString);

        if (!ignoreInfoMessages && connection is SqlConnection sqlConnection)
        {
            sqlConnection.InfoMessage += OnInfoMessage;
            sqlConnection.FireInfoMessageEventOnUserErrors = fireInfoMessageEventOnUserErrors;
        }

        connection.Open();
        return connection;
    }

    private static void LogSqlScript(string name, string sql)
    {
        var cwd = AppContext.BaseDirectory;
        FileWrapper.GetFromFactory().WriteAllText(Path.Combine(cwd, name), sql);
    }

    private Exception? _infoMessageException;
    private void QuenchDatabaseObjects(IDbCommand destCmd, List<SqlScript> templateObjects, bool showErrors = true)
    {
        var lastQuenchCount = 0;
        while (lastQuenchCount != templateObjects.Count(s => !s.HasBeenQuenched) && templateObjects.Any(s => !s.HasBeenQuenched))
        {
            lastQuenchCount = templateObjects.Count(s => !s.HasBeenQuenched);
            foreach (var script in templateObjects.Where(s => !s.HasBeenQuenched))
                QuenchOneScript(destCmd, script);
        }

        _debugFileLocation = "";
        if (showErrors) LogScriptErrors(templateObjects);
    }

    private void QuenchOneScript(IDbCommand destCmd, SqlScript script)
    {
        _debugFileLocation = (destCmd.Connection as SqlConnection)?.FireInfoMessageEventOnUserErrors ?? false ? script.LogPath : "";
        ProgressLog($"    Quenching {script.LogPath}");
        var needDBReset = false;
        try
        {
            foreach (var batch in script.Batches)
            {
                needDBReset = needDBReset || batch.ContainsIgnoringCase("USE ");
                destCmd.CommandText = batch;
                _infoMessageException = null;
                destCmd.ExecuteNonQuery();
                if (_infoMessageException != null) throw _infoMessageException;
            }

            script.HasBeenQuenched = true;
            script.Error = null;
        }
        catch (Exception ex)
        {
            script.Error = ex;
        }
        finally
        {
            if (needDBReset) ResetDb(destCmd);
        }
    }

    private void ResetDb(IDbCommand destCmd)
    {
        try
        {
            destCmd.CommandText = $"USE [{dbName}]";
            destCmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore error resetting db
        }
    }

    private void QuenchTemplateScripts(IDbCommand destCmd, string slot, List<SqlScript> scripts)
    {
        var alreadyRan = trackRunOnceMigrations ? GetCompletedEntriesBySlot(destCmd, slot) : [];
        foreach (var script in scripts.Where(s => !s.HasBeenQuenched))
        {
            if (ShouldAlwaysRun(script.Name) || !alreadyRan.Contains(GetRelativeScriptPath(script.LogPath)))
            {
                QuenchOneScript(destCmd, script);

                if (script.HasBeenQuenched && trackRunOnceMigrations && !ShouldAlwaysRun(script.Name))
                    MarkScriptCompleted(destCmd, script.LogPath, slot);
            }
            else
            {
                script.HasBeenQuenched = true;
                ProgressLog($"    Skipping (previously quenched) {script.LogPath}");
            }
        }

        if (trackRunOnceMigrations && pruneObsoleteMigrationTracking)
            RemoveObsoleteCompletedScriptEntries(destCmd, slot, scripts, alreadyRan);

        _debugFileLocation = "";
        LogScriptErrors(scripts);
    }

    private void RemoveObsoleteCompletedScriptEntries(IDbCommand destCmd, string slot, List<SqlScript> scripts, List<string> alreadyRan)
    {
        foreach (var obsoleteScript in alreadyRan.Where(a => scripts.All(s => GetRelativeScriptPath(s.LogPath) != a)))
        {
            destCmd.CommandText = $"DELETE SchemaSmith.CompletedMigrationScripts WHERE [ProductName] = '{productName}' AND [QuenchSlot] = '{slot}' AND [ScriptPath] = '{obsoleteScript}'";
            destCmd.ExecuteNonQuery();
        }
    }

    private bool ShouldAlwaysRun(string scriptName) => Path.GetFileNameWithoutExtension(scriptName).EndsWith("[ALWAYS]");

    private List<string> GetCompletedEntriesBySlot(IDbCommand destCmd, string slot)
    {
        destCmd.CommandText = $"SELECT [ScriptPath] FROM SchemaSmith.CompletedMigrationScripts WITH (NOLOCK) WHERE [ProductName] = '{productName}' AND [QuenchSlot] = '{slot}'";
        using var reader = destCmd.ExecuteReader();
        var entries = new List<string>();
        while (reader.Read())
            entries.Add(reader.GetString(0));
        return entries;
    }

    private void MarkScriptCompleted(IDbCommand destCmd, string scriptPath, string slot)
    {
        destCmd.CommandText = $"INSERT SchemaSmith.CompletedMigrationScripts ([ScriptPath], [ProductName], [QuenchSlot]) VALUES('{GetRelativeScriptPath(scriptPath)}', '{productName}', '{slot}')";
        destCmd.ExecuteNonQuery();
    }

    private string GetRelativeScriptPath(string filePath)
    {
        return LongPathSupport.StripLongPathPrefix(filePath)
            .Replace(Path.GetDirectoryName(template.LogPath) ?? "", "")
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .TrimStart(Path.AltDirectorySeparatorChar);
    }

    private void LogScriptErrors(List<SqlScript> scripts)
    {
        if (scripts.All(x => x.HasBeenQuenched)) return;

        foreach (var sqlScript in scripts.Where(s => !s.HasBeenQuenched))
            ProgressLogError($"Unable to quench '{sqlScript.LogPath}':\r\n{sqlScript.Error}");

        throw new Exception("Unable to quench all scripts");
    }

    private void QuenchIndexedViews(IDbCommand command)
    {
        // Validate: every indexed view must have a unique clustered index
        foreach (var view in template.IndexedViews)
        {
            if (!view.Indexes.Any(i => i.Clustered))
                throw new Exception($"Indexed view {view.Schema}.{view.Name} must have a unique clustered index");
        }

        var viewJson = template.IndexedViewSchema.Replace("'", "''");
        var updateFillFactor = template.UpdateFillFactor ? "1" : "0";

        ProgressLog("  Quenching indexed views");
        command.CommandText = $"EXEC [{dbName}].SchemaSmith.IndexedViewQuench @ProductName = '{productName}', @IndexedViewSchema = '{viewJson}', @WhatIf = {whatIfOnly}, @UpdateFillFactor = {updateFillFactor}";
        _debugFileLocation = $"SchemaQuench - IndexedViewQuench {dbName}.sql";
        LogSqlScript(_debugFileLocation, command.CommandText);
        ExecuteNonQueryAndRethrowInfoMessageError(command);
        _debugFileLocation = "";
    }

    private void ProgressLog(string msg)
    {
        _progressLog.Info($"[{dbName}] {msg}");
    }

    private void ProgressLogError(string msg)
    {
        _progressLog.Error($"[{dbName}] {msg}");
    }

    private void ErrorLogError(string msg)
    {
        _errorLog.Error($"[{dbName}] {msg}");
    }

    private void OnInfoMessage(object sender, SqlInfoMessageEventArgs e)
    {
        foreach (SqlError err in e.Errors)
        {
            if (err.Class > 10)
            {
                ProgressLogError(err.Message);
                if (!string.IsNullOrWhiteSpace(_debugFileLocation))
                {
                    ProgressLogError("");
                    ProgressLogError($"Debug Script: '{_debugFileLocation}'");
                }

                ErrorLogError("");
                ErrorLogError(err.Message);
                ErrorLogError($"  at Line: {err.LineNumber}");
                ErrorLogError("");
                _infoMessageException = new Exception(err.Message);
            }
            else
                ProgressLog($"      {err.Message}");
        }
    }
}
