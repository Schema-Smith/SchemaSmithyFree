// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// A template-level <c>ScriptTokens</c> entry using <c>&lt;*File*&gt;</c> that points at a file
/// which doesn't exist on disk. <c>TokenHelper.ResolveFileTokens</c> collects this as an error
/// during <c>Template.Load</c>; PackageLoader must surface it as an SS-TOK-004 finding instead of
/// swallowing it (the bug this fixture guards: the exception used to vanish in
/// <c>PackageLoader.TryLoadTemplate</c>'s bare catch, leaving `--Validate` reporting a clean pass
/// over a package that fails at deploy).
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMissingFileTokenTemplateTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MissingFileTokenAtTemplateLevel_ReportsTokenCodeWithResolvedPath()
    {
        var result = RunValidate(FixturePath("MissingFileTokenTemplate", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var finding = result.Findings.SingleOrDefault(f => f.Code == "SS-TOK-004");
        Assert.That(finding, Is.Not.Null, "Missing template-level file token must be reported as SS-TOK-004.");
        Assert.That(finding.Message, Does.Contain("Widget.tabledata"), "The token key must be named in the message.");
        Assert.That(finding.Message, Does.Contain("data/NOPE.tabledata"),
            "The resolved path the loader looked for must be named in the message — that's the actionable part.");
        Assert.That(finding.Location, Does.Contain("Template.json"),
            "The finding must point at the manifest that declared the token.");
    }
}
