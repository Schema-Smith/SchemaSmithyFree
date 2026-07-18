// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Task Slice 3.1, Part A: a table JSON with broken/truncated JSON — the package fails to load,
/// reported as a single SS-LOAD-001 finding (<see cref="Schema.Validation.SchemaPackageValidator"/>'s
/// load-failure gate) rather than an unhandled exception — proves `--Validate` exits 2 and reports
/// the finding against a real on-disk package.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMalformedJsonTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MalformedJson_ExitsWithError_AndReportsLoadCode()
    {
        var result = RunValidate(FixturePath("MalformedJson", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-LOAD-001"));
    }
}
