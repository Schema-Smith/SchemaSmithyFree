// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Domain;

namespace SchemaQuench;

/// <summary>
/// Validates <c>Target.TemplateTargets</c> configuration against the loaded product's
/// templates and the active <c>Target.Templates</c> filter. Implements rules 1-5 of
/// the six fail-fast rules in the design (rule 6 — filter values outside the override
/// universe — lives in <see cref="WorkUnitFilter"/> because it composes with the
/// existing filter logic). The thrown <see cref="TemplateTargetValidationException"/>
/// is defined alongside <see cref="TemplateTargetProvisioningException"/> in
/// <c>TemplateTargetExceptions.cs</c>.
/// </summary>
public static class TemplateTargetValidator
{
    public static void Validate(
        IReadOnlyDictionary<string, TemplateTarget> targets,
        IReadOnlyList<Template> templates,
        IReadOnlyList<string> targetTemplatesFilter)
    {
        if (targets == null || targets.Count == 0) return;

        var templatesByName = templates.ToDictionary(
            t => t.Name,
            t => t,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (templateName, target) in targets)
        {
            // Rule 1: unknown template name.
            if (!templatesByName.TryGetValue(templateName, out var template))
            {
                var known = string.Join(", ", templates.Select(t => t.Name));
                throw new TemplateTargetValidationException(
                    $"TemplateTargets entry '{templateName}' does not match any template in " +
                    $"Product.json TemplateOrder. Known templates: [{known}].");
            }

            // Rule 2: template filtered out by Target.Templates.
            if (targetTemplatesFilter.Count > 0 &&
                !targetTemplatesFilter.Contains(templateName, StringComparer.OrdinalIgnoreCase))
            {
                var filterList = string.Join(", ", targetTemplatesFilter);
                throw new TemplateTargetValidationException(
                    $"TemplateTargets entry '{templateName}' references a template that is excluded " +
                    $"by Target.Templates=[{filterList}]. Either remove the TemplateTargets entry or " +
                    $"expand Target.Templates.");
            }

            // Rule 3: empty entry.
            if (target.HasNoTargets)
            {
                throw new TemplateTargetValidationException(
                    $"TemplateTargets entry '{templateName}' must declare at least one of " +
                    $"Databases or Schemas.");
            }

            // Rule 4: Schemas without SchemaIdentificationScript.
            if (target.Schemas.Any() && string.IsNullOrWhiteSpace(template.SchemaIdentificationScript))
            {
                throw new TemplateTargetValidationException(
                    $"TemplateTargets.{templateName}.Schemas requires Template '{templateName}' to " +
                    $"declare a SchemaIdentificationScript. Add a placeholder script (e.g., SELECT " +
                    $"'CONFIG-DRIVEN' AS SchemaName WHERE 1=0) to mark this template as schema-fan-out.");
            }

            // Rule 5: Databases without DatabaseIdentificationScript.
            if (target.Databases.Any() && string.IsNullOrWhiteSpace(template.DatabaseIdentificationScript))
            {
                throw new TemplateTargetValidationException(
                    $"TemplateTargets.{templateName}.Databases requires Template '{templateName}' to " +
                    $"declare a DatabaseIdentificationScript. Add a placeholder script (e.g., SELECT " +
                    $"'CONFIG-DRIVEN' AS DatabaseName WHERE 1=0) to mark this template as database-fan-out.");
            }
        }
    }
}
