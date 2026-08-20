// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// The headline case for the decision that <c>JsonSchemaCheck</c>'s validation must not depend on
/// <c>.json-schemas/</c> being committed: this package has NO <c>.json-schemas</c> directory at
/// all, yet its one table carries a genuine, unrecognised property. Before the fix, the missing
/// directory made the whole check no-op and this package validated clean; it must now report
/// SS-JSON-001 against a schema generated in memory from the domain model.
/// <para>
/// Also pins the companion property: with nothing committed there is nothing to compare, so
/// staleness (SS-STALE-001) must never fire for a package that never had a committed schema.
/// </para>
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateNoCommittedSchemasTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_NoCommittedSchemas_StillReportsJsonSchemaCode()
    {
        var result = RunValidate(FixturePath("NoCommittedSchemas", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-JSON-001"));
        Assert.That(result.Findings.Select(f => f.Code), Has.None.EqualTo("SS-STALE-001"),
            "Nothing was ever committed, so there is nothing to be stale against.");
    }
}
