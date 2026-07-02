// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Linq;
using Schema.Domain;

namespace SchemaQuench.Validation;

/// <summary>
/// Thin production loader for `--Validate`: reads the configured package from disk. Mirrors
/// <c>ProductQuench.LoadTemplates</c> minus the deploy-only special-token cross-resolution (the
/// linter only needs the loaded domain objects, not deployment-time token wiring). Integration-
/// covered in Slice 3 — not unit-tested here (would require a real package on disk).
/// </summary>
public static class PackageLoader
{
    public static LoadedPackage LoadPackage()
    {
        var product = Product.Load();
        var templates = product.TemplateOrder
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => Template.Load(n, product))
            .ToList();
        return new LoadedPackage(product, templates);
    }
}
