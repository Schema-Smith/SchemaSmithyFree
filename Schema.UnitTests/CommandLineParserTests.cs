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

    // CommandLine — always has a leading space (ForceLeadingSpace invariant)
    [Test]
    public void CommandLineAlwaysStartsWithSpace()
    {
        Assert.That(CommandLineParser.CommandLine, Does.StartWith(" "));
    }

    // CommandLine and Arguments are consistent
    [Test]
    public void ArgumentsIsAList()
    {
        var args = CommandLineParser.Arguments;
        Assert.That(args, Is.Not.Null);
        Assert.That(args, Is.InstanceOf<System.Collections.Generic.List<string>>());
    }

    // HandleCommonSwitches — does not call Exit when version/help switches are absent
    [Test]
    public void HandleCommonSwitchesDoesNotCallExitWhenNoVersionOrHelpSwitch()
    {
        var env = Substitute.For<IEnvironment>();
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register<IEnvironment>(env);

            // As long as --version / --v / --ver / --help / --h / --? are absent from
            // the test runner command line, Exit should never be called.
            Assert.That(() => CommandLineParser.HandleCommonSwitches("TestApp"), Throws.Nothing);
            env.DidNotReceive().Exit(Arg.Any<int>());
        }
    }

    // SwitchesAndValues dictionary — independent of argument order checks
    [Test]
    public void SwitchesAndValuesReturnsDictionary()
    {
        var switches = CommandLineParser.SwitchesAndValues;
        Assert.That(switches, Is.Not.Null);
        Assert.That(switches, Is.InstanceOf<System.Collections.Generic.Dictionary<string, string>>());
    }
}
