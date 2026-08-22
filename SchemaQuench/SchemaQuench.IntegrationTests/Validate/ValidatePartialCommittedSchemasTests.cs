// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// The partial-coverage case: <c>.json-schemas/</c> exists and carries current committed schemas
/// for products/templates/indexedviews, but <c>tables.sqlserver.schema</c> was never committed —
/// and the one table has a genuine, unrecognised property. Before the fix, a missing individual
/// schema file skipped that type entirely; it must now be validated against a schema generated in
/// memory, same as the whole-directory-missing case, while the types that ARE committed still
/// validate normally (and would still be caught for staleness, per
/// <c>ValidateStaleSchemaTests</c>, which this fixture does not need to re-prove).
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidatePartialCommittedSchemasTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_PartialCommittedSchemas_StillReportsJsonSchemaCodeForMissingType()
    {
        var result = RunValidate(FixturePath("PartialCommittedSchemas", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);

        var jsonFinding = result.Findings.SingleOrDefault(f => f.Code == "SS-JSON-001");
        Assert.That(jsonFinding, Is.Not.Null,
            "The table's unrecognised property must be reported even though tables.sqlserver.schema was never committed.");
        Assert.That(jsonFinding.Location, Does.Contain("dbo.Widget.json"));

        Assert.That(result.Findings.Select(f => f.Code), Has.None.EqualTo("SS-STALE-001"),
            "Nothing was committed for the tables type, so there is nothing to be stale against — and the types " +
            "that ARE committed (products/templates/indexedviews) are current and must not report stale either.");
    }
}
