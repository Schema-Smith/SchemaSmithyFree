// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SchemaQuench.Reporting;

namespace SchemaQuench.UnitTests;

/// <summary>
/// E4d: proves the PURE mapping from run-fact captures (E1 RunTiming, E4a TargetResult, E4b
/// MigrationScriptRun, E4c WhatIfRun, Group D FailureRecord) into the frozen v1
/// <see cref="DeploymentSummary"/> object graph. Asserts on the object graph directly — E2's
/// <c>DeploymentSummaryJsonTests</c> already covers serialization.
/// </summary>
[TestFixture]
public class DeploymentSummaryAssemblerTests
{
    private static readonly DateTime StartedUtc = new(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinishedUtc = new(2026, 7, 9, 10, 5, 0, DateTimeKind.Utc);

    private static RunTiming BuildTiming()
    {
        var timing = new RunTiming();
        timing.Record("[srv].[dbA]", "dbA", "ModifiedTables", 8021, 3);
        timing.Record("[srv].[dbA]", "dbA", "ForeignKeys", 10, 0);
        timing.Record("[srv].[dbB]", "dbB", "ModifiedTables", 100, 1);
        return timing;
    }

    private static DeploymentSummary AssembleWith(
        RunMode mode = RunMode.Quench,
        IReadOnlyCollection<TargetResult> targets = null,
        RunTiming timing = null,
        IReadOnlyList<MigrationScriptRun> migrationScripts = null,
        IReadOnlyList<WhatIfRun> whatIfEntries = null,
        IReadOnlyList<FailureRecord> failures = null,
        long bottleneckThresholdMs = 5000)
    {
        targets ??= new List<TargetResult>
        {
            new("[srv].[dbA]", "srv", "dbA", "sales", "TenantSchema", TargetOutcome.Success, 8031),
            new("[srv].[dbB]", "srv", "dbB", "", "TenantDatabase", TargetOutcome.Failed, 100)
        };
        timing ??= BuildTiming();
        migrationScripts ??= new List<MigrationScriptRun>();
        whatIfEntries ??= new List<WhatIfRun>();
        failures ??= new List<FailureRecord>();

        return DeploymentSummaryAssembler.Assemble(
            product: "MyProduct",
            platform: "PostgreSQL",
            toolVersion: "2.3.0",
            startedUtc: StartedUtc,
            finishedUtc: FinishedUtc,
            mode: mode,
            outcome: RunOutcome.PartialFailure,
            exitCode: 2,
            resumedFromCheckpoint: false,
            targets: targets,
            timing: timing,
            migrationScripts: migrationScripts,
            whatIfEntries: whatIfEntries,
            failures: failures,
            bottleneckThresholdMs: bottleneckThresholdMs);
    }

    [Test]
    public void Assemble_MapsRunMetadata_Verbatim()
    {
        var summary = AssembleWith();

        Assert.That(summary.SchemaVersion, Is.EqualTo("1.0"));
        Assert.That(summary.Tool, Is.EqualTo("SchemaQuench"));
        Assert.That(summary.ToolVersion, Is.EqualTo("2.3.0"));

        Assert.That(summary.Run.Product, Is.EqualTo("MyProduct"));
        Assert.That(summary.Run.Platform, Is.EqualTo("PostgreSQL"));
        Assert.That(summary.Run.StartedUtc, Is.EqualTo(StartedUtc));
        Assert.That(summary.Run.FinishedUtc, Is.EqualTo(FinishedUtc));
        Assert.That(summary.Run.DurationMs, Is.EqualTo(summary.Timing.TotalMs));
        Assert.That(summary.Run.Mode, Is.EqualTo(RunMode.Quench));
        Assert.That(summary.Run.Outcome, Is.EqualTo(RunOutcome.PartialFailure));
        Assert.That(summary.Run.ExitCode, Is.EqualTo(2));
        Assert.That(summary.Run.ResumedFromCheckpoint, Is.False);
    }

    [Test]
    public void Assemble_MapsTargets_OutcomeAndDuration_AndEmptySchemaToNull()
    {
        var summary = AssembleWith();

        var dbA = summary.Targets.Single(t => t.Database == "dbA");
        Assert.That(dbA.Server, Is.EqualTo("srv"));
        Assert.That(dbA.Schema, Is.EqualTo("sales"));
        Assert.That(dbA.Template, Is.EqualTo("TenantSchema"));
        Assert.That(dbA.Outcome, Is.EqualTo(TargetOutcome.Success));
        Assert.That(dbA.DurationMs, Is.EqualTo(8031));

        var dbB = summary.Targets.Single(t => t.Database == "dbB");
        Assert.That(dbB.Schema, Is.Null, "empty Schema on a DB-level target must map to null");
        Assert.That(dbB.Outcome, Is.EqualTo(TargetOutcome.Failed));
    }

    [Test]
    public void Assemble_PopulatesPerTargetSlots_OnlyFromThatTargetsScope()
    {
        var summary = AssembleWith();

        var dbA = summary.Targets.Single(t => t.Database == "dbA");
        Assert.That(dbA.Slots.Select(s => s.Slot), Is.EquivalentTo(new[] { "ModifiedTables", "ForeignKeys" }));
        Assert.That(dbA.Slots.Single(s => s.Slot == "ModifiedTables").DurationMs, Is.EqualTo(8021));
        Assert.That(dbA.Slots.Single(s => s.Slot == "ModifiedTables").ScriptsRun, Is.EqualTo(3));

        var dbB = summary.Targets.Single(t => t.Database == "dbB");
        Assert.That(dbB.Slots.Select(s => s.Slot), Is.EquivalentTo(new[] { "ModifiedTables" }));
        Assert.That(dbB.Slots.Single().DurationMs, Is.EqualTo(100));
    }

    [Test]
    public void Assemble_MapsMigrationScripts_OutcomeRan_AndEmptySchemaDatabaseToNull()
    {
        var scripts = new List<MigrationScriptRun>
        {
            new("srv", "dbA", "sales", "TenantSchema", "Before", "Scripts/Before/001.sql"),
            new("srv", "", "", "", "After", "Product/After/reindex.sql")
        };

        var summary = AssembleWith(migrationScripts: scripts);

        var scoped = summary.MigrationScripts.Single(m => m.Path == "Scripts/Before/001.sql");
        Assert.That(scoped.Slot, Is.EqualTo("Before"));
        Assert.That(scoped.Template, Is.EqualTo("TenantSchema"));
        Assert.That(scoped.Schema, Is.EqualTo("sales"));
        Assert.That(scoped.Server, Is.EqualTo("srv"));
        Assert.That(scoped.Database, Is.EqualTo("dbA"));
        Assert.That(scoped.Outcome, Is.EqualTo("Ran"));

        var productLevel = summary.MigrationScripts.Single(m => m.Path == "Product/After/reindex.sql");
        Assert.That(productLevel.Schema, Is.Null, "empty Schema must map to null");
        Assert.That(productLevel.Database, Is.Null, "empty Database must map to null");
        Assert.That(productLevel.Template, Is.EqualTo(""), "empty Template is left as-is (product-level scripts have no template)");
        Assert.That(productLevel.Outcome, Is.EqualTo("Ran"));
    }

    [Test]
    public void Assemble_MapsTimingAggregates_FromRunTiming_UsingPassedBottleneckThreshold()
    {
        var timing = BuildTiming();

        var summary = AssembleWith(timing: timing, bottleneckThresholdMs: 5000);

        Assert.That(summary.Timing.TotalMs, Is.EqualTo(timing.TotalMs));
        Assert.That(summary.Run.DurationMs, Is.EqualTo(timing.TotalMs));
        Assert.That(summary.Timing.BySlot.Select(s => s.Slot), Is.EquivalentTo(new[] { "ModifiedTables", "ForeignKeys" }));
        Assert.That(summary.Timing.ByDatabase.Select(d => d.Database), Is.EquivalentTo(new[] { "dbA", "dbB" }));

        // 8021ms entry is over the 5000ms threshold; 10ms and 100ms are not.
        Assert.That(summary.Timing.Bottlenecks.Count, Is.EqualTo(1));
        Assert.That(summary.Timing.Bottlenecks[0].DurationMs, Is.EqualTo(8021));
    }

    [Test]
    public void Assemble_PassesFailuresThrough()
    {
        var failure = new FailureRecord("AfterScripts", "[srv].[dbA]", "boom", new List<string> { "line 1" }, "artifact.sql");

        var summary = AssembleWith(failures: new List<FailureRecord> { failure });

        Assert.That(summary.Failures.Count, Is.EqualTo(1));
        Assert.That(summary.Failures[0], Is.EqualTo(failure));
    }

    [Test]
    public void Assemble_WhatIfMode_GroupsEntriesByCategory()
    {
        var entries = new List<WhatIfRun>
        {
            new(WhatIfCategory.Apply, "[srv].[dbA]", "ALTER TABLE ..."),
            new(WhatIfCategory.Skip, "[srv].[dbA]", "Scripts/Templates/002.sql"),
            new(WhatIfCategory.Deliver, "[srv].[dbA]", "Scripts/Data/003.sql"),
            new(WhatIfCategory.Apply, "[srv].[dbB]", "CREATE INDEX ...")
        };

        var summary = AssembleWith(mode: RunMode.WhatIf, whatIfEntries: entries);

        Assert.That(summary.WhatIf, Is.Not.Null);
        Assert.That(summary.WhatIf.WouldApply.Count, Is.EqualTo(2));
        Assert.That(summary.WhatIf.WouldApply.Select(e => e.Script), Does.Contain("ALTER TABLE ..."));
        Assert.That(summary.WhatIf.WouldApply.Select(e => e.Script), Does.Contain("CREATE INDEX ..."));

        Assert.That(summary.WhatIf.WouldSkip.Count, Is.EqualTo(1));
        Assert.That(summary.WhatIf.WouldSkip[0].Scope, Is.EqualTo("[srv].[dbA]"));
        Assert.That(summary.WhatIf.WouldSkip[0].Script, Is.EqualTo("Scripts/Templates/002.sql"));

        Assert.That(summary.WhatIf.WouldDeliver.Count, Is.EqualTo(1));
        Assert.That(summary.WhatIf.WouldDeliver[0].Script, Is.EqualTo("Scripts/Data/003.sql"));
    }

    [Test]
    public void Assemble_NonWhatIfMode_WhatIfIsNull_EvenWhenEntriesNonEmpty()
    {
        var entries = new List<WhatIfRun> { new(WhatIfCategory.Apply, "[srv].[dbA]", "ALTER TABLE ...") };

        var summary = AssembleWith(mode: RunMode.Quench, whatIfEntries: entries);

        Assert.That(summary.WhatIf, Is.Null, "mode governs WhatIf presence, not entry count");
    }

    [Test]
    public void Assemble_ObjectChanges_NotInstrumented_AllCountsZero()
    {
        var summary = AssembleWith();

        Assert.That(summary.ObjectChanges.Instrumented, Is.False);
        Assert.That(summary.ObjectChanges.Created, Is.EqualTo(new CreatedCounts(0, 0, 0, 0, 0, 0, 0)));
        Assert.That(summary.ObjectChanges.Modified, Is.EqualTo(new ModifiedCounts(0, 0)));
        Assert.That(summary.ObjectChanges.Dropped, Is.EqualTo(new DroppedCounts(0, 0, 0, 0)));
        Assert.That(summary.ObjectChanges.Details, Is.Empty);
    }
}
