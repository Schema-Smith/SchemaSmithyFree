// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Four unresolvable <c>&lt;*File*&gt;</c> tokens in one package — two declared in
/// <c>Product.json</c>, two in <c>Template.json</c> — proving the substance of the fix:
/// <c>TokenHelper.ResolveFileTokensByTag</c> already collects every error before throwing, and
/// PackageLoader must not collapse that back down to a single finding (or worse, stop after the
/// first template/product and never reach the rest). Mirrors
/// <c>ValidateMultipleMisnamedPropertiesTests</c>'s "report every offending file, not just the
/// first" proof, for the file-token-resolution failure mode instead of the JSON-shape one.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMissingFileTokenMultipleTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MultipleMissingFileTokens_ReportsEveryOne()
    {
        var result = RunValidate(FixturePath("MissingFileTokenMultiple", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var tokenFindings = result.Findings.Where(f => f.Code == "SS-TOK-004").ToList();
        Assert.That(tokenFindings, Has.Count.EqualTo(4),
            "All four unresolvable tokens (two product-level, two template-level) must be reported in one pass.");

        var messages = tokenFindings.Select(f => f.Message).ToList();
        Assert.That(messages, Has.Some.Contains("ProductMissingA"));
        Assert.That(messages, Has.Some.Contains("ProductMissingB"));
        Assert.That(messages, Has.Some.Contains("TemplateMissingA"));
        Assert.That(messages, Has.Some.Contains("TemplateMissingB"));
    }
}
