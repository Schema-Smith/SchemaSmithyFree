// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class ChangeAuditCaptureTests
{
    [Test]
    public void Record_And_Snapshot_RoundTrips_AndRanUsesRanAction()
    {
        var cap = new ChangeAuditCapture();
        Assert.That(cap.Instrumented, Is.False);

        cap.Record("table", "dbo.Orders", "created");
        cap.RecordRan("procedure", "Procedures/usp_Get.sql");
        cap.MarkInstrumented();

        var rows = cap.Snapshot();
        Assert.That(cap.Instrumented, Is.True);
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Any(r => r is { ObjectType: "table", ObjectName: "dbo.Orders", Action: "created" }));
        Assert.That(rows.Any(r => r is { ObjectType: "procedure", ObjectName: "Procedures/usp_Get.sql", Action: "ran" }));
    }
}
