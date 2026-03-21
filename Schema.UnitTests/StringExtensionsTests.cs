using Schema.Utility;
using System.IO;

namespace Schema.UnitTests;

public class StringExtensionsTests
{
    // ContainsIgnoringCase
    [Test]
    public void ContainsIgnoringCaseReturnsTrueForMatchingCase()
    {
        Assert.That("HelloWorld".ContainsIgnoringCase("world"), Is.True);
    }

    [Test]
    public void ContainsIgnoringCaseReturnsFalseWhenNotFound()
    {
        Assert.That("HelloWorld".ContainsIgnoringCase("xyz"), Is.False);
    }

    // EndsWithIgnoringCase
    [Test]
    public void EndsWithIgnoringCaseReturnsTrueForMatchingSuffix()
    {
        Assert.That("Schema.sql".EndsWithIgnoringCase(".SQL"), Is.True);
    }

    [Test]
    public void EndsWithIgnoringCaseReturnsFalseWhenSuffixDoesNotMatch()
    {
        Assert.That("Schema.sql".EndsWithIgnoringCase(".json"), Is.False);
    }

    // EqualsIgnoringCase
    [Test]
    public void EqualsIgnoringCaseReturnsTrueForSameStringDifferentCase()
    {
        Assert.That("MSSQL".EqualsIgnoringCase("mssql"), Is.True);
    }

    [Test]
    public void EqualsIgnoringCaseReturnsFalseForDifferentString()
    {
        Assert.That("MSSQL".EqualsIgnoringCase("PostgreSQL"), Is.False);
    }

    // IndexOfIgnoringCase (no startIndex)
    [Test]
    public void IndexOfIgnoringCaseReturnsCorrectIndex()
    {
        Assert.That("HelloWorld".IndexOfIgnoringCase("WORLD"), Is.EqualTo(5));
    }

    [Test]
    public void IndexOfIgnoringCaseReturnsNegativeOneWhenNotFound()
    {
        Assert.That("HelloWorld".IndexOfIgnoringCase("xyz"), Is.EqualTo(-1));
    }

    // IndexOfIgnoringCase (with startIndex) — covers the overload
    [Test]
    public void IndexOfIgnoringCaseWithStartIndexFindsAfterOffset()
    {
        Assert.That("abcABC".IndexOfIgnoringCase("abc", 1), Is.EqualTo(3));
    }

    [Test]
    public void IndexOfIgnoringCaseWithStartIndexReturnsNegativeOneWhenNotFound()
    {
        Assert.That("abc".IndexOfIgnoringCase("xyz", 1), Is.EqualTo(-1));
    }

    // StartsWithIgnoringCase
    [Test]
    public void StartsWithIgnoringCaseReturnsTrueForMatchingPrefix()
    {
        Assert.That("SchemaQuench".StartsWithIgnoringCase("schema"), Is.True);
    }

    [Test]
    public void StartsWithIgnoringCaseReturnsFalseWhenPrefixDoesNotMatch()
    {
        Assert.That("SchemaQuench".StartsWithIgnoringCase("quench"), Is.False);
    }

    // ReplaceIgnoringCase
    [Test]
    public void ReplaceIgnoringCaseReplacesAllOccurrences()
    {
        var result = "Hello hello HELLO".ReplaceIgnoringCase("hello", "Hi");
        Assert.That(result, Is.EqualTo("Hi Hi Hi"));
    }

    // Unquote — the main coverage target
    [Test]
    public void UnquoteRemovesDoubleQuotesWhenBothPresent()
    {
        Assert.That("\"value\"".Unquote(), Is.EqualTo("value"));
    }

    [Test]
    public void UnquoteReturnsOriginalValueWhenOnlyLeadingQuote()
    {
        // Starts with " but does NOT end with " — returns the original value (not trimmed)
        // This is the uncovered branch: result.StartsWith("\"") && result.EndsWith("\"") is false
        var input = "\"noClosingQuote";
        Assert.That(input.Unquote(), Is.EqualTo(input));
    }

    [Test]
    public void UnquoteReturnsOriginalValueWhenOnlyTrailingQuote()
    {
        // Ends with " but does NOT start with " — condition is false, returns value
        var input = "noOpeningQuote\"";
        Assert.That(input.Unquote(), Is.EqualTo(input));
    }

    [Test]
    public void UnquoteReturnsUnchangedStringWhenNoQuotes()
    {
        Assert.That("plainvalue".Unquote(), Is.EqualTo("plainvalue"));
    }

    [Test]
    public void UnquoteTrimsWhitespaceBeforeCheckingQuotes()
    {
        // With surrounding spaces — Trim() is called first; if trimmed result has both quotes, strips them
        Assert.That("  \"value\"  ".Unquote(), Is.EqualTo("value"));
    }

    [Test]
    public void UnquoteReturnsOriginalWhenSpacedAndOnlyLeadingQuote()
    {
        // Trim yields "\"noclose" — starts with " but doesn't end with " — returns original (with spaces)
        var input = "  \"noclose  ";
        Assert.That(input.Unquote(), Is.EqualTo(input));
    }

    // ToStream
    [Test]
    public void ToStreamCreatesReadableStreamWithCorrectContent()
    {
        const string content = "SELECT 1";
        using var stream = content.ToStream();
        using var reader = new StreamReader(stream);
        Assert.That(reader.ReadToEnd(), Is.EqualTo(content));
    }

    [Test]
    public void ToStreamPositionIsAtStart()
    {
        const string content = "test";
        using var stream = content.ToStream();
        Assert.That(stream.Position, Is.EqualTo(0));
    }
}
