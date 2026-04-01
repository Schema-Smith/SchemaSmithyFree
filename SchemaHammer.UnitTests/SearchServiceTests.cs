// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Services;

namespace SchemaHammer.UnitTests;

public class SearchServiceTests
{
    private readonly SearchService _service = new();

    [Test]
    public void FindNext_FindsMatch_ReturnsPosition()
    {
        var result = _service.FindNext("hello world", "world", 0, false);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Start, Is.EqualTo(6));
        Assert.That(result.Value.Length, Is.EqualTo(5));
    }

    [Test]
    public void FindNext_NoMatch_ReturnsNull()
    {
        var result = _service.FindNext("hello world", "xyz", 0, false);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindNext_CaseInsensitive_FindsMatch()
    {
        var result = _service.FindNext("Hello World", "hello", 0, false);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Start, Is.EqualTo(0));
    }

    [Test]
    public void FindNext_CaseSensitive_NoMatch()
    {
        var result = _service.FindNext("Hello World", "hello", 0, true);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindNext_FromOffset_FindsNextOccurrence()
    {
        var result = _service.FindNext("abcabc", "abc", 1, false);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Start, Is.EqualTo(3));
    }

    [Test]
    public void FindNext_WrapsAround_WhenPastEnd()
    {
        var result = _service.FindNext("abc hello abc", "abc", 5, false);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Start, Is.EqualTo(10));
    }

    [Test]
    public void FindNext_WrapsToBeginning_WhenNoMatchAfterOffset()
    {
        var result = _service.FindNext("abc hello", "abc", 5, false);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Start, Is.EqualTo(0));
    }

    [Test]
    public void FindPrevious_FindsMatch_BeforeOffset()
    {
        var result = _service.FindPrevious("abc hello abc", "abc", 12, false);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Start, Is.EqualTo(10));
    }

    [Test]
    public void FindPrevious_WrapsToEnd_WhenNoMatchBefore()
    {
        var result = _service.FindPrevious("hello abc", "abc", 2, false);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Start, Is.EqualTo(6));
    }

    [Test]
    public void FindPrevious_NoMatch_ReturnsNull()
    {
        var result = _service.FindPrevious("hello world", "xyz", 10, false);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void CountMatches_ReturnsCorrectCount()
    {
        var count = _service.CountMatches("abc abc abc", "abc", false);
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public void CountMatches_CaseInsensitive()
    {
        var count = _service.CountMatches("Abc ABC abc", "abc", false);
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public void CountMatches_CaseSensitive()
    {
        var count = _service.CountMatches("Abc ABC abc", "abc", true);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void CountMatches_NoMatches_ReturnsZero()
    {
        var count = _service.CountMatches("hello world", "xyz", false);
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void FindNext_EmptyTerm_ReturnsNull()
    {
        var result = _service.FindNext("hello", "", 0, false);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void CountMatches_EmptyTerm_ReturnsZero()
    {
        var count = _service.CountMatches("hello", "", false);
        Assert.That(count, Is.EqualTo(0));
    }
}
