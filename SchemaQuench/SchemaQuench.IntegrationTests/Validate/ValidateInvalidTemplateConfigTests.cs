// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Exercises the <c>InvalidOperationException</c> arm of <c>PackageLoader.TryLoadTemplate</c>'s
/// narrowed catch — the other half of the same catch <see cref="ValidateMalformedTemplateJsonTests"/>
/// covers for <c>JsonException</c>. <c>Templates/Main/Template.json</c> is well-formed JSON but sets
/// <c>CreateSchemaIfMissing: true</c> with no <c>SchemaIdentificationScript</c>, which
/// <c>Template.ValidateSchemaTemplateRules</c> rejects with <c>InvalidOperationException</c> — a
/// self-contradictory template configuration, not a parse failure. Before this fix that exception
/// vanished the same way an unparseable <c>Template.json</c> did.
/// <para>
/// A well-formed sibling (<c>Secondary</c>) with an unrelated, deliberate duplicate-column defect
/// (SS-DUP-001) proves the same property as the JsonException case: excluding the broken template
/// must not take down check coverage for every OTHER template in the package.
/// </para>
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateInvalidTemplateConfigTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_InvalidTemplateConfig_ReportsTemplateLoadCode_AndStillChecksSiblingTemplate()
    {
        var result = RunValidate(FixturePath("InvalidTemplateConfig", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var loadFinding = result.Findings.SingleOrDefault(f => f.Code == "SS-LOAD-002");
        Assert.That(loadFinding, Is.Not.Null,
            "A self-contradictory template configuration (CreateSchemaIfMissing without a SchemaIdentificationScript) must be reported as SS-LOAD-002, not silently dropped.");
        Assert.That(loadFinding.Message, Does.Contain("Main"), "The finding must name which template was excluded.");
        Assert.That(loadFinding.Location, Does.Contain("Template.json"),
            "The finding must point at the manifest that failed to load.");

        // The property worth protecting: excluding Main must not silently exclude Secondary too.
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-DUP-001"),
            "Secondary is a sibling of the broken template and must still be loaded and fully checked.");

        // Regression pin: this is the whole-template code, not the component-inside-a-loadable-template one.
        Assert.That(result.Findings.Select(f => f.Code), Has.None.EqualTo("SS-LOAD-001"));
    }
}
