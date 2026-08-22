// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Guard for the fix's happy path: a package whose <c>&lt;*File*&gt;</c> tokens (one product-level,
/// one template-level) both point at files that DO exist must still pass with no SS-TOK-004
/// finding — the new check must not false-positive on a package that was already valid.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateFileTokenResolvesTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_FileTokensAllResolve_ReportsNoTokenResolutionFinding()
    {
        var result = RunValidate(FixturePath("FileTokenResolves", "SqlServer"));

        Assert.That(result.Findings.Select(f => f.Code), Has.None.EqualTo("SS-TOK-004"));
        Assert.That(result.HasErrors, Is.False,
            $"Unexpected findings: {string.Join("; ", result.Findings.Select(f => f.Code + ": " + f.Message))}");
    }
}
