// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;

namespace SchemaQuench.Validation;

/// <summary>
/// Pure formatter for the <c>--Validate</c> report. Converts findings into log-ready lines;
/// the caller logs them via the progress logger. No I/O, no side effects — fully unit-testable
/// in isolation (mirrors <see cref="PreFlightReporter"/>).
/// </summary>
public static class ValidationReporter
{
    /// <summary>
    /// Renders findings as errors (grouped first) then warnings, each formatted
    /// <c>"{SEVERITY} [{Code}] {Location}: {Message}"</c>, followed by a summary count line.
    /// Returns a single clean "PASS" line when there are no findings.
    /// </summary>
    public static IReadOnlyList<string> Render(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0)
            return new[] { "PASS — no issues found" };

        var lines = new List<string>();

        var errors = findings.Where(f => f.Severity == Severity.Error).ToList();
        var warnings = findings.Where(f => f.Severity == Severity.Warning).ToList();

        foreach (var finding in errors)
            lines.Add(FormatLine(finding));
        foreach (var finding in warnings)
            lines.Add(FormatLine(finding));

        lines.Add($"{errors.Count} error(s), {warnings.Count} warning(s)");

        return lines;
    }

    private static string FormatLine(Finding finding)
    {
        var severityLabel = finding.Severity == Severity.Error ? "ERROR" : "WARN";
        return $"{severityLabel} [{finding.Code}] {finding.Location}: {finding.Message}";
    }
}
