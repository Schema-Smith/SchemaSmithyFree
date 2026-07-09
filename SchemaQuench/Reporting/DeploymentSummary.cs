// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;

namespace SchemaQuench.Reporting;

/// <summary>
/// Root of the Deployment Summary Report record graph (#243). This is the frozen v1 JSON
/// contract the paid Intelligence add-ons deserialize — field names, casing, nesting, and enum
/// string values must not change without a schema-version bump. Purely a data model: nothing in
/// this file (or <see cref="DeploymentSummaryJson"/>) wires into the run, populates itself, or
/// scrubs content. A later slice (E4) builds and emits instances; E5 fills in
/// <see cref="ObjectChangeSummary"/> detail.
/// </summary>
public sealed record DeploymentSummary(
    string SchemaVersion,
    string Tool,
    string ToolVersion,
    RunInfo Run,
    IReadOnlyList<TargetSummary> Targets,
    IReadOnlyList<MigrationScriptRecord> MigrationScripts,
    TimingSummary Timing,
    IReadOnlyList<FailureRecord> Failures,
    WhatIfSummary WhatIf,
    ObjectChangeSummary ObjectChanges);

public sealed record RunInfo(
    string Product,
    string Platform,
    DateTime StartedUtc,
    DateTime FinishedUtc,
    long DurationMs,
    RunMode Mode,
    RunOutcome Outcome,
    int ExitCode,
    bool ResumedFromCheckpoint);

public sealed record TargetSummary(
    string Server,
    string Database,
    string Schema,
    string Template,
    TargetOutcome Outcome,
    long DurationMs,
    IReadOnlyList<TargetSlotTiming> Slots);

/// <summary>Per-target slot timing — distinct from E1's aggregate <see cref="SlotTiming"/>.</summary>
public sealed record TargetSlotTiming(string Slot, long DurationMs, int ScriptsRun);

/// <summary>
/// <see cref="Outcome"/> is a plain string (e.g. <c>"Ran"</c>) in Phase 1 — intentionally not an
/// enum yet; do not over-model.
/// </summary>
public sealed record MigrationScriptRecord(
    string Path,
    string Slot,
    string Template,
    string Schema,
    string Server,
    string Database,
    string Outcome);

/// <summary>
/// The three list types (<see cref="SlotTiming"/>, <see cref="DbTiming"/>,
/// <see cref="BottleneckEntry"/>) are E1's existing records from <c>RunTiming.cs</c> — reused
/// verbatim, not redefined or projected.
/// </summary>
public sealed record TimingSummary(
    long TotalMs,
    IReadOnlyList<SlotTiming> BySlot,
    IReadOnlyList<DbTiming> ByDatabase,
    IReadOnlyList<BottleneckEntry> Bottlenecks);

public sealed record WhatIfSummary(
    IReadOnlyList<WhatIfEntry> WouldApply,
    IReadOnlyList<WhatIfEntry> WouldSkip,
    IReadOnlyList<WhatIfEntry> WouldDeliver);

public sealed record WhatIfEntry(string Scope, string Script);

public sealed record ObjectChangeSummary(
    bool Instrumented,
    CreatedCounts Created,
    ModifiedCounts Modified,
    DroppedCounts Dropped,
    IReadOnlyList<ObjectChangeDetail> Details);

public sealed record CreatedCounts(
    int Tables,
    int Indexes,
    int Constraints,
    int ForeignKeys,
    int Procedures,
    int Views,
    int Functions);

public sealed record ModifiedCounts(int Tables, int Columns);

public sealed record DroppedCounts(int Tables, int Indexes, int Constraints, int ForeignKeys);

public sealed record ObjectChangeDetail(string ObjectType, string ObjectName, string Action);

public enum RunMode
{
    Quench,
    WhatIf,
    Validate
}

public enum RunOutcome
{
    Success,
    PartialFailure,
    Aborted
}

public enum TargetOutcome
{
    Success,
    Failed,
    Skipped
}
