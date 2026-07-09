// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class WhatIfCaptureTests
{
    [Test]
    public void Record_AccumulatesEntries_AcrossCategories_WithCorrectFields()
    {
        var capture = new WhatIfCapture();

        capture.Record(WhatIfCategory.Apply, "[primary].[db1]", "Scripts/Before/001.sql");
        capture.Record(WhatIfCategory.Skip, "[primary].[db1]", "Scripts/Templates/002.sql");
        capture.Record(WhatIfCategory.Deliver, "[primary].[db1]", "Scripts/Data/003.sql");

        var snapshot = capture.Snapshot();

        Assert.That(snapshot.Count, Is.EqualTo(3));

        var apply = snapshot.Single(r => r.Category == WhatIfCategory.Apply);
        Assert.That(apply.Scope, Is.EqualTo("[primary].[db1]"));
        Assert.That(apply.Script, Is.EqualTo("Scripts/Before/001.sql"));

        var skip = snapshot.Single(r => r.Category == WhatIfCategory.Skip);
        Assert.That(skip.Scope, Is.EqualTo("[primary].[db1]"));
        Assert.That(skip.Script, Is.EqualTo("Scripts/Templates/002.sql"));

        var deliver = snapshot.Single(r => r.Category == WhatIfCategory.Deliver);
        Assert.That(deliver.Scope, Is.EqualTo("[primary].[db1]"));
        Assert.That(deliver.Script, Is.EqualTo("Scripts/Data/003.sql"));
    }

    [Test]
    public void Record_IsThreadSafe_UnderConcurrentCalls()
    {
        var capture = new WhatIfCapture();
        const int callCount = 1000;

        Parallel.For(0, callCount, i =>
        {
            capture.Record(WhatIfCategory.Apply, "[primary].[db1]", $"Scripts/Before/{i}.sql");
        });

        var snapshot = capture.Snapshot();

        Assert.That(snapshot.Count, Is.EqualTo(callCount));
    }

    [Test]
    public void Snapshot_ReturnsStableCopy_UnaffectedByLaterRecords()
    {
        var capture = new WhatIfCapture();
        capture.Record(WhatIfCategory.Apply, "[primary].[db1]", "Scripts/Before/001.sql");

        var snapshot = capture.Snapshot();
        Assert.That(snapshot.Count, Is.EqualTo(1));

        capture.Record(WhatIfCategory.Skip, "[primary].[db1]", "Scripts/Templates/002.sql");

        Assert.That(snapshot.Count, Is.EqualTo(1));
        Assert.That(capture.Snapshot().Count, Is.EqualTo(2));
    }
}
