// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Task Slice 3.1, Part A: a committed <c>.json-schemas/tables.sqlserver.schema</c> deliberately
/// out of date vs. the current domain model (a hand-edit narrowing <c>VariantName</c>'s
/// <c>maxLength</c> and altering its description — see the fixture file) —
/// <c>JsonSchemaCheck</c> Pass 1 (SS-STALE-001) — proves `--Validate` exits 2 and reports the
/// finding against a real on-disk package.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateStaleSchemaTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_StaleSchema_ExitsWithError_AndReportsStaleCode()
    {
        var result = RunValidate(FixturePath("StaleSchema", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-STALE-001"));
    }
}
