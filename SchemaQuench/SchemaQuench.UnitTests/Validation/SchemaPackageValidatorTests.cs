// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;
using Schema.Validation;

namespace SchemaQuench.UnitTests.Validation;

[TestFixture]
public class SchemaPackageValidatorTests
{
    [Test]
    public void Validate_LoadThrows_ReportsMalformedLoadError_NoCrash()
    {
        var validator = new SchemaPackageValidator(
            loader: () => throw new Exception("Error loading table from Tables/Bad.json\r\nUnexpected token"),
            checks: Array.Empty<ISchemaCheck>());
        var result = validator.Validate("pkg");
        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Findings, Has.Exactly(1).Items);
        Assert.That(result.Findings[0].Category, Is.EqualTo("Load"));
        Assert.That(result.Findings[0].Code, Is.EqualTo("SS-LOAD-001"));
        Assert.That(result.Findings[0].Message, Does.Contain("Bad.json"));
    }

    [Test]
    public void Validate_RunsAllChecks_AggregatesFindings()
    {
        var pkg = ValidationTestPackages.Minimal(Platform.SqlServer);
        var check = Substitute.For<ISchemaCheck>();
        check.Run(Arg.Any<ValidationContext>())
             .Returns(new[] { new Finding(Severity.Error, "X", "Cat", "loc", "msg") });
        var result = new SchemaPackageValidator(() => pkg, new[] { check }).Validate("pkg");
        Assert.That(result.Findings, Has.Exactly(1).Items);
        check.Received(1).Run(Arg.Any<ValidationContext>());
    }

    [Test]
    public void ValidationContext_AllTables_FlattensTemplates()
    {
        var pkg = ValidationTestPackages.Minimal(Platform.SqlServer);
        var ctx = new ValidationContext(pkg.Product, pkg.Templates, "pkg");
        Assert.That(ctx.AllTables, Is.Not.Empty);
        Assert.That(ctx.Platform, Is.EqualTo(Platform.SqlServer));
    }
}
