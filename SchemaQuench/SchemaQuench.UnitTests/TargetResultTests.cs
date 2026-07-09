// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using SchemaQuench.Reporting;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class TargetResultTests
{
    [Test]
    public void DeriveOutcome_SuccessfulAndNotSkipped_ReturnsSuccess()
    {
        Assert.That(TargetResult.DeriveOutcome(quenchSuccessful: true, wasSkipped: false), Is.EqualTo(TargetOutcome.Success));
    }

    [Test]
    public void DeriveOutcome_SuccessfulAndSkipped_ReturnsSkipped()
    {
        Assert.That(TargetResult.DeriveOutcome(quenchSuccessful: true, wasSkipped: true), Is.EqualTo(TargetOutcome.Skipped));
    }

    [Test]
    public void DeriveOutcome_NotSuccessfulAndNotSkipped_ReturnsFailed()
    {
        Assert.That(TargetResult.DeriveOutcome(quenchSuccessful: false, wasSkipped: false), Is.EqualTo(TargetOutcome.Failed));
    }

    [Test]
    public void DeriveOutcome_NotSuccessfulAndSkipped_FailureDominates_ReturnsFailed()
    {
        Assert.That(TargetResult.DeriveOutcome(quenchSuccessful: false, wasSkipped: true), Is.EqualTo(TargetOutcome.Failed));
    }

    [Test]
    public void TargetResult_RoundTripsConstructorArguments()
    {
        var result = new TargetResult(
            ScopeKey: "[srv].[dbA]",
            Server: "srv",
            Database: "dbA",
            Schema: "sales",
            Template: "TenantTemplate",
            Outcome: TargetOutcome.Success,
            DurationMs: 1234);

        Assert.That(result.ScopeKey, Is.EqualTo("[srv].[dbA]"));
        Assert.That(result.Server, Is.EqualTo("srv"));
        Assert.That(result.Database, Is.EqualTo("dbA"));
        Assert.That(result.Schema, Is.EqualTo("sales"));
        Assert.That(result.Template, Is.EqualTo("TenantTemplate"));
        Assert.That(result.Outcome, Is.EqualTo(TargetOutcome.Success));
        Assert.That(result.DurationMs, Is.EqualTo(1234));
    }
}
