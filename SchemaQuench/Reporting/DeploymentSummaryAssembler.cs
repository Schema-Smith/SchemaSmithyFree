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
        long bottleneckThresholdMs)
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

        var objectChanges = new ObjectChangeSummary(
            Instrumented: false,
            Created: new CreatedCounts(0, 0, 0, 0, 0, 0, 0),
            Modified: new ModifiedCounts(0, 0),
            Dropped: new DroppedCounts(0, 0, 0, 0),
            Details: Array.Empty<ObjectChangeDetail>());

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
            ObjectChanges: objectChanges);
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
