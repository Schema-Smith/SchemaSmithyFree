// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Task Slice 3.1, Part A: a table JSON with a typo'd property (<c>"Nam"</c> instead of
/// <c>"Name"</c>) validated against a CURRENT, real, SchemaTongs-generated
/// <c>.json-schemas/tables.sqlserver.schema</c> (<c>JsonSchemaCheck</c> Pass 2, SS-JSON-001) —
/// proves `--Validate` exits 2 and reports the finding against a real on-disk package.
/// </summary>
[TestFixture]
[Category("Validate")]
public class ValidateMisnamedPropertyTests : ValidateFixtureTestBase
{
    [Test]
    public void Validate_MisnamedProperty_ExitsWithError_AndReportsJsonSchemaCode()
    {
        var result = RunValidate(FixturePath("MisnamedProperty", "SqlServer"));

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Findings.Select(f => f.Code), Contains.Item("SS-JSON-001"));
    }
}
