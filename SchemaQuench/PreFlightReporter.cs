// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;

namespace SchemaQuench;

/// <summary>
/// Captures the pre-flight preview result for one template: the in-scope work units and
/// whether the template is required and matched nothing.
/// </summary>
/// <param name="TemplateName">Template name used for display.</param>
/// <param name="required">True when <c>RequireAtLeastOneTarget</c> is set on the template.</param>
/// <param name="Units">Work units discovered for this template after per-template target filtering.</param>
/// <param name="matchedNothing">True when <paramref name="required"/> is true and <paramref name="Units"/> is empty.</param>
public sealed record TemplatePreview(
    string TemplateName,
    bool required,
    IReadOnlyList<WorkUnit> Units,
    bool matchedNothing);

/// <summary>
/// Pure formatter for the <c>--PreviewTargets</c> report. Converts a list of
/// <see cref="TemplatePreview"/> records into log-ready lines; the caller logs them via the
/// progress logger. No I/O, no side effects — fully unit-testable in isolation.
/// </summary>
public static class PreFlightReporter
{
    /// <summary>
    /// Renders the per-template target tree into a flat list of log lines.
    /// <para>
    /// For each template:
    /// <list type="bullet">
    ///   <item>A header line names the template.</item>
    ///   <item>Each database is listed as <c>db: &lt;name&gt;</c> (with <c>(would be created)</c>
    ///   when <see cref="WorkUnit.WouldCreateDatabase"/> is true).</item>
    ///   <item>For schema-template units (non-empty <c>SchemaName</c>), the schemas targeting
    ///   the same database are listed as <c>schemas: a, b</c> on the same db line.</item>
    ///   <item>When <see cref="TemplatePreview.matchedNothing"/> is true, an
    ///   <c>ERROR: matched 0 ...</c> line replaces the unit listing.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Render(IReadOnlyList<TemplatePreview> previews)
    {
        var lines = new List<string>();
        foreach (var preview in previews)
            RenderTemplate(preview, lines);
        return lines;
    }

    private static void RenderTemplate(TemplatePreview preview, List<string> lines)
    {
        lines.Add($"Template: {preview.TemplateName}{(preview.required ? " [required]" : "")}");

        if (preview.matchedNothing)
        {
            lines.Add($"  ERROR: matched 0 targets for required template '{preview.TemplateName}' — no databases or schemas were discovered");
            return;
        }

        if (preview.Units.Count == 0)
        {
            lines.Add("  (no targets matched)");
            return;
        }

        // Group by (server, database) so schema-template schemas can be listed on the same db line.
        var byServerDb = preview.Units
            .GroupBy(u => (u.Server, u.DatabaseName))
            .OrderBy(g => g.Key.Server)
            .ThenBy(g => g.Key.DatabaseName);

        foreach (var group in byServerDb)
        {
            var db = group.Key.DatabaseName;
            var wouldCreate = group.Any(u => u.WouldCreateDatabase);
            var schemas = group
                .Select(u => u.SchemaName)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var wouldCreateSuffix = wouldCreate ? " (would be created)" : "";
            if (schemas.Count > 0)
            {
                lines.Add($"  db: {db}{wouldCreateSuffix}");
                lines.Add($"    schemas: {string.Join(", ", schemas)}");
            }
            else
            {
                lines.Add($"  db: {db}{wouldCreateSuffix}");
            }
        }
    }
}
