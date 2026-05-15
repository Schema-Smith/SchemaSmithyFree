// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Checkpointing;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench;

public class ProductQuench
{
    private readonly IConfigurationRoot _config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
    private readonly ILog _errorLog = LogFactory.GetLogger("ErrorLog");
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private readonly Product _product = Product.Load();

    private readonly string _whatIfOnly;
    private readonly int _maxThreads;
    private readonly string _primaryServer;
    private readonly List<string> _secondaryServers = [];
    private readonly bool _runScriptsTwice;
    private readonly bool _skipKindling;
    private readonly string _dropRemovedTables;
    private readonly bool _updateTables;
    private readonly bool _deliverData;
    private readonly bool _trackRunOnceMigrations;
    private readonly bool _pruneObsoleteMigrationTracking;
    private readonly ICheckpointing _checkpointing;
    private bool _updateFailed;
    private bool _anyFailure;

    /// <summary>
    /// True when any template or product-level step reported a fatal failure during
    /// QuenchProduct. Callers can inspect this after QuenchProduct returns to decide
    /// whether to preserve checkpoint files — a failed run must preserve checkpoints
    /// so the next invocation can resume.
    /// </summary>
    public bool Failed => _anyFailure;

    public ProductQuench()
    {
        if (_product.Platform == Platform.Unknown)
            throw new Exception($"Product '{_product.Name}' does not have a Platform assigned. Use SchemaTongs or edit the product.json file to assign a platform before quenching.");

        if (!int.TryParse(_config["MaxThreads"], out _maxThreads) || _maxThreads < 1 || _maxThreads > 20)
            _maxThreads = 10;
        _whatIfOnly = FormatWhatIfOnly(_config["WhatIfONLY"]?.ToLower() == "true");
        _runScriptsTwice = _config["RunScriptsTwice"]?.ToLower() == "true";
        _primaryServer = _config["Target:Server"] ?? "localhost";
        _skipKindling = _config["KindleTheForge"]?.ToLower() == "false";
        _dropRemovedTables = FormatBooleanFlag(_config["DropTablesRemovedFromProduct"]?.ToLower() != "false");
        _updateTables = _config["UpdateTables"]?.ToLower() != "false";
        _deliverData = _config["DeliverData"]?.ToLower() != "false";
        _trackRunOnceMigrations = _config["TrackRunOnceMigrations"]?.ToLower() != "false";
        _pruneObsoleteMigrationTracking = _config["PruneObsoleteMigrationTracking"]?.ToLower() != "false";
        _checkpointing = FileCheckpointManager.GetFromFactory();

        // Secondary servers are SqlServer-only (Availability Groups)
        if (_product.Platform == Platform.SqlServer)
        {
            _secondaryServers.AddRange((_config["Target:SecondaryServers"] ?? "")
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }
    }

    // Visible for testing
    internal Product LoadedProduct => _product;

    /// <summary>
    /// Returns the init database name used for server-level connections per platform.
    /// </summary>
    internal static string GetInitDatabase(Platform platform) => platform switch
    {
        Platform.SqlServer => "master",
        Platform.PostgreSQL => "postgres",
        Platform.MySQL => "information_schema",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}")
    };

    /// <summary>
    /// Returns the server identification query per platform.
    /// </summary>
    internal static string GetServerIdQuery(Platform platform) => platform switch
    {
        Platform.SqlServer => "SELECT @@SERVERNAME",
        Platform.PostgreSQL => "SELECT inet_server_addr();",
        Platform.MySQL => "SELECT @@hostname",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, $"Unsupported platform: {platform}")
    };

    /// <summary>
    /// Formats the WhatIfOnly value per platform convention.
    /// SqlServer/MySQL use "1"/"0"; PostgreSQL uses "true"/"false".
    /// </summary>
    internal string FormatWhatIfOnly(bool isWhatIf) => _product.Platform switch
    {
        Platform.PostgreSQL => isWhatIf ? "true" : "false",
        _ => isWhatIf ? "1" : "0"
    };

    /// <summary>
    /// Formats boolean flags per platform convention.
    /// SqlServer/MySQL use "1"/"0"; PostgreSQL uses "true"/"false".
    /// </summary>
    internal string FormatBooleanFlag(bool value) => _product.Platform switch
    {
        Platform.PostgreSQL => value ? "true" : "false",
        _ => value ? "1" : "0"
    };

    /// <summary>
    /// Converts a scalar result to boolean.
    /// MySQL returns Int64 (0/1) from EXISTS(), SQL Server returns bool, PostgreSQL returns bool.
    /// </summary>
    internal static bool ScalarToBool(object result) => result switch
    {
        null or DBNull => false,
        bool b => b,
        _ => Convert.ToInt64(result) != 0
    };

    /// <summary>
    /// Returns the active WhatIfOnly value (for testing visibility).
    /// </summary>
    internal bool IsWhatIfOnly => _product.Platform == Platform.PostgreSQL
        ? _whatIfOnly == "true"
        : _whatIfOnly == "1";

    private TrackingScope ProductScope => new TrackingScope { ProductName = _product.Name };

    private TrackingScope ProductScopeForServer(string server) => new TrackingScope
    {
        ProductName = _product.Name,
        Server = server
    };

    internal virtual IDbCommand GetCommand(string server)
    {
        var initDb = GetInitDatabase(_product.Platform);
        var connectionStringOverride = CommandLineParser.ValueOfSwitch("ConnectionString", null);
        string connectionString;
        if (!string.IsNullOrEmpty(connectionStringOverride) && server == _primaryServer)
        {
            if (_product.Platform == Platform.MySQL &&
                !connectionStringOverride.Contains("AllowUserVariables", StringComparison.OrdinalIgnoreCase))
            {
                LogFactory.GetLogger("ProgressLog").Warn("Connection string override for MySQL does not contain AllowUserVariables=true. " +
                                                         "This is required for SchemaSmith stored procedures that use PREPARE/EXECUTE.");
            }
            connectionString = connectionStringOverride;
        }
        else
        {
            var connectionProperties = ConnectionString.ReadProperties(_config, "Target:ConnectionProperties");
            connectionString = ConnectionString.Build(_product.Platform, server, initDb, _config["Target:User"], _config["Target:Password"], _config["Target:Port"], connectionProperties);
        }
        var factory = DbConnectionFactory.ForPlatform(_product.Platform);
        var connection = factory.GetDbConnection(connectionString);
        try
        {
            connection.Open();
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to connect to {server}{(!string.IsNullOrWhiteSpace(_config["Target:User"]) ? $" with user {_config["Target:User"]}" : "")}", e);
        }
        var command = connection.CreateCommand();
        command.CommandTimeout = 0;

        return command;
    }

    public void QuenchProduct(bool suppressKindlingForTesting = false)
    {
        _progressLog.Info($"Begin Quench of {_product.Name}");

        LogProductInfo();

        TestServerConnections();

        RemoveOldTableQuenchScripts();

        var summary = _checkpointing.GetProductCheckpointSummary(_product.Name);
        if (summary.HasAnyCompleted)
            _progressLog.Info($"Resuming from checkpoint (Before Scripts: {summary.TotalBeforeScripts} across {summary.ServersWithBeforeScripts} server(s), Templates: {summary.CompletedTemplates}, After Scripts: {summary.TotalAfterScripts} across {summary.ServersWithAfterScripts} server(s))");

        using var command = GetCommand(_primaryServer);

        try
        {
            if (!string.IsNullOrWhiteSpace(_product.ValidationScript))
            {
                _progressLog.Info("Validate Server");
                command.CommandText = _product.ValidationScript;
                if (!ScalarToBool(command.ExecuteScalar()))
                    throw new Exception("Invalid server for this product");
            }

            if (!string.IsNullOrWhiteSpace(_product.BaselineValidationScript))
            {
                _progressLog.Info("Validate Baseline");
                command.CommandText = _product.BaselineValidationScript;
                if (!ScalarToBool(command.ExecuteScalar()))
                    throw new Exception("Invalid baseline for this release");
            }

            if (_product.QueryTokens.Count > 0)
            {
                _progressLog.Info("Resolving Product Level Query Tokens");
                TokenHelper.ResolveQueryTokens(_product.QueryTokens, _product.NonQueryTokens.ToList(), command, Path.GetDirectoryName(_product.FilePath), _product.Platform);
                foreach (var script in _product.ScriptFolders.SelectMany(p => p.Scripts))
                    script.ReplaceQueryTokens(_product.QueryTokens.ToList());
            }

            QuenchProductScriptsWithCheckpoint(_product.BeforeFolders, "Before Product", true);

            var templates = LoadTemplates();
            var suppressKindling = suppressKindlingForTesting || _skipKindling;
            foreach (var template in templates)
            {
                var stepName = $"Template:{template.Name}";
                if (_checkpointing.HasCompleted(ProductScope, stepName))
                {
                    _progressLog.Info($"Skipping template '{template.Name}' (previously completed per checkpoint)");
                    continue;
                }
                // Explicit check-then-mark rather than Track(): QuenchTemplate handles its own
                // failure via LogBackup.BackupLogsAndExit(2), which terminates the process in
                // production but is a mocked no-op under tests. Using Track() would interpret the
                // non-throwing return as success and record the template as complete even when
                // _updateFailed is set, leading to the template being skipped on the next run.
                QuenchTemplate(template, suppressKindling);
                if (!_updateFailed)
                    _checkpointing.MarkStepCompleted(ProductScope, stepName);
            }

            QuenchProductScriptsWithCheckpoint(_product.AfterFolders, "After Product", false);

            if (!string.IsNullOrWhiteSpace(_product.VersionStampScript))
            {
                _progressLog.Info("Stamp product version");
                command.CommandText = _product.VersionStampScript;
                command.ExecuteNonQuery();
            }
        }
        finally
        {
            command.Connection?.Close();
            command.Connection?.Dispose();
        }

        _progressLog.Info($"Completed quench of {_product.Name}");
    }

    internal static readonly string[] SpecialTokenTags = ["TableSchema_", "ObjectScripts_", "QueryTokens_", "MaterializedViewSchema_", "IndexedViewSchema_"];

    internal static Dictionary<string, string> BuildSpecialTokens(Template template)
    {
        var tokens = new Dictionary<string, string>
        {
            { $"TableSchema_{template.Name}", template.TableSchema.Replace("'", "''") },
            { $"ObjectScripts_{template.Name}", JsonHelper.Serialize(template.ObjectScripts.Concat(template.AfterTablesObjectScripts)).Replace("'", "''") },
            { $"QueryTokens_{template.Name}", JsonHelper.Serialize(template.QueryTokens).Replace("'", "''") },
            { $"MaterializedViewSchema_{template.Name}", template.MaterializedViewSchema.Replace("'", "''") },
            { $"IndexedViewSchema_{template.Name}", template.IndexedViewSchema.Replace("'", "''") }
        };
        return tokens;
    }

    private List<Template> LoadTemplates()
    {
        var templates = new List<Template>();
        var specialTokens = new Dictionary<string, string>();
        foreach (var templateName in _product.TemplateOrder.Where(templateName => !string.IsNullOrWhiteSpace(templateName)))
        {
            _progressLog.Info($"Load Template Schema: {templateName}");
            var template = Template.Load(templateName, _product);
            foreach (var kvp in BuildSpecialTokens(template))
                specialTokens.Add(kvp.Key, kvp.Value);
            templates.Add(template);
        }

        _progressLog.Info("Check for Template Special Script Tokens");
        var scriptsWithSchemaTokens = templates.SelectMany(t => t.ScriptFolders).SelectMany(f => f.Scripts)
            .Where(s => s.RemainingTokens.Any(b => SpecialTokenTags.Any(b.EqualsIgnoringCase))).ToList();
        if (scriptsWithSchemaTokens.Count > 0)
        {
            _progressLog.Info("Process Template Special Script Tokens");
            var tokenList = specialTokens.ToList();
            foreach (var script in scriptsWithSchemaTokens)
                script.ReplaceQueryTokens(tokenList);
        }

        return templates;
    }

    private void QuenchProductScriptsWithCheckpoint(List<ProductFolder> folders, string msg, bool isBefore)
    {
        _updateFailed = false;

        if (_product.Platform == Platform.SqlServer && _secondaryServers.Count > 0)
        {
            // SQL Server: quench to primary and secondary servers in parallel, filtering by ServerToQuench
            var serverQueue = new TaskQueueManager<string>(_maxThreads);
            _secondaryServers.Union([_primaryServer]).Distinct().ToList()
                .ForEach(server => serverQueue.AddToQueue(server, s => QuenchScriptsToServerWithCheckpoint(s, msg,
                    folders.Where(f => s.Equals(_primaryServer) ? f.QuenchOnPrimary : f.QuenchOnSecondary)
                        .SelectMany(f => f.Scripts).ToList(), isBefore)));
            serverQueue.WaitForAll();
        }
        else
        {
            // PostgreSQL/MySQL: single server only
            QuenchScriptsToServerWithCheckpoint(_primaryServer, msg,
                folders.SelectMany(f => f.Scripts).ToList(), isBefore);
        }

        if (_updateFailed)
        {
            _anyFailure = true;
            throw new Exception("Product script quench FAILED");
        }
    }

    private void LogProductInfo()
    {
        _progressLog.Info($"ProductName: {_product.Name}, Platform: {_product.Platform}, TemplateOrder: [{string.Join(",", _product.TemplateOrder)}], ValidationScript: {_product.ValidationScript}");
        if (_product.ScriptTokens.Count == 0) return;

        _progressLog.Info("  Product Script Tokens:");
        _product.ScriptTokens.ToList().ForEach(token => _progressLog.Info($"    {token.Key}: {token.Value}"));

        _progressLog.Info("");
    }

    private void TestServerConnections()
    {
        var shouldFail = false;
        var serverNames = new Dictionary<string, string>();
        _progressLog.Info("Testing connection to configured servers");
        var allServers = _secondaryServers.Union([_primaryServer]).ToList();

        foreach (var server in allServers)
        {
            if (serverNames.ContainsKey(server))
            {
                _progressLog.Error($"  Duplicate server configured: {server}");
                _errorLog.Error($"Duplicate server configured {server}");
                shouldFail = true;
                continue;
            }

            try
            {
                using var cmd = GetCommand(server);

                try
                {
                    cmd.CommandText = GetServerIdQuery(_product.Platform);
                    var serverName = cmd.ExecuteScalar()?.ToString() ?? "UNKNOWN";
                    serverNames.Add(server, serverName);

                    _progressLog.Info($"  {server} ({serverName}) connection succeeded");
                }
                finally
                {
                    cmd.Connection?.Close();
                    cmd.Connection?.Dispose();
                }
            }
            catch (Exception e)
            {
                _progressLog.Error($"  {server}: {e.Message} **CONNECTION FAILED**");
                _errorLog.Error($"Unable to connect to {server}:\r\n{e}");
                shouldFail = true;
            }
        }

        if (serverNames.Values.Distinct().Count() != serverNames.Count)
        {
            _progressLog.Error("  Duplicate server names detected while validating the configured server list");
            _errorLog.Error("Duplicate server names detected while validating the configured server list");
            shouldFail = true;
        }

        if (shouldFail) throw new Exception("Error validating configured servers");

        _progressLog.Info("");
        _progressLog.Info("");
    }

    private void QuenchScriptsToServerWithCheckpoint(string server, string message, IEnumerable<SqlScript> scripts, bool isBefore)
    {
        _progressLog.Info($"Quenching {message} Scripts to {server}");
        using var productScriptCommand = GetCommand(server);
        try
        {
            var scriptsToQuench = scripts.Select(j => j.Clone()).ToList();
            QuenchScriptsWithCheckpoint(productScriptCommand, scriptsToQuench, server, isBefore);
        }
        catch
        {
            _updateFailed = true;
        }
        finally
        {
            productScriptCommand.Connection?.Close();
            productScriptCommand.Connection?.Dispose();
        }
    }

    private static void RemoveOldTableQuenchScripts()
    {
        var dir = DirectoryInfoFactory.GetFromFactory().GetDirectoryInfoWrapper(".");
        foreach (var file in dir.GetFiles("SchemaQuench - Quench Tables*.sql", SearchOption.TopDirectoryOnly))
            file.Delete();
    }

    private void QuenchTemplate(Template template, bool suppressKindling)
    {
        if (string.IsNullOrWhiteSpace(template.DatabaseIdentificationScript)) return;

        _progressLog.Info($"Quenching Template: {template.Name}");
        if (template.LoggableTokens.Any())
        {
            _progressLog.Info("Template Script Tokens:");
            template.LoggableTokens.ToList()
                .ForEach(token => _progressLog.Info($"    {token.Key}: {token.Value}"));
        }

        _updateFailed = false;

        if (_product.Platform == Platform.SqlServer)
            QuenchTemplateSqlServer(template, suppressKindling);
        else
            UpdateDatabasesForTemplate(template, suppressKindling, _primaryServer);

        if (!_updateFailed) return;
        _anyFailure = true;
        _progressLog.Error("One or more database quenches FAILED");
        LogBackup.BackupLogsAndExit("SchemaQuench", 2);
    }

    /// <summary>
    /// SQL Server template quench: handles ServerToQuench (Primary/Secondary/Both) from SqlServerTemplate.
    /// </summary>
    private void QuenchTemplateSqlServer(Template template, bool suppressKindling)
    {
        var serverList = new List<string>();
        var serverToQuench = ServerToQuench.Primary;
        if (template is SqlServerTemplate sqlTemplate)
            serverToQuench = sqlTemplate.ServerToQuench;

        if (serverToQuench is ServerToQuench.Primary or ServerToQuench.Both)
            serverList.Add(_primaryServer);
        if (serverToQuench is ServerToQuench.Secondary or ServerToQuench.Both)
            serverList.AddRange(_secondaryServers);

        var serverQueue = new TaskQueueManager<string>(_maxThreads);
        serverList.Distinct().ToList()
            .ForEach(server => serverQueue.AddToQueue(server, s => UpdateDatabasesForTemplate(template, suppressKindling, s)));
        serverQueue.WaitForAll();
    }

    private void UpdateDatabasesForTemplate(Template template, bool suppressKindling, string server)
    {
        var dbList = new List<DatabaseQuench>();
        var dbQueue = new TaskQueueManager<DatabaseQuench>(_maxThreads);
        using var command = GetCommand(server);
        try
        {
            _progressLog.Info("Locate Databases To Quench");
            command.CommandText = template.DatabaseIdentificationScript;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var quench = new DatabaseQuench(server, _product, template, $"{reader[0]}", suppressKindling,
                    _whatIfOnly, _runScriptsTwice, _dropRemovedTables, _product.DropUnknownIndexes,
                    _updateTables && template.Tables.Count > 0, _deliverData, _checkpointing,
                    _trackRunOnceMigrations, _pruneObsoleteMigrationTracking);
                dbList.Add(quench);
                dbQueue.AddToQueue(quench, db => db.Execute());
            }
        }
        finally
        {
            command.Connection?.Close();
            command.Connection?.Dispose();
        }

        dbQueue.WaitForAll();

        if (template.Required && dbList.Count == 0)
        {
            _progressLog.Error($"No databases found to quench for required template {template.Name} on server {server}");
            _updateFailed = true;
            return;
        }

        if (dbList.All(d => d.QuenchSuccessful)) return;
        _progressLog.Error($"One or more database quenches FAILED on {server}");
        _updateFailed = true;
    }

    private void QuenchScriptsWithCheckpoint(IDbCommand destCmd, List<SqlScript> scriptList, string server, bool isBefore)
    {
        var initDb = GetInitDatabase(_product.Platform);
        var serverMsg = string.IsNullOrWhiteSpace(server) ? "" : $"[{server}].";
        var slot = isBefore ? "Before" : "After";

        foreach (var script in scriptList)
        {
            if (_checkpointing.HasCompletedScript(ProductScopeForServer(server), slot, script.LogPath))
            {
                _progressLog.Info($"{serverMsg}[{initDb}]    {(IsWhatIfOnly ? "Would SKIP" : "Skipping")} (previously quenched per checkpoint) {script.LogPath}");
                script.HasBeenQuenched = true;
                continue;
            }

            if (IsWhatIfOnly)
            {
                _progressLog.Info($"{serverMsg}[{initDb}]    Would Quench {script.LogPath}");
                script.HasBeenQuenched = true;
                continue;
            }

            _progressLog.Info($"{serverMsg}[{initDb}]    Quenching {script.LogPath}");
            var needDBReset = false;
            try
            {
                script.CheckForUnresolvedTokens(initDb, serverMsg, _progressLog.Warn);
                for (var i = 0; i < (_runScriptsTwice ? 2 : 1); i++)
                {
                    foreach (var batch in script.Batches)
                    {
                        needDBReset = needDBReset || batch.ContainsIgnoringCase("USE ");
                        destCmd.CommandText = batch;
                        destCmd.ExecuteNonQuery();
                    }
                }

                script.HasBeenQuenched = true;
                script.Error = null;
                _checkpointing.MarkScriptCompleted(ProductScopeForServer(server), slot, script.LogPath);
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

        if (scriptList.Any(x => !x.HasBeenQuenched))
        {
            foreach (var sqlScript in scriptList.Where(s => !s.HasBeenQuenched))
                _progressLog.Error($"{serverMsg}[{initDb}] Unable to quench '{sqlScript.LogPath}':\r\n{sqlScript.Error}");

            throw new Exception($"{serverMsg}[{initDb}] Unable to quench one or more scripts");
        }
    }

    private void ResetDb(IDbCommand destCmd)
    {
        try
        {
            var initDb = GetInitDatabase(_product.Platform);
            destCmd.CommandText = _product.Platform switch
            {
                Platform.SqlServer => $"USE [{initDb}]",
                Platform.PostgreSQL => initDb, // PostgreSQL uses ChangeDatabase
                Platform.MySQL => $"USE `{initDb}`",
                _ => throw new ArgumentOutOfRangeException()
            };

            if (_product.Platform == Platform.PostgreSQL)
                destCmd.Connection.ChangeDatabase(initDb);
            else
                destCmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore error resetting db
        }
    }
}
