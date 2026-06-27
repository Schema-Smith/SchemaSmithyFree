// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class PreFlightReporterTests
{
    // ─── helper: build a WorkUnit list for use in TemplatePreview ─────────────────

    /// <summary>
    /// Build a mix of WorkUnit types: regular template unit (S = regular) or
    /// schema template unit (S = schema). For test purposes we only need
    /// DatabaseName, SchemaName, and WouldCreateDatabase populated.
    /// </summary>
    private static WorkUnit RegularUnit(string server, string db, bool wouldCreate = false)
        => new WorkUnit(server, db, "Template", "") { WouldCreateDatabase = wouldCreate };

    private static WorkUnit SchemaUnit(string server, string db, string schema)
        => new WorkUnit(server, db, "Template", schema);

    // ─── Render: required template that matched nothing → ERROR line ──────────────

    [Test]
    public void Render_RequiredTemplateMatchedNothing_EmitsError()
    {
        var previews = new[]
        {
            new TemplatePreview("CoreSchema", required: true,
                new[] { SchemaUnit("S", "AcmeProd", "sales"), SchemaUnit("S", "AcmeProd", "hr") },
                matchedNothing: false),
            new TemplatePreview("TenantInit", required: true, Array.Empty<WorkUnit>(), matchedNothing: true),
        };
        var lines = PreFlightReporter.Render(previews);
        Assert.That(lines, Has.Some.Contains("CoreSchema"));
        Assert.That(lines, Has.Some.Contains("ERROR").And.Some.Contains("TenantInit"));
    }

    // ─── Render: template header appears ─────────────────────────────────────────

    [Test]
    public void Render_SingleTemplate_EmitsTemplateNameHeader()
    {
        var previews = new[]
        {
            new TemplatePreview("Shared", required: false,
                new[] { RegularUnit("primary", "AppDb") },
                matchedNothing: false)
        };
        var lines = PreFlightReporter.Render(previews);
        Assert.That(lines, Has.Some.Contains("Shared"));
    }

    // ─── Render: database axis listed ────────────────────────────────────────────

    [Test]
    public void Render_RegularTemplate_ListsDatabaseNames()
    {
        var previews = new[]
        {
            new TemplatePreview("Shared", required: false,
                new[] { RegularUnit("primary", "AppDb"), RegularUnit("primary", "ReportDb") },
                matchedNothing: false)
        };
        var lines = PreFlightReporter.Render(previews);
        Assert.That(lines, Has.Some.Contains("AppDb"));
        Assert.That(lines, Has.Some.Contains("ReportDb"));
    }

    // ─── Render: would-be-created annotation ─────────────────────────────────────

    [Test]
    public void Render_WouldCreateDatabase_EmitsAnnotation()
    {
        var previews = new[]
        {
            new TemplatePreview("Shared", required: false,
                new[] { RegularUnit("primary", "NewDb", wouldCreate: true) },
                matchedNothing: false)
        };
        var lines = PreFlightReporter.Render(previews);
        Assert.That(lines, Has.Some.Contains("would be created").And.Contains("NewDb"));
    }

    // ─── Render: existing database does NOT get "would be created" ───────────────

    [Test]
    public void Render_ExistingDatabase_DoesNotEmitWouldBeCreatedAnnotation()
    {
        var previews = new[]
        {
            new TemplatePreview("Shared", required: false,
                new[] { RegularUnit("primary", "ExistingDb", wouldCreate: false) },
                matchedNothing: false)
        };
        var lines = PreFlightReporter.Render(previews);
        Assert.That(lines, Has.None.Contains("would be created"));
    }

    // ─── Render: schema template lists schemas ────────────────────────────────────

    [Test]
    public void Render_SchemaTemplate_ListsSchemaNames()
    {
        var previews = new[]
        {
            new TemplatePreview("TenantBody", required: true,
                new[] { SchemaUnit("primary", "AcmeProd", "sales"), SchemaUnit("primary", "AcmeProd", "hr") },
                matchedNothing: false)
        };
        var lines = PreFlightReporter.Render(previews);

        var combinedOutput = string.Join("\n", lines);
        Assert.That(combinedOutput, Does.Contain("sales").Or.Contain("hr"),
            "Schema names must appear somewhere in the rendered output");
    }

    // ─── Render: zero targets, NOT required → no ERROR ───────────────────────────

    [Test]
    public void Render_NotRequired_ZeroUnits_NoErrorLine()
    {
        var previews = new[]
        {
            new TemplatePreview("Initialize", required: false, Array.Empty<WorkUnit>(), matchedNothing: false)
        };
        var lines = PreFlightReporter.Render(previews);
        Assert.That(lines, Has.None.Contains("ERROR"));
    }

    // ─── Render: multiple templates produce output for each ──────────────────────

    [Test]
    public void Render_MultipleTemplates_EmitsEachTemplateName()
    {
        var previews = new[]
        {
            new TemplatePreview("Alpha", required: false,
                new[] { RegularUnit("primary", "AlphaDb") }, matchedNothing: false),
            new TemplatePreview("Beta", required: true,
                new[] { RegularUnit("primary", "BetaDb") }, matchedNothing: false),
        };
        var lines = PreFlightReporter.Render(previews);
        Assert.That(lines, Has.Some.Contains("Alpha"));
        Assert.That(lines, Has.Some.Contains("Beta"));
    }

    // ─── Render: empty preview list → returns empty list ─────────────────────────

    [Test]
    public void Render_EmptyInput_ReturnsEmpty()
    {
        var lines = PreFlightReporter.Render(Array.Empty<TemplatePreview>());
        Assert.That(lines, Is.Empty);
    }
}
