// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using NSubstitute;
using Schema.Delivery;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.UnitTests.Delivery;

/// <summary>
/// Issue #390: DataTongs emits a {{key}} content-file token into every tokenized merge script but
/// never wrote the matching ScriptTokens entry that resolves it. These tests cover the configurator
/// that wires it, following DataDeliveryConfiguratorImpl's idempotency shape (SetIfDifferent /
/// changed / "already up to date") exactly.
/// </summary>
[TestFixture]
public class MergeScriptTokenConfiguratorImplTests
{
    private static readonly string TemplateRoot = Path.Join(Path.GetTempPath(), "ss_mst_configurator_template");
    private static readonly string TemplateJsonPath = Path.Join(TemplateRoot, "Template.json");
    private static readonly string ContentFilePath = Path.Join(TemplateRoot, "data", "Widget.tabledata");

    private IFile _file;
    private List<string> _warnings;
    private List<string> _progress;

    [SetUp]
    public void SetUp()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            _file = Substitute.For<IFile>();
            FactoryContainer.Register(_file);
        }

        _warnings = new List<string>();
        _progress = new List<string>();

        _file.Exists(TemplateJsonPath).Returns(true);
    }

    [TearDown]
    public void TearDown()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
        }
    }

    private MergeScriptTokenConfiguratorContext MakeContext() => new()
    {
        TemplateRootPath = TemplateRoot,
        TokenKey = "Widget.tabledata",
        ContentFilePath = ContentFilePath,
        WarningLog = _warnings.Add,
        ProgressLog = _progress.Add
    };

    [Test]
    public void Configure_NoScriptTokensYet_WritesFileTokenWithTemplateRelativePath()
    {
        _file.ReadAllText(TemplateJsonPath).Returns("""
            { "Name": "Main" }
            """);

        MergeScriptTokenConfiguratorImpl.GetFromFactory().Configure(MakeContext());

        _file.Received(1).WriteAllText(TemplateJsonPath, Arg.Is<string>(s =>
            s.ContainsIgnoringCase("\"ScriptTokens\"") &&
            s.ContainsIgnoringCase("\"Widget.tabledata\": \"<*File*>data/Widget.tabledata\"")));
    }

    [Test]
    public void Configure_ExistingScriptTokensObject_AddsKeyPreservingOthers()
    {
        _file.ReadAllText(TemplateJsonPath).Returns("""
            {
              "Name": "Main",
              "ScriptTokens": { "Other": "<*Query*>SELECT 1" }
            }
            """);

        MergeScriptTokenConfiguratorImpl.GetFromFactory().Configure(MakeContext());

        _file.Received(1).WriteAllText(TemplateJsonPath, Arg.Is<string>(s =>
            s.ContainsIgnoringCase("\"Other\": \"<*Query*>SELECT 1\"") &&
            s.ContainsIgnoringCase("\"Widget.tabledata\": \"<*File*>data/Widget.tabledata\"")));
    }

    [Test]
    public void Configure_SameValueAlreadyPresent_WritesNothing_LogsUpToDate()
    {
        _file.ReadAllText(TemplateJsonPath).Returns("""
            {
              "Name": "Main",
              "ScriptTokens": { "Widget.tabledata": "<*File*>data/Widget.tabledata" }
            }
            """);

        MergeScriptTokenConfiguratorImpl.GetFromFactory().Configure(MakeContext());

        _file.DidNotReceiveWithAnyArgs().WriteAllText(default, default);
        Assert.That(_progress, Has.Some.Matches<string>(p => p.ContainsIgnoringCase("already up to date")));
    }

    [Test]
    public void Configure_ExistingFileTokenStalePath_Overwrites()
    {
        // A prior extraction wrote this file token pointing at a different (now stale) path -- safe
        // to refresh since it is DataTongs' own token shape, not a deliberate user override.
        _file.ReadAllText(TemplateJsonPath).Returns("""
            {
              "Name": "Main",
              "ScriptTokens": { "Widget.tabledata": "<*File*>old/Widget.tabledata" }
            }
            """);

        MergeScriptTokenConfiguratorImpl.GetFromFactory().Configure(MakeContext());

        _file.Received(1).WriteAllText(TemplateJsonPath, Arg.Is<string>(s =>
            s.ContainsIgnoringCase("\"Widget.tabledata\": \"<*File*>data/Widget.tabledata\"") &&
            !s.ContainsIgnoringCase("old/Widget.tabledata")));
    }

    [Test]
    public void Configure_ExistingNonFileTokenValue_LeavesUnchanged_Warns()
    {
        // A deliberate user override (not the <*File*> shape DataTongs itself writes) must survive.
        _file.ReadAllText(TemplateJsonPath).Returns("""
            {
              "Name": "Main",
              "ScriptTokens": { "Widget.tabledata": "<*Query*>SELECT '{{SchemaName}}_widget'" }
            }
            """);

        MergeScriptTokenConfiguratorImpl.GetFromFactory().Configure(MakeContext());

        _file.DidNotReceiveWithAnyArgs().WriteAllText(default, default);
        Assert.That(_warnings, Has.Some.Matches<string>(w =>
            w.ContainsIgnoringCase("Widget.tabledata") && w.ContainsIgnoringCase("not a DataTongs-managed file token")));
    }

    [Test]
    public void Configure_TemplateJsonMissing_WarnsAndDoesNothing()
    {
        _file.Exists(TemplateJsonPath).Returns(false);

        Assert.DoesNotThrow(() => MergeScriptTokenConfiguratorImpl.GetFromFactory().Configure(MakeContext()));

        _file.DidNotReceiveWithAnyArgs().WriteAllText(default, default);
        Assert.That(_warnings, Has.Some.Matches<string>(w => w.ContainsIgnoringCase("Template.json not found")));
    }
}
