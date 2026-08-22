// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// The product-level twin of <see cref="ValidateMissingFileTokenTemplateTests"/>: a
/// <c>Product.json</c> <c>ScriptTokens</c> entry using <c>&lt;*File*&gt;</c> that points at a file
/// which doesn't exist. Product-level tokens resolve against the package root, not a template
/// directory — a different base path than the template-level case, and easy to get wrong, so this
/// is covered as its own fixture rather than assumed to follow from the template-level one.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMissingFileTokenProductTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MissingFileTokenAtProductLevel_ReportsTokenCodeWithResolvedPath()
    {
        var result = RunValidate(FixturePath("MissingFileTokenProduct", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var finding = result.Findings.SingleOrDefault(f => f.Code == "SS-TOK-004");
        Assert.That(finding, Is.Not.Null, "Missing product-level file token must be reported as SS-TOK-004.");
        Assert.That(finding.Message, Does.Contain("GlobalSeed"), "The token key must be named in the message.");
        Assert.That(finding.Message, Does.Contain("seed/MISSING.seed"),
            "The resolved path (against the package root, not a template directory) must be named in the message.");
        Assert.That(finding.Location, Does.Contain("Product.json"),
            "The finding must point at the manifest that declared the token.");
    }
}
