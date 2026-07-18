// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using NUnit.Framework;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Validation;
using Schema.Validation.Checks;

namespace SchemaQuench.UnitTests.Validation.Checks;

/// <summary>
/// Slice 2.1: ShouldApply-aware duplication check. A same-name group at any level is a legitimate
/// variant set (no error) only when EVERY member is gated by a non-empty ShouldApplyExpression;
/// otherwise it's an accidental duplicate (SS-DUP-001, Error). A fully-gated variant set missing
/// VariantName labels on any member gets an advisory SS-DUP-VAR-002 Warning.
/// </summary>
[TestFixture]
public class DuplicationCheckTests
{
    private static Product Product(params string[] templateOrder) => new()
    {
        Name = "Acme",
        Platform = Platform.SqlServer,
        TemplateOrder = templateOrder.ToList()
    };

    private static ValidationContext Context(Product product, params Template[] templates) =>
        new(product, templates, "pkg");

    private static Template TemplateWithTable(string templateName, SqlServerTable table)
    {
        var template = new Template { Name = templateName };
        template.Tables.Add(table);
        return template;
    }

    [Test]
    public void DuplicateUngatedColumns_AreError()
    {
        var table = new SqlServerTable
        {
            Name = "Customer",
            Columns =
            {
                new SqlServerColumn { Name = "Id", DataType = "int" },
                new SqlServerColumn { Name = "Id", DataType = "int" }
            }
        };
        var ctx = Context(Product(), TemplateWithTable("Main", table));

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-DUP-001"));
        Assert.That(findings[0].Category, Is.EqualTo("Duplicate"));
        Assert.That(findings[0].Location, Is.EqualTo("Template 'Main' / Table 'Customer'"));
    }

    [Test]
    public void SameNameColumns_AllGated_AreValidVariantSet()
    {
        var table = new SqlServerTable
        {
            Name = "Customer",
            Columns =
            {
                new SqlServerColumn { Name = "Id", DataType = "int", ShouldApplyExpression = "{{IsPartitioned}}", VariantName = "Partitioned" },
                new SqlServerColumn { Name = "Id", DataType = "bigint", ShouldApplyExpression = "{{IsNotPartitioned}}", VariantName = "NotPartitioned" }
            }
        };
        var ctx = Context(Product(), TemplateWithTable("Main", table));

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void SameNameColumns_MixedGatedAndUngated_AreError()
    {
        var table = new SqlServerTable
        {
            Name = "Customer",
            Columns =
            {
                new SqlServerColumn { Name = "Id", DataType = "int", ShouldApplyExpression = "{{IsPartitioned}}", VariantName = "Partitioned" },
                new SqlServerColumn { Name = "Id", DataType = "bigint" }
            }
        };
        var ctx = Context(Product(), TemplateWithTable("Main", table));

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-DUP-001"));
    }

    [Test]
    public void VariantSet_AllGated_MissingVariantName_IsWarning()
    {
        var table = new SqlServerTable
        {
            Name = "Customer",
            Columns =
            {
                new SqlServerColumn { Name = "Id", DataType = "int", ShouldApplyExpression = "{{IsPartitioned}}" },
                new SqlServerColumn { Name = "Id", DataType = "bigint", ShouldApplyExpression = "{{IsNotPartitioned}}" }
            }
        };
        var ctx = Context(Product(), TemplateWithTable("Main", table));

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Warning));
        Assert.That(findings[0].Code, Is.EqualTo("SS-DUP-VAR-002"));
    }

    [Test]
    public void DuplicateUngatedIndexes_AreError()
    {
        var table = new SqlServerTable
        {
            Name = "Customer",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" } },
            Indexes =
            {
                new SqlServerIndex { Name = "IX_Customer_Name", IndexColumns = "Name" },
                new SqlServerIndex { Name = "IX_Customer_Name", IndexColumns = "Name DESC" }
            }
        };
        var ctx = Context(Product(), TemplateWithTable("Main", table));

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-DUP-001"));
        Assert.That(findings[0].Location, Is.EqualTo("Template 'Main' / Table 'Customer'"));
    }

    [Test]
    public void DuplicateTablesInTemplate_Ungated_AreError()
    {
        var template = new Template { Name = "Main" };
        template.Tables.Add(new SqlServerTable { Name = "Customer", Schema = "dbo" });
        template.Tables.Add(new SqlServerTable { Name = "Customer", Schema = "dbo" });
        var ctx = Context(Product(), template);

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-DUP-001"));
        Assert.That(findings[0].Location, Is.EqualTo("Template 'Main'"));
    }

    [Test]
    public void GatedTableVariants_AreValid()
    {
        var template = new Template { Name = "Main" };
        template.Tables.Add(new SqlServerTable
        {
            Name = "Customer", Schema = "dbo",
            ShouldApplyExpression = "{{IsPartitioned}}", VariantName = "Partitioned"
        });
        template.Tables.Add(new SqlServerTable
        {
            Name = "Customer", Schema = "dbo",
            ShouldApplyExpression = "{{IsNotPartitioned}}", VariantName = "NotPartitioned"
        });
        var ctx = Context(Product(), template);

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void DifferentSchemaSameTableName_AreNotDuplicates()
    {
        // Two tables named "Customer" but in different explicit schemas are distinct objects —
        // schema-qualification in the grouping key must prevent a false-positive collision.
        var template = new Template { Name = "Main" };
        template.Tables.Add(new SqlServerTable { Name = "Customer", Schema = "dbo" });
        template.Tables.Add(new SqlServerTable { Name = "Customer", Schema = "sales" });
        var ctx = Context(Product(), template);

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void DuplicateTemplateNamesInTemplateOrder_AreError()
    {
        var product = Product("Main", "Reference", "Main");
        var ctx = Context(product);

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-DUP-001"));
        Assert.That(findings[0].Location, Is.EqualTo("Product 'Acme' / TemplateOrder"));
    }

    [Test]
    public void CleanPackage_NoFindings()
    {
        var pkg = ValidationTestPackages.Minimal(Platform.SqlServer);
        var ctx = new ValidationContext(pkg.Product, pkg.Templates, "pkg");

        var findings = new DuplicationCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }
}
