// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using System.Linq;
using Schema.Validation;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// The reporter renders every finding as <c>"{SEVERITY} [{Code}] {Location}: {Message}"</c>, so a
/// message that also opens with its own location prints it twice. Ten checks did exactly that and
/// nothing caught it — the existing tests all assert on message fragments, which match just as well
/// against a doubled line. This walks every `--Validate` fixture and enforces the invariant across
/// whichever checks actually fire, so a new check cannot reintroduce the wart.
/// </summary>
[Category("Validate")]
public class ValidateFindingMessageShapeTests : ValidateFixtureTestBase
{
    private static string[] AllFixtureCases()
    {
        var root = Path.GetDirectoryName(FixturePath("x", "y"))!;
        root = Path.GetDirectoryName(root)!;
        return Directory.Exists(root)
            ? Directory.GetDirectories(root).SelectMany(Directory.GetDirectories).ToArray()
            : [];
    }

    [Test]
    public void NoFindingMessageRepeatsItsOwnLocation()
    {
        var cases = AllFixtureCases();
        Assert.That(cases, Is.Not.Empty, "fixtures should have been copied next to the test assembly");

        var offenders = cases
            .SelectMany(p => RunValidate(p).Findings)
            .Where(f => !string.IsNullOrEmpty(f.Location)
                        && f.Message.StartsWith(f.Location, System.StringComparison.Ordinal))
            .Select(f => $"[{f.Code}] {f.Location}: {f.Message}")
            .Distinct()
            .ToList();

        Assert.That(offenders, Is.Empty,
            "these findings restate their location, which the reporter has already printed:\r\n  "
            + string.Join("\r\n  ", offenders));
    }
}
