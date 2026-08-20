// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Two tables, each with its own distinct misnamed property (<c>dbo.Widget.json</c>: "Nam",
/// <c>dbo.Gadget.json</c>: "Naem"). Proves the substance of the lenient-load decision: a linter
/// that stops at the first problem forces N round trips to discover N problems, so PackageLoader
/// must not abort after the first unrecognised property — every offending file gets its own
/// SS-JSON-001, in one pass, not just the first one encountered.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMultipleMisnamedPropertiesTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MultipleMisnamedProperties_ReportsEveryOffendingFile()
    {
        var result = RunValidate(FixturePath("MultipleMisnamedProperties", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var jsonFindings = result.Findings.Where(f => f.Code == "SS-JSON-001").ToList();
        var flaggedFiles = jsonFindings.Select(f => Path.GetFileName(f.Location)).Distinct().ToList();

        Assert.That(flaggedFiles, Does.Contain("dbo.Widget.json"),
            "Widget's misnamed property ('Nam') must be reported.");
        Assert.That(flaggedFiles, Does.Contain("dbo.Gadget.json"),
            "Gadget's misnamed property ('Naem') must be reported too — both in the same pass, " +
            "not just the first file the loader happened to reach.");

        // The regression this decision fixes: a strict load aborts on the FIRST unknown property
        // and never reaches the second file at all.
        Assert.That(result.Findings.Select(f => f.Code), Has.None.EqualTo("SS-LOAD-001"));
    }
}
