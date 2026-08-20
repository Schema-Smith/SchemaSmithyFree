// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using Newtonsoft.Json;
using Schema.Domain;

namespace Schema.Validation;

/// <summary>
/// Thin production loader for `--Validate`: reads the configured package from disk. Mirrors
/// <c>ProductQuench.LoadTemplates</c> minus the deploy-only special-token cross-resolution (the
/// linter only needs the loaded domain objects, not deployment-time token wiring). Integration-
/// covered in Slice 3 — not unit-tested here (would require a real package on disk).
/// <para>
/// Loads leniently on purpose — this is the one caller that passes
/// <see cref="MissingMemberHandling.Ignore"/> to <see cref="Product.Load"/> and
/// <c>tolerateComponentLoadErrors: true</c> to <see cref="Template.Load"/>. `--Validate`'s
/// contract (<c>SchemaPackageValidator</c>) is to enumerate every problem it can find in one
/// pass; strict deserialization aborts the whole run on the FIRST unrecognised property, hiding
/// every other finding a full pass would otherwise report. A component that still fails to load
/// (malformed JSON, not just an unknown property) is skipped here rather than propagated — the
/// package stays as complete as it can be, and <c>JsonSchemaCheck</c> independently re-validates
/// every file straight off disk regardless of what loaded here, so the skipped file's precise
/// SS-JSON-001 finding still surfaces. The deploy path (<c>ProductQuench</c>) never sets either
/// flag, so an unrecognised property there still aborts the run as intended.
/// </para>
/// </summary>
public static class PackageLoader
{
    public static LoadedPackage LoadPackage()
    {
        var product = Product.Load(MissingMemberHandling.Ignore);
        var templates = product.TemplateOrder
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => TryLoadTemplate(n, product))
            .Where(t => t != null)
            .ToList();
        // Skip-and-report, not skip-and-forget: a component file Template.Load excluded because it
        // wasn't valid JSON at all (Template.ComponentLoadErrors) still needs to surface as a
        // finding, or the run would exit clean over a package it silently loaded less of. A
        // parseable-but-wrong component (misnamed property) never lands here — see
        // Template.RecordComponentLoadErrorIfUnparseable — because JsonSchemaCheck's own pass
        // already reports that precisely as SS-JSON-001.
        var loadFindings = templates
            .SelectMany(t => t.ComponentLoadErrors)
            .Select(e => new Finding(Severity.Error, "SS-LOAD-001", "Load", e.FilePath, e.Message))
            .ToList();
        return new LoadedPackage(product, templates, loadFindings);
    }

    // A template that fails to load at all (e.g. its own Template.json is unparseable) is
    // excluded rather than aborting LoadPackage — every OTHER template still gets
    // Duplication/Coherence coverage, and JsonSchemaCheck's own disk scan still reaches this
    // template's files regardless (it reads ValidationContext.PackagePath, not pkg.Templates).
    private static Template TryLoadTemplate(string name, Product product)
    {
        try
        {
            return Template.Load(name, product, tolerateComponentLoadErrors: true);
        }
        catch
        {
            return null;
        }
    }
}
