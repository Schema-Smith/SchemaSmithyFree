// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
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

    /// <summary>
    /// Override-origin signal. True when this iteration's schema came from a
    /// <c>TemplateTargets:&lt;Template&gt;:Schemas</c> override. Drives the
    /// skip-missing branch in <see cref="EnsureSchemaExists(System.Data.IDbCommand)"/>:
    /// override-sourced units honor the work-unit-level <see cref="ProvisionSchemaIfMissing"/>
    /// flag, with missing schemas SKIPPED (no error) when that flag is false. Discovery-sourced
    /// units (default <c>false</c>) preserve today's strict <c>Template.CreateSchemaIfMissing</c>
    /// behavior — a missing schema fails the iteration with the three-onboarding-paths message.
    /// </summary>
    public bool SchemaFromOverride { get; init; }

    /// <summary>
    /// Work-unit-level provisioning flag, mirrored from
    /// <c>TemplateTargets.&lt;Template&gt;.CreateIfMissing</c>. When true on an
    /// override-sourced unit, a missing schema is provisioned via
    /// <see cref="SchemaProvisioner.EnsureSchemaExists"/>; when false, a missing schema is
    /// SKIPPED with an info log. On discovery-sourced units this flag is ignored —
    /// <see cref="Template.CreateSchemaIfMissing"/> remains the authority.
    /// </summary>
    public bool ProvisionSchemaIfMissing { get; init; }

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
    private readonly bool _forceReKindle;

    private string _debugFileLocation = "";
    private Exception _infoMessageException;
    private StatusMessageMonitor _statusMonitor;
    private readonly object _lockObject = new();

    // Per-iteration content built by PrepareIterationContent at the start of Execute(). For schema-
    // template iterations the script collections are cloned (isolating {{SchemaName}}-substituted
    // batches from sibling iterations that share the same in-memory Template) and the table / view
    // JSON carries the substituted schema. For regular templates the collections alias _template's
    // own collections (preserving the cross-iteration HasBeenQuenched semantics the engine relies on)
    // and the schema strings stay null so the accessor properties below fall back to _template.<field>
    // — no substitution, no behavior change. That fall-back also lets test-only entry points that call
    // the Quench* methods directly (bypassing Execute → PrepareIterationContent) keep working.
    private readonly IterationContent _iteration = new();

    private sealed class IterationContent
    {
        public List<SqlScript> BeforeScripts { get; set; }
        public List<SqlScript> ObjectScripts { get; set; }
        public List<SqlScript> AfterTablesObjectScripts { get; set; }
        public List<SqlScript> BetweenTablesAndKeysScripts { get; set; }
        public List<SqlScript> AfterTableScripts { get; set; }
        public List<SqlScript> TableDataScripts { get; set; }
        public List<SqlScript> AfterScripts { get; set; }
        public string BaselineValidationScript { get; set; }
        public string VersionStampScript { get; set; }
        public string TableSchema { get; set; }
        public string MaterializedViewSchema { get; set; }
        public string IndexedViewSchema { get; set; }
    }
    internal string IterationTableSchema => _iteration.TableSchema ?? _template.TableSchema ?? "";
    internal string IterationMaterializedViewSchema => _iteration.MaterializedViewSchema ?? _template.MaterializedViewSchema ?? "";
    // I10: Mirror the iteration-schema pattern for indexed views. QuenchIndexedViews used to
    // rebuild the JSON inline per call; routing through this field puts the substitution alongside
    // the table / materialized-view substitution in PrepareIterationContent. Per-call ShouldApply
    // filtering still happens inside QuenchIndexedViews (the filter is per-view and can't be done
    // once at iteration-prepare time without losing the filter on regular templates that bypass
    // PrepareIterationContent through the constructor → QuenchIndexedViews test entry points).
    internal string IterationIndexedViewSchema => _iteration.IndexedViewSchema ?? _template.IndexedViewSchema ?? "";

    public DatabaseQuench(string server, Product product, Template template, string databaseName,
        string schemaName, bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        bool dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true, bool forceReKindle = false)
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
        _forceReKindle = forceReKindle;
    }

    // Convenience overload matching the pre-schema-templates positional signature so existing
    // callers (mostly tests) that have no schema concept compile without modification. Forwards
    // to the canonical constructor with empty schemaName.
    public DatabaseQuench(string server, Product product, Template template, string databaseName,
        bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        bool dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true, bool forceReKindle = false)
        : this(server, product, template, databaseName, "", suppressKindling, whatIfOnly, runScriptsTwice,
            dropRemovedTables, dropUnknownIndexes, updateTables, deliverData, checkpointing,
            trackRunOnceMigrations, pruneObsoleteMigrationTracking, forceReKindle)
    {
    }

    // Internal constructor for testing — allows direct injection of all parameters
    internal DatabaseQuench(string server, Product product, Template template, string databaseName,
        string schemaName, bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        string dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true, bool forceReKindle = false)
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
        _forceReKindle = forceReKindle;
    }

    // Convenience overload for tests pre-dating the schemaName parameter.
    internal DatabaseQuench(string server, Product product, Template template, string databaseName,
        bool suppressKindling, string whatIfOnly, bool runScriptsTwice, string dropRemovedTables,
        string dropUnknownIndexes, bool updateTables, bool deliverData, ICheckpointing checkpointing,
        bool trackRunOnceMigrations = true, bool pruneObsoleteMigrationTracking = true, bool forceReKindle = false)
        : this(server, product, template, databaseName, "", suppressKindling, whatIfOnly, runScriptsTwice,
            dropRemovedTables, dropUnknownIndexes, updateTables, deliverData, checkpointing,
            trackRunOnceMigrations, pruneObsoleteMigrationTracking, forceReKindle)
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
                // Schema-template existence check: run before slot 1 (kindling) so a missing
                // schema fails fast with a clear error before any DDL is attempted. MySQL schema
                // templates are rejected at load time (no namespace-inside-database concept);
                // the empty-schemaName guard handles MySQL and regular templates both.
                // Skip-missing short-circuit: when an override-sourced iteration's schema is
                // missing and CreateIfMissing is false, EnsureSchemaExists logs the skip and
                // returns MissingSkipped. Mark the unit successful and bail — the dispatcher
                // records a benign skip rather than running deployment work against a
                // non-existent target.
                if (EnsureSchemaExists(command) == SchemaPresence.MissingSkipped)
                {
                    QuenchSuccessful = true;
                    return;
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
                        ForgeKindler.KindleTheForge(effectiveSilentCmd, _product.Platform, _forceReKindle);
                    });
                }

                // Step: Validate baseline. Resolved against per-iteration tokens (BaselineValidationScript
                // may reference {{SchemaName}} for schema templates).
                if (!string.IsNullOrWhiteSpace(_iteration.BaselineValidationScript))
                {
                    _checkpointing.Track(DbScope, "ValidateBaseline", () =>
                    {
                        SafeProgressLog("  Validate Baseline");
                        command.CommandText = _iteration.BaselineValidationScript;
                        if (!Convert.ToBoolean(command.ExecuteScalar()))
                            throw new Exception("Invalid baseline for this release");
                    });
                }

                // Step: Object scripts without unresolved tokens
                var nonTokenScripts = _iteration.ObjectScripts.Where(s => s.Batches.All(b => !b.Contains("{{") && !b.Contains("}}"))).ToList();
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
                // Intentionally NOT wrapped in `_checkpointing.Track` — this step parses the
                // table JSON into session-scoped temp tables (`#Tables` on SQL Server,
                // `temp_tables` on PG, `_SchemaSmith_Tables` on MySQL) before delegating to
                // `MissingTableAndColumnQuench`. The temp tables are consumed by the next two
                // tracked steps (`ModifiedTables`, `IndexesAndConstraints`); they DON'T survive
                // across connections, so on resume the next session has no temp state.
                // The action itself is database-idempotent (the engine procs add missing
                // tables/columns and no-op when they already exist), so always-running it on
                // resume is safe and cheap. Skip-on-resume would fire `Invalid object name
                // '#Tables'` (SQL Server) / `relation "temp_tables" does not exist` (PG) on
                // the next tracked step. MySQL's `QuenchModifiedTables` and
                // `QuenchIndexesAndConstraints` already defend against the missing-temp-tables
                // case via `MySqlTempTablesExist` + `ParseMySqlTableJson` re-parse; lifting the
                // Track wrapper here is the equivalent defense for SQL Server / PG.
                if (!_template.IndexOnlyTableQuenches && _updateTables)
                {
                    QuenchMissingTablesAndColumns(effectiveTableCmd);
                }

                if (!IsWhatIf)
                {
                    SafeProgressLog("  Quenching object scripts without query tokens");
                    QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd,
                        _iteration.ObjectScripts.Where(s => s.Batches.All(b => !b.Contains("{{") && !b.Contains("}}"))).ToList(),
                        false, DatabaseScriptSlot.Object);

                    ResolveAndApplyQueryTokens(effectiveSilentCmd);

                    SafeProgressLog("  Quenching before database scripts");
                    QuenchTemplateScriptsWithCheckpoint(command, "Before", _iteration.BeforeScripts, DatabaseScriptSlot.Before);
                }
                else
                {
                    SafeProgressLog("  [WhatIf] Object scripts without query tokens:");
                    WhatIfLogScripts(_iteration.ObjectScripts.Where(s => s.Batches.All(b => !b.Contains("{{") && !b.Contains("}}"))).ToList(), DatabaseScriptSlot.Object);

                    if (_template.QueryTokens.Count > 0)
                        SafeProgressLog("  [WhatIf] Would resolve template query tokens");

                    SafeProgressLog("  [WhatIf] Before database scripts:");
                    WhatIfLogTemplateScripts(command, "Before", _iteration.BeforeScripts, DatabaseScriptSlot.Before);
                }

                // Step: Modified tables
                if (!_template.IndexOnlyTableQuenches && _updateTables)
                {
                    _checkpointing.Track(DbScope, "ModifiedTables", () => QuenchModifiedTables(effectiveTableCmd));
                }

                if (!IsWhatIf)
                {
                    SafeProgressLog("  Quenching object scripts");
                    QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd, _iteration.AfterTablesObjectScripts, false, DatabaseScriptSlot.AfterTablesObject);

                    SafeProgressLog("  Quenching between table and keys scripts");
                    QuenchTemplateScriptsWithCheckpoint(command, "Between Table And Keys", _iteration.BetweenTablesAndKeysScripts, DatabaseScriptSlot.BetweenTablesAndKeys);
                }
                else
                {
                    SafeProgressLog("  [WhatIf] Object scripts (after tables):");
                    WhatIfLogScripts(_iteration.AfterTablesObjectScripts, DatabaseScriptSlot.AfterTablesObject);

                    SafeProgressLog("  [WhatIf] Between table and keys scripts:");
                    WhatIfLogTemplateScripts(command, "Between Table And Keys", _iteration.BetweenTablesAndKeysScripts, DatabaseScriptSlot.BetweenTablesAndKeys);
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
                    QuenchTemplateScriptsWithCheckpoint(command, "After Table", _iteration.AfterTableScripts, DatabaseScriptSlot.AfterTable);

                    if (_iteration.ObjectScripts.Union(_iteration.AfterTablesObjectScripts).Any(s => !s.HasBeenQuenched))
                    {
                        SafeProgressLog("  Quenching object scripts");
                        QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd, _iteration.AfterTablesObjectScripts.ToList(), true, DatabaseScriptSlot.AfterTablesObject);
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
                                SchemaName = _schemaName,
                                TemplateRootPath = Path.GetDirectoryName(_template.FilePath) ?? "",
                                ScriptHelper = FactoryContainer.Resolve<IMergeScriptHelper>(),
                                ReadFileContent = path => ProductFileWrapper.GetFromFactory().ReadAllText(path),
                                ExecuteScript = (name, script) => { effectiveSilentCmd.CommandText = script; effectiveSilentCmd.ExecuteNonQuery(); },
                                ProgressLog = SafeProgressLog,
                                ProgressLogError = SafeProgressLogError,
                                WhatIf = IsWhatIf,
                                WriteResolvedSqlArtifact = (label, sql) =>
                                {
                                    try
                                    {
                                        var content = ResolvedSqlArtifactWriter.BuildArtifact(
                                            $"Failed data delivery: {_server}.{_databaseName}" +
                                            $"{(string.IsNullOrEmpty(_schemaName) ? "" : $" [Schema: {_schemaName}]")} [{label}]",
                                            new List<string> { sql }, failingBatchIndex: 0);
                                        if (ScrubArtifactsEnabled)
                                            content = ResolvedSqlArtifactWriter.Scrub(content, SensitiveTokenValues());
                                        var safeLabel = string.Concat(label.Select(c =>
                                            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                                        var path = ResolvedSqlArtifactWriter.Write(ResolveArtifactDirectory(),
                                            GetDebugFileName($"Failed DataDelivery {safeLabel}"), content);
                                        SafeProgressLogError($"    Resolved SQL written to: {path}");
                                    }
                                    catch (Exception artifactEx)
                                    {
                                        SafeProgressLog($"    Could not write resolved-SQL artifact for data delivery '{label}': {artifactEx.Message}");
                                    }
                                }
                            });
                        });

                        if (_iteration.ObjectScripts.Union(_iteration.TableDataScripts).Any(s => !s.HasBeenQuenched))
                        {
                            SafeProgressLog("  Quenching table data scripts");
                            QuenchDatabaseObjectsWithCheckpoint(effectiveObjectsCmd, _iteration.TableDataScripts.ToList(), true, DatabaseScriptSlot.TableData);
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
                    QuenchTemplateScriptsWithCheckpoint(command, "After", _iteration.AfterScripts, DatabaseScriptSlot.After);

                    if (!string.IsNullOrWhiteSpace(_iteration.VersionStampScript))
                    {
                        _checkpointing.Track(DbScope, "VersionStamp", () =>
                        {
                            SafeProgressLog("  Stamp version");
                            command.CommandText = _iteration.VersionStampScript;
                            ExecuteNonQueryHandlingMessages(command);
                        });
                    }
                }
                else
                {
                    SafeProgressLog("  [WhatIf] After table scripts:");
                    WhatIfLogTemplateScripts(command, "After Table", _iteration.AfterTableScripts, DatabaseScriptSlot.AfterTable);

                    SafeProgressLog("  [WhatIf] Object scripts (final pass):");
                    WhatIfLogScripts(_iteration.AfterTablesObjectScripts.ToList(), DatabaseScriptSlot.AfterTablesObject);

                    if (_deliverData)
                    {
                        SafeProgressLog("  [WhatIf] Table data delivery:");
                        WhatIfLogTableDataScripts(_iteration.TableDataScripts.ToList());

                        if (FactoryContainer.Resolve<IMergeScriptHelper>() is not MergeScriptHelperAdapter whatIfAdapter || whatIfAdapter.Platform != _product.Platform)
                            FactoryContainer.Register<IMergeScriptHelper>(new MergeScriptHelperAdapter(_product.Platform));

                        DataDeliveryProcessor.GetFromFactory().DeliverTables(new DataDeliveryContext
                        {
                            Tables = _template.Tables.Cast<IDeliverableTable>().ToList(),
                            Command = command,
                            Platform = _product.Platform.ToString(),
                            DatabaseName = _databaseName,
                            SchemaName = _schemaName,
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
                    WhatIfLogTemplateScripts(command, "After", _iteration.AfterScripts, DatabaseScriptSlot.After);

                    if (!string.IsNullOrWhiteSpace(_iteration.VersionStampScript))
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
            if (!string.IsNullOrWhiteSpace(_debugFileLocation))
                SafeProgressLogError($"Debug Script: '{_debugFileLocation}'");
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
    internal void PrepareIterationContent()
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
            _iteration.BeforeScripts = _template.BeforeScripts;
            _iteration.ObjectScripts = _template.ObjectScripts;
            _iteration.AfterTablesObjectScripts = _template.AfterTablesObjectScripts;
            _iteration.BetweenTablesAndKeysScripts = _template.BetweenTablesAndKeysScripts;
            _iteration.AfterTableScripts = _template.AfterTableScripts;
            _iteration.TableDataScripts = _template.TableDataScripts;
            _iteration.AfterScripts = _template.AfterScripts;
            _iteration.BaselineValidationScript = _template.BaselineValidationScript;
            _iteration.VersionStampScript = _template.VersionStampScript;
            // _iteration.TableSchema / _iteration.MaterializedViewSchema deliberately left null —
            // the IterationTableSchema / IterationMaterializedViewSchema properties fall back to
            // _template.<field>, so regular templates and bypass-Execute test entry points both
            // observe the existing string verbatim.
            return;
        }

        // Schema-template iteration: deep-clone every script collection so {{SchemaName}}
        // substitution (and later, query-token substitution) operates on this iteration's
        // own copies and never mutates the shared Template instance.
        var schemaNameTokens = new List<KeyValuePair<string, string>>
        {
            new("SchemaName", _schemaName)
        };

        _iteration.BeforeScripts = CloneAndSubstitute(_template.BeforeScripts, schemaNameTokens);
        _iteration.ObjectScripts = CloneAndSubstitute(_template.ObjectScripts, schemaNameTokens);
        _iteration.AfterTablesObjectScripts = CloneAndSubstitute(_template.AfterTablesObjectScripts, schemaNameTokens);
        _iteration.BetweenTablesAndKeysScripts = CloneAndSubstitute(_template.BetweenTablesAndKeysScripts, schemaNameTokens);
        _iteration.AfterTableScripts = CloneAndSubstitute(_template.AfterTableScripts, schemaNameTokens);
        _iteration.TableDataScripts = CloneAndSubstitute(_template.TableDataScripts, schemaNameTokens);
        _iteration.AfterScripts = CloneAndSubstitute(_template.AfterScripts, schemaNameTokens);

        _iteration.BaselineValidationScript = SqlScript.TokenReplace(
            _template.BaselineValidationScript ?? "", schemaNameTokens, _product.Platform);
        _iteration.VersionStampScript = SqlScript.TokenReplace(
            _template.VersionStampScript ?? "", schemaNameTokens, _product.Platform);

        // Engine-generated DDL (MissingTableAndColumnQuench, IndexOnlyQuench, MaterializedViewQuench)
        // consumes the serialized table-definition JSON literally. Slice 1's SchemaDefaultResolver
        // defaults schema-template tables / views to "{{SchemaName}}", which means the JSON carries
        // the token verbatim — substitute here so each iteration sees a fully-qualified DDL payload.
        _iteration.TableSchema = (_template.TableSchema ?? "").Replace("{{SchemaName}}", _schemaName);
        _iteration.MaterializedViewSchema = (_template.MaterializedViewSchema ?? "").Replace("{{SchemaName}}", _schemaName);
        _iteration.IndexedViewSchema = (_template.IndexedViewSchema ?? "").Replace("{{SchemaName}}", _schemaName);
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
    /// Outcome of <see cref="EnsureSchemaExists(System.Data.IDbCommand)"/>. <see cref="Execute"/>
    /// short-circuits the iteration as a benign skip on <see cref="MissingSkipped"/>; every other
    /// value means "schema situation is fine, proceed with the iteration".
    /// </summary>
    private enum SchemaPresence
    {
        NotApplicable,  // regular template or MySQL — no schema iteration
        AlreadyExists,  // schema was already present on the target
        Provisioned,    // schema was created this run (override-provision or CreateSchemaIfMissing)
        MissingSkipped  // override-sourced, missing, CreateIfMissing:false — skip the iteration
    }

    /// <summary>
    /// Schema-template existence check (design §5.3 step 3). Called before slot 1 (kindling)
    /// on every schema-template iteration. For regular templates and MySQL, <see cref="_schemaName"/>
    /// is empty and this method returns immediately. For schema templates:
    /// <list type="bullet">
    ///   <item><description>Schema exists → return (no-op).</description></item>
    ///   <item><description>Schema missing + <c>CreateSchemaIfMissing: true</c> → emit
    ///   <c>CREATE SCHEMA</c> and proceed.</description></item>
    ///   <item><description>Schema missing + <c>CreateSchemaIfMissing: false</c> (default) →
    ///   throw with a clear error pointing at the three onboarding paths.</description></item>
    /// </list>
    /// Schema names are safe to interpolate directly: <see cref="SchemaDiscovery.Discover"/>
    /// already rejects any name containing bracket / quote / brace characters (slice-3 audit I6),
    /// so <c>[{_schemaName}]</c> / <c>"{_schemaName}"</c> interpolation cannot be injected.
    /// </summary>
    private SchemaPresence EnsureSchemaExists(IDbCommand command)
    {
        if (string.IsNullOrEmpty(_schemaName)) return SchemaPresence.NotApplicable;  // regular template or MySQL — no schema iteration

        if (SchemaExists(command))
            return SchemaPresence.AlreadyExists;

        // Override-sourced units honor the work-unit-level provisioning flag. CreateIfMissing: true
        // → provision via SchemaProvisioner (idempotent IF-NOT-EXISTS DDL). CreateIfMissing: false
        // (default) → SKIP the iteration silently with an info log: return MissingSkipped so Execute()
        // short-circuits cleanly and the dispatcher records a benign skip rather than treating an
        // explicitly-declared "deploy to this list if present" as a hard failure. Discovery-sourced
        // units bypass this branch and keep the strict CreateSchemaIfMissing contract below.
        if (SchemaFromOverride)
        {
            if (ProvisionSchemaIfMissing)
            {
                ProvisionSchemaViaProvisioner(command);
                return SchemaPresence.Provisioned;
            }
            SafeProgressLog(
                $"  Schema '{_schemaName}' does not exist in database '{_databaseName}' and " +
                $"TemplateTargets CreateIfMissing is false — skipping this iteration.");
            return SchemaPresence.MissingSkipped;
        }

        if (!_template.CreateSchemaIfMissing)
            throw new Exception(
                $"Schema '{_schemaName}' does not exist in database '{_databaseName}'. " +
                "Onboarding options: (1) Set CreateSchemaIfMissing: true on the template " +
                "(accepts typo risk in discovery output). (2) Pre-create the schema manually. " +
                "(3) Call a Shared-template helper procedure (e.g., dbo.OnboardTenant) " +
                "that runs CREATE SCHEMA + INSERT atomically — see the TenantCRM demo.");

        ProvisionSchemaViaProvisioner(command);
        return SchemaPresence.Provisioned;
    }

    /// <summary>
    /// Delegates idempotent CREATE SCHEMA DDL to <see cref="SchemaProvisioner"/>. Used by both
    /// the TemplateTargets override path (<c>SchemaFromOverride</c> + <c>ProvisionSchemaIfMissing</c>)
    /// and the legacy <c>Template.CreateSchemaIfMissing: true</c> path so both share one DDL surface
    /// and one WhatIf branch. Under WhatIf the DDL renders through the progress log without touching
    /// the database.
    /// </summary>
    private void ProvisionSchemaViaProvisioner(IDbCommand command)
    {
        command.Parameters.Clear();
        try
        {
            new SchemaProvisioner().EnsureSchemaExists(command, _schemaName, _product.Platform,
                IsWhatIf, SafeProgressLog);
        }
        catch (Exception ex)
        {
            SafeProgressLogError(
                $"  CREATE SCHEMA failed for [{_schemaName}] (CreateIfMissing: true) " +
                $"— possible permission or race condition: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Returns true if <see cref="_schemaName"/> already exists on the target platform.
    /// Uses a parameter to pass the schema name so no string interpolation into the WHERE clause.
    /// </summary>
    private bool SchemaExists(IDbCommand command)
    {
        command.Parameters.Clear();
        command.CommandText = _product.Platform == Platform.SqlServer
            ? "SELECT COUNT(*) FROM sys.schemas WHERE name = @name"
            : "SELECT COUNT(*) FROM pg_namespace WHERE nspname = @name";
        var param = command.CreateParameter();
        param.ParameterName = "@name";
        param.Value = _schemaName;
        param.DbType = DbType.String;
        command.Parameters.Add(param);
        var result = Convert.ToInt32(command.ExecuteScalar());
        command.Parameters.Clear();
        return result > 0;
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
        //    pass through unchanged — their resolved value does not depend on the iteration
        //    schema, so it can be cached across iterations (step 3 below).
        var iterationQueryTokens = new Dictionary<string, string>(_template.QueryTokens.Count);
        foreach (var kv in _template.QueryTokens)
        {
            var body = kv.Value;
            if (_template.IsIterationScoped(kv.Key) && !string.IsNullOrEmpty(body))
                body = body.Replace("{{SchemaName}}", _schemaName, StringComparison.OrdinalIgnoreCase);
            iterationQueryTokens[kv.Key] = body;
        }

        // 3. Per-DB query token cache (post-slice-8 perf, Commit B): consult the Template's
        //    per-(server, database) cache and pre-fill any per-DB token whose value has already
        //    been resolved against this target database in a prior iteration. We pull the
        //    cached entries OUT of iterationQueryTokens before handing it to ResolveQueryTokens
        //    because that resolver runs the connection round-trip for every token in the dict;
        //    leaving cached tokens in would defeat the cache. After resolution, any per-DB
        //    token that we just resolved (i.e., not already in the cache) is deposited back so
        //    sibling iterations can reuse it.
        //
        //    The cache reduces query-token connection round-trips from O(tokens × tenants) to
        //    O(tokens + tenants) for the per-DB subset. Iteration-scoped tokens still resolve
        //    every iteration as before — their resolved value differs per iteration by design.
        var perDbCache = _template.GetOrCreatePerDbTokenCache(_server, _databaseName);
        var cachedResolvedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (perDbCache != null)
        {
            // Snapshot the keys before iterating — we mutate iterationQueryTokens inside the loop.
            foreach (var tokenName in iterationQueryTokens.Keys.ToList())
            {
                if (!_template.IsPerDb(tokenName)) continue;
                if (!perDbCache.TryGetValue(tokenName, out var cachedValue)) continue;
                cachedResolvedValues[tokenName] = cachedValue;
                iterationQueryTokens.Remove(tokenName);
            }
        }

        // 4. Resolve all REMAINING query tokens against the live connection. ResolveQueryTokens
        //    mutates the supplied dictionary in place, so we hand it our iteration-scoped
        //    copy and leave _template.QueryTokens untouched for the next iteration.
        TokenHelper.ResolveQueryTokens(iterationQueryTokens, iterationNonQueryTokens,
            silentCmd, Path.GetDirectoryName(_template.FilePath), _product.Platform);

        // 5. Deposit newly-resolved per-DB token values into the cache so sibling iterations
        //    against the same (server, database) skip their round-trips.
        if (perDbCache != null)
        {
            foreach (var kv in iterationQueryTokens)
            {
                if (_template.IsPerDb(kv.Key))
                    perDbCache[kv.Key] = kv.Value;
            }
        }

        // 6. Merge cached + freshly-resolved values into a single list for downstream substitution.
        foreach (var kv in cachedResolvedValues)
            iterationQueryTokens[kv.Key] = kv.Value;

        // 7. Apply resolved values to this iteration's cloned scripts.
        var tokenList = iterationQueryTokens.ToList();
        ApplyToIteration(_iteration.BeforeScripts, tokenList);
        ApplyToIteration(_iteration.ObjectScripts, tokenList);
        ApplyToIteration(_iteration.AfterTablesObjectScripts, tokenList);
        ApplyToIteration(_iteration.BetweenTablesAndKeysScripts, tokenList);
        ApplyToIteration(_iteration.AfterTableScripts, tokenList);
        ApplyToIteration(_iteration.TableDataScripts, tokenList);
        ApplyToIteration(_iteration.AfterScripts, tokenList);
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

    internal string ResolveArtifactDirectory()
    {
        var configured = FactoryContainer.ResolveOrCreate<IConfigurationRoot>()["ArtifactPath"];
        return string.IsNullOrWhiteSpace(configured) ? Directory.GetCurrentDirectory() : configured;
    }

    internal bool ScrubArtifactsEnabled =>
        FactoryContainer.ResolveOrCreate<IConfigurationRoot>()["ScrubArtifacts"]?.ToLower() == "true";

    internal IReadOnlyList<KeyValuePair<string, string>> SensitiveTokenValues()
    {
        var options = LogHygieneOptions.FromConfiguration(FactoryContainer.ResolveOrCreate<IConfigurationRoot>());
        return _product.ScriptTokens
            .Concat(_template.ScriptTokens)
            .Where(kv => LogScrubber.ShouldScrubName(kv.Key, options))
            .ToList();
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
DECLARE @TableDefinitions VARCHAR(MAX)= '{EscapeSqlLiteral(IterationTableSchema)}',
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
  table_json JSON = '{EscapeSqlLiteral(IterationTableSchema)}';
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
                tableCommand.CommandText = $"CALL SchemaSmith_MissingTableAndColumnQuench('{EscapeSqlLiteral(_databaseName)}', {whatIf})";
                break;
            }
        }

        _debugFileLocation = LogSqlScript(GetDebugFileName("Quench Missing Tables And Columns"), tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand, retryOnDeadlock: true);
        _debugFileLocation = "";
    }

    internal void QuenchModifiedTables(IDbCommand tableCommand)
    {
        if (_product.Platform == Platform.MySQL && _template.Tables.Count == 0)
            return;

        SafeProgressLog("  Quenching modified tables");

        switch (_product.Platform)
        {
            case Platform.SqlServer:
                tableCommand.CommandText = $"EXEC [{_databaseName}].SchemaSmith.ModifiedTableQuench @ProductName = '{EscapeSqlLiteral(_product.Name)}', @DropUnknownIndexes = {_dropUnknownIndexes}, @WhatIf = {_whatIfOnly}, @DropTablesRemovedFromProduct = {_dropRemovedTables}";
                break;
            case Platform.PostgreSQL:
                tableCommand.CommandText = $@"
CALL ""SchemaSmith"".""ValidateTableOwnership""(p_ProductName := '{EscapeSqlLiteral(_product.Name)}', p_WhatIf := {_whatIfOnly}, p_TemplateName := '{EscapeSqlLiteral(_template.Name)}', p_SchemaName := '{EscapeSqlLiteral(_schemaName)}');
CALL ""SchemaSmith"".""ModifiedTableQuench""(p_DropUnknownIndexes := {_dropUnknownIndexes}, p_WhatIf := {_whatIfOnly}, p_DropTablesRemovedFromProduct := {_dropRemovedTables});";
                break;
            case Platform.MySQL:
            {
                if (!MySqlTempTablesExist(tableCommand))
                    ParseMySqlTableJson(tableCommand);
                var whatIf = _whatIfOnly == "1" ? 1 : 0;
                var dropRemoved = _dropRemovedTables == "1" ? 1 : 0;
                tableCommand.CommandText = $"CALL SchemaSmith_ModifiedTableQuench('{EscapeSqlLiteral(_product.Name)}', '{EscapeSqlLiteral(_databaseName)}', {whatIf}, {dropRemoved})";
                break;
            }
        }

        _debugFileLocation = LogSqlScript(GetDebugFileName("Quench Modified Tables"), tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand, retryOnDeadlock: true);
        _debugFileLocation = "";
    }

    internal void QuenchIndexesAndConstraints(IDbCommand tableCommand)
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
                    ? $"EXEC [{_databaseName}].SchemaSmith.IndexOnlyQuench @ProductName = '{EscapeSqlLiteral(_product.Name)}', @TableDefinitions = '{EscapeSqlLiteral(IterationTableSchema)}', @DropUnknownIndexes = {_dropUnknownIndexes}, @UpdateFillFactor = {updateFillFactor}, @WhatIf = {_whatIfOnly}"
                    : $"EXEC [{_databaseName}].SchemaSmith.MissingIndexesAndConstraintsQuench @ProductName = '{EscapeSqlLiteral(_product.Name)}', @WhatIf = {_whatIfOnly}";
                break;
            }
            case Platform.PostgreSQL:
                tableCommand.CommandText = _template.IndexOnlyTableQuenches
                    ? $@"
CALL ""SchemaSmith"".""IndexOnlyQuench""(p_TableDefinitions := '{EscapeSqlLiteral(IterationTableSchema)}', p_DropUnknownIndexes := {_dropUnknownIndexes}, p_WhatIf := {_whatIfOnly}, p_UpdateFillFactor := {_template.UpdateFillFactor.ToString().ToLower()});
CALL ""SchemaSmith"".""FixupIndexOwnership""(p_ProductName := '{EscapeSqlLiteral(_product.Name)}', p_TemplateName := '{EscapeSqlLiteral(_template.Name)}', p_SchemaName := '{EscapeSqlLiteral(_schemaName)}');
"
                    : $@"
CALL ""SchemaSmith"".""MissingIndexesAndConstraintsQuench""(p_WhatIf := {_whatIfOnly});
CALL ""SchemaSmith"".""FixupTableOwnership""(p_ProductName := '{EscapeSqlLiteral(_product.Name)}', p_TemplateName := '{EscapeSqlLiteral(_template.Name)}', p_SchemaName := '{EscapeSqlLiteral(_schemaName)}');
CALL ""SchemaSmith"".""FixupIndexOwnership""(p_ProductName := '{EscapeSqlLiteral(_product.Name)}', p_TemplateName := '{EscapeSqlLiteral(_template.Name)}', p_SchemaName := '{EscapeSqlLiteral(_schemaName)}');
";
                break;
            case Platform.MySQL:
            {
                if (!MySqlTempTablesExist(tableCommand))
                    ParseMySqlTableJson(tableCommand);
                var whatIf = _whatIfOnly == "1" ? 1 : 0;
                var dropUnknown = _dropUnknownIndexes == "1" ? 1 : 0;
                tableCommand.CommandText = _template.IndexOnlyTableQuenches
                    ? $"CALL SchemaSmith_IndexOnlyQuench('{EscapeSqlLiteral(_product.Name)}', '{EscapeSqlLiteral(_databaseName)}', {whatIf}, {dropUnknown})"
                    : $"CALL SchemaSmith_MissingIndexesAndConstraintsQuench('{EscapeSqlLiteral(_product.Name)}', '{EscapeSqlLiteral(_databaseName)}', {whatIf}, {dropUnknown})";
                break;
            }
        }

        _debugFileLocation = LogSqlScript(GetDebugFileName("Quench Indexes"), tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand, retryOnDeadlock: true);
        _debugFileLocation = "";
    }

    internal void QuenchForeignKeys(IDbCommand tableCommand)
    {
        if (_template.Tables.Count == 0)
            return;

        SafeProgressLog("  Quenching foreign keys");

        switch (_product.Platform)
        {
            case Platform.SqlServer:
                tableCommand.CommandText = $"EXEC [{_databaseName}].SchemaSmith.ForeignKeyQuench @ProductName = '{EscapeSqlLiteral(_product.Name)}', @WhatIf = {_whatIfOnly}";
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
                tableCommand.CommandText = $"CALL SchemaSmith_ForeignKeyQuench('{EscapeSqlLiteral(_product.Name)}', '{EscapeSqlLiteral(_databaseName)}', {whatIf}, {dropUnknown})";
                break;
            }
        }

        _debugFileLocation = LogSqlScript(GetDebugFileName("Quench Foreign Keys"), tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand, retryOnDeadlock: true);
        _debugFileLocation = "";
    }

    internal void QuenchMaterializedViews(IDbCommand tableCommand)
    {
        SafeProgressLog("  Quenching materialized views");

        var updateFillFactor = _template.UpdateFillFactor.ToString().ToLower();
        tableCommand.CommandText = $@"CALL ""SchemaSmith"".""MaterializedViewQuench""('{EscapeSqlLiteral(_product.Name)}', '{EscapeSqlLiteral(IterationMaterializedViewSchema)}', {_whatIfOnly}, {updateFillFactor}, '{EscapeSqlLiteral(_template.Name)}', '{EscapeSqlLiteral(_schemaName)}');";

        _debugFileLocation = LogSqlScript(GetDebugFileName("Quench Materialized Views"), tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand, retryOnDeadlock: true);
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

        // Pass ALL indexed views to the proc; ShouldApplyExpression is evaluated per-target
        // server-side (mirroring PostgreSQL materialized views), so no C# pre-filtering.
        // Route through the iteration-aware schema string so {{SchemaName}} substitution
        // (schema templates) is already applied; for regular templates it's the full set verbatim.
        var viewSchema = IterationIndexedViewSchema;
        var updateFillFactor = _template.UpdateFillFactor.ToString().ToLower();
        // B5 fix: thread @TemplateName + @SchemaName so the existing-views lookup in the proc
        // is scoped to the iteration's schema. Regular templates pass @SchemaName = '' and the
        // proc falls through to today's all-schemas behavior.
        tableCommand.CommandText = $@"EXEC [SchemaSmith].[IndexedViewQuench] @ProductName = '{EscapeSqlLiteral(_product.Name)}', @IndexedViewSchema = '{EscapeSqlLiteral(viewSchema)}', @WhatIf = {_whatIfOnly}, @UpdateFillFactor = {updateFillFactor}, @TemplateName = N'{EscapeSqlLiteral(_template.Name)}', @SchemaName = N'{EscapeSqlLiteral(_schemaName)}';";

        _debugFileLocation = LogSqlScript(GetDebugFileName("Quench Indexed Views"), tableCommand.CommandText);
        ExecuteNonQueryHandlingMessages(tableCommand, retryOnDeadlock: true);
        _debugFileLocation = "";
    }

    #endregion

    #region MySQL Temp Tables

    private void ParseMySqlTableJson(IDbCommand command)
    {
        var tableJson = !string.IsNullOrEmpty(_template.TableSchema)
            ? _template.TableSchema
            : JsonHelper.SerializeAll(_template.Tables);
        command.CommandText = $"CALL SchemaSmith_ParseTableJson('{EscapeSqlLiteral(_databaseName)}', @tableJson)";
        _debugFileLocation = LogSqlScript(GetDebugFileName("Parse Table Json"), command.CommandText.Replace("@tableJson", $"'{EscapeSqlLiteral(tableJson)}'"));
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
            : ConnectionString.RetargetDatabase(connectionStringOverride, _databaseName, _product.Platform);
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

    // Bounded retry policy for deadlock victims — defense-in-depth. Internal (not const) so tests
    // can shrink the delay/attempts. The PostgreSQL convergence-proc deadlock that originally
    // motivated this is now prevented at the SQL level (MissingIndexesAndConstraintsQuench reads
    // pg_catalog instead of materialising information_schema.columns); retry remains to cover
    // residual cross-iteration contention such as SQL Server IndexedViewQuench. The convergence
    // procs are idempotent (recompute desired-vs-existing every run), so a victim re-converges cleanly.
    internal int MaxDeadlockAttempts = 20;
    internal int DeadlockRetryBaseMs = 100;

    /// <param name="retryOnDeadlock">
    /// When <c>true</c>, a deadlock-victim failure is retried (bounded, with backoff). Only pass
    /// <c>true</c> for idempotent convergence procs (table/index/constraint/FK/view quenches) —
    /// never for run-once migrations or arbitrary user scripts, whose re-execution isn't safe.
    /// </param>
    internal void ExecuteNonQueryHandlingMessages(IDbCommand command, bool retryOnDeadlock = false)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                ExecuteNonQueryOnce(command);
                return;
            }
            catch (Exception ex) when (retryOnDeadlock
                                       && attempt < MaxDeadlockAttempts
                                       && DeadlockClassifier.IsDeadlock(ex))
            {
                var delayMs = DeadlockBackoffMs(attempt);
                SafeProgressLog(
                    $"    Deadlock contention from a parallel iteration; retrying " +
                    $"(attempt {attempt + 1} of {MaxDeadlockAttempts}){(delayMs > 0 ? $" after {delayMs} ms" : "")}");
                if (delayMs > 0) Thread.Sleep(delayMs);
            }
        }
    }

    // "Full jitter" exponential backoff (sleep uniformly in [0, capped-exponential]). If several
    // iterations do deadlock at once, full jitter de-synchronises them far better than
    // fixed-delay-plus-small-jitter, so retries don't re-collide in lockstep.
    private int DeadlockBackoffMs(int attempt)
    {
        if (DeadlockRetryBaseMs <= 0) return 0;
        var capped = Math.Min(DeadlockRetryBaseMs * (1 << Math.Min(attempt - 1, 5)), 2000);
        return Random.Shared.Next(0, capped + 1);
    }

    private void ExecuteNonQueryOnce(IDbCommand command)
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

    private string LogSqlScript(string name, string sql)
    {
        try
        {
            var dir = ResolveArtifactDirectory();
            DirectoryWrapper.GetFromFactory().CreateDirectory(dir);
            var path = Path.Combine(dir, name);
            FileWrapper.GetFromFactory().WriteAllText(path, sql);
            return path;
        }
        catch (Exception ex)
        {
            SafeProgressLog($"    Could not write debug SQL artifact '{name}': {ex.Message}");
            return "";
        }
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
        catch (Exception ex) when (SentinelClassifier.IsShouldNotApply(ex))
        {
            script.HasBeenQuenched = true;
            script.Error = null;
            script.Outcome = ScriptOutcome.Skipped;
            SafeProgressLog($"    Skipped (ShouldNotApply): {script.LogPath}");
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

        var directory = ResolveArtifactDirectory();
        foreach (var sqlScript in scripts.Where(s => !s.HasBeenQuenched))
        {
            sqlScript.Outcome = ScriptOutcome.Failed;

            try
            {
                var header = $"Failed: {_server}.{_databaseName}" +
                             $"{(string.IsNullOrEmpty(_schemaName) ? "" : $" [Schema: {_schemaName}]")}" +
                             $" [{sqlScript.LogPath}] — {sqlScript.Error?.Message}";
                var content = ResolvedSqlArtifactWriter.BuildArtifact(header, sqlScript.Batches, FailingBatchIndex(sqlScript));
                if (ScrubArtifactsEnabled)
                    content = ResolvedSqlArtifactWriter.Scrub(content, SensitiveTokenValues());

                var fileName = GetDebugFileName($"Failed {Path.GetFileNameWithoutExtension(sqlScript.Name)}");
                var path = ResolvedSqlArtifactWriter.Write(directory, fileName, content);
                SafeProgressLogError($"    Resolved SQL written to: {path}");
                SafeErrorLogError($"Unable to quench '{sqlScript.LogPath}': {sqlScript.Error?.Message} — resolved SQL: {path}");
            }
            catch (Exception artifactEx)
            {
                SafeProgressLog($"    Could not write resolved-SQL artifact for '{sqlScript.LogPath}': {artifactEx.Message}");
            }

            SafeProgressLogError($"Unable to quench '{sqlScript.LogPath}': {sqlScript.Error?.Message}");
        }

        throw new Exception("Unable to quench all scripts");
    }

    private static int FailingBatchIndex(SqlScript script) => script.Batches.Count - 1;

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

    /// <summary>
    /// Builds the debug script filename used by <see cref="LogSqlScript"/>. For schema-template
    /// iterations the schema name is appended as a suffix so parallel iterations of the same
    /// database write to distinct files — without it, sibling iterations collide on a single
    /// path and hit a Win32 file-sharing violation that throws before the SQL batch executes
    /// (slice-3 audit bug B2). Regular templates leave <c>_schemaName</c> empty, so the
    /// suffix is omitted and the pre-slice-3 filename shape is preserved.
    /// </summary>
    internal string GetDebugFileName(string label)
    {
        var schemaSuffix = string.IsNullOrEmpty(_schemaName) ? "" : $".{_schemaName}";
        return $"SchemaQuench - {label} {_server}.{_databaseName}{schemaSuffix}.sql";
    }

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
                // Preserve the error number so deadlock detection (1205) keys on the code, not
                // the locale-dependent message. Errors surfaced here (severity ≤ 16) are never
                // thrown as SqlException on this connection (FireInfoMessageEventOnUserErrors).
                _infoMessageException = new SqlServerErrorException(err.Number, err.Message);

                // Deadlock victims (1205) are recoverable: ExecuteNonQueryHandlingMessages retries
                // the idempotent convergence proc. Don't hard-log them here — otherwise a deadlock
                // that the retry *recovers* still surfaces as a scary error (and unlike PostgreSQL,
                // where the deadlock is thrown and caught silently by the retry loop, SQL Server
                // delivers it through this InfoMessage handler before the retry loop sees it). If
                // retries exhaust, the propagated exception is logged by the caller's per-iteration
                // failure handling; the retry loop logs an Info note on each recoverable attempt.
                if (err.Number == 1205) continue;

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
