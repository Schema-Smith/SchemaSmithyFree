// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Guard twin of <see cref="ValidateNoCommittedSchemasTests"/>: a package with no
/// <c>.json-schemas</c> directory at all, but otherwise identical in shape to the <c>Clean</c>
/// fixture, must still validate with zero findings. Proves that generating the schema in memory
/// when nothing is committed does not itself introduce false positives.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateNoCommittedSchemasCleanTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_NoCommittedSchemasClean_HasNoFindings()
    {
        var result = RunValidate(FixturePath("NoCommittedSchemasClean", "SqlServer"));

        Assert.That(result.HasErrors, Is.False,
            $"Unexpected findings: {string.Join("; ", result.Findings.Select(f => f.Code + ": " + f.Message))}");
        Assert.That(result.Findings, Is.Empty);
    }
}
