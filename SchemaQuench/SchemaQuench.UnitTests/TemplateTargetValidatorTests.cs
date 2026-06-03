// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using NUnit.Framework;
using Schema.Domain;
using SchemaQuench;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class TemplateTargetValidatorTests
{
    // The validator is checked against the canonical diagnostic strings from design §5.
    // If a diagnostic string changes here, design §5 must change too, and vice versa.

    private static Template SchemaTemplate(string name) => new()
    {
        Name = name,
        DatabaseIdentificationScript = "SELECT 'db' AS DatabaseName",
        SchemaIdentificationScript = "SELECT 'schema' AS SchemaName"
    };

    private static Template DatabaseOnlyTemplate(string name) => new()
    {
        Name = name,
        DatabaseIdentificationScript = "SELECT 'db' AS DatabaseName"
    };

    private static Template RegularTemplate(string name) => new()
    {
        Name = name,
        DatabaseIdentificationScript = "SELECT DB_NAME() AS DatabaseName"
    };

    [Test]
    public void Rule1_UnknownTemplateName_FailsFastWithExpectedDiagnostic()
    {
        var templates = new List<Template> { RegularTemplate("Shared"), SchemaTemplate("TenantSchema") };
        var targets = new Dictionary<string, TemplateTarget>
        {
            ["FooBar"] = new() { Schemas = new() { "x" } }
        };
        var targetTemplatesFilter = new List<string>();

        var ex = Assert.Throws<TemplateTargetValidationException>(() =>
            TemplateTargetValidator.Validate(targets, templates, targetTemplatesFilter));

        Assert.That(ex!.Message, Does.Contain(
            "TemplateTargets entry 'FooBar' does not match any template in Product.json TemplateOrder. " +
            "Known templates: [Shared, TenantSchema]."));
    }

    [Test]
    public void Rule2_FilteredOutTemplate_FailsFastWithExpectedDiagnostic()
    {
        var templates = new List<Template> { RegularTemplate("Shared"), SchemaTemplate("TenantSchema") };
        var targets = new Dictionary<string, TemplateTarget>
        {
            ["TenantSchema"] = new() { Schemas = new() { "acme" } }
        };
        var targetTemplatesFilter = new List<string> { "Shared" };

        var ex = Assert.Throws<TemplateTargetValidationException>(() =>
            TemplateTargetValidator.Validate(targets, templates, targetTemplatesFilter));

        Assert.That(ex!.Message, Does.Contain(
            "TemplateTargets entry 'TenantSchema' references a template that is excluded by " +
            "Target.Templates=[Shared]. Either remove the TemplateTargets entry or expand Target.Templates."));
    }

    [Test]
    public void Rule3_EmptyEntry_FailsFastWithExpectedDiagnostic()
    {
        var templates = new List<Template> { SchemaTemplate("TenantSchema") };
        var targets = new Dictionary<string, TemplateTarget>
        {
            ["TenantSchema"] = new()  // Neither Databases nor Schemas
        };

        var ex = Assert.Throws<TemplateTargetValidationException>(() =>
            TemplateTargetValidator.Validate(targets, templates, new List<string>()));

        Assert.That(ex!.Message, Does.Contain(
            "TemplateTargets entry 'TenantSchema' must declare at least one of Databases or Schemas."));
    }

    [Test]
    public void Rule4_SchemasWithoutScript_FailsFastWithExpectedDiagnostic()
    {
        var templates = new List<Template> { RegularTemplate("Shared") };
        var targets = new Dictionary<string, TemplateTarget>
        {
            ["Shared"] = new() { Schemas = new() { "acme" } }
        };

        var ex = Assert.Throws<TemplateTargetValidationException>(() =>
            TemplateTargetValidator.Validate(targets, templates, new List<string>()));

        Assert.That(ex!.Message, Does.Contain(
            "TemplateTargets.Shared.Schemas requires Template 'Shared' to declare a " +
            "SchemaIdentificationScript. Add a placeholder script (e.g., SELECT 'CONFIG-DRIVEN' AS " +
            "SchemaName WHERE 1=0) to mark this template as schema-fan-out."));
    }

    [Test]
    public void Rule5_DatabasesWithoutScript_FailsFastWithExpectedDiagnostic()
    {
        var templates = new List<Template>
        {
            new()
            {
                Name = "NoScriptTemplate"
                // No DatabaseIdentificationScript at all
            }
        };
        var targets = new Dictionary<string, TemplateTarget>
        {
            ["NoScriptTemplate"] = new() { Databases = new() { "x" } }
        };

        var ex = Assert.Throws<TemplateTargetValidationException>(() =>
            TemplateTargetValidator.Validate(targets, templates, new List<string>()));

        Assert.That(ex!.Message, Does.Contain(
            "TemplateTargets.NoScriptTemplate.Databases requires Template 'NoScriptTemplate' to " +
            "declare a DatabaseIdentificationScript. Add a placeholder script (e.g., SELECT " +
            "'CONFIG-DRIVEN' AS DatabaseName WHERE 1=0) to mark this template as database-fan-out."));
    }

    [Test]
    public void ValidConfig_DoesNotThrow()
    {
        // Locks in the validator's OrdinalIgnoreCase choice across both lookup surfaces:
        //  - the templates-by-name lookup uses StringComparer.OrdinalIgnoreCase, so the targets
        //    dict key "TenantSchema" matches the in-memory Template "TenantSchema" verbatim;
        //  - the Target.Templates filter membership check also uses OrdinalIgnoreCase, so a
        //    lowercase filter entry "tenantschema" still passes rule 2 against the canonical
        //    "TenantSchema" target key. A refactor to case-sensitive `Contains` would fail this
        //    test on the filter-membership check.
        var templates = new List<Template>
        {
            RegularTemplate("Shared"),
            SchemaTemplate("TenantSchema"),
            DatabaseOnlyTemplate("PerTenantDB")
        };
        var targets = new Dictionary<string, TemplateTarget>
        {
            ["TenantSchema"] = new() { Schemas = new() { "acme", "globex" }, CreateIfMissing = true },
            ["PerTenantDB"] = new() { Databases = new() { "tenant_a" } }
        };
        var targetTemplatesFilter = new List<string> { "tenantschema", "PerTenantDB" };

        Assert.DoesNotThrow(() =>
            TemplateTargetValidator.Validate(targets, templates, targetTemplatesFilter));
    }

    // Rule 6 (filter outside override universe) is tested at the WorkUnitFilter boundary
    // in slice 2 because it composes with the existing filter logic; the validator owns
    // rules 1-5.
}
