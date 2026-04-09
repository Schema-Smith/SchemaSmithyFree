// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NSubstitute;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class CommandLineParserTests
{
    private IEnvironment _mockEnvironment;

    [SetUp]
    public void SetUp()
    {
        _mockEnvironment = Substitute.For<IEnvironment>();
        FactoryContainer.Register<IEnvironment>(_mockEnvironment);
    }

    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    [Test]
    public void Arguments_ParsesSimpleArguments()
    {
        _mockEnvironment.CommandLine.Returns("app.exe arg1 arg2 arg3");

        var args = CommandLineParser.Arguments;

        Assert.That(args, Has.Count.EqualTo(4));
        Assert.That(args[0], Is.EqualTo("app.exe"));
        Assert.That(args[1], Is.EqualTo("arg1"));
        Assert.That(args[2], Is.EqualTo("arg2"));
        Assert.That(args[3], Is.EqualTo("arg3"));
    }

    [Test]
    public void Arguments_ParsesQuotedArguments()
    {
        _mockEnvironment.CommandLine.Returns("app.exe \"arg with spaces\" arg2");

        var args = CommandLineParser.Arguments;

        Assert.That(args, Has.Count.EqualTo(3));
        Assert.That(args[0], Is.EqualTo("app.exe"));
        Assert.That(args[1], Is.EqualTo("arg with spaces"));
        Assert.That(args[2], Is.EqualTo("arg2"));
    }

    [Test]
    public void Arguments_HandlesEmptyCommandLine()
    {
        _mockEnvironment.CommandLine.Returns("app.exe");

        var args = CommandLineParser.Arguments;

        Assert.That(args, Has.Count.EqualTo(1));
        Assert.That(args[0], Is.EqualTo("app.exe"));
    }

    [Test]
    public void Arguments_HandlesMultipleSpaces()
    {
        _mockEnvironment.CommandLine.Returns("app.exe   arg1   arg2");

        var args = CommandLineParser.Arguments;

        Assert.That(args, Has.Count.EqualTo(3));
        Assert.That(args[1], Is.EqualTo("arg1"));
        Assert.That(args[2], Is.EqualTo("arg2"));
    }

    [Test]
    public void SwitchesAndValues_ParsesDashSwitchWithColonValue()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --ConfigFile:settings.json");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches.ContainsKey("ConfigFile"), Is.True);
        Assert.That(switches["ConfigFile"], Is.EqualTo("settings.json"));
    }

    [Test]
    public void SwitchesAndValues_ParsesDashSwitchWithEqualsValue()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --ConfigFile=settings.json");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches.ContainsKey("ConfigFile"), Is.True);
        Assert.That(switches["ConfigFile"], Is.EqualTo("settings.json"));
    }

    [Test]
    public void SwitchesAndValues_ParsesSlashSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe /verbose");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches.ContainsKey("verbose"), Is.True);
        Assert.That(switches["verbose"], Is.EqualTo(string.Empty));
    }

    [Test]
    public void SwitchesAndValues_ParsesSingleDashSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe -v");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches.ContainsKey("v"), Is.True);
    }

    [Test]
    public void SwitchesAndValues_HandlesValueWithEmbeddedColon()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --Server:localhost:1433");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches["Server"], Is.EqualTo("localhost:1433"));
    }

    [Test]
    public void SwitchesAndValues_IsCaseInsensitive()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --CONFIGFILE:test.json");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches.ContainsKey("configfile"), Is.True);
        Assert.That(switches.ContainsKey("CONFIGFILE"), Is.True);
        Assert.That(switches.ContainsKey("ConfigFile"), Is.True);
    }

    [Test]
    public void SwitchesAndValues_IgnoresNonSwitchArguments()
    {
        _mockEnvironment.CommandLine.Returns("app.exe arg1 --switch:value arg2");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches, Has.Count.EqualTo(1));
        Assert.That(switches.ContainsKey("switch"), Is.True);
    }

    [Test]
    public void ContainsSwitch_ReturnsTrueForExistingSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --verbose");

        Assert.That(CommandLineParser.ContainsSwitch("verbose"), Is.True);
    }

    [Test]
    public void ContainsSwitch_ReturnsFalseForMissingSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --verbose");

        Assert.That(CommandLineParser.ContainsSwitch("debug"), Is.False);
    }

    [Test]
    public void ValueOfSwitch_ReturnsValueForExistingSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --LogPath:c:\\logs");

        Assert.That(CommandLineParser.ValueOfSwitch("LogPath"), Is.EqualTo("c:\\logs"));
    }

    [Test]
    public void ValueOfSwitch_ReturnsDefaultForMissingSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe");

        Assert.That(CommandLineParser.ValueOfSwitch("LogPath", "default"), Is.EqualTo("default"));
    }

    [Test]
    public void ValueOfSwitch_ReturnsEmptyStringDefaultWhenNoDefault()
    {
        _mockEnvironment.CommandLine.Returns("app.exe");

        Assert.That(CommandLineParser.ValueOfSwitch("LogPath"), Is.EqualTo(string.Empty));
    }

    [Test]
    public void ValueOfSwitch_TrimsQuotesFromValue()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --LogPath:\"c:\\my logs\"");

        Assert.That(CommandLineParser.ValueOfSwitch("LogPath"), Is.EqualTo("c:\\my logs"));
    }

    [Test]
    public void IntValueOfSwitch_ReturnsIntValue()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --MaxTasks:5");

        Assert.That(CommandLineParser.IntValueOfSwitch("MaxTasks"), Is.EqualTo(5));
    }

    [Test]
    public void IntValueOfSwitch_ReturnsDefaultForNonNumeric()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --MaxTasks:abc");

        Assert.That(CommandLineParser.IntValueOfSwitch("MaxTasks", 10), Is.EqualTo(10));
    }

    [Test]
    public void IntValueOfSwitch_ReturnsNegativeOneDefaultWhenNoDefault()
    {
        _mockEnvironment.CommandLine.Returns("app.exe");

        Assert.That(CommandLineParser.IntValueOfSwitch("MaxTasks"), Is.EqualTo(-1));
    }

    [Test]
    public void HandleCommonSwitches_CallsExitOnVersionSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --version");

        CommandLineParser.HandleCommonSwitches("TestApp");

        _mockEnvironment.Received().Exit(0);
    }

    [Test]
    public void HandleCommonSwitches_CallsExitOnHelpSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --help");

        CommandLineParser.HandleCommonSwitches("TestApp");

        _mockEnvironment.Received().Exit(0);
    }

    [Test]
    public void HandleCommonSwitches_CallsExitOnQuestionMarkSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe -?");

        CommandLineParser.HandleCommonSwitches("TestApp");

        _mockEnvironment.Received().Exit(0);
    }

    [Test]
    public void HandleCommonSwitches_CallsExitOnVSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe -v");

        CommandLineParser.HandleCommonSwitches("TestApp");

        _mockEnvironment.Received().Exit(0);
    }

    [Test]
    public void HandleCommonSwitches_CallsExitOnHSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe -h");

        CommandLineParser.HandleCommonSwitches("TestApp");

        _mockEnvironment.Received().Exit(0);
    }

    [Test]
    public void HandleCommonSwitches_DoesNotExitWithoutSwitch()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --other");

        CommandLineParser.HandleCommonSwitches("TestApp");

        _mockEnvironment.DidNotReceive().Exit(Arg.Any<int>());
    }

    [Test]
    public void HandleCommonSwitches_InvokesToolSpecificSwitchesOnHelp()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --help");
        var invoked = false;

        CommandLineParser.HandleCommonSwitches("TestApp", () => invoked = true);

        Assert.That(invoked, Is.True);
    }

    [Test]
    public void SwitchesAndValues_ParsesMultipleSwitches()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --ConfigFile:test.json --LogPath:c:\\logs --verbose");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches, Has.Count.EqualTo(3));
        Assert.That(switches["ConfigFile"], Is.EqualTo("test.json"));
        Assert.That(switches["LogPath"], Is.EqualTo("c:\\logs"));
        Assert.That(switches["verbose"], Is.EqualTo(string.Empty));
    }

    [Test]
    public void SwitchesAndValues_HandlesDoubleDash()
    {
        _mockEnvironment.CommandLine.Returns("app.exe --key:value");

        var switches = CommandLineParser.SwitchesAndValues;

        Assert.That(switches.ContainsKey("key"), Is.True);
        Assert.That(switches["key"], Is.EqualTo("value"));
    }
}
