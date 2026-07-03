// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Cross-engine broken-package proof (Task Slice 3.1, Part A): a table with two ungated
/// same-name columns is an accidental duplicate (<c>DuplicationCheck</c>, SS-DUP-001) on every
/// engine — proves `--Validate` exits 2 and reports the expected finding code regardless of
/// platform.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateDuplicateColumnTests : ValidateFixtureTestBase
{
    [TestCase("SqlServer")]
    [TestCase("PostgreSQL")]
    [TestCase("MySQL")]
    public void Validate_DuplicateColumn_ExitsWithError_AndReportsDupCode(string engine)
    {
        var result = RunValidate(FixturePath("DuplicateColumn", engine));

        Assert.That(result.HasErrors, Is.True, $"Expected HasErrors on {engine}.");
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-DUP-001"));
    }
}
