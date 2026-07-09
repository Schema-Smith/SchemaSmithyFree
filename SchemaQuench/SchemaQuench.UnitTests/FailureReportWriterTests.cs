// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class FailureReportWriterTests
{
    [Test]
    public void Render_NoFailures_ReturnsEmpty()
    {
        Assert.That(FailureReportWriter.Render(new List<FailureRecord>()), Is.Empty);
    }

    [Test]
    public void Render_GroupsByPhase_AndShowsHeaderCount()
    {
        var records = new List<FailureRecord>
        {
            new("BeforeScripts", "[primary]", "bad before script",
                new[] { "running before.sql", "ERROR 1064" }, null),
            new("Template:TenantSchema", "[primary].[acme_db] [Schema: acme]", "quench failed",
                new[] { "creating table x", "deadlock" }, "SchemaQuench - conv primary.acme_db.acme.sql"),
        };

        var report = FailureReportWriter.Render(records);

        Assert.That(report, Does.Contain("2 failure(s)"));
        Assert.That(report, Does.Contain("[BeforeScripts]"));
        Assert.That(report, Does.Contain("[primary]"));
        Assert.That(report, Does.Contain("[Template:TenantSchema]"));
        Assert.That(report, Does.Contain("[Schema: acme]"));
        Assert.That(report, Does.Contain("deadlock"));
        Assert.That(report, Does.Contain("SchemaQuench - conv primary.acme_db.acme.sql"));
    }

    [Test]
    public void Render_NoContextTail_RendersNoneCaptured()
    {
        var records = new List<FailureRecord>
        {
            new("Validate", "Product", "min version failed", new string[0], null),
        };
        Assert.That(FailureReportWriter.Render(records), Does.Contain("none captured"));
    }
}
