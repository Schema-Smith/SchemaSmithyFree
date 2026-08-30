// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Configuration;

namespace Schema.UnitTests.Configuration;

/// <summary>
/// Every setting a tool accepts has to appear in that tool's shipped example settings file.
/// <para>The example file is the discovery surface. Most users meet a setting by reading the file they
/// copied, not by reading the reference docs, so a setting missing from it is effectively missing —
/// <c>Target:IntegratedSecurity</c> shipped in v2.5.0 and the whole <c>RebuildPolicy</c> trio shipped
/// with table rebuilds, and neither appeared in the file meant to introduce them. Twenty-four accepted
/// keys were absent across the four tools when this was first measured.</para>
/// <para>This is checkable rather than a matter of taste only because <see cref="SettingsContract"/>
/// already records which keys belong to which tool. Without that the audit would need human judgement
/// per key, which is exactly how the files drifted this far — so the guard is worth having precisely
/// where the answer is mechanical.</para>
/// <para><b>Deliberate limitation.</b> A key counts as present if its name appears anywhere in the
/// file, comments included. That is not an accident: advanced settings are shown commented-out with
/// their default, and a check that demanded a live JSON member would forbid the tiered layout this
/// file set uses. So this proves a setting is *mentioned*, not that it is mentioned *well* — it
/// catches the failure that actually happened (a shipped setting nobody wrote down) and cannot catch
/// a bad comment.</para>
/// </summary>
[TestFixture]
public class SettingsExampleCompletenessTests
{
    private static readonly Dictionary<SettingsTool, string[]> ExampleFile = new()
    {
        [SettingsTool.SchemaQuench] = ["SchemaQuench", "SchemaQuench.settings.json"],
        [SettingsTool.SchemaTongs] = ["SchemaTongs", "SchemaTongs.settings.json"],
        [SettingsTool.DataTongs] = ["DataTongs", "DataTongs.settings.json"],
        [SettingsTool.SchemaShears] = ["SchemaShears", "SchemaShears.settings.json"]
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Join(dir.FullName, "SchemaSmith.sln")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    [TestCaseSource(nameof(Tools))]
    public void EveryAcceptedSettingAppearsInTheToolsExampleFile(SettingsTool tool)
    {
        var root = RepoRoot();
        Assert.That(root, Is.Not.Null, "could not locate the repository root");

        var path = Path.Join(new[] { root }.Concat(ExampleFile[tool]).ToArray());
        Assert.That(File.Exists(path), Is.True, $"{tool} has no example settings file at {path}");

        var text = File.ReadAllText(path);
        var accepted = SettingsContract.AcceptedKeys(tool);

        // Guards the premise: an empty accepted set would make this pass while proving nothing.
        Assert.That(accepted, Is.Not.Empty, $"{tool} accepts no settings, which cannot be right");

        var missing = accepted
            .Where(key => !text.Contains($"\"{key.Split(':').Last()}\"", StringComparison.Ordinal))
            .ToList();

        Assert.That(missing, Is.Empty,
            $"{tool} accepts {accepted.Count} settings but its example file never mentions "
            + $"{missing.Count} of them, so a user copying that file cannot discover they exist: "
            + string.Join(", ", missing)
            + ". Add each one — inline if it is common, or commented with its default under the "
            + "advanced section if it is not.");
    }

    private static IEnumerable<SettingsTool> Tools() => Enum.GetValues<SettingsTool>();
}
