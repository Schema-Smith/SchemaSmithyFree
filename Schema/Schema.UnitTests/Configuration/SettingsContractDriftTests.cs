// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Schema.Configuration;

namespace Schema.UnitTests.Configuration;

/// <summary>
/// The guard that makes <see cref="SettingsContract"/> a contract rather than a promise.
/// <para>A manifest of accepted keys that nothing checks is a second source of truth, and it drifts the
/// moment someone adds a <c>config["…"]</c> read and forgets to register it. This scans the product
/// sources for configuration reads and fails if any key is unregistered, so the contract cannot fall
/// behind the code without the suite going red.</para>
/// </summary>
[TestFixture]
public class SettingsContractDriftTests
{
    private static readonly string[] ProductProjects =
        ["Schema", "SchemaQuench", "SchemaTongs", "DataTongs", "SchemaShears"];

    // config["Key"] / _config["Key"] / Configuration["Key"], and GetSection("Key").
    private static readonly Regex ConfigRead =
        new(@"(?:_?[Cc]onfig(?:uration)?(?:Root)?\s*\[\s*""(?<key>[^""]+)""\s*\]|GetSection\(\s*""(?<key>[^""]+)""\s*\))",
            RegexOptions.Compiled);

    // Documentation quotes config reads to explain them (this file does it too), so comments must not
    // be scanned or the contract would be asked to register examples.
    private static string StripComments(string source) =>
        string.Join(Environment.NewLine,
            source.Split('\n')
                  .Select(l => l.TrimStart())
                  .Select(l => l.StartsWith("//") || l.StartsWith('*') || l.StartsWith("/*") ? "" : l));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SchemaSmith.sln")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    private static IEnumerable<(string File, string Key)> ProductConfigReads(string root)
    {
        foreach (var project in ProductProjects)
        {
            var projectDir = Path.Combine(root, project);
            if (!Directory.Exists(projectDir)) continue;

            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                // Test projects live beneath the product project directories; they legitimately use
                // harness-only keys (SqlServer:Server, PostgreSQL:Password, …) that are not settings.
                if (file.Contains("Tests", StringComparison.OrdinalIgnoreCase)) continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

                foreach (Match m in ConfigRead.Matches(StripComments(File.ReadAllText(file))))
                {
                    var key = m.Groups["key"].Value;
                    // Interpolated or composed keys cannot be checked statically.
                    if (key.Length == 0 || key.Contains('{')) continue;
                    yield return (Path.GetRelativePath(root, file), key);
                }
            }
        }
    }

    [Test]
    public void EveryConfigurationKeyReadByProductCode_IsRegisteredInTheContract()
    {
        var root = RepoRoot();
        Assert.That(root, Is.Not.Null, "could not locate the repository root from the test output directory");

        var accepted = SettingsContract.AllAcceptedKeys().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unregistered = ProductConfigReads(root)
            .Where(r => !accepted.Contains(r.Key))
            .Where(r => !SettingsContract.IsOpenSection(r.Key))
            // A relative sub-key read from an already-scoped section (child.GetSection("Databases")).
            .Where(r => !IsScopedSubKey(r.Key))
            .Distinct()
            .OrderBy(r => r.Key)
            .ToList();

        Assert.That(unregistered, Is.Empty,
            "These configuration keys are read by product code but are not registered in SettingsContract, " +
            "so the contract has drifted behind the code:" + Environment.NewLine +
            string.Join(Environment.NewLine, unregistered.Select(r => $"  {r.Key}   ({r.File})")));
    }

    // Sub-keys read relative to a section already resolved from a registered parent — e.g.
    // Target:TemplateTargets:<Template> then GetSection("Databases"). The parent is what the
    // contract registers; these are members of an open section.
    private static bool IsScopedSubKey(string key) =>
        !key.Contains(':') &&
        key is "Databases" or "Schemas" or "Tables" or "CreateIfMissing";

    [Test]
    public void ContractKeysAreWellFormed()
    {
        foreach (var key in SettingsContract.AllAcceptedKeys())
        {
            Assert.That(key.Trim(), Is.EqualTo(key), $"'{key}' has surrounding whitespace");
            Assert.That(key, Is.Not.Empty);
            Assert.That(key, Does.Not.StartWith(":"), $"'{key}' starts with a separator");
            Assert.That(key, Does.Not.EndWith(":"), $"'{key}' ends with a separator");
        }
    }

    [Test]
    public void EveryToolHasKeys()
    {
        foreach (SettingsTool tool in Enum.GetValues<SettingsTool>())
            Assert.That(SettingsContract.AcceptedKeys(tool), Is.Not.Empty, $"{tool} registered no settings keys");
    }
}
