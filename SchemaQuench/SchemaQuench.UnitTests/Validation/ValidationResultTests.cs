// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using SchemaQuench.Validation;

namespace SchemaQuench.UnitTests.Validation;

[TestFixture]
public class ValidationResultTests
{
    [Test]
    public void HasErrors_TrueWhenAnyErrorFinding()
    {
        var r = new ValidationResult(new[]
        {
            new Finding(Severity.Warning, "W1", "Token", "a.json", "unused"),
            new Finding(Severity.Error,   "E1", "Duplicate", "b.json", "dup"),
        });
        Assert.That(r.HasErrors, Is.True);
    }

    [Test]
    public void HasErrors_FalseWhenOnlyWarnings()
    {
        var r = new ValidationResult(new[] { new Finding(Severity.Warning, "W1", "Token", "a.json", "unused") });
        Assert.That(r.HasErrors, Is.False);
    }

    [Test]
    public void Findings_ExposedInOrder()
    {
        var f1 = new Finding(Severity.Error, "E1", "C", "l", "m1");
        var f2 = new Finding(Severity.Warning, "W1", "C", "l", "m2");
        var r = new ValidationResult(new[] { f1, f2 });
        Assert.That(r.Findings, Is.EqualTo(new[] { f1, f2 }));
    }
}
