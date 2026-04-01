// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Utility;

namespace Schema.UnitTests;

[TestFixture]
public class FileNameEncoderTests
{
    // --- Passthrough tests ---

    [Test]
    public void Encode_PlainName_Unchanged()
    {
        Assert.That(FileNameEncoder.Encode("MyTable"), Is.EqualTo("MyTable"));
    }

    [Test]
    public void Encode_InteriorSpaces_Unchanged()
    {
        Assert.That(FileNameEncoder.Encode("My Table Name"), Is.EqualTo("My Table Name"));
    }

    [Test]
    public void Encode_InteriorDots_Unchanged()
    {
        Assert.That(FileNameEncoder.Encode("dbo.MyTable"), Is.EqualTo("dbo.MyTable"));
    }

    [Test]
    public void Encode_EmptyString_ReturnsEmpty()
    {
        Assert.That(FileNameEncoder.Encode(""), Is.EqualTo(""));
    }

    [Test]
    public void Encode_Null_ReturnsNull()
    {
        Assert.That(FileNameEncoder.Encode(null), Is.Null);
    }

    [Test]
    public void Encode_UnicodeCharacters_Unchanged()
    {
        Assert.That(FileNameEncoder.Encode("Schéma_Täble"), Is.EqualTo("Schéma_Täble"));
    }

    // --- Filesystem-illegal character tests ---

    [TestCase('\\', "%5C")]
    [TestCase('/', "%2F")]
    [TestCase(':', "%3A")]
    [TestCase('*', "%2A")]
    [TestCase('?', "%3F")]
    [TestCase('"', "%22")]
    [TestCase('<', "%3C")]
    [TestCase('>', "%3E")]
    [TestCase('|', "%7C")]
    public void Encode_IllegalChar_IsEncoded(char illegal, string expected)
    {
        Assert.That(FileNameEncoder.Encode($"foo{illegal}bar"), Is.EqualTo($"foo{expected}bar"));
    }

    [Test]
    public void Encode_MultipleIllegalChars_AllEncoded()
    {
        Assert.That(FileNameEncoder.Encode("a:b*c?d"), Is.EqualTo("a%3Ab%2Ac%3Fd"));
    }

    // --- Escape character tests ---

    [Test]
    public void Encode_PercentSign_EncodedAsPercent25()
    {
        Assert.That(FileNameEncoder.Encode("100%done"), Is.EqualTo("100%25done"));
    }

    [Test]
    public void Encode_PreEncodedLookingName_DoubleEncodes()
    {
        Assert.That(FileNameEncoder.Encode("foo%3Abar"), Is.EqualTo("foo%253Abar"));
    }

    // --- Leading/trailing tests ---

    [Test]
    public void Encode_LeadingSpace_Encoded()
    {
        Assert.That(FileNameEncoder.Encode(" foo"), Is.EqualTo("%20foo"));
    }

    [Test]
    public void Encode_TrailingSpace_Encoded()
    {
        Assert.That(FileNameEncoder.Encode("foo "), Is.EqualTo("foo%20"));
    }

    [Test]
    public void Encode_LeadingDot_Encoded()
    {
        Assert.That(FileNameEncoder.Encode(".hidden"), Is.EqualTo("%2Ehidden"));
    }

    [Test]
    public void Encode_TrailingDot_Encoded()
    {
        Assert.That(FileNameEncoder.Encode("foo."), Is.EqualTo("foo%2E"));
    }

    [Test]
    public void Encode_MultipleLeadingSpaces_AllEncoded()
    {
        Assert.That(FileNameEncoder.Encode("   foo"), Is.EqualTo("%20%20%20foo"));
    }

    [Test]
    public void Encode_MultipleTrailingDots_AllEncoded()
    {
        Assert.That(FileNameEncoder.Encode("foo..."), Is.EqualTo("foo%2E%2E%2E"));
    }

    [Test]
    public void Encode_OnlySpaces_AllEncoded()
    {
        Assert.That(FileNameEncoder.Encode("   "), Is.EqualTo("%20%20%20"));
    }

    // --- Windows reserved name tests ---

    [TestCase("CON")]
    [TestCase("PRN")]
    [TestCase("AUX")]
    [TestCase("NUL")]
    [TestCase("COM1")]
    [TestCase("COM2")]
    [TestCase("COM3")]
    [TestCase("COM4")]
    [TestCase("COM5")]
    [TestCase("COM6")]
    [TestCase("COM7")]
    [TestCase("COM8")]
    [TestCase("COM9")]
    [TestCase("LPT1")]
    [TestCase("LPT2")]
    [TestCase("LPT3")]
    [TestCase("LPT4")]
    [TestCase("LPT5")]
    [TestCase("LPT6")]
    [TestCase("LPT7")]
    [TestCase("LPT8")]
    [TestCase("LPT9")]
    public void Encode_WindowsReservedName_FirstCharEncoded(string reserved)
    {
        var result = FileNameEncoder.Encode(reserved);
        Assert.That(result, Is.Not.EqualTo(reserved));
        Assert.That(result, Does.StartWith("%"));
        Assert.That(FileNameEncoder.Decode(result), Is.EqualTo(reserved));
    }

    [TestCase("con")]
    [TestCase("Con")]
    [TestCase("cOn")]
    public void Encode_WindowsReservedName_CaseInsensitive(string reserved)
    {
        var result = FileNameEncoder.Encode(reserved);
        Assert.That(result, Is.Not.EqualTo(reserved));
        Assert.That(FileNameEncoder.Decode(result), Is.EqualTo(reserved));
    }

    [Test]
    public void Encode_NonReservedSuperstring_NotEncoded()
    {
        Assert.That(FileNameEncoder.Encode("CONSOLE"), Is.EqualTo("CONSOLE"));
    }

    [Test]
    public void Encode_COM10_NotEncoded()
    {
        Assert.That(FileNameEncoder.Encode("COM10"), Is.EqualTo("COM10"));
    }

    // --- Decode tests ---

    [Test]
    public void Decode_PlainName_Unchanged()
    {
        Assert.That(FileNameEncoder.Decode("MyTable"), Is.EqualTo("MyTable"));
    }

    [Test]
    public void Decode_EmptyString_ReturnsEmpty()
    {
        Assert.That(FileNameEncoder.Decode(""), Is.EqualTo(""));
    }

    [Test]
    public void Decode_Null_ReturnsNull()
    {
        Assert.That(FileNameEncoder.Decode(null), Is.Null);
    }

    [TestCase("%5C", "\\")]
    [TestCase("%2F", "/")]
    [TestCase("%3A", ":")]
    [TestCase("%2A", "*")]
    [TestCase("%3F", "?")]
    [TestCase("%22", "\"")]
    [TestCase("%3C", "<")]
    [TestCase("%3E", ">")]
    [TestCase("%7C", "|")]
    [TestCase("%20", " ")]
    [TestCase("%2E", ".")]
    public void Decode_EncodedChar_DecodesCorrectly(string encoded, string expected)
    {
        Assert.That(FileNameEncoder.Decode($"foo{encoded}bar"), Is.EqualTo($"foo{expected}bar"));
    }

    [Test]
    public void Decode_Percent25_DecodesToPercent()
    {
        Assert.That(FileNameEncoder.Decode("100%25done"), Is.EqualTo("100%done"));
    }

    [Test]
    public void Decode_CaseInsensitiveHex()
    {
        Assert.That(FileNameEncoder.Decode("foo%3abar"), Is.EqualTo("foo:bar"));
    }

    [Test]
    public void Decode_IncompleteSequence_LeftAsIs()
    {
        Assert.That(FileNameEncoder.Decode("foo%2"), Is.EqualTo("foo%2"));
    }

    [Test]
    public void Decode_InvalidHexSequence_LeftAsIs()
    {
        Assert.That(FileNameEncoder.Decode("foo%ZZbar"), Is.EqualTo("foo%ZZbar"));
    }

    // --- Round-trip tests ---

    [TestCase("PlainName")]
    [TestCase("foo\\bar")]
    [TestCase("foo/bar")]
    [TestCase("foo:bar")]
    [TestCase("foo*bar")]
    [TestCase("foo?bar")]
    [TestCase("foo\"bar")]
    [TestCase("foo<bar")]
    [TestCase("foo>bar")]
    [TestCase("foo|bar")]
    [TestCase("a:b*c?d")]
    [TestCase("100%done")]
    [TestCase("foo%3Abar")]
    [TestCase(" leading")]
    [TestCase("trailing ")]
    [TestCase(".hidden")]
    [TestCase("trailing.")]
    [TestCase("CON")]
    [TestCase("NUL")]
    [TestCase("LPT1")]
    [TestCase("Schéma_Täble")]
    [TestCase("   ")]
    public void RoundTrip_DecodeEncode_ReturnsOriginal(string original)
    {
        Assert.That(FileNameEncoder.Decode(FileNameEncoder.Encode(original)), Is.EqualTo(original));
    }

    [Test]
    public void RoundTrip_AllIllegalCharsCombined()
    {
        var allIllegal = "a\\b/c:d*e?f\"g<h>i|j";
        Assert.That(FileNameEncoder.Decode(FileNameEncoder.Encode(allIllegal)), Is.EqualTo(allIllegal));
    }

    [Test]
    public void RoundTrip_AllSpaces()
    {
        var spaces = "   ";
        Assert.That(FileNameEncoder.Decode(FileNameEncoder.Encode(spaces)), Is.EqualTo(spaces));
    }
}
