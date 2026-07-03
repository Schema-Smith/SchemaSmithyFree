// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Task Slice 3.1, Part A: an FK whose RelatedTable does not resolve to any known table
/// (<c>CoherenceCheck</c>, SS-FK-002) — proves `--Validate` exits 2 and reports the finding
/// against a real on-disk package.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateDanglingFkTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_DanglingFk_ExitsWithError_AndReportsFkCode()
    {
        var result = RunValidate(FixturePath("DanglingFk", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-FK-002"));
    }
}
