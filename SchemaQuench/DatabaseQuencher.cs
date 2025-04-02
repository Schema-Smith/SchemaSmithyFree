using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench;

public class DatabaseQuencher(string productName, Template template, string dbName, bool suppressKindlingForgeForTesting, string dropUnknownIndexes, string whatIfOnly)
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
                if (!suppressKindlingForgeForTesting)
                {
                    using var kindlingConnection = GetConnection(ignoreInfoMessages: true);
                    using var kindlingCommand = kindlingConnection.CreateCommand();
                    kindlingCommand.CommandTimeout = 0;
                    ProgressLog("  Kindling Forge");
                    KindlingForge(kindlingCommand);
                }

                if (whatIfOnly != "1")
                {
                    ProgressLog("  Quenching before database scripts");
                    QuenchTemplateScripts(command, "Before", template.BeforeScripts);

                    ProgressLog("  Quenching object scripts");
                    QuenchDatabaseObjects(objectsCommand, template.ObjectScripts, false);
                }

                ProgressLog("  Quenching tables");
                command.CommandText = $"EXEC [{dbName}].SchemaSmith.TableQuench @ProductName = '{productName}', @TableDefinitions = '{template.TableSchema.Replace("'", "''")}', @DropUnknownIndexes = {dropUnknownIndexes}, @UpdateFillFactor = {(template.UpdateFillFactor ? "1" : "0")}, @WhatIf = {whatIfOnly}";
                _debugFileLocation = $"SchemaQuench - Quench Tables {dbName}.sql";
                LogSqlScript(_debugFileLocation, command.CommandText);
                ExecuteNonQueryAndRethrowInfoMessageError(command);
                _debugFileLocation = "";

                if (whatIfOnly != "1")
                {
                    if (template.ObjectScripts.Any(s => !s.HasBeenQuenched))
                    {
                        ProgressLog("  Quenching object scripts");
                        QuenchDatabaseObjects(objectsCommand, template.ObjectScripts);
                    }

                    ProgressLog("  Quenching after database scripts");
                    QuenchTemplateScripts(command, "After", template.AfterScripts);

                    if (!string.IsNullOrWhiteSpace(template.VersionStampScript))
                    {
                        ProgressLog("  Stamp version");
                        command.CommandText = template.VersionStampScript;
                        ExecuteNonQueryAndRethrowInfoMessageError(command);
                    }
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

    private void ExecuteNonQueryAndRethrowInfoMessageError(IDbCommand command)
    {
        _infoMessageException = null;
        command.ExecuteNonQuery();
        if (_infoMessageException != null) throw _infoMessageException;
    }

    private IDbConnection GetConnection(bool fireInfoMessageEventOnUserErrors = true, bool ignoreInfoMessages = false)
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var connectionString = ConnectionString.Build(config["Target:Server"], dbName, config["Target:User"], config["Target:Password"]);

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
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var cwd = Path.GetDirectoryName(assembly.Location) ?? @".\";

        FileWrapper.GetFromFactory().WriteAllText(Path.Combine(cwd, name), sql);
    }

    public static void KindlingForge(IDbCommand command)
    {
        command.CommandText = ResourceLoader.Load("Kindling_SchemaSmith_Schema.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_StripParenWrapping.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_StripBracketWrapping.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_SafeBracketWrap.sql");
        command.ExecuteNonQuery();        

        command.CommandText = ResourceLoader.Load("SchemaSmith.TableQuench.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("Kindling_CompletedMigrations_Table.sql");
        command.ExecuteNonQuery();
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
        try
        {
            foreach (var batch in script.Batches)
            {
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
    }

    private void QuenchTemplateScripts(IDbCommand destCmd, string slot, List<SqlScript> scripts)
    {
        var alreadyRan = GetCompletedEntriesBySlot(destCmd, slot);
        foreach (var script in scripts.Where(s => !s.HasBeenQuenched))
        {
            if (ShouldAlwaysRun(script.Name) || !alreadyRan.Contains(GetRelativeScriptPath(script.LogPath)))
            {
                QuenchOneScript(destCmd, script);

                if (script.HasBeenQuenched && !ShouldAlwaysRun(script.Name))
                    MarkScriptCompleted(destCmd, script.LogPath, slot);
            }
            else
            {
                script.HasBeenQuenched = true;
                ProgressLog($"    Skipping (previously quenched) {script.LogPath}");
            }
        }

        RemoveObsoleteCompletedScriptEntries(destCmd, slot, scripts, alreadyRan);

        _debugFileLocation = "";
        LogScriptErrors(scripts);
    }

    private void RemoveObsoleteCompletedScriptEntries(IDbCommand destCmd, string slot, List<SqlScript> scripts, List<string> alreadyRan)
    {
        foreach (var obsoleteScript in alreadyRan.Where(a => !scripts.Any(s => GetRelativeScriptPath(s.LogPath) == a)))
        {
            destCmd.CommandText = $"DELETE SchemaSmith.CompletedMigrationScripts WHERE [ProductName] = '{productName}' AND [QuenchSlot] = '{slot}' AND [ScriptPath] = '{obsoleteScript}'";
            destCmd.ExecuteNonQuery();
        }
    }

    private bool ShouldAlwaysRun(string scriptName) => Path.GetFileNameWithoutExtension(scriptName)?.EndsWith("[ALWAYS]") ?? false;

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

        throw new Exception($"Unable to quench all scripts");
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