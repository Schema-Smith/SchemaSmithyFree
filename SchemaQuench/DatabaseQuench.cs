// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Newtonsoft.Json.Linq;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;
using Schema.Checkpointing;
using Schema.Delivery;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench;

/// <summary>
/// Quenches a single database for a template. Platform-aware: dispatches connection setup,
/// identifier quoting, and SQL commands based on Product.Platform.
/// </summary>
public class DatabaseQuench
{
    public bool QuenchSuccessful { get; private set; }

    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private readonly ILog _errorLog = LogFactory.GetLogger("ErrorLog");

    private readonly string _server;
    private readonly Product _product;
    private readonly Template _template;
    private readonly string _databaseName;
    private readonly string _schemaName;
    private readonly bool _suppressKindling;
    private readonly string _whatIfOnly;
    private readonly bool _runScriptsTwice;
    private readonly string _dropRemovedTables;
    private readonly bool _updateTables;
    private readonly bool _deliverData;
    private readonly ICheckpointing _checkpointing;
    private readonly string _dropUnknownIndexes;
    private readonly bool _trackRunOnceMigrations;
    private readonly bool _pruneObsoleteMigrationTracking;

    private string _debugFileLocation = "";
    private Exception _infoMessageException;
    private StatusMessageMonitor _statusMonitor;
    private readonly object _lockObject = new();

    // Per-iteration cloned script collections, populated at the start of Execute() when this is
    // a schema-template iteration. Cloning isolates {{SchemaName}}-substituted batches from sibling
    // iterations that share the same in-memory Template instance. For regular templates these stay
    // null and execution reads through to _template.<slot>Scripts as before.
    private List<SqlScript> _iterationBeforeScripts;
    private List<SqlScript> _iterationObjectScripts;
    private List<SqlScript> _iterationAfterTablesObjectScripts;
    private List<SqlScript> _iterationBetweenTablesAndKeysScripts;
    private List<SqlScript> _iterationAfterTableScripts;
    private List<SqlScript> _iterationTableDataScripts;
    private List<SqlScript> _iterationAfterScripts;
    private string _iterationBaselineValidationScript;
    private string _iterationVersionStampScript;

    public DatabaseQuench(string server, Product product, Template template, string databaseName,
        string schemaName, bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        bool dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true)
    {
        _server = server;
        _product = product;
        _template = template;
        _databaseName = databaseName;
        _schemaName = schemaName ?? "";
        _suppressKindling = suppressKindling;
        _whatIfOnly = whatIfOnly;
        _runScriptsTwice = runScriptsTwice;
        _dropRemovedTables = dropRemovedTables;
        _updateTables = updateTables;
        _deliverData = deliverData;
        _checkpointing = checkpointing;
        _dropUnknownIndexes = FormatBooleanFlag(dropUnknownIndexes);
        _trackRunOnceMigrations = trackRunOnceMigrations;
        _pruneObsoleteMigrationTracking = pruneObsoleteMigrationTracking;
    }

    // Convenience overload matching the pre-schema-templates positional signature so existing
    // callers (mostly tests) that have no schema concept compile without modification. Forwards
    // to the canonical constructor with empty schemaName.
    public DatabaseQuench(string server, Product product, Template template, string databaseName,
        bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        bool dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true)
        : this(server, product, template, databaseName, "", suppressKindling, whatIfOnly, runScriptsTwice,
            dropRemovedTables, dropUnknownIndexes, updateTables, deliverData, checkpointing,
            trackRunOnceMigrations, pruneObsoleteMigrationTracking)
    {
    }

    // Internal constructor for testing — allows direct injection of all parameters
    internal DatabaseQuench(string server, Product product, Template template, string databaseName,
        string schemaName, bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        string dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true)
    {
        _server = server;
        _product = product;
        _template = template;
        _databaseName = databaseName;
        _schemaName = schemaName ?? "";
        _suppressKindling = suppressKindling;
        _whatIfOnly = whatIfOnly;
        _runScriptsTwice = runScriptsTwice;
        _dropRemovedTables = dropRemovedTables;
        _dropUnknownIndexes = dropUnknownIndexes;
        _updateTables = updateTables;
        _deliverData = deliverData;
        _checkpointing = checkpointing;
        _trackRunOnceMigrations = trackRunOnceMigrations;
        _pruneObsoleteMigrationTracking = pruneObsoleteMigrationTracking;
    }

    // Convenience overload for tests pre-dating the schemaName parameter.
    internal DatabaseQuench(string server, Product product, Template template, string databaseName,
        bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        string dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true)
        : this(server, product, template, databaseName, "", suppressKindling, whatIfOnly, runScriptsTwice,
            dropRemovedTables, dropUnknownIndexes, updateTables, deliverData, checkpointing,
            trackRunOnceMigrations, pruneObsoleteMigrationTracking)
    {
    }

    internal Platform Platform => _product.Platform;
    internal string ProductName => _product.Name;

    /// <summary>
    /// The iteration schema for this database quench. Empty string for regular (non-schema) templates,
    /// which surfaces through <see cref="TrackingScope.SchemaName"/> consistently with the persisted
    /// tracking-table convention (slice 2). Exposed for tests; production code reads through DbScope.
    /// </summary>
    internal string SchemaName => _schemaName ?? "";

    private TrackingScope DbScope => new TrackingScope
    {
        ProductName = _product.Name,
        TemplateName = _template.Name,
        Server = _server,
        DatabaseName = _databaseName,
        SchemaName = _schemaName ?? ""
    };

    public void Execute()
    {
        SafeProgressLog("Begin Quench");

        var checkpointSummary = _checkpointing?.GetDatabaseCheckpointSummary(DbScope) ?? DatabaseCheckpointSummary.Empty;
        if (checkpointSummary.HasAnyCompleted)
            SafeProgressLog($"  [{_databaseName}] Resuming from checkpoint (Completed Steps: {checkpointSummary.CompletedSteps}, Completed Scripts: {checkpointSummary.TotalCompletedScripts})");

        // Schema templates clone their script collections per iteration so {{SchemaName}}-substituted
        // batches don't pollute sibling iterations of the same template that share the in-memory
        // Template instance. Regular templates point straight at _template's collections (no clone) —
        // preserves today's behavior bit-for-bit on the regular-template path.
        PrepareIterationContent();

        try
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            command.CommandTimeout = 0;

            // SQL Server and PostgreSQL use multiple connections for parallel operations
            IDbConnection tableConnection = null;
            IDbCommand tableCommand = null;
            IDbConnection objectsConnection = null;
            IDbCommand objectsCommand = null;
            IDbConnection silentConnection = null;
            IDbCommand silentCommand = null;

            if (_product.Platform != Platform.MySQL)
            {
                tableConnection = GetConnection();
                tableCommand = tableConnection.CreateCommand();
                tableCommand.CommandTimeout = 0;
                objectsConnection = GetConnection(fireInfoMessageEventOnUserErrors: false);
                objectsCommand = objectsConnection.CreateCommand();
                objectsCommand.CommandTimeout = 0;
                silentConnection = GetConnection(ignoreInfoMessages: true);
                silentCommand = silentConnection.CreateCommand();
                silentCommand.CommandTimeout = 0;
            }

            try
            {
                // MySQL: switch to target database
                if (_product.Platform == Platform.MySQL)
                {
                    command.CommandText = QuoteUseDatabase(_databaseName);
                    command.ExecuteNonQuery();
                }

                var effectiveTableCmd = tableCommand ?? command;
                var effectiveObjectsCmd = objectsCommand ?? command;
                var effectiveSilentCmd = silentCommand ?? command;

                // Step: Kindle the forge
                if (!_suppressKindling)
                {
                    _checkpointing.Track(DbScope, "KindleForge", () =>
                    {
                        SafeProgressLog("  Kindling the forge");
                        ForgeKindler.KindleTheForge(effectiveSilentCmd, _product.Platform);
                    });
                }

                // Step: Validate baseline. Resolved against per-iteration tokens (BaselineValidationScript
                // may reference {{SchemaName}} for schema templates).
                if (!string.IsNullOrWhiteSpace(_iterationBaselineValidationScript))
                {
                    _checkpointing.Track(DbScope, "ValidateBaseline", () =>
                    {
                        SafeProgressLog("  Validate Baseline");
                        command.CommandText = _iterationBaselineValidationScript;
                        if (!Convert.ToBoolean(command.ExecuteScalar()))
                            throw new Exception("Invalid baseline for this release");
                    });
                }

                // Step: Object scripts without unresolved tokens
                var nonTokenScripts = _iterationObjectScripts.Where(s => s.Batches.All(b => !b.Contains("{{") && !b.Contains("}}"))).ToList();
                if (!IsWhatIf)
                {
                    SafeProgressLog("  Quenching object scripts without unresolved tokens");
                    QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd, nonTokenScripts, false, DatabaseScriptSlot.Object);
                }
                else
                {
                    SafeProgressLog("  [WhatIf] Object scripts without unresolved tokens:");
                    WhatIfLogScripts(nonTokenScripts, DatabaseScriptSlot.Object);
                }

                // Step: Missing tables and columns
                if (!_template.IndexOnlyTableQuenches && _updateTables)
                {
                    _checkpointing.Track(DbScope, "MissingTablesAndColumns", () => QuenchMissingTablesAndColumns(effectiveTableCmd));
                }

                if (!IsWhatIf)
                {
                    SafeProgressLog("  Quenching object scripts without query tokens");
                    QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd,
                        _iterationObjectScripts.Where(s => s.Batches.All(b => !b.Contains("{{") && !b.Contains("}}"))).ToList(),
                        false, DatabaseScriptSlot.Object);

                    ResolveAndApplyQueryTokens(effectiveSilentCmd);

                    SafeProgressLog("  Quenching before database scripts");
                    QuenchTemplateScriptsWithCheckpoint(command, "Before", _iterationBeforeScripts, DatabaseScriptSlot.Before);
                }
                else
                {
                    SafeProgressLog("  [WhatIf] Object scripts without query tokens:");
                    WhatIfLogScripts(_iterationObjectScripts.Where(s => s.Batches.All(b => !b.Contains("{{") && !b.Contains("}}"))).ToList(), DatabaseScriptSlot.Object);

                    if (_template.QueryTokens.Count > 0)
                        SafeProgressLog("  [WhatIf] Would resolve template query tokens");

                    SafeProgressLog("  [WhatIf] Before database scripts:");
                    WhatIfLogTemplateScripts(command, "Before", _iterationBeforeScripts, DatabaseScriptSlot.Before);
                }

                // Step: Modified tables
                if (!_template.IndexOnlyTableQuenches && _updateTables)
                {
                    _checkpointing.Track(DbScope, "ModifiedTables", () => QuenchModifiedTables(effectiveTableCmd));
                }

                if (!IsWhatIf)
                {
                    SafeProgressLog("  Quenching object scripts");
                    QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd, _iterationAfterTablesObjectScripts, false, DatabaseScriptSlot.AfterTablesObject);

                    SafeProgressLog("  Quenching between table and keys scripts");
                    QuenchTemplateScriptsWithCheckpoint(command, "Between Table And Keys", _iterationBetweenTablesAndKeysScripts, DatabaseScriptSlot.BetweenTablesAndKeys);
                }
                else
                {
                    SafeProgressLog("  [WhatIf] Object scripts (after tables):");
                    WhatIfLogScripts(_iterationAfterTablesObjectScripts, DatabaseScriptSlot.AfterTablesObject);

                    SafeProgressLog("  [WhatIf] Between table and keys scripts:");
                    WhatIfLogTemplateScripts(command, "Between Table And Keys", _iterationBetweenTablesAndKeysScripts, DatabaseScriptSlot.BetweenTablesAndKeys);
                }

                // Step: Indexes and constraints
                if (_updateTables)
                {
                    _checkpointing.Track(DbScope, "IndexesAndConstraints", () => QuenchIndexesAndConstraints(effectiveTableCmd));
                }

                // MySQL: cleanup temp tables after index quench
                if (_product.Platform == Platform.MySQL)
                    CleanupMySqlTempTables(command);

                if (!IsWhatIf)
                {
                    SafeProgressLog("  Quenching after table scripts");
                    QuenchTemplateScriptsWithCheckpoint(command, "After Table", _iterationAfterTableScripts, DatabaseScriptSlot.AfterTable);

                    if (_iterationObjectScripts.Union(_iterationAfterTablesObjectScripts).Any(s => !s.HasBeenQuenched))
                    {
                        SafeProgressLog("  Quenching object scripts");
                        QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd, _iterationAfterTablesObjectScripts.ToList(), true, DatabaseScriptSlot.AfterTablesObject);
                    }

                    if (_deliverData)
                    {
                        // Data delivery — Pro determines behavior based on license.
                        _checkpointing.Track(DbScope, "TableDataDelivery", () =>
                        {
                            // Register platform-specific script helper if not already registered
                            if (FactoryContainer.Resolve<IMergeScriptHelper>() is not MergeScriptHelperAdapter adapter || adapter.Platform != _product.Platform)
                                FactoryContainer.Register<IMergeScriptHelper>(new MergeScriptHelperAdapter(_product.Platform));

                            DataDeliveryProcessor.GetFromFactory().DeliverTables(new DataDeliveryContext
                            {
                                Tables = _template.Tables.Cast<IDeliverableTable>().ToList(),
                                Command = effectiveSilentCmd,
                                Platform = _product.Platform.ToString(),
                                DatabaseName = _databaseName,
                                TemplateRootPath = Path.GetDirectoryName(_template.FilePath) ?? "",
                                ScriptHelper = FactoryContainer.Resolve<IMergeScriptHelper>(),
                                ReadFileContent = path => ProductFileWrapper.GetFromFactory().ReadAllText(path),
                                ExecuteScript = (name, script) => { effectiveSilentCmd.CommandText = script; effectiveSilentCmd.ExecuteNonQuery(); },
                                ProgressLog = SafeProgressLog,
                                ProgressLogError = SafeProgressLogError,
                                WhatIf = IsWhatIf
                            });
                        });

                        if (_iterationObjectScripts.Union(_iterationTableDataScripts).Any(s => !s.HasBeenQuenched))
                        {
                            SafeProgressLog("  Quenching table data scripts");
                            QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd, _iterationTableDataScripts.ToList(), true, DatabaseScriptSlot.TableData);
                        }
                    }

                    // Foreign keys after data delivery (all platforms)
                    if (!_template.IndexOnlyTableQuenches && _updateTables)
                    {
                        _checkpointing.Track(DbScope, "ForeignKeys", () =>
                        {
                            QuenchForeignKeys(effectiveTableCmd);
                            if (_product.Platform == Platform.MySQL)
                                CleanupMySqlTempTables(command);
                        });
                    }

                    // Step: Materialized views (PostgreSQL only)
                    if (_product.Platform == Platform.PostgreSQL && _template.MaterializedViews.Count > 0)
                    {
                        _checkpointing.Track(DbScope, "MaterializedViewQuench", () => QuenchMaterializedViews(effectiveTableCmd));
                    }

                    // Step: Indexed views (SQL Server only)
                    if (_product.Platform == Platform.SqlServer && _template.IndexedViews.Count > 0)
                    {
                        _checkpointing.Track(DbScope, "IndexedViewQuench", () => QuenchIndexedViews(effectiveTableCmd));
                    }

                    SafeProgressLog("  Quenching after database scripts");
                    QuenchTemplateScriptsWithCheckpoint(command, "After", _iterationAfterScripts, DatabaseScriptSlot.After);

                    if (!string.IsNullOrWhiteSpace(_iterationVersionStampScript))
                    {
                        _checkpointing.Track(DbScope, "VersionStamp", () =>
                        {
                            SafeProgressLog("  Stamp version");
                            command.CommandText = _iterationVersionStampScript;
                            ExecuteNonQueryHandlingMessages(command);
                        });
                    }
                }
                else
                {
                    SafeProgressLog("  [WhatIf] After table scripts:");
                    WhatIfLogTemplateScripts(command, "After Table", _iterationAfterTableScripts, DatabaseScriptSlot.AfterTable);

                    SafeProgressLog("  [WhatIf] Object scripts (final pass):");
                    WhatIfLogScripts(_iterationAfterTablesObjectScripts.ToList(), DatabaseScriptSlot.AfterTablesObject);

                    if (_deliverData)
                    {
                        SafeProgressLog("  [WhatIf] Table data delivery:");
                        WhatIfLogTableDataScripts(_iterationTableDataScripts.ToList());

                        if (FactoryContainer.Resolve<IMergeScriptHelper>() is not MergeScriptHelperAdapter whatIfAdapter || whatIfAdapter.Platform != _product.Platform)
                            FactoryContainer.Register<IMergeScriptHelper>(new MergeScriptHelperAdapter(_product.Platform));

                        DataDeliveryProcessor.GetFromFactory().DeliverTables(new DataDeliveryContext
                        {
                            Tables = _template.Tables.Cast<IDeliverableTable>().ToList(),
                            Command = command,
                            Platform = _product.Platform.ToString(),
                            DatabaseName = _databaseName,
                            TemplateRootPath = Path.GetDirectoryName(_template.FilePath) ?? "",
                            ScriptHelper = FactoryContainer.Resolve<IMergeScriptHelper>(),
                            ReadFileContent = path => ProductFileWrapper.GetFromFactory().ReadAllText(path),
                            ExecuteScript = (_, _) => { },
                            ProgressLog = SafeProgressLog,
                            ProgressLogError = SafeProgressLogError,
                            WhatIf = true
                        });
                    }

                    // WhatIf: Materialized views (PostgreSQL only)
                    if (_product.Platform == Platform.PostgreSQL && _template.MaterializedViews.Count > 0)
                    {
                        SafeProgressLog("  [WhatIf] Would quench materialized views");
                    }

                    // WhatIf: Indexed views (SQL Server only)
                    if (_product.Platform == Platform.SqlServer && _template.IndexedViews.Count > 0)
                    {
                        SafeProgressLog($"  [WhatIf] Would quench {_template.IndexedViews.Count} indexed view(s)");
                    }

                    SafeProgressLog("  [WhatIf] After database scripts:");
                    WhatIfLogTemplateScripts(command, "After", _iterationAfterScripts, DatabaseScriptSlot.After);

                    if (!string.IsNullOrWhiteSpace(_iterationVersionStampScript))
                        SafeProgressLog("  [WhatIf] Would stamp version");
                }
            }
            finally
            {
                _statusMonitor?.Dispose();
                _statusMonitor = null;
                connection.Close();
                tableConnection?.Close();
                objectsConnection?.Close();
                silentConnection?.Close();
            }

            SafeProgressLog("Successfully Quenched");
            QuenchSuccessful = true;
        }
        catch (Exception e)
        {
            SafeProgressLogError($"FAILED to quench:\r\n{e.Message}");
        }
    }

    #region Per-Iteration Content (Schema Templates)

    /// <summary>
    /// Populates the per-iteration script collections and validation-script strings.
    /// <para>
    /// For <b>regular templates</b>: the iteration fields point directly at the
    /// shared <c>_template.&lt;slot&gt;Scripts</c> collections — no clone, no
    /// substitution, behaviour identical to the pre-schema-templates engine.
    /// </para>
    /// <para>
    /// For <b>schema templates</b>: every script collection is deep-cloned so that
    /// the per-iteration <c>{{SchemaName}}</c> substitution applied below cannot
    /// pollute sibling iterations that share the same in-memory <see cref="Template"/>.
    /// <c>{{SchemaName}}</c> is then substituted into every batch of every cloned
    /// script and into the <see cref="Template.BaselineValidationScript"/> /
    /// <see cref="Template.VersionStampScript"/> strings. Iteration-scoped query
    /// tokens are NOT resolved here — they need a live connection and run inside
    /// <see cref="ResolveAndApplyQueryTokens"/>.
    /// </para>
    /// </summary>
    private void PrepareIterationContent()
    {
        if (string.IsNullOrEmpty(_schemaName))
        {
            // Regular templates intentionally use the existing computed-script accessors (which
            // allocate a fresh List<SqlScript> per access but share the SqlScript references).
            // This preserves today's shared-state behavior across parallel iterations — multiple
            // regular-template DatabaseQuench instances on the same Template observe each other's
            // mutations to SqlScript.HasBeenQuenched, which is how the engine avoids re-running an
            // already-applied script on a sibling DB inside the same template. A future refactor
            // that switches to per-iteration clones for regular templates would silently change
            // observable script-mutation semantics — don't "fix" the aliasing without explicitly
            // re-validating the regular-template parallel-DB scenarios.
            _iterationBeforeScripts = _template.BeforeScripts;
            _iterationObjectScripts = _template.ObjectScripts;
            _iterationAfterTablesObjectScripts = _template.AfterTablesObjectScripts;
            _iterationBetweenTablesAndKeysScripts = _template.BetweenTablesAndKeysScripts;
            _iterationAfterTableScripts = _template.AfterTableScripts;
            _iterationTableDataScripts = _template.TableDataScripts;
            _iterationAfterScripts = _template.AfterScripts;
            _iterationBaselineValidationScript = _template.BaselineValidationScript;
            _iterationVersionStampScript = _template.VersionStampScript;
            return;
        }

        // Schema-template iteration: deep-clone every script collection so {{SchemaName}}
        // substitution (and later, query-token substitution) operates on this iteration's
        // own copies and never mutates the shared Template instance.
        var schemaNameTokens = new List<KeyValuePair<string, string>>
        {
            new("SchemaName", _schemaName)
        };

        _iterationBeforeScripts = CloneAndSubstitute(_template.BeforeScripts, schemaNameTokens);
        _iterationObjectScripts = CloneAndSubstitute(_template.ObjectScripts, schemaNameTokens);
        _iterationAfterTablesObjectScripts = CloneAndSubstitute(_template.AfterTablesObjectScripts, schemaNameTokens);
        _iterationBetweenTablesAndKeysScripts = CloneAndSubstitute(_template.BetweenTablesAndKeysScripts, schemaNameTokens);
        _iterationAfterTableScripts = CloneAndSubstitute(_template.AfterTableScripts, schemaNameTokens);
        _iterationTableDataScripts = CloneAndSubstitute(_template.TableDataScripts, schemaNameTokens);
        _iterationAfterScripts = CloneAndSubstitute(_template.AfterScripts, schemaNameTokens);

        _iterationBaselineValidationScript = SqlScript.TokenReplace(
            _template.BaselineValidationScript ?? "", schemaNameTokens, _product.Platform);
        _iterationVersionStampScript = SqlScript.TokenReplace(
            _template.VersionStampScript ?? "", schemaNameTokens, _product.Platform);
    }

    private static List<SqlScript> CloneAndSubstitute(
        List<SqlScript> source, List<KeyValuePair<string, string>> tokens)
    {
        var cloned = source.Select(s => s.Clone()).ToList();
        foreach (var script in cloned)
            script.ReplaceQueryTokens(tokens);
        return cloned;
    }

    /// <summary>
    /// Resolves the template's <c>&lt;*Query*&gt;</c> tokens against the live silent command and
    /// applies the resolved values to every script in this iteration. <para>
    /// Regular templates keep today's behavior — the <c>_template.QueryTokens</c> dict is mutated
    /// in place and the substitution targets the shared script objects. (Idempotent because
    /// SqlScript.TokenReplace only touches placeholders that still exist in the batch text, so a
    /// second sibling DB's pass over already-resolved batches is a no-op.)
    /// </para>
    /// <para>
    /// Schema templates take a different path: query tokens are resolved into a per-iteration
    /// dictionary (NOT the shared <c>_template.QueryTokens</c>), with iteration-scoped tokens
    /// (those whose body references <c>{{SchemaName}}</c> directly or transitively per
    /// <see cref="Template.IsIterationScoped"/>) having <c>{{SchemaName}}</c> substituted in
    /// their bodies first. The resolved per-iteration map is then applied to the cloned
    /// scripts owned by this iteration.
    /// </para>
    /// </summary>
    private void ResolveAndApplyQueryTokens(IDbCommand silentCmd)
    {
        if (_template.QueryTokens.Count == 0) return;

        if (string.IsNullOrEmpty(_schemaName))
        {
            // Regular-template path: today's behavior — mutate the shared dict + scripts.
            SafeProgressLog("  Resolving template query tokens");
            TokenHelper.ResolveQueryTokens(_template.QueryTokens, _template.NonQueryTokens.ToList(),
                silentCmd, Path.GetDirectoryName(_template.FilePath), _product.Platform);
            foreach (var script in _template.ScriptFolders.SelectMany(f => f.Scripts))
                script.ReplaceQueryTokens(_template.QueryTokens.ToList());
            return;
        }

        // Schema-template path: resolve into a per-iteration copy so the next iteration of
        // the same template starts from the template's pristine <*Query*> bodies.
        SafeProgressLog("  Resolving template query tokens (per-iteration)");

        // 1. Build the per-iteration NonQueryTokens list — any iteration-scoped non-query token
        //    body (one containing {{SchemaName}} directly or transitively) needs the substitution
        //    applied BEFORE it's fed into ResolveQueryTokens (where it's inlined into <*Query*>
        //    bodies). Other non-query tokens pass through unchanged.
        var iterationNonQueryTokens = _template.NonQueryTokens
            .Select(kv =>
            {
                if (_template.IsIterationScoped(kv.Key) && !string.IsNullOrEmpty(kv.Value))
                {
                    return new KeyValuePair<string, string>(kv.Key,
                        kv.Value.Replace("{{SchemaName}}", _schemaName, StringComparison.OrdinalIgnoreCase));
                }
                return kv;
            })
            .ToList();

        // 2. Build the per-iteration QueryTokens dict, substituting {{SchemaName}} into the
        //    bodies of iteration-scoped tokens BEFORE the query runs. Per-DB query tokens
        //    pass through unchanged; ResolveQueryTokens still executes them against the
        //    connection, which means they get re-run per iteration too — slice 3 keeps the
        //    fan-out cost simple (one execution per query token per work unit). Slice 4+
        //    can introduce per-DB caching if profiling shows it matters.
        var iterationQueryTokens = new Dictionary<string, string>(_template.QueryTokens.Count);
        foreach (var kv in _template.QueryTokens)
        {
            var body = kv.Value;
            if (_template.IsIterationScoped(kv.Key) && !string.IsNullOrEmpty(body))
                body = body.Replace("{{SchemaName}}", _schemaName, StringComparison.OrdinalIgnoreCase);
            iterationQueryTokens[kv.Key] = body;
        }

        // 3. Resolve all query tokens against the live connection. ResolveQueryTokens
        //    mutates the supplied dictionary in place, so we hand it our iteration-scoped
        //    copy and leave _template.QueryTokens untouched for the next iteration.
        TokenHelper.ResolveQueryTokens(iterationQueryTokens, iterationNonQueryTokens,
            silentCmd, Path.GetDirectoryName(_template.FilePath), _product.Platform);

        // 4. Apply resolved values to this iteration's cloned scripts.
        var tokenList = iterationQueryTokens.ToList();
        ApplyToIteration(_iterationBeforeScripts, tokenList);
        ApplyToIteration(_iterationObjectScripts, tokenList);
        ApplyToIteration(_iterationAfterTablesObjectScripts, tokenList);
        ApplyToIteration(_iterationBetweenTablesAndKeysScripts, tokenList);
        ApplyToIteration(_iterationAfterTableScripts, tokenList);
        ApplyToIteration(_iterationTableDataScripts, tokenList);
        ApplyToIteration(_iterationAfterScripts, tokenList);
    }

    private static void ApplyToIteration(
        List<SqlScript> scripts, List<KeyValuePair<string, string>> tokens)
    {
        if (scripts == null || scripts.Count == 0 || tokens.Count == 0) return;
        foreach (var script in scripts)
            script.ReplaceQueryTokens(tokens);
    }

    #endregion

    #region Platform Dispatch Helpers

    /// <summary>
    /// Returns true if the current run is WhatIf-only.
    /// SqlServer/MySQL use "1"; PostgreSQL uses "true".
    /// </summary>
    internal bool IsWhatIf => _product.Platform == Platform.PostgreSQL
        ? _whatIfOnly == "true"
        : _whatIfOnly == "1";

    /// <summary>
    /// Formats a boolean value for platform-specific SQL parameters.
    /// </summary>
    internal string FormatBooleanFlag(bool value) => _product.Platform switch
    {
        Platform.PostgreSQL => value ? "true" : "false",
        _ => value ? "1" : "0"
    };

    /// <summary>
    /// Quotes a database name for USE statement per platform.
    /// </summary>
    internal static string QuoteUseDatabase(string dbName, Platform platform) => platform switch
    {
        Platform.SqlServer => $"USE [{dbName}]",
        Platform.PostgreSQL => dbName, // PostgreSQL uses ChangeDatabase API
        Platform.MySQL => $"USE `{dbName}`",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
    };

    private string QuoteUseDatabase(string dbName) => QuoteUseDatabase(dbName, _product.Platform);

    /// <summary>
    /// Quotes an identifier per platform.
    /// </summary>
    internal static string QuoteIdentifier(string name, Platform platform) => platform switch
    {
        Platform.SqlServer => $"[{name}]",
        Platform.PostgreSQL => $"\"{name}\"",
        Platform.MySQL => $"`{name}`",
        _ => name
    };

    internal static string EscapeSqlLiteral(string value) => value?.Replace("'", "''") ?? "";

    /// <summary>
    /// Gets the delete SQL for CompletedMigrationScripts per platform. Strict equality on
    /// BOTH template_name and schema_name so a prune in one (template, schema) iteration
    /// can never delete rows owned by a different iteration — including legacy blank-template
    /// rows that may be shared across multiple templates in the same product. (Reads are
    /// permissive on template_name to pick up those legacy rows; writes/deletes are strict.)
    /// </summary>
    internal string GetDeleteCompletedScriptSql(string productName, string slot, string obsoleteScript, string templateName, string schemaName) => _product.Platform switch
    {
        Platform.SqlServer => $"DELETE SchemaSmith.CompletedMigrationScripts WHERE [ProductName] = '{EscapeSqlLiteral(productName)}' AND [QuenchSlot] = '{EscapeSqlLiteral(slot)}' AND [ScriptPath] = '{EscapeSqlLiteral(obsoleteScript)}' AND [template_name] = '{EscapeSqlLiteral(templateName)}' AND [schema_name] = '{EscapeSqlLiteral(schemaName)}'",
        Platform.PostgreSQL => $"DELETE FROM \"SchemaSmith\".\"CompletedMigrationScripts\" WHERE \"ProductName\" = '{EscapeSqlLiteral(productName)}' AND \"QuenchSlot\" = '{EscapeSqlLiteral(slot)}' AND \"ScriptPath\" = '{EscapeSqlLiteral(obsoleteScript)}' AND template_name = '{EscapeSqlLiteral(templateName)}' AND schema_name = '{EscapeSqlLiteral(schemaName)}'",
        Platform.MySQL => $"DELETE FROM `SchemaSmith_CompletedMigrationScripts` WHERE `ProductName` = '{EscapeSqlLiteral(productName)}' AND `QuenchSlot` = '{EscapeSqlLiteral(slot)}' AND `ScriptPath` = '{EscapeSqlLiteral(obsoleteScript)}' AND `template_name` = '{EscapeSqlLiteral(templateName)}' AND `schema_name` = '{EscapeSqlLiteral(schemaName)}'",
        _ => throw new ArgumentOutOfRangeException()
    };

    /// <summary>
    /// Gets the SELECT SQL for completed migration scripts per platform. Same scope-aware
    /// predicate shape as the DELETE builder — permissive template_name, strict schema_name.
    /// </summary>
    internal string GetSelectCompletedScriptsSql(string productName, string slot, string templateName, string schemaName) => _product.Platform switch
    {
        Platform.SqlServer => $"SELECT [ScriptPath] FROM SchemaSmith.CompletedMigrationScripts WITH (NOLOCK) WHERE [ProductName] = '{EscapeSqlLiteral(productName)}' AND [QuenchSlot] = '{EscapeSqlLiteral(slot)}' AND [template_name] IN ('', '{EscapeSqlLiteral(templateName)}') AND [schema_name] = '{EscapeSqlLiteral(schemaName)}'",
        Platform.PostgreSQL => $"SELECT \"ScriptPath\" FROM \"SchemaSmith\".\"CompletedMigrationScripts\" WHERE \"ProductName\" = '{EscapeSqlLiteral(productName)}' AND \"QuenchSlot\" = '{EscapeSqlLiteral(slot)}' AND template_name IN ('', '{EscapeSqlLiteral(templateName)}') AND schema_name = '{EscapeSqlLiteral(schemaName)}'",
        Platform.MySQL => $"SELECT `ScriptPath` FROM `SchemaSmith_CompletedMigrationScripts` WHERE `ProductName` = '{EscapeSqlLiteral(productName)}' AND `QuenchSlot` = '{EscapeSqlLiteral(slot)}' AND `template_name` IN ('', '{EscapeSqlLiteral(templateName)}') AND `schema_name` = '{EscapeSqlLiteral(schemaName)}'",
        _ => throw new ArgumentOutOfRangeException()
    };

    /// <summary>
    /// Gets the INSERT SQL for completed migration scripts per platform. Always writes the
    /// actual template_name + schema_name values from the active scope (legacy blank rows
    /// only arrive from pre-extension databases; new writes always have real values).
    /// </summary>
    internal string GetInsertCompletedScriptSql(string scriptPath, string productName, string slot, string templateName, string schemaName) => _product.Platform switch
    {
        Platform.SqlServer => $"INSERT SchemaSmith.CompletedMigrationScripts ([ScriptPath], [ProductName], [QuenchSlot], [template_name], [schema_name]) VALUES('{EscapeSqlLiteral(scriptPath)}', '{EscapeSqlLiteral(productName)}', '{EscapeSqlLiteral(slot)}', '{EscapeSqlLiteral(templateName)}', '{EscapeSqlLiteral(schemaName)}')",
        Platform.PostgreSQL => $"INSERT INTO \"SchemaSmith\".\"CompletedMigrationScripts\" (\"ScriptPath\", \"ProductName\", \"QuenchSlot\", template_name, schema_name) VALUES('{EscapeSqlLiteral(scriptPath)}', '{EscapeSqlLiteral(productName)}', '{EscapeSqlLiteral(slot)}', '{EscapeSqlLiteral(templateName)}', '{EscapeSqlLiteral(schemaName)}')",
        Platform.MySQL => $"INSERT INTO `SchemaSmith_CompletedMigrationScripts` (`ScriptPath`, `ProductName`, `QuenchSlot`, `template_name`, `schema_name`) VALUES('{EscapeSqlLiteral(scriptPath)}', '{EscapeSqlLiteral(productName)}', '{EscapeSqlLiteral(slot)}', '{EscapeSqlLiteral(templateName)}', '{EscapeSqlLiteral(schemaName)}')",
        _ => throw new ArgumentOutOfRangeException()
    };

    /// <summary>
    /// TRANSITIONAL (slice 2 of schema-templates): claim ownership of legacy blank-template
    /// tracking rows for the current (template, schema) scope. UPDATEs template_name from
    /// '' to @template on rows whose ScriptPath is in the provided list (the current template's
    /// on-disk script set). Scoping to on-disk paths prevents mis-attributing a row that was
    /// originally tracking another template's work.
    /// </summary>
    /// <remarks>
    /// Pairs with the permissive template_name IN ('', @template) SELECT. Both mechanisms
    /// are transitional aids for pre-extension data; both go away once the legacy data is
    /// migrated. Tracking item in the Community roadmap under "Schema templates — slice 2
    /// legacy-data migration cleanup".
    /// </remarks>
    internal string GetClaimLegacyTrackingRowsSql(string productName, string slot, string templateName, string schemaName, IReadOnlyList<string> scriptPaths)
    {
        var inList = string.Join(",", scriptPaths.Select(p => $"'{EscapeSqlLiteral(p)}'"));
        return _product.Platform switch
        {
            Platform.SqlServer => $"UPDATE SchemaSmith.CompletedMigrationScripts SET [template_name] = '{EscapeSqlLiteral(templateName)}' WHERE [ProductName] = '{EscapeSqlLiteral(productName)}' AND [QuenchSlot] = '{EscapeSqlLiteral(slot)}' AND [template_name] = '' AND [schema_name] = '{EscapeSqlLiteral(schemaName)}' AND [ScriptPath] IN ({inList})",
            Platform.PostgreSQL => $"UPDATE \"SchemaSmith\".\"CompletedMigrationScripts\" SET template_name = '{EscapeSqlLiteral(templateName)}' WHERE \"ProductName\" = '{EscapeSqlLiteral(productName)}' AND \"QuenchSlot\" = '{EscapeSqlLiteral(slot)}' AND template_name = '' AND schema_name = '{EscapeSqlLiteral(schemaName)}' AND \"ScriptPath\" IN ({inList})",
            Platform.MySQL => $"UPDATE `SchemaSmith_CompletedMigrationScripts` SET `template_name` = '{EscapeSqlLiteral(templateName)}' WHERE `ProductName` = '{EscapeSqlLiteral(productName)}' AND `QuenchSlot` = '{EscapeSqlLiteral(slot)}' AND `template_name` = '' AND `schema_name` = '{EscapeSqlLiteral(schemaName)}' AND `ScriptPath` IN ({inList})",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    #endregion

    #region Platform-Specific Table Quench SQL

    private void QuenchMissingTablesAndColumns(IDbCommand tableCommand)
    {
        if (_product.Platform == Platform.MySQL && _template.Tables.Count == 0)
            return;

        SafeProgressLog("  Quenching missing tables and columns");

        switch (_product.Platform)
        {
            case Platform.SqlServer:
            {
                var updateFillFactor = _template.UpdateFillFactor ? "1" : "0";
                tableCommand.CommandText = $@"
DECLARE @TableDefinitions VARCHAR(MAX)= '{_template.TableSchema.Replace("'", "''")}',
        @UpdateFillFactor BIT = {updateFillFactor}
{ForgeKindler.GetParseTableJsonScript(Platform.SqlServer)}
EXEC [{_databaseName}].SchemaSmith.MissingTableAndColumnQuench @WhatIf = {_whatIfOnly}";
                break;
            }
            case Platform.PostgreSQL:
            {
                tableCommand.CommandText = $@"
DO $$
DECLARE
  p_UpdateFillFactor BOOL = {_template.UpdateFillFactor.ToString().ToLower()};
  table_json JSON = '{_template.TableSchema.Replace("'", "''")}';
  sql_script TEXT = '';
BEGIN
{ForgeKindler.GetParseTableJsonScript(Platform.PostgreSQL)}
END $$ LANGUAGE plpgsql;

CALL ""SchemaSmith"".""MissingTableAndColumnQuench""(p_WhatIf := {_whatIfOnly})";
                break;
            }
            case Platform.MySQL:
            {
                ParseMySqlTableJson(tableCommand);
                var whatIf = _whatIfOnly == "1" ? 1 : 0;
                tableCommand.CommandText = $"CALL SchemaSmith_MissingTableAndColumnQuench('{_databaseName.Replace("'", "''")}', {whatIf})";
                break;
            }
        }

        _debugFileLocation = $"SchemaQuench - Quench Missing Tables And Columns {_server}.{_databaseName}.sql";
        LogSqlScript(_debugFileLocation, tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand);
        _debugFileLocation = "";
    }

    private void QuenchModifiedTables(IDbCommand tableCommand)
    {
        if (_product.Platform == Platform.MySQL && _template.Tables.Count == 0)
            return;

        SafeProgressLog("  Quenching modified tables");

        switch (_product.Platform)
        {
            case Platform.SqlServer:
                tableCommand.CommandText = $"EXEC [{_databaseName}].SchemaSmith.ModifiedTableQuench @ProductName = '{_product.Name}', @DropUnknownIndexes = {_dropUnknownIndexes}, @WhatIf = {_whatIfOnly}, @DropTablesRemovedFromProduct = {_dropRemovedTables}";
                break;
            case Platform.PostgreSQL:
                tableCommand.CommandText = $@"
CALL ""SchemaSmith"".""ValidateTableOwnership""(p_ProductName := '{_product.Name}', p_WhatIf := {_whatIfOnly});
CALL ""SchemaSmith"".""ModifiedTableQuench""(p_DropUnknownIndexes := {_dropUnknownIndexes}, p_WhatIf := {_whatIfOnly}, p_DropTablesRemovedFromProduct := {_dropRemovedTables});";
                break;
            case Platform.MySQL:
            {
                if (!MySqlTempTablesExist(tableCommand))
                    ParseMySqlTableJson(tableCommand);
                var whatIf = _whatIfOnly == "1" ? 1 : 0;
                var dropRemoved = _dropRemovedTables == "1" ? 1 : 0;
                tableCommand.CommandText = $"CALL SchemaSmith_ModifiedTableQuench('{_product.Name.Replace("'", "''")}', '{_databaseName.Replace("'", "''")}', {whatIf}, {dropRemoved})";
                break;
            }
        }

        _debugFileLocation = $"SchemaQuench - Quench Modified Tables {_server}.{_databaseName}.sql";
        LogSqlScript(_debugFileLocation, tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand);
        _debugFileLocation = "";
    }

    private void QuenchIndexesAndConstraints(IDbCommand tableCommand)
    {
        if (_product.Platform == Platform.MySQL && _template.Tables.Count == 0)
            return;

        SafeProgressLog($"  Quenching indexes{(_template.IndexOnlyTableQuenches ? "" : " and constraints")}");

        switch (_product.Platform)
        {
            case Platform.SqlServer:
            {
                var updateFillFactor = _template.UpdateFillFactor ? "1" : "0";
                tableCommand.CommandText = _template.IndexOnlyTableQuenches
                    ? $"EXEC [{_databaseName}].SchemaSmith.IndexOnlyQuench @ProductName = '{_product.Name}', @TableDefinitions = '{_template.TableSchema.Replace("'", "''")}', @DropUnknownIndexes = {_dropUnknownIndexes}, @UpdateFillFactor = {updateFillFactor}, @WhatIf = {_whatIfOnly}"
                    : $"EXEC [{_databaseName}].SchemaSmith.MissingIndexesAndConstraintsQuench @ProductName = '{_product.Name}', @WhatIf = {_whatIfOnly}";
                break;
            }
            case Platform.PostgreSQL:
                tableCommand.CommandText = _template.IndexOnlyTableQuenches
                    ? $@"
CALL ""SchemaSmith"".""IndexOnlyQuench""(p_TableDefinitions := '{_template.TableSchema.Replace("'", "''")}', p_DropUnknownIndexes := {_dropUnknownIndexes}, p_WhatIf := {_whatIfOnly}, p_UpdateFillFactor := {_template.UpdateFillFactor.ToString().ToLower()});
CALL ""SchemaSmith"".""FixupIndexOwnership""(p_ProductName := '{_product.Name}');
"
                    : $@"
CALL ""SchemaSmith"".""MissingIndexesAndConstraintsQuench""(p_WhatIf := {_whatIfOnly});
CALL ""SchemaSmith"".""FixupTableOwnership""(p_ProductName := '{_product.Name}');
CALL ""SchemaSmith"".""FixupIndexOwnership""(p_ProductName := '{_product.Name}');
";
                break;
            case Platform.MySQL:
            {
                if (!MySqlTempTablesExist(tableCommand))
                    ParseMySqlTableJson(tableCommand);
                var whatIf = _whatIfOnly == "1" ? 1 : 0;
                var dropUnknown = _dropUnknownIndexes == "1" ? 1 : 0;
                tableCommand.CommandText = _template.IndexOnlyTableQuenches
                    ? $"CALL SchemaSmith_IndexOnlyQuench('{_product.Name.Replace("'", "''")}', '{_databaseName.Replace("'", "''")}', {whatIf}, {dropUnknown})"
                    : $"CALL SchemaSmith_MissingIndexesAndConstraintsQuench('{_product.Name.Replace("'", "''")}', '{_databaseName.Replace("'", "''")}', {whatIf}, {dropUnknown})";
                break;
            }
        }

        _debugFileLocation = $"SchemaQuench - Quench Indexes {_server}.{_databaseName}.sql";
        LogSqlScript(_debugFileLocation, tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand);
        _debugFileLocation = "";
    }

    private void QuenchForeignKeys(IDbCommand tableCommand)
    {
        if (_template.Tables.Count == 0)
            return;

        SafeProgressLog("  Quenching foreign keys");

        switch (_product.Platform)
        {
            case Platform.SqlServer:
                tableCommand.CommandText = $"EXEC [{_databaseName}].SchemaSmith.ForeignKeyQuench @ProductName = '{_product.Name}', @WhatIf = {_whatIfOnly}";
                break;
            case Platform.PostgreSQL:
                tableCommand.CommandText = $@"CALL ""SchemaSmith"".""ForeignKeyQuench""(p_WhatIf := {_whatIfOnly});";
                break;
            case Platform.MySQL:
            {
                if (!MySqlTempTablesExist(tableCommand))
                    ParseMySqlTableJson(tableCommand);
                var whatIf = _whatIfOnly == "1" ? 1 : 0;
                var dropUnknown = _dropUnknownIndexes == "1" ? 1 : 0;
                tableCommand.CommandText = $"CALL SchemaSmith_ForeignKeyQuench('{_product.Name.Replace("'", "''")}', '{_databaseName.Replace("'", "''")}', {whatIf}, {dropUnknown})";
                break;
            }
        }

        _debugFileLocation = $"SchemaQuench - Quench Foreign Keys {_server}.{_databaseName}.sql";
        LogSqlScript(_debugFileLocation, tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand);
        _debugFileLocation = "";
    }

    internal void QuenchMaterializedViews(IDbCommand tableCommand)
    {
        SafeProgressLog("  Quenching materialized views");

        var updateFillFactor = _template.UpdateFillFactor.ToString().ToLower();
        tableCommand.CommandText = $@"CALL ""SchemaSmith"".""MaterializedViewQuench""('{_product.Name.Replace("'", "''")}', '{_template.MaterializedViewSchema.Replace("'", "''")}', {_whatIfOnly}, {updateFillFactor});";

        _debugFileLocation = $"SchemaQuench - Quench Materialized Views {_server}.{_databaseName}.sql";
        LogSqlScript(_debugFileLocation, tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand);
        _debugFileLocation = "";
    }

    internal void QuenchIndexedViews(IDbCommand tableCommand)
    {
        // Note: Cross-product conflict detection (view owned by a different product) is handled
        // inside IndexedViewQuench.sql via THROW, mirroring PostgreSQL's ValidateMaterializedViewOwnership.
        // The standalone ValidateIndexedViewOwnership.sql and FixupIndexedViewOwnership.sql procedures
        // serve a different purpose: they find/adopt *untagged* indexed views (created outside SchemaSmith).
        // They are deployed to the database by ForgeKindler for manual/diagnostic use but are not called
        // during the quench flow — untagged views are simply left alone (the quench creates new views
        // rather than adopting existing ones).
        SafeProgressLog("  Quenching indexed views");

        // Validate required clustered index before attempting quench
        foreach (var iv in _template.IndexedViews)
        {
            if (!iv.Indexes.Any(i => i.Clustered && i.Unique))
                throw new Exception($"Indexed view {iv.Schema}.{iv.Name} requires a unique clustered index");
        }

        // Filter out indexed views where ShouldApplyExpression evaluated to false
        var applicableViews = _template.IndexedViews
            .Where(iv => string.IsNullOrEmpty(iv.ShouldApplyExpression) || iv.ShouldApplyExpression != "false")
            .ToList();
        if (applicableViews.Count == 0) return;

        var viewSchema = JArray.FromObject(applicableViews).ToString();
        var updateFillFactor = _template.UpdateFillFactor.ToString().ToLower();
        tableCommand.CommandText = $@"EXEC [SchemaSmith].[IndexedViewQuench] @ProductName = '{_product.Name.Replace("'", "''")}', @IndexedViewSchema = '{viewSchema.Replace("'", "''")}', @WhatIf = {_whatIfOnly}, @UpdateFillFactor = {updateFillFactor};";

        _debugFileLocation = $"SchemaQuench - Quench Indexed Views {_server}.{_databaseName}.sql";
        LogSqlScript(_debugFileLocation, tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand);
        _debugFileLocation = "";
    }

    #endregion

    #region MySQL Temp Tables

    private void ParseMySqlTableJson(IDbCommand command)
    {
        var tableJson = !string.IsNullOrEmpty(_template.TableSchema)
            ? _template.TableSchema
            : JsonHelper.SerializeAll(_template.Tables);
        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{_databaseName.Replace("'", "''")}', @tableJson)";
        _debugFileLocation = $"SchemaQuench - Parse Table Json {_server}.{_databaseName}.sql";
        LogSqlScript(_debugFileLocation, command.CommandText.Replace("@tableJson", $"'{tableJson.Replace("'", "''")}'"));
        AddJsonParameter(command, "@tableJson", tableJson);
        ExecuteNonQueryHandlingMessages(command);
        ClearParameters(command);
    }

    private static bool MySqlTempTablesExist(IDbCommand command)
    {
        try
        {
            command.CommandText = "SELECT COUNT(*) FROM _SchemaSmith_Tables";
            command.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CleanupMySqlTempTables(IDbCommand command)
    {
        try
        {
            command.CommandText = @"
                DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Tables;
                DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Columns;
                DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Indexes;
                DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ForeignKeys;
                DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CheckConstraints;";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static void AddJsonParameter(IDbCommand command, string name, string json)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = json;
        param.DbType = DbType.String;
        command.Parameters.Add(param);
    }

    private static void ClearParameters(IDbCommand command)
    {
        command.Parameters.Clear();
    }

    #endregion

    #region Connection and Execution

    private IDbConnection GetConnection(bool fireInfoMessageEventOnUserErrors = true, bool ignoreInfoMessages = false)
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var connectionStringOverride = CommandLineParser.ValueOfSwitch("ConnectionString", null);
        var connectionProperties = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        var connectionString = string.IsNullOrEmpty(connectionStringOverride)
            ? ConnectionString.Build(_product.Platform, _server, _databaseName, config["Target:User"], config["Target:Password"], config["Target:Port"], connectionProperties)
            : connectionStringOverride;
        var factory = DbConnectionFactory.ForPlatform(_product.Platform);
        var connection = factory.GetDbConnection(connectionString);

        // Platform-specific message handling
        if (!ignoreInfoMessages)
        {
            switch (_product.Platform)
            {
                case Platform.SqlServer when connection is SqlConnection sqlConnection:
                    sqlConnection.InfoMessage += OnSqlServerInfoMessage;
                    sqlConnection.FireInfoMessageEventOnUserErrors = fireInfoMessageEventOnUserErrors;
                    break;
                case Platform.PostgreSQL when connection is NpgsqlConnection pgConnection:
                    pgConnection.Notice += OnPostgreSqlNotice;
                    break;
            }
        }

        connection.Open();

        // MySQL: start status message monitor
        if (_product.Platform == Platform.MySQL && _statusMonitor == null)
        {
            try
            {
                using var sessionCmd = connection.CreateCommand();
                sessionCmd.CommandText = "SELECT CONNECTION_ID()";
                var sessionId = Convert.ToInt64(sessionCmd.ExecuteScalar());
                _statusMonitor = new StatusMessageMonitor(connectionString, sessionId, msg => SafeProgressLog($"    {msg}"));
            }
            catch
            {
                // Status monitoring is best-effort
            }
        }

        return connection;
    }

    private void ExecuteNonQueryHandlingMessages(IDbCommand command)
    {
        _infoMessageException = null;

        if (_product.Platform == Platform.MySQL)
        {
            try
            {
                command.ExecuteNonQuery();
                _statusMonitor?.Flush();
            }
            catch
            {
                _statusMonitor?.Flush();
                throw;
            }
        }
        else
        {
            command.ExecuteNonQuery();
            if (_infoMessageException != null) throw _infoMessageException;
        }
    }

    private static void LogSqlScript(string name, string sql)
    {
        var cwd = AppContext.BaseDirectory;
        FileWrapper.GetFromFactory().WriteAllText(Path.Combine(cwd, name), sql);
    }

    #endregion

    #region Script Quenching

    private void QuenchDatabaseObjectsWithCheckpoint(IDbCommand destCmd, List<SqlScript> templateObjects, bool showErrors, DatabaseScriptSlot slot)
    {
        var lastQuenchCount = 0;
        while (lastQuenchCount != templateObjects.Count(s => !s.HasBeenQuenched) && templateObjects.Any(s => !s.HasBeenQuenched))
        {
            lastQuenchCount = templateObjects.Count(s => !s.HasBeenQuenched);
            foreach (var script in templateObjects.Where(s => !s.HasBeenQuenched))
            {
                if (_checkpointing.HasCompletedScript(DbScope, slot.ToString(), script.LogPath))
                {
                    if (showErrors) SafeProgressLog($"    Skipping (previously quenched per checkpoint) {script.LogPath}");
                    script.HasBeenQuenched = true;
                    continue;
                }

                QuenchOneScript(destCmd, script, _runScriptsTwice, showErrors);
                if (script.HasBeenQuenched)
                    _checkpointing.MarkScriptCompleted(DbScope, slot.ToString(), script.LogPath);
            }
        }

        _debugFileLocation = "";
        if (showErrors) LogScriptErrors(templateObjects);
    }

    internal void QuenchDatabaseObjects(IDbCommand destCmd, List<SqlScript> templateObjects, bool showErrors = true)
    {
        var lastQuenchCount = 0;
        while (lastQuenchCount != templateObjects.Count(s => !s.HasBeenQuenched) && templateObjects.Any(s => !s.HasBeenQuenched))
        {
            lastQuenchCount = templateObjects.Count(s => !s.HasBeenQuenched);
            foreach (var script in templateObjects.Where(s => !s.HasBeenQuenched))
                QuenchOneScript(destCmd, script, _runScriptsTwice, showErrors);
        }

        _debugFileLocation = "";
        if (showErrors) LogScriptErrors(templateObjects);
    }

    internal void QuenchOneScript(IDbCommand destCmd, SqlScript script, bool runTwice, bool showErrors = true)
    {
        if (_product.Platform == Platform.SqlServer)
            _debugFileLocation = (destCmd.Connection as SqlConnection)?.FireInfoMessageEventOnUserErrors ?? false ? script.LogPath : "";
        else
            _debugFileLocation = showErrors ? script.LogPath : "";

        if (showErrors) SafeProgressLog($"    Quenching {script.LogPath}");
        var needDBReset = false;
        try
        {
            script.CheckForUnresolvedTokens(_databaseName, "", _progressLog.Warn);
            for (var i = 1; i <= (runTwice ? 2 : 1); i++)
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
            if (!showErrors) SafeProgressLog($"    Quenched {script.LogPath}");
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
            if (_product.Platform == Platform.PostgreSQL)
            {
                destCmd.Connection?.ChangeDatabase(_databaseName);
            }
            else
            {
                destCmd.CommandText = QuoteUseDatabase(_databaseName);
                destCmd.ExecuteNonQuery();
            }
        }
        catch
        {
            // ignore error resetting db
        }
    }

    private void QuenchTemplateScriptsWithCheckpoint(IDbCommand destCmd, string slot, List<SqlScript> scripts, DatabaseScriptSlot checkpointSlot)
    {
        var onDiskRelativePaths = scripts.Select(s => GetRelativeScriptPath(s.LogPath)).ToList();
        var alreadyRan = _trackRunOnceMigrations ? GetCompletedEntriesBySlot(destCmd, slot, onDiskRelativePaths) : [];
        foreach (var script in scripts.Where(s => !s.HasBeenQuenched))
        {
            if (ShouldAlwaysRun(script.Name) || !alreadyRan.Contains(GetRelativeScriptPath(script.LogPath)))
            {
                if (_checkpointing.HasCompletedScript(DbScope, checkpointSlot.ToString(), script.LogPath))
                {
                    script.HasBeenQuenched = true;
                    SafeProgressLog($"    Skipping (previously quenched per checkpoint) {script.LogPath}");
                    continue;
                }

                QuenchOneScript(destCmd, script, _runScriptsTwice & ShouldAlwaysRun(script.Name));
                if (script.HasBeenQuenched)
                {
                    _checkpointing.MarkScriptCompleted(DbScope, checkpointSlot.ToString(), script.LogPath);
                    if (_trackRunOnceMigrations && !ShouldAlwaysRun(script.Name))
                        MarkScriptCompleted(destCmd, script.LogPath, slot);
                }
            }
            else
            {
                script.HasBeenQuenched = true;
                SafeProgressLog($"    Skipping (previously quenched) {script.LogPath}");
            }
        }

        if (_trackRunOnceMigrations && _pruneObsoleteMigrationTracking)
            RemoveObsoleteCompletedScriptEntries(destCmd, slot, scripts, alreadyRan);

        _debugFileLocation = "";
        LogScriptErrors(scripts);
    }

    private void RemoveObsoleteCompletedScriptEntries(IDbCommand destCmd, string slot, List<SqlScript> scripts, List<string> alreadyRan)
    {
        foreach (var obsoleteScript in alreadyRan.Where(a => scripts.All(s => GetRelativeScriptPath(s.LogPath) != a)))
        {
            destCmd.CommandText = GetDeleteCompletedScriptSql(
                _product.Name, slot, obsoleteScript, _template.Name, DbScope.SchemaName ?? "");
            destCmd.ExecuteNonQuery();
        }
    }

    internal static bool ShouldAlwaysRun(string scriptName) => Path.GetFileNameWithoutExtension(scriptName).EndsWith("[ALWAYS]");

    private List<string> GetCompletedEntriesBySlot(IDbCommand destCmd, string slot, IReadOnlyList<string> onDiskRelativePaths = null)
    {
        var entries = new List<string>();
        try
        {
            destCmd.CommandText = GetSelectCompletedScriptsSql(
                _product.Name, slot, _template.Name, DbScope.SchemaName ?? "");
            using var reader = destCmd.ExecuteReader();
            while (reader.Read())
                entries.Add(reader.GetString(0));
        }
        catch
        {
            // Table may not exist yet (MySQL) or on first run
            return entries;
        }

        // TRANSITIONAL (slice 2 of schema-templates): claim ownership of any legacy
        // blank-template tracking rows whose ScriptPath is also present on the current
        // template's disk. Without this, a legacy row whose script is later removed AND
        // replaced with a new file using the same filename would silently shadow the new
        // file. Scoped to on-disk paths so we don't mis-attribute a row that was originally
        // tracking some other template's work.
        //
        // Pre-extension behavior treated such shared-filename rows as "complete for all
        // templates," which was itself a silent bug — two scripts that should both have
        // run would mark as complete after only the first one. Per-template ownership is
        // the design intent; this code is the transitional aid that gets us there safely.
        //
        // ROADMAP: remove this AND the permissive template_name IN ('', @template) read
        // once legacy data is reasonably presumed migrated. Tracked in the Community roadmap
        // under "Schema templates — slice 2 legacy-data migration cleanup".
        if (onDiskRelativePaths is { Count: > 0 } && entries.Count > 0)
        {
            try
            {
                var legacyClaimable = entries
                    .Where(e => onDiskRelativePaths.Contains(e, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (legacyClaimable.Count > 0)
                {
                    destCmd.CommandText = GetClaimLegacyTrackingRowsSql(
                        _product.Name, slot, _template.Name, DbScope.SchemaName ?? "", legacyClaimable);
                    destCmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Best-effort transitional aid; failure here must not gate the actual quench.
            }
        }

        return entries;
    }

    private void MarkScriptCompleted(IDbCommand destCmd, string scriptPath, string slot)
    {
        try
        {
            destCmd.CommandText = GetInsertCompletedScriptSql(
                GetRelativeScriptPath(scriptPath), _product.Name, slot,
                _template.Name, DbScope.SchemaName ?? "");
            destCmd.ExecuteNonQuery();
        }
        catch
        {
            // Ignore errors if table doesn't exist or duplicate entry
        }
    }

    internal string GetRelativeScriptPath(string filePath)
    {
        return LongPathSupport.StripLongPathPrefix(filePath)
            .Replace(Path.GetDirectoryName(_template.LogPath) ?? "", "")
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .TrimStart(Path.AltDirectorySeparatorChar);
    }

    #endregion

    #region Error Logging

    private void LogScriptErrors(List<SqlScript> scripts)
    {
        if (scripts.All(x => x.HasBeenQuenched)) return;

        foreach (var sqlScript in scripts.Where(s => !s.HasBeenQuenched))
        {
            SafeProgressLogError($"Unable to quench '{sqlScript.LogPath}':\r\n{sqlScript.Error}");
            SafeErrorLogError($"Unable to quench '{sqlScript.LogPath}':\r\n{sqlScript.Error}\r\n\r\n");
            foreach (var batch in sqlScript.Batches) SafeErrorLogError($"\r\n{batch}");
        }

        throw new Exception("Unable to quench all scripts");
    }

    /// <summary>
    /// Per-tenant log discipline (design §5.8): when this is a schema-template iteration, every log
    /// line carries a <c>[Schema: &lt;name&gt;]</c> prefix so a 100-iteration deploy log is still
    /// greppable per tenant. Empty schema name (regular template) skips the prefix entirely.
    /// <para>Visible to tests (matches the existing <c>internal Platform Platform</c> /
    /// <c>internal string ProductName</c> / <c>internal string SchemaName</c> pattern) so tests can
    /// assert the prefix shape without reflection.</para>
    /// </summary>
    internal string LogPrefix => string.IsNullOrEmpty(_schemaName)
        ? $"[{_server}].[{_databaseName}]"
        : $"[{_server}].[{_databaseName}] [Schema: {_schemaName}]";

    private void SafeProgressLog(string msg)
    {
        lock (_lockObject) _progressLog.Info($"{LogPrefix} {msg}");
    }

    private void SafeProgressLogError(string msg)
    {
        lock (_lockObject) _progressLog.Error($"{LogPrefix} {msg}");
    }

    private void SafeErrorLogError(string msg)
    {
        lock (_lockObject) _errorLog.Error($"{LogPrefix} {msg}");
    }

    #endregion

    #region Platform Message Handlers

    private void OnSqlServerInfoMessage(object sender, SqlInfoMessageEventArgs e)
    {
        foreach (SqlError err in e.Errors)
        {
            if (err.Class > 10)
            {
                SafeProgressLogError(err.Message);
                if (!string.IsNullOrWhiteSpace(_debugFileLocation))
                {
                    SafeProgressLogError("");
                    SafeProgressLogError($"Debug Script: '{_debugFileLocation}'");
                }

                SafeErrorLogError("");
                SafeErrorLogError(err.Message);
                SafeErrorLogError($"  at Line: {err.LineNumber}");
                SafeErrorLogError("");
                _infoMessageException = new Exception(err.Message);
            }
            else if (_product != null)
            {
                var verboseLogging = FactoryContainer.ResolveOrCreate<IConfigurationRoot>()["VerboseLogging"]?.ToLower() == "true";
                if (verboseLogging || err.State == 100)
                    SafeProgressLog($"      {err.Message}");
            }
        }
    }

    private void OnPostgreSqlNotice(object sender, NpgsqlNoticeEventArgs e)
    {
        if (e.Notice.MessageText.EndsWith("does not exist, skipping") || e.Notice.MessageText.EndsWith("already exists, skipping")) return;
        SafeProgressLog($"      {e.Notice.MessageText}");
    }

    #endregion

    #region WhatIf Logging

    private void WhatIfLogScripts(List<SqlScript> scripts, DatabaseScriptSlot slot)
    {
        foreach (var script in scripts)
            SafeProgressLog($"    Would APPLY: {script.LogPath}");
    }

    private void WhatIfLogTableDataScripts(List<SqlScript> scripts)
    {
        foreach (var script in scripts)
            SafeProgressLog($"    Would DELIVER: {Path.GetFileNameWithoutExtension(script.LogPath)}");
    }

    private void WhatIfLogTemplateScripts(IDbCommand destCmd, string slot, List<SqlScript> scripts, DatabaseScriptSlot checkpointSlot)
    {
        var alreadyRan = _trackRunOnceMigrations ? GetCompletedEntriesBySlot(destCmd, slot) : [];
        foreach (var script in scripts)
        {
            if (!ShouldAlwaysRun(script.Name) && alreadyRan.Contains(GetRelativeScriptPath(script.LogPath)))
                SafeProgressLog($"    Would SKIP (previously quenched): {script.LogPath}");
            else
                SafeProgressLog($"    Would APPLY: {script.LogPath}");
        }
    }

#endregion
}
