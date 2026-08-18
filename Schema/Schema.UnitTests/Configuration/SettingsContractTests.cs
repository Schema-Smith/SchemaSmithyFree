// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Schema.Configuration;

namespace Schema.UnitTests.Configuration;

[TestFixture]
public class SettingsContractTests
{
    private static IConfigurationRoot Config(params (string Key, string Value)[] entries)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (k, v) in entries) dict[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Test]
    public void MistypedKeyInAnOwnedSection_IsReported()
    {
        // The motivating case: Target:Sever binds nothing and the run proceeds as though the setting
        // were absent — the config-side twin of the unrecognised-CLI-argument defect.
        var unrecognized = SettingsContract.UnrecognizedKeys(
            Config(("Target:Server", "srv"), ("Target:Sever", "typo")), SettingsTool.SchemaQuench);

        Assert.That(unrecognized, Is.EquivalentTo(new[] { "Target:Sever" }));
    }

    [Test]
    public void CorrectKeys_ReportNothing()
    {
        var unrecognized = SettingsContract.UnrecognizedKeys(
            Config(("Target:Server", "srv"), ("Target:User", "u"), ("Target:Password", "p"),
                   ("Target:IntegratedSecurity", "true")), SettingsTool.SchemaQuench);

        Assert.That(unrecognized, Is.Empty);
    }

    [Test]
    public void ForeignTopLevelSection_IsIgnored()
    {
        // The integration harness's test.settings.json carries SqlServer/PostgreSQL/MySQL/MariaDB
        // sections that are not product settings at all. Reporting them would bury the real case and
        // would break the existing DidNotReceive().Warn(Arg.Any<string>()) assertions.
        var unrecognized = SettingsContract.UnrecognizedKeys(
            Config(("SqlServer:Server", "127.0.0.1"), ("PostgreSQL:Password", "pw"),
                   ("MySQL:Port", "3306"), ("MariaDB:User", "u")), SettingsTool.SchemaQuench);

        Assert.That(unrecognized, Is.Empty);
    }

    [TestCase("ScriptTokens:MainDB")]
    [TestCase("Target:ConnectionProperties:Encrypt")]
    [TestCase("Target:TemplateTargets:TenantBody:Databases:0")]
    public void OpenSectionChildren_AreNeverUnrecognized(string key)
    {
        Assert.That(SettingsContract.UnrecognizedKeys(Config((key, "v")), SettingsTool.SchemaQuench), Is.Empty);
    }

    [TestCase("Target:Databases:0")]
    [TestCase("Target:Templates:2")]
    [TestCase("Target:Schemas:11")]
    public void ArrayElements_AreNotTreatedAsMembers(string key)
    {
        Assert.That(SettingsContract.UnrecognizedKeys(Config((key, "v")), SettingsTool.SchemaQuench), Is.Empty);
    }

    [Test]
    public void AnotherToolsSection_DoesNotWarn()
    {
        // SchemaQuench does not own Source: — a shared or repurposed file must not produce noise.
        Assert.That(SettingsContract.UnrecognizedKeys(
            Config(("Source:Server", "srv")), SettingsTool.SchemaQuench), Is.Empty);
    }

    [Test]
    public void MistypedShouldCastKey_IsReportedForTheTongs()
    {
        var unrecognized = SettingsContract.UnrecognizedKeys(
            Config(("ShouldCast:Tables", "true"), ("ShouldCast:Tabels", "true")), SettingsTool.SchemaTongs);

        Assert.That(unrecognized, Is.EquivalentTo(new[] { "ShouldCast:Tabels" }));
    }

    [Test]
    public void WarnOnUnrecognizedKeys_NamesTheKeyAndTheTool()
    {
        var warnings = new List<string>();
        SettingsContract.WarnOnUnrecognizedKeys(
            Config(("Target:Sever", "typo")), SettingsTool.SchemaQuench, warnings.Add);

        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("Target:Sever"));
        Assert.That(warnings[0], Does.Contain("SchemaQuench"));
    }

    [Test]
    public void WarnOnUnrecognizedKeys_SilentWhenEverythingIsRecognized()
    {
        var warnings = new List<string>();
        SettingsContract.WarnOnUnrecognizedKeys(
            Config(("Target:Server", "srv")), SettingsTool.SchemaQuench, warnings.Add);

        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void NullConfigOrWarner_IsSafe()
    {
        Assert.That(SettingsContract.UnrecognizedKeys(null, SettingsTool.SchemaQuench), Is.Empty);
        Assert.DoesNotThrow(() => SettingsContract.WarnOnUnrecognizedKeys(Config(("Target:Sever", "x")), SettingsTool.SchemaQuench, null));
    }

    [Test]
    public void AcceptedKeys_AreToolScoped()
    {
        var quench = SettingsContract.AcceptedKeys(SettingsTool.SchemaQuench);
        var shears = SettingsContract.AcceptedKeys(SettingsTool.SchemaShears);

        Assert.That(quench, Does.Contain(SettingsKeys.Target.Server));
        Assert.That(quench, Does.Not.Contain(SettingsKeys.SourcePath), "SchemaShears' package-carving keys are not SchemaQuench settings");
        Assert.That(shears, Does.Contain(SettingsKeys.SourcePath));
        Assert.That(SettingsContract.AllAcceptedKeys(), Is.SupersetOf(quench));
    }

    [Test]
    public void EveryKeyInTheShippedSamples_IsRegistered()
    {
        // A typo in a shipped sample is inert and invisible today; this makes it a test failure.
        foreach (var (tool, file) in new[]
                 {
                     (SettingsTool.SchemaQuench, "SchemaQuench/SchemaQuench.settings.json"),
                     (SettingsTool.SchemaTongs, "SchemaTongs/SchemaTongs.settings.json"),
                     (SettingsTool.DataTongs, "DataTongs/DataTongs.settings.json"),
                     (SettingsTool.SchemaShears, "SchemaShears/SchemaShears.settings.json")
                 })
        {
            var path = System.IO.Path.Combine(RepoRoot(), file);
            if (!System.IO.File.Exists(path)) continue;

            var config = new ConfigurationBuilder().AddJsonFile(path).Build();
            Assert.That(SettingsContract.UnrecognizedKeys(config, tool), Is.Empty,
                $"{file} contains keys {tool} does not read");
        }
    }

    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "SchemaSmith.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? "";
    }
}
