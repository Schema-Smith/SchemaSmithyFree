// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SchemaQuench.Reporting;

namespace SchemaQuench.UnitTests;

/// <summary>
/// E2: proves the DeploymentSummary record graph serializes to the frozen v1 JSON contract
/// (field names, camelCase keys, enum-as-string, null preservation). The paid Intelligence
/// add-ons deserialize this JSON, so assertions parse the output with JObject/SelectToken rather
/// than substring-matching, and pin down the exact shapes a consumer would rely on.
/// </summary>
[TestFixture]
public class DeploymentSummaryJsonTests
{
    private static DeploymentSummary BuildFullyPopulatedSummary()
    {
        var run = new RunInfo(
            Product: "MyProduct",
            Platform: "PostgreSQL",
            StartedUtc: new DateTime(2026, 7, 8, 14, 22, 3, DateTimeKind.Utc),
            FinishedUtc: new DateTime(2026, 7, 8, 14, 23, 31, DateTimeKind.Utc),
            DurationMs: 88214,
            Mode: RunMode.Quench,
            Outcome: RunOutcome.PartialFailure,
            ExitCode: 2,
            ResumedFromCheckpoint: false);

        var targets = new List<TargetSummary>
        {
            new(
                Server: "primary",
                Database: "TenantA",
                Schema: "sales",
                Template: "TenantSchema",
                Outcome: TargetOutcome.Success,
                DurationMs: 12044,
                Slots: new List<TargetSlotTiming> { new("ModifiedTables", 8021, 0) }),
            new(
                Server: "primary",
                Database: "TenantB",
                Schema: null,
                Template: "TenantDatabase",
                Outcome: TargetOutcome.Failed,
                DurationMs: 3000,
                Slots: new List<TargetSlotTiming>())
        };

        var migrationScripts = new List<MigrationScriptRecord>
        {
            new(
                Path: "After Scripts/01_seed.sql",
                Slot: "After",
                Template: "TenantSchema",
                Schema: "sales",
                Server: "primary",
                Database: "TenantA",
                Outcome: "Ran")
        };

        var timing = new TimingSummary(
            TotalMs: 88214,
            BySlot: new List<SlotTiming> { new("ModifiedTables", 41230, 5) },
            ByDatabase: new List<DbTiming> { new("TenantA", 12044) },
            Bottlenecks: new List<BottleneckEntry> { new("primary/TenantA [sales]", "ModifiedTables", 8021) });

        var failures = new List<FailureRecord>
        {
            new(
                Phase: "AfterScripts",
                ScopeKey: "[primary]",
                Error: "boom",
                ContextTail: new List<string> { "line 1", "line 2" },
                ArtifactPath: "SchemaQuench - After primary.public.sql")
        };

        var whatIf = new WhatIfSummary(
            WouldApply: new List<WhatIfEntry> { new("primary/TenantA", "ALTER TABLE ...") },
            WouldSkip: new List<WhatIfEntry>(),
            WouldDeliver: new List<WhatIfEntry>());

        var objectChanges = new ObjectChangeSummary(
            Instrumented: false,
            Created: new CreatedCounts(0, 0, 0, 0, 0, 0, 0),
            Modified: new ModifiedCounts(0, 0),
            Dropped: new DroppedCounts(0, 0, 0, 0),
            Details: new List<ObjectChangeDetail>());

        return new DeploymentSummary(
            SchemaVersion: "1.0",
            Tool: "SchemaQuench",
            ToolVersion: "2.3.0",
            Run: run,
            Targets: targets,
            MigrationScripts: migrationScripts,
            Timing: timing,
            Failures: failures,
            WhatIf: whatIf,
            ObjectChanges: objectChanges);
    }

    [Test]
    public void Serialize_EmitsFrozenTopLevelIdentity()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        Assert.That(json.SelectToken("schemaVersion")?.Value<string>(), Is.EqualTo("1.0"));
        Assert.That(json.SelectToken("tool")?.Value<string>(), Is.EqualTo("SchemaQuench"));
    }

    [Test]
    public void Serialize_EmitsEveryTopLevelSection()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        Assert.That(json["run"], Is.Not.Null);
        Assert.That(json["targets"], Is.Not.Null);
        Assert.That(json["migrationScripts"], Is.Not.Null);
        Assert.That(json["timing"], Is.Not.Null);
        Assert.That(json["failures"], Is.Not.Null);
        Assert.That(json["whatIf"], Is.Not.Null);
        Assert.That(json["objectChanges"], Is.Not.Null);
    }

    [Test]
    public void Serialize_UsesStringEnumConverter_NotIntegers()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        Assert.That(json.SelectToken("run.mode")?.Value<string>(), Is.EqualTo("Quench"));
        Assert.That(json.SelectToken("run.outcome")?.Value<string>(), Is.EqualTo("PartialFailure"));
        Assert.That(json.SelectToken("targets[0].outcome")?.Value<string>(), Is.EqualTo("Success"));
        Assert.That(json.SelectToken("targets[1].outcome")?.Value<string>(), Is.EqualTo("Failed"));
    }

    [Test]
    public void Serialize_UsesCamelCaseKeys_NotPascalCase()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        Assert.That(json.SelectToken("run.durationMs"), Is.Not.Null);
        Assert.That(json.SelectToken("run.resumedFromCheckpoint"), Is.Not.Null);
        Assert.That(json.SelectToken("schemaVersion"), Is.Not.Null);

        Assert.That(json.SelectToken("run.DurationMs"), Is.Null);
        Assert.That(json.SelectToken("SchemaVersion"), Is.Null);
    }

    [Test]
    public void Serialize_EmitsObjectChangesInstrumentedMarker_EvenWhenFalse()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        var instrumented = json.SelectToken("objectChanges.instrumented");
        Assert.That(instrumented, Is.Not.Null);
        Assert.That(instrumented.Value<bool>(), Is.False);
    }

    [Test]
    public void Serialize_ProjectsFailureRecordFieldsUnderCamelCaseResolver()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        var failure = json.SelectToken("failures[0]");
        Assert.That(failure, Is.Not.Null);
        Assert.That(failure.SelectToken("phase")?.Value<string>(), Is.EqualTo("AfterScripts"));
        Assert.That(failure.SelectToken("scopeKey")?.Value<string>(), Is.EqualTo("[primary]"));
        Assert.That(failure.SelectToken("error")?.Value<string>(), Is.EqualTo("boom"));
        Assert.That(failure.SelectToken("contextTail")?.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(failure.SelectToken("contextTail")?.Values<string>(), Is.EqualTo(new[] { "line 1", "line 2" }));
        Assert.That(failure.SelectToken("artifactPath")?.Value<string>(), Is.EqualTo("SchemaQuench - After primary.public.sql"));
    }

    [Test]
    public void Serialize_EmitsNullSchemaAsJsonNull_ForDbLevelTarget()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        var schemaToken = json.SelectToken("targets[1].schema");
        Assert.That(schemaToken, Is.Not.Null);
        Assert.That(schemaToken.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void Serialize_ProjectsE1SlotTimingFieldsUnderTiming()
    {
        var json = JObject.Parse(DeploymentSummaryJson.Serialize(BuildFullyPopulatedSummary()));

        var slot = json.SelectToken("timing.bySlot[0]");
        Assert.That(slot, Is.Not.Null);
        Assert.That(slot.SelectToken("slot")?.Value<string>(), Is.EqualTo("ModifiedTables"));
        Assert.That(slot.SelectToken("totalMs")?.Value<long>(), Is.EqualTo(41230));
        Assert.That(slot.SelectToken("targetCount")?.Value<int>(), Is.EqualTo(5));
    }
}
