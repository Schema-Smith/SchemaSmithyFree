// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Highlighting;

namespace SchemaHammer.UnitTests;

public class TokenHighlightRendererTests
{
    [Test]
    public void TokenPattern_MatchesSingleToken()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("SELECT {{MainDB}}.dbo.MyTable");
        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Value, Is.EqualTo("{{MainDB}}"));
    }

    [Test]
    public void TokenPattern_MatchesMultipleTokens()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("USE [{{MainDB}}]; SELECT {{SchemaName}}.{{TableName}}");
        Assert.That(matches, Has.Count.EqualTo(3));
    }

    [Test]
    public void TokenPattern_EmptyText_ReturnsNone()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("");
        Assert.That(matches, Has.Count.EqualTo(0));
    }

    [Test]
    public void TokenPattern_NoTokens_ReturnsNone()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("SELECT * FROM dbo.MyTable WHERE Id = 1");
        Assert.That(matches, Has.Count.EqualTo(0));
    }

    [Test]
    public void TokenPattern_SingleBraces_DoesNotMatch()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("SELECT {NotAToken} FROM table");
        Assert.That(matches, Has.Count.EqualTo(0));
    }

    [Test]
    public void TokenPattern_EmptyBraces_DoesNotMatch()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("SELECT {{}} FROM table");
        Assert.That(matches, Has.Count.EqualTo(0));
    }

    [Test]
    public void TokenPattern_TokenWithSpecialChars_Matches()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("{{My-Token_123}}");
        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Value, Is.EqualTo("{{My-Token_123}}"));
    }

    [Test]
    public void TokenPattern_TokenWithSpaces_DoesNotMatch()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("{{Not A Token}}");
        Assert.That(matches, Has.Count.EqualTo(0));
    }

    [Test]
    public void TokenPattern_AdjacentTokens_MatchesBoth()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("{{A}}{{B}}");
        Assert.That(matches, Has.Count.EqualTo(2));
    }

    [Test]
    public void TokenPattern_TokenInsideSqlString_Matches()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("WHERE Name = '{{DefaultName}}'");
        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Value, Is.EqualTo("{{DefaultName}}"));
    }

    [Test]
    public void TokenPattern_TokenInsideComment_Matches()
    {
        var regex = TokenHighlightRenderer.TokenPattern();
        var matches = regex.Matches("-- This uses {{MainDB}}");
        Assert.That(matches, Has.Count.EqualTo(1));
    }
}
