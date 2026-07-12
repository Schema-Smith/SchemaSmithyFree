// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SchemaQuench.Reporting;

/// <summary>
/// Pure mapping from the run-fact captures (#243, E1 <see cref="RunTiming"/>, E4a
/// <see cref="TargetResult"/>, E4b <see cref="MigrationScriptRun"/>, E4c <see cref="WhatIfRun"/>,
/// Group D <see cref="FailureRecord"/>) into the frozen v1 <see cref="DeploymentSummary"/> object
/// graph. No I/O, no clock, no config — every input (including timestamps) is a parameter. Wiring
/// this into the actual run and writing the report to disk is a later slice (E4e); this class only
/// assembles the object graph.
/// </summary>
public static class DeploymentSummaryAssembler
{
    public static DeploymentSummary Assemble(
        string product,
        string platform,
        string toolVersion,
        DateTime startedUtc,
        DateTime finishedUtc,
        RunMode mode,
        RunOutcome outcome,
        int exitCode,
        bool resumedFromCheckpoint,
        IReadOnlyCollection<TargetResult> targets,
        RunTiming timing,
        IReadOnlyList<MigrationScriptRun> migrationScripts,
        IReadOnlyList<WhatIfRun> whatIfEntries,
        IReadOnlyList<FailureRecord> failures,
        long bottleneckThresholdMs,
        ChangeAuditCapture changeAudit,
        bool protectedModeEnabled = false)
    {
        var run = new RunInfo(
            Product: product,
            Platform: platform,
            StartedUtc: startedUtc,
            FinishedUtc: finishedUtc,
            DurationMs: timing.TotalMs,
            Mode: mode,
            Outcome: outcome,
            ExitCode: exitCode,
            ResumedFromCheckpoint: resumedFromCheckpoint);

        var mappedTargets = targets
            .Select(tr => new TargetSummary(
                Server: tr.Server,
                Database: tr.Database,
                Schema: NullIfEmpty(tr.Schema),
                Template: tr.Template,
                Outcome: tr.Outcome,
                DurationMs: tr.DurationMs,
                Slots: timing.SlotsForScope(tr.ScopeKey)))
            .ToList();

        var mappedMigrationScripts = migrationScripts
            .Select(m => new MigrationScriptRecord(
                Path: m.Path,
                Slot: m.Slot,
                Template: m.Template,
                Schema: NullIfEmpty(m.Schema),
                Server: m.Server,
                Database: NullIfEmpty(m.Database),
                Outcome: "Ran"))
            .ToList();

        var timingSummary = new TimingSummary(
            TotalMs: timing.TotalMs,
            BySlot: timing.BySlot(),
            ByDatabase: timing.ByDatabase(),
            Bottlenecks: timing.Bottlenecks(bottleneckThresholdMs));

        var whatIf = mode == RunMode.WhatIf ? BuildWhatIfSummary(whatIfEntries) : null;

        var objectChanges = BuildObjectChanges(changeAudit);

        var preventDrop = protectedModeEnabled ? BuildPreventDrop(changeAudit) : null;

        return new DeploymentSummary(
            SchemaVersion: "1.0",
            Tool: "SchemaQuench",
            ToolVersion: toolVersion,
            Run: run,
            Targets: mappedTargets,
            MigrationScripts: mappedMigrationScripts,
            Timing: timingSummary,
            Failures: failures,
            WhatIf: whatIf,
            ObjectChanges: objectChanges,
            PreventDrop: preventDrop);
    }

    /// <summary>
    /// No-drop protection tier manifest (#270 Slice E). Itemizes the objects the procs reported as
    /// <c>wouldDrop</c> — dropped by absence but suppressed because the environment is protected.
    /// Emitted only when protected mode is enabled; empty <see cref="PreventDropSummary.WouldDrop"/>
    /// means protection was on but nothing was suppressed this run.
    /// </summary>
    private static PreventDropSummary BuildPreventDrop(ChangeAuditCapture changeAudit)
    {
        var wouldDrop = changeAudit is { Instrumented: true }
            ? changeAudit.Snapshot()
                .Where(r => r.Action == "wouldDrop")
                .Select(r => new WouldDropEntry(r.ObjectType, r.ObjectName))
                .ToArray()
            : Array.Empty<WouldDropEntry>();
        return new PreventDropSummary(Enabled: true, WouldDrop: wouldDrop);
    }

    /// <summary>
    /// Aggregates the object-change audit (#243 E5) into the report's objectChanges block. The block
    /// is <c>instrumented:false</c> (all zeros) unless the run's engine produced a real audit read —
    /// see <see cref="ChangeAuditCapture.Instrumented"/>. created/modified/dropped counts are
    /// verified change from the 4 table procs (object types table/column/index/constraint/foreignKey);
    /// object scripts (procedures/views/functions) never "created" — they "ran" (scriptsRan).
    /// </summary>
    private static ObjectChangeSummary BuildObjectChanges(ChangeAuditCapture changeAudit)
    {
        if (changeAudit is not { Instrumented: true })
            return new ObjectChangeSummary(
                Instrumented: false,
                Created: new CreatedCounts(0, 0, 0, 0, 0, 0, 0),
                Modified: new ModifiedCounts(0, 0),
                Dropped: new DroppedCounts(0, 0, 0, 0),
                ScriptsRan: 0,
                Details: Array.Empty<ObjectChangeDetail>());

        var rows = changeAudit.Snapshot();

        int Count(string type, string action) =>
            rows.Count(r => r.ObjectType == type && r.Action == action);

        var created = new CreatedCounts(
            Tables: Count("table", "created"),
            Indexes: Count("index", "created"),
            Constraints: Count("constraint", "created"),
            ForeignKeys: Count("foreignKey", "created"),
            Procedures: 0,
            Views: 0,
            Functions: 0);

        var modified = new ModifiedCounts(
            Tables: Count("table", "modified") + Count("table", "renamed"),
            Columns: Count("column", "modified"));

        var dropped = new DroppedCounts(
            Tables: Count("table", "dropped"),
            Indexes: Count("index", "dropped"),
            Constraints: Count("constraint", "dropped"),
            ForeignKeys: Count("foreignKey", "dropped"));

        var scriptsRan = rows.Count(r => r.Action == "ran");
        // 'wouldDrop' rows are protection-suppressed drops, not changes that occurred — they belong
        // to the PreventDropSummary manifest, not the object-change detail.
        var details = rows
            .Where(r => r.Action != "wouldDrop")
            .Select(r => new ObjectChangeDetail(r.ObjectType, r.ObjectName, r.Action))
            .ToArray();

        return new ObjectChangeSummary(true, created, modified, dropped, scriptsRan, details);
    }

    private static WhatIfSummary BuildWhatIfSummary(IReadOnlyList<WhatIfRun> whatIfEntries)
    {
        IReadOnlyList<WhatIfEntry> ForCategory(WhatIfCategory category) =>
            whatIfEntries
                .Where(w => w.Category == category)
                .Select(w => new WhatIfEntry(w.Scope, w.Script))
                .ToList();

        return new WhatIfSummary(
            WouldApply: ForCategory(WhatIfCategory.Apply),
            WouldSkip: ForCategory(WhatIfCategory.Skip),
            WouldDeliver: ForCategory(WhatIfCategory.Deliver));
    }

    private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
