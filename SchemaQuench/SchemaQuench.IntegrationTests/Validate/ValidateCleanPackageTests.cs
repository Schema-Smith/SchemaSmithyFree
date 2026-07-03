// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// The core cross-engine parity proof (Task Slice 3.1, Part A): a small but VALID real on-disk
/// package — Product.json + one Template.json + one table + current generated
/// <c>.json-schemas/</c> (via SchemaTongs <c>--WriteSchemasOnly</c>) — validates clean with zero
/// findings on all three engines.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateCleanPackageTests : ValidateFixtureTestBase
{
    [TestCase("SqlServer")]
    [TestCase("PostgreSQL")]
    [TestCase("MySQL")]
    public void Validate_CleanPackage_HasNoFindings(string engine)
    {
        var result = RunValidate(FixturePath("Clean", engine));

        Assert.That(result.HasErrors, Is.False,
            $"Unexpected findings on {engine}: {string.Join("; ", result.Findings.Select(f => f.Code + ": " + f.Message))}");
        Assert.That(result.Findings, Is.Empty);
    }
}
