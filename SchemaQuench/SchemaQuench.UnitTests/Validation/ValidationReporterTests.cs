// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Linq;
using NUnit.Framework;
using Schema.Validation;

namespace SchemaQuench.UnitTests.Validation;

[TestFixture]
public class ValidationReporterTests
{
    [Test]
    public void Render_Empty_ReportsClean()
    {
        Assert.That(ValidationReporter.Render(Array.Empty<Finding>()),
            Has.Some.Contains("PASS").Or.Some.Contains("no issues"));
    }

    [Test]
    public void Render_ShowsErrorWithLocationAndCode()
    {
        var lines = ValidationReporter.Render(new[]
            { new Finding(Severity.Error, "SS-DUP-001", "Duplicate", "Tables/Customer.json", "duplicate column 'Id'") });
        Assert.That(lines, Has.Some.Contains("ERROR").And.Some.Contains("SS-DUP-001").And.Some.Contains("Customer.json"));
    }

    [Test]
    public void Render_ErrorsBeforeWarnings()
    {
        var lines = ValidationReporter.Render(new[]
        {
            new Finding(Severity.Warning, "W", "Token", "a.json", "unused"),
            new Finding(Severity.Error,   "E", "Dup",   "b.json", "dup"),
        });
        var errIdx = lines.ToList().FindIndex(l => l.Contains("ERROR"));
        var warnIdx = lines.ToList().FindIndex(l => l.Contains("WARN"));
        Assert.That(errIdx, Is.LessThan(warnIdx));
    }
}
