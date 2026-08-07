// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using NUnit.Framework;
using Schema.DataAccess;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class SqlServerBatchSplitterTests
{
    [Test]
    public void Split_NoGo_ReturnsSingleBatch()
    {
        var batches = SqlServerBatchSplitter.Split("CREATE PROCEDURE dbo.Foo AS SELECT 1");
        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0].Trim(), Is.EqualTo("CREATE PROCEDURE dbo.Foo AS SELECT 1"));
    }

    [Test]
    public void Split_OnGo_SeparatesBatches_AndDropsTheSeparator()
    {
        const string script = "IF OBJECT_ID('dbo.Foo','P') IS NOT NULL DROP PROCEDURE dbo.Foo\nGO\nCREATE PROCEDURE dbo.Foo AS SELECT 1";
        var batches = SqlServerBatchSplitter.Split(script);
        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(batches[0], Does.Contain("DROP PROCEDURE").And.Not.Contain("GO"));
        Assert.That(batches[1], Does.Contain("CREATE PROCEDURE").And.Not.Contain("GO"));
    }

    [Test]
    public void Split_GoIsCaseInsensitive_AndToleratesSurroundingWhitespace()
    {
        var batches = SqlServerBatchSplitter.Split("SELECT 1\n   go   \nSELECT 2");
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void Split_GoWithRepeatCount_IsASeparator()
    {
        var batches = SqlServerBatchSplitter.Split("SELECT 1\nGO 5\nSELECT 2");
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void Split_GoInsideAStringOrIdentifier_IsNotASeparator()
    {
        // GO only separates when it is the whole (trimmed) line — "GO" as a token mid-line must stay put.
        var batches = SqlServerBatchSplitter.Split("SELECT 'GO', [GO] FROM dbo.T WHERE X = 'GO'");
        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void Split_IgnoresEmptyBatchesBetweenSeparators()
    {
        var batches = SqlServerBatchSplitter.Split("SELECT 1\nGO\nGO\n   \nGO\nSELECT 2");
        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(batches.All(b => b.Trim().Length > 0), Is.True);
    }
}
