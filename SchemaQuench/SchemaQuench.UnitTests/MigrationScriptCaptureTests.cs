// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class MigrationScriptCaptureTests
{
    [Test]
    public void Record_AccumulatesEntries_WithCorrectFields()
    {
        var capture = new MigrationScriptCapture();

        capture.Record("srv1", "db1", "sales", "TemplateA", "Before", "Scripts/Before/001.sql");
        capture.Record("srv1", "", "", "", "After", "Product/After/reindex.sql");

        var snapshot = capture.Snapshot();

        Assert.That(snapshot.Count, Is.EqualTo(2));

        var first = snapshot.Single(r => r.Path == "Scripts/Before/001.sql");
        Assert.That(first.Server, Is.EqualTo("srv1"));
        Assert.That(first.Database, Is.EqualTo("db1"));
        Assert.That(first.Schema, Is.EqualTo("sales"));
        Assert.That(first.Template, Is.EqualTo("TemplateA"));
        Assert.That(first.Slot, Is.EqualTo("Before"));

        var second = snapshot.Single(r => r.Path == "Product/After/reindex.sql");
        Assert.That(second.Server, Is.EqualTo("srv1"));
        Assert.That(second.Database, Is.EqualTo(""));
        Assert.That(second.Schema, Is.EqualTo(""));
        Assert.That(second.Template, Is.EqualTo(""));
        Assert.That(second.Slot, Is.EqualTo("After"));
    }

    [Test]
    public void Record_IsThreadSafe_UnderConcurrentCalls()
    {
        var capture = new MigrationScriptCapture();
        const int callCount = 1000;

        Parallel.For(0, callCount, i =>
        {
            capture.Record("srv", $"db{i % 10}", "", "TemplateA", "Before", $"Scripts/Before/{i}.sql");
        });

        var snapshot = capture.Snapshot();

        Assert.That(snapshot.Count, Is.EqualTo(callCount));
    }

    [Test]
    public void Snapshot_ReturnsStableCopy_UnaffectedByLaterRecords()
    {
        var capture = new MigrationScriptCapture();
        capture.Record("srv", "db", "", "TemplateA", "Before", "Scripts/Before/001.sql");

        var snapshot = capture.Snapshot();
        Assert.That(snapshot.Count, Is.EqualTo(1));

        capture.Record("srv", "db", "", "TemplateA", "After", "Scripts/After/002.sql");

        Assert.That(snapshot.Count, Is.EqualTo(1));
        Assert.That(capture.Snapshot().Count, Is.EqualTo(2));
    }
}
