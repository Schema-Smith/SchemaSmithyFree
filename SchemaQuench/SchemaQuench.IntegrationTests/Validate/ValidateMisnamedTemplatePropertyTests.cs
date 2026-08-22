// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Proves the leniency handoff <c>PackageLoader.TryLoadTemplate</c> now relies on:
/// <c>Templates/Main/Template.json</c> is well-formed, parseable JSON carrying one unrecognised
/// property (<c>"NotARealProperty"</c>) — not the genuinely-broken-JSON case
/// <see cref="ValidateMalformedTemplateJsonTests"/> covers. <c>Template.Load</c> now runs with
/// <c>MissingMemberHandling.Ignore</c> on the `--Validate` path (mirroring <c>Product.Load</c>),
/// so this no longer throws at all: the template loads fully, and <c>JsonSchemaCheck</c>'s
/// independent re-validation — which now generates its comparison schema in memory even with no
/// committed <c>.json-schemas/</c> (this fixture deliberately carries none) — reports the property
/// precisely as SS-JSON-001. Before that leniency, this exact case vanished the whole template as
/// a vague SS-LOAD-002 with no other check ever reaching it.
/// <para>
/// The property worth protecting: the template's own contents must still be checked, not just
/// "the template didn't blow up" — <c>Tables/dbo.Widget.json</c> carries an unrelated, deliberate
/// duplicate-column defect (SS-DUP-001) that can only be reported if <c>DuplicationCheck</c>
/// actually ran against this template's loaded tables.
/// </para>
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMisnamedTemplatePropertyTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MisnamedTemplateProperty_ReportsJsonSchemaCodeOnly_AndStillChecksTemplateContents()
    {
        var result = RunValidate(FixturePath("MisnamedTemplateProperty", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var jsonFindings = result.Findings.Where(f => f.Code == "SS-JSON-001").ToList();
        Assert.That(jsonFindings, Has.Count.EqualTo(1),
            "Exactly one finding for the misnamed property — not zero (silent), not more (double-reported).");
        Assert.That(jsonFindings[0].Message, Does.Contain("NotARealProperty"),
            "SS-JSON-001 is the useful finding here: it must name the offending property.");

        // The regression this fix closes: a parseable-but-wrong Template.json must never also
        // report the vague, template-excluding SS-LOAD-002 alongside the precise SS-JSON-001.
        Assert.That(result.Findings.Select(f => f.Code), Has.None.EqualTo("SS-LOAD-002"));

        // The property worth protecting: the template's own table content was still fully checked.
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-DUP-001"),
            "The template loaded successfully despite the unknown property, so its own Widget table's duplicate column must still be caught.");
    }
}
