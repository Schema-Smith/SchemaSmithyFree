// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaQuench.UnitTests;

[TestFixture]
public class VerboseLoggingTests
{
    [Test]
    public void ShouldLogInfoMessage_State100_AlwaysTrue()
    {
        Assert.That(DatabaseQuencher.ShouldLogInfoMessage(state: 100, verboseLogging: false), Is.True);
        Assert.That(DatabaseQuencher.ShouldLogInfoMessage(state: 100, verboseLogging: true), Is.True);
    }

    [Test]
    public void ShouldLogInfoMessage_NonState100_VerboseTrue_ReturnsTrue()
    {
        Assert.That(DatabaseQuencher.ShouldLogInfoMessage(state: 1, verboseLogging: true), Is.True);
    }

    [Test]
    public void ShouldLogInfoMessage_NonState100_VerboseFalse_ReturnsFalse()
    {
        Assert.That(DatabaseQuencher.ShouldLogInfoMessage(state: 1, verboseLogging: false), Is.False);
    }

    [Test]
    public void ShouldLogInfoMessage_State0_VerboseFalse_ReturnsFalse()
    {
        Assert.That(DatabaseQuencher.ShouldLogInfoMessage(state: 0, verboseLogging: false), Is.False);
    }
}
