// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Schema.Validation;

/// <summary>
/// Orchestrates a `--Validate` run: loads the package, and — if the load itself succeeds — runs
/// every registered <see cref="ISchemaCheck"/> and aggregates their findings. A load failure is
/// reported as a single finding (v1 LOAD-GATE) rather than propagating the exception: reporting
/// cleanly instead of crashing is the point of the linter.
/// </summary>
public sealed class SchemaPackageValidator
{
    private readonly Func<LoadedPackage> _loader;
    private readonly IReadOnlyList<ISchemaCheck> _checks;

    public SchemaPackageValidator(Func<LoadedPackage> loader, IReadOnlyList<ISchemaCheck> checks)
    {
        _loader = loader;
        _checks = checks;
    }

    public ValidationResult Validate(string packagePath)
    {
        LoadedPackage pkg;
        try
        {
            pkg = _loader();
        }
        catch (Exception e)
        {
            var location = e.Message.Split('\n')[0].TrimEnd('\r');
            return new ValidationResult(new[]
            {
                new Finding(Severity.Error, "SS-LOAD-001", "Load", location, e.Message)
            });
        }

        var ctx = new ValidationContext(pkg.Product, pkg.Templates, packagePath);
        // No per-check try/catch (YAGNI) — the real checks don't throw; a check exception is a
        // bug in the check and should surface, not be swallowed as a finding.
        return new ValidationResult(_checks.SelectMany(check => check.Run(ctx)));
    }
}
