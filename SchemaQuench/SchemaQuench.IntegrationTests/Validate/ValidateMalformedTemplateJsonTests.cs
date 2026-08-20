// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// The template-manifest twin of <see cref="ValidateMalformedJsonTests"/>: that fixture breaks a
/// TABLE inside an otherwise well-formed <c>Template.json</c> (the already-working SS-LOAD-001
/// component-skip path). This one inverts it — <c>Templates/Main/Template.json</c> itself is
/// unparseable, with a perfectly well-formed table underneath it. Before this fix,
/// <c>PackageLoader.TryLoadTemplate</c>'s bare <c>catch { return null; }</c> discarded that
/// exception with no finding at all, and the whole template (not just one component) vanished —
/// `--Validate` reported a clean pass over a package that fails at deploy.
/// <para>
/// A sibling template (<c>Secondary</c>) with an unrelated, deliberate defect (a duplicate column
/// name — SS-DUP-001) proves the property worth protecting: excluding the broken template must
/// not take down check coverage for every OTHER template in the package.
/// </para>
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMalformedTemplateJsonTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MalformedTemplateJson_ReportsTemplateLoadCode_AndStillChecksSiblingTemplate()
    {
        var result = RunValidate(FixturePath("MalformedTemplateJson", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var loadFinding = result.Findings.SingleOrDefault(f => f.Code == "SS-LOAD-002");
        Assert.That(loadFinding, Is.Not.Null,
            "The whole template's own Template.json failing to load must be reported as SS-LOAD-002, not silently dropped.");
        Assert.That(loadFinding.Message, Does.Contain("Main"), "The finding must name which template was excluded.");
        Assert.That(loadFinding.Location, Does.Contain("Template.json"),
            "The finding must point at the manifest that failed to load.");

        // The property worth protecting: excluding Main must not silently exclude Secondary too.
        // Secondary's own duplicate-column defect can only be reported if Secondary actually made
        // it into the loaded package and through DuplicationCheck.
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-DUP-001"),
            "Secondary is a sibling of the broken template and must still be loaded and fully checked.");

        // Regression pin: this must never be reported as SS-LOAD-001 (that code is a component-inside-
        // a-loadable-template skip; this is the whole template failing, which is a bigger blast radius).
        Assert.That(result.Findings.Select(f => f.Code), Has.None.EqualTo("SS-LOAD-001"));
    }
}
