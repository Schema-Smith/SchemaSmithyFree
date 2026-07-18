// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;

namespace Schema.Validation;

/// <summary>
/// Immutable outcome of a <c>--Validate</c> run: the full set of findings plus a convenience
/// flag for whether any of them are error-severity (callers use this to decide the exit code).
/// </summary>
public sealed class ValidationResult
{
    public IReadOnlyList<Finding> Findings { get; }
    public bool HasErrors => Findings.Any(f => f.Severity == Severity.Error);

    public ValidationResult(IEnumerable<Finding> findings)
    {
        Findings = findings.ToList();
    }
}
