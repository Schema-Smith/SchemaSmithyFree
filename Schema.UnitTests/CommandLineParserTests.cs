// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.UnitTests;

public class CommandLineParserTests
{
    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    private static IEnvironment MockCommandLine(string commandLine)
    {
        var env = Substitute.For<IEnvironment>();
        env.CommandLine.Returns(commandLine);
        FactoryContainer.Register<IEnvironment>(env);
        return env;
    }

    // ContainsSwitch — switch definitely absent from test runner command line
    [Test]
    public void ContainsSwitchReturnsFalseForAbsentSwitch()
    {
        Assert.That(CommandLineParser.ContainsSwitch("xyzzy-not-a-real-switch"), Is.False);
    }

    [Test]
    public void ContainsSwitchIsCaseInsensitive()
    {
        // Both casing lookups on the same absent switch should agree
        Assert.That(CommandLineParser.ContainsSwitch("XYZZY-UPPER"), Is.False);
        Assert.That(CommandLineParser.ContainsSwitch("xyzzy-upper"), Is.False);
    }

    [Test]
    public void ContainsSwitchReturnsTrueWhenSwitchPresent()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            MockCommandLine("myapp.exe --foo --bar:value");
            Assert.That(CommandLineParser.ContainsSwitch("foo"), Is.True);
        }
    }

    [Test]
    public void ContainsSwitchIsCaseInsensitiveForPresentSwitch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            MockCommandLine("myapp.exe --FOO");
            Assert.That(CommandLineParser.ContainsSwitch("foo"), Is.True);
            Assert.That(CommandLineParser.ContainsSwitch("FOO"), Is.True);
        }
    }

    // ValueOfSwitch — returns supplied default when switch is absent
    [Test]
    public void ValueOfSwitchReturnsEmptyStringDefaultWhenSwitchAbsent()
    {
        var result = CommandLineParser.ValueOfSwitch("xyzzy-missing");
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ValueOfSwitchReturnsSuppliedDefaultWhenSwitchAbsent()
    {
        var result = CommandLineParser.ValueOfSwitch("xyzzy-missing", "fallback");
        Assert.That(result, Is.EqualTo("fallback"));
    }

    [Test]
    public void ValueOfSwitchExtractsColonDelimitedValue()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            MockCommandLine("myapp.exe --LogPath:C:\\logs");
            Assert.That(CommandLineParser.ValueOfSwitch("LogPath"), Is.EqualTo("C:\\logs"));
        }
    }

    [Test]
    public void ValueOfSwitchExtractsEqualsDelimitedValue()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            MockCommandLine("myapp.exe --ConfigFile=appsettings.json");
            Assert.That(CommandLineParser.ValueOfSwitch("ConfigFile"), Is.EqualTo("appsettings.json"));
        }
    }

    [Test]
    public void ValueOfSwitchPreservesEmbeddedColonInValue()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            // Only the first colon is the delimiter; the rest belongs to the value
            MockCommandLine("myapp.exe --LogPath:C:\\some:path");
            Assert.That(CommandLineParser.ValueOfSwitch("LogPath"), Is.EqualTo("C:\\some:path"));
        }
    }

    // IntValueOfSwitch — returns default when switch absent
    [Test]
    public void IntValueOfSwitchReturnsNegativeOneDefaultWhenSwitchAbsent()
    {
        var result = CommandLineParser.IntValueOfSwitch("xyzzy-missing");
        Assert.That(result, Is.EqualTo(-1));
    }

    [Test]
    public void IntValueOfSwitchReturnsSuppliedDefaultWhenSwitchAbsent()
    {
        var result = CommandLineParser.IntValueOfSwitch("xyzzy-missing", 42);
        Assert.That(result, Is.EqualTo(42));
    }

    // SwitchesAndValues — strip leading dashes and slashes from keys
    [Test]
    public void SwitchesAndValuesDoesNotContainLeadingDashes()
    {
        // Any switches the test runner does pass will have had their leading dashes stripped
        foreach (var key in CommandLineParser.SwitchesAndValues.Keys)
        {
            Assert.That(key, Does.Not.StartWith("-"));
            Assert.That(key, Does.Not.StartWith("/"));
        }
    }

    // Arguments is a list
    [Test]
    public void ArgumentsIsAList()
    {
        var args = CommandLineParser.Arguments;
        Assert.That(args, Is.Not.Null);
        Assert.That(args, Is.InstanceOf<System.Collections.Generic.List<string>>());
    }

    // SwitchesAndValues dictionary — independent of argument order checks
    [Test]
    public void SwitchesAndValuesReturnsDictionary()
    {
        var switches = CommandLineParser.SwitchesAndValues;
        Assert.That(switches, Is.Not.Null);
        Assert.That(switches, Is.InstanceOf<System.Collections.Generic.Dictionary<string, string>>());
    }

    // HandleCommonSwitches — does not call Exit when version/help switches are absent
    [Test]
    public void HandleCommonSwitchesDoesNotCallExitWhenNoVersionOrHelpSwitch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var env = MockCommandLine("myapp.exe --unrelated");

            Assert.That(() => CommandLineParser.HandleCommonSwitches("TestApp"), Throws.Nothing);
            env.DidNotReceive().Exit(Arg.Any<int>());
        }
    }

    [Test]
    public void HandleCommonSwitchesCallsExitForVersionSwitch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var env = MockCommandLine("myapp.exe --version");
            CommandLineParser.HandleCommonSwitches("TestApp");
            env.Received(1).Exit(0);
        }
    }

    [Test]
    public void HandleCommonSwitchesCallsExitForVerSwitch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var env = MockCommandLine("myapp.exe --ver");
            CommandLineParser.HandleCommonSwitches("TestApp");
            env.Received(1).Exit(0);
        }
    }

    [Test]
    public void HandleCommonSwitchesCallsExitForVSwitch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var env = MockCommandLine("myapp.exe --v");
            CommandLineParser.HandleCommonSwitches("TestApp");
            env.Received(1).Exit(0);
        }
    }

    [Test]
    public void HandleCommonSwitchesCallsExitForHelpSwitch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var env = MockCommandLine("myapp.exe --help");
            CommandLineParser.HandleCommonSwitches("TestApp");
            env.Received(1).Exit(0);
        }
    }

    [Test]
    public void HandleCommonSwitchesCallsExitForHSwitch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var env = MockCommandLine("myapp.exe --h");
            CommandLineParser.HandleCommonSwitches("TestApp");
            env.Received(1).Exit(0);
        }
    }

    [Test]
    public void ShowHelpAndExitOutputsExpectedHelpText()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var env = MockCommandLine("myapp.exe --help");

            var originalOut = Console.Out;
            var output = new StringWriter();
            Console.SetOut(output);
            try
            {
                CommandLineParser.HandleCommonSwitches("myapp");
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            var text = output.ToString();
            Assert.That(text, Does.Contain("myapp.exe"));
            Assert.That(text, Does.Contain("--version"));
            Assert.That(text, Does.Contain("--help"));
            Assert.That(text, Does.Contain("--LogPath"));
            Assert.That(text, Does.Contain("--ConfigFile"));
        }
    }
}
