// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Task Slice 3.1, Part A: a <c>{{Nope}}</c> reference with no matching definition anywhere in the
/// package (<c>TokenCheck</c>, SS-TOK-001) — proves `--Validate` exits 2 and reports the finding
/// against a real on-disk package (the check scans the RAW files, not the loaded domain model).
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateUndefinedTokenTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_UndefinedToken_ExitsWithError_AndReportsTokenCode()
    {
        var result = RunValidate(FixturePath("UndefinedToken", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-TOK-001"));
    }
}
