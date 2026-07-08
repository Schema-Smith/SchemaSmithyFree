// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class FailureContextTests
{
    private static IConfigurationRoot ConfigWith(string failureContextLines)
    {
        var values = new Dictionary<string, string>();
        if (failureContextLines != null) values["FailureContextLines"] = failureContextLines;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Test]
    public void ResolveCapacity_ParsesConfiguredValue()
    {
        Assert.That(FailureContext.ResolveCapacity(ConfigWith("40")), Is.EqualTo(40));
    }

    [Test]
    public void ResolveCapacity_DefaultsWhenAbsentOrUnparseable()
    {
        Assert.That(FailureContext.ResolveCapacity(ConfigWith(null)), Is.EqualTo(FailureContext.DefaultCapacity));
        Assert.That(FailureContext.ResolveCapacity(ConfigWith("garbage")), Is.EqualTo(FailureContext.DefaultCapacity));
    }

    [Test]
    public void ResolveCapacity_FloorsNegativeToZero()
    {
        Assert.That(FailureContext.ResolveCapacity(ConfigWith("-5")), Is.EqualTo(0));
    }

    [Test]
    public void ResolveCapacity_ZeroDisablesCapture()
    {
        Assert.That(FailureContext.ResolveCapacity(ConfigWith("0")), Is.EqualTo(0));
    }

    [Test]
    public void Log_KeepsOnlyLastCapacityLines()
    {
        var ctx = new FailureContext("Template:acme", "[p].[db]", capacity: 3);
        foreach (var i in Enumerable.Range(1, 5)) ctx.Log($"line {i}");
        var record = ctx.ToRecord("boom", null);
        Assert.That(record.ContextTail, Is.EqualTo(new[] { "line 3", "line 4", "line 5" }));
    }

    [Test]
    public void ToRecord_CarriesIdentityErrorAndArtifact()
    {
        var ctx = new FailureContext("BeforeScripts", "[primary]", capacity: 25);
        ctx.Log("only line");
        var record = ctx.ToRecord("connection failed", "SchemaQuench - x.sql");
        Assert.That(record.Phase, Is.EqualTo("BeforeScripts"));
        Assert.That(record.ScopeKey, Is.EqualTo("[primary]"));
        Assert.That(record.Error, Is.EqualTo("connection failed"));
        Assert.That(record.ArtifactPath, Is.EqualTo("SchemaQuench - x.sql"));
        Assert.That(record.ContextTail, Is.EqualTo(new[] { "only line" }));
    }

    [Test]
    public void CapacityZero_CapturesNoContext()
    {
        var ctx = new FailureContext("Validate", "Product", capacity: 0);
        ctx.Log("ignored");
        Assert.That(ctx.ToRecord("err", null).ContextTail, Is.Empty);
    }

    [Test]
    public void Log_IsThreadSafe()
    {
        var ctx = new FailureContext("Template:x", "[p].[db]", capacity: 50);
        System.Threading.Tasks.Parallel.For(0, 500, i => ctx.Log($"l{i}"));
        Assert.That(ctx.ToRecord("e", null).ContextTail.Count, Is.EqualTo(50));
    }
}
