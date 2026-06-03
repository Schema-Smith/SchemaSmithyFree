// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class IdentifierTests
{
    [Test]
    public void Unwrap_SqlServer_StripsBracketDelimiters()
    {
        Assert.That(Identifier.Unwrap("[dbo]", Platform.SqlServer), Is.EqualTo("dbo"));
    }

    [Test]
    public void Unwrap_SqlServer_UnescapesDoubledClosingBrackets()
    {
        Assert.That(Identifier.Unwrap("[weird]]name]", Platform.SqlServer), Is.EqualTo("weird]name"));
    }

    [Test]
    public void Unwrap_SqlServer_LeavesUndelimitedAsIs()
    {
        Assert.That(Identifier.Unwrap("dbo", Platform.SqlServer), Is.EqualTo("dbo"));
    }

    [Test]
    public void Unwrap_SqlServer_LeavesUnmatchedDelimitersAsIs()
    {
        Assert.That(Identifier.Unwrap("[dbo", Platform.SqlServer), Is.EqualTo("[dbo"));
        Assert.That(Identifier.Unwrap("dbo]", Platform.SqlServer), Is.EqualTo("dbo]"));
    }

    [Test]
    public void Unwrap_PostgreSQL_StripsDoubleQuoteDelimiters()
    {
        Assert.That(Identifier.Unwrap("\"public\"", Platform.PostgreSQL), Is.EqualTo("public"));
    }

    [Test]
    public void Unwrap_PostgreSQL_UnescapesDoubledQuotes()
    {
        Assert.That(Identifier.Unwrap("\"weird\"\"name\"", Platform.PostgreSQL), Is.EqualTo("weird\"name"));
    }

    [Test]
    public void Unwrap_PostgreSQL_LeavesUndelimitedAsIs()
    {
        Assert.That(Identifier.Unwrap("public", Platform.PostgreSQL), Is.EqualTo("public"));
    }

    [Test]
    public void Unwrap_MySQL_DelegatesToBacktickUnquote()
    {
        Assert.That(Identifier.Unwrap("`my_db`", Platform.MySQL), Is.EqualTo("my_db"));
        Assert.That(Identifier.Unwrap("`my``db`", Platform.MySQL), Is.EqualTo("my`db"));
    }

    [Test]
    public void Unwrap_HandlesNullOrEmpty()
    {
        Assert.That(Identifier.Unwrap(null, Platform.SqlServer), Is.Null);
        Assert.That(Identifier.Unwrap("", Platform.PostgreSQL), Is.EqualTo(""));
    }
}
