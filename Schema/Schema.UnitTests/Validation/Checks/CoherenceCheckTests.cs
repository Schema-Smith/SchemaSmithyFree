// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using NUnit.Framework;
using Schema.Domain;
using Schema.Domain.SqlServer;
using Schema.Validation;
using Schema.Validation.Checks;

namespace Schema.UnitTests.Validation.Checks;

/// <summary>
/// Slice 2.2: cross-object coherence (FK/index). Structural reference checks only — no type
/// comparisons, no DeleteAction/UpdateAction checks (out of scope; those belong to JSON-schema
/// lint). Covers FK local-column, related-table resolution (incl. schema defaulting),
/// related-column, cardinality, and index-column existence. No ambiguity check: FK targets always
/// resolve to a concrete schema post schema-resolution (SchemaDefaultResolver.
/// ResolveRelatedTableSchema runs inside Template.Load), so an unqualified reference is never
/// actually ambiguous — it either resolves-and-exists (fine) or resolves-and-missing (SS-FK-002).
/// </summary>
[TestFixture]
public class CoherenceCheckTests
{
    private static Product Product() => new()
    {
        Name = "Acme",
        Platform = Platform.SqlServer,
        TemplateOrder = new System.Collections.Generic.List<string>()
    };

    private static ValidationContext Context(params Template[] templates) =>
        new(Product(), templates, "pkg");

    private static Template TemplateWithTables(string templateName, params SqlServerTable[] tables)
    {
        var template = new Template { Name = templateName };
        foreach (var table in tables) template.Tables.Add(table);
        return template;
    }

    private static SqlServerTable Customer(string schema = "dbo") => new()
    {
        Name = "Customer",
        Schema = schema,
        Columns = { new SqlServerColumn { Name = "Id", DataType = "int" } }
    };

    [Test]
    public void FkLocalColumnMissing_IsError()
    {
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" } },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId",
                    RelatedTable = "Customer",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "Id"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order, Customer()));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-FK-001"));
        Assert.That(findings[0].Category, Is.EqualTo("Coherence"));
        Assert.That(findings[0].Location, Is.EqualTo("Template 'Main' / Table 'Order' / FK 'FK_Order_Customer'"));
    }

    [Test]
    public void FkRelatedTableMissing_IsError()
    {
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" }, new SqlServerColumn { Name = "CustomerId", DataType = "int" } },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId",
                    RelatedTable = "NoSuchTable",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "Id"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-FK-002"));
        Assert.That(findings[0].Category, Is.EqualTo("Coherence"));
    }

    [Test]
    public void FkRelatedTableGatedVariants_NotAmbiguous()
    {
        // Two GATED variants of the SAME (schema,name) are one logical table — not ambiguous,
        // and the FK should resolve cleanly with no findings.
        var customerA = new SqlServerTable
        {
            Name = "Customer", Schema = "dbo", ShouldApplyExpression = "{{IsPartitioned}}", VariantName = "Partitioned",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" } }
        };
        var customerB = new SqlServerTable
        {
            Name = "Customer", Schema = "dbo", ShouldApplyExpression = "{{IsNotPartitioned}}", VariantName = "NotPartitioned",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" } }
        };
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" }, new SqlServerColumn { Name = "CustomerId", DataType = "int" } },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId",
                    RelatedTable = "Customer",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "Id"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order, customerA, customerB));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void FkRelatedColumnMissing_IsError()
    {
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" }, new SqlServerColumn { Name = "CustomerId", DataType = "int" } },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId",
                    RelatedTable = "Customer",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "NoSuchColumn"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order, Customer()));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-FK-004"));
        Assert.That(findings[0].Category, Is.EqualTo("Coherence"));
    }

    [Test]
    public void FkColumnCountMismatch_IsError()
    {
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns =
            {
                new SqlServerColumn { Name = "Id", DataType = "int" },
                new SqlServerColumn { Name = "CustomerId", DataType = "int" },
                new SqlServerColumn { Name = "CustomerRegion", DataType = "int" }
            },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId, CustomerRegion",
                    RelatedTable = "Customer",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "Id"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order, Customer()));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-FK-005"));
        Assert.That(findings[0].Category, Is.EqualTo("Coherence"));
    }

    [Test]
    public void IndexColumnMissing_IsError()
    {
        var table = new SqlServerTable
        {
            Name = "Customer",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" } },
            Indexes = { new SqlServerIndex { Name = "IX_Customer_Name", IndexColumns = "Name DESC" } }
        };
        var ctx = Context(TemplateWithTables("Main", table));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Has.Exactly(1).Items);
        Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
        Assert.That(findings[0].Code, Is.EqualTo("SS-IDX-001"));
        Assert.That(findings[0].Category, Is.EqualTo("Coherence"));
    }

    [Test]
    public void ValidFkAcrossTemplates_NoFindings()
    {
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" }, new SqlServerColumn { Name = "CustomerId", DataType = "int" } },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId",
                    RelatedTable = "Customer",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "Id"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order), TemplateWithTables("Reference", Customer()));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void ReferencedVariantColumn_Exists_NoFinding()
    {
        // The related column exists only as a gated variant on the related table — still counts.
        var customer = new SqlServerTable
        {
            Name = "Customer",
            Schema = "dbo",
            Columns =
            {
                new SqlServerColumn { Name = "Id", DataType = "int", ShouldApplyExpression = "{{UseGuidKeys}}", VariantName = "GuidKey" },
                new SqlServerColumn { Name = "Id", DataType = "bigint", ShouldApplyExpression = "{{UseIntKeys}}", VariantName = "IntKey" }
            }
        };
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" }, new SqlServerColumn { Name = "CustomerId", DataType = "int" } },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId",
                    RelatedTable = "Customer",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "Id"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order, customer));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void CleanPackage_NoFindings()
    {
        // A real, populated package — a table with a valid FK (local + related columns present,
        // cardinality matching) AND a valid index — proving every remaining check is satisfied
        // together, not just vacuously on an FK/index-free table.
        var customer = new SqlServerTable
        {
            Name = "Customer",
            Schema = "dbo",
            Columns =
            {
                new SqlServerColumn { Name = "Id", DataType = "int" },
                new SqlServerColumn { Name = "Name", DataType = "nvarchar" }
            },
            Indexes = { new SqlServerIndex { Name = "IX_Customer_Name", IndexColumns = "Name" } }
        };
        var order = new SqlServerTable
        {
            Name = "Order",
            Schema = "dbo",
            Columns = { new SqlServerColumn { Name = "Id", DataType = "int" }, new SqlServerColumn { Name = "CustomerId", DataType = "int" } },
            ForeignKeys =
            {
                new SqlServerForeignKey
                {
                    Name = "FK_Order_Customer",
                    Columns = "CustomerId",
                    RelatedTable = "Customer",
                    RelatedTableSchema = "dbo",
                    RelatedColumns = "Id"
                }
            }
        };
        var ctx = Context(TemplateWithTables("Main", order, customer));

        var findings = new CoherenceCheck().Run(ctx).ToList();

        Assert.That(findings, Is.Empty);
    }
}
