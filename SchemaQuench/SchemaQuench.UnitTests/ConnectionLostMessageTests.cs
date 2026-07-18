// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class ConnectionLostMessageTests
{
    [Test]
    public void Build_NamesServerAndPhase_AndGivesEnvironmentGuidance()
    {
        var msg = ConnectionLostMessage.Build("demoserver", "Template:Northwind");
        Assert.That(msg, Does.Contain("demoserver"));
        Assert.That(msg, Does.Contain("Template:Northwind"));
        Assert.That(msg, Does.Contain("environment").IgnoreCase);
        Assert.That(msg, Does.Contain("re-run").IgnoreCase);
    }

    [Test]
    public void Build_MissingPhase_StillReadable()
    {
        var msg = ConnectionLostMessage.Build("srv", null);
        Assert.That(msg, Does.Contain("srv"));
        Assert.That(msg, Does.Not.Contain(" during "));
        Assert.That(msg, Is.Not.Empty);
    }

    [Test]
    public void Build_MissingServer_FallsBackToGenericName()
    {
        var msg = ConnectionLostMessage.Build(null, "deployment");
        Assert.That(msg, Does.Contain("the target server"));
        Assert.That(msg, Does.Contain("during deployment"));
    }
}
