// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class StringHelperTests
{
    [Test]
    public void StripBrackets_RemovesBrackets()
    {
        Assert.That(StringHelper.StripBrackets("[dbo]"), Is.EqualTo("dbo"));
    }

    [Test]
    public void StripBrackets_HandlesNoBrackets()
    {
        Assert.That(StringHelper.StripBrackets("dbo"), Is.EqualTo("dbo"));
    }

    [Test]
    public void StripBrackets_HandlesNull()
    {
        Assert.That(StringHelper.StripBrackets(null), Is.EqualTo(""));
    }

    [Test]
    public void StripBrackets_HandlesEmpty()
    {
        Assert.That(StringHelper.StripBrackets(""), Is.EqualTo(""));
    }

    [Test]
    public void StripBrackets_HandlesNestedBrackets()
    {
        Assert.That(StringHelper.StripBrackets("[My [Table]]"), Is.EqualTo("My Table"));
    }

    [Test]
    public void StripBrackets_MatchesCaseInsensitive()
    {
        Assert.That(
            StringHelper.StripBrackets("[Users]").Equals(StringHelper.StripBrackets("users"), StringComparison.OrdinalIgnoreCase),
            Is.True);
    }
}
