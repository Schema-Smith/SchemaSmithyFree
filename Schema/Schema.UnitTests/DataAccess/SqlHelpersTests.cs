// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Schema.DataAccess;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class SqlHelpersTests
{
    [Test]
    public void SplitIntoBatches_SingleBatch_ReturnsSingleItem()
    {
        var script = "SELECT 1";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0].Trim(), Is.EqualTo("SELECT 1"));
    }

    [Test]
    public void SplitIntoBatches_TwoBatches_ReturnsTwoItems()
    {
        var script = "SELECT 1\nGO\nSELECT 2";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void SplitIntoBatches_EmptyBatches_AreSkipped()
    {
        var script = "GO\nGO\nSELECT 1\nGO\nGO";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void SplitIntoBatches_GoInComment_NotTreatedAsBatchSeparator()
    {
        var script = "SELECT 1 -- GO\nSELECT 2";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void SplitIntoBatches_GoInMultilineComment_NotTreatedAsBatchSeparator()
    {
        var script = "SELECT 1\n/* GO */\nSELECT 2";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void SplitIntoBatches_GoInString_NotTreatedAsBatchSeparator()
    {
        var script = "SELECT 'GO'";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void SplitIntoBatches_GoInDoubleQuotedString_NotTreatedAsBatchSeparator()
    {
        var script = "SELECT \"GO\"";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void SplitIntoBatches_GoInBracketedIdentifier_NotTreatedAsBatchSeparator()
    {
        var script = "SELECT [GO]";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void SplitIntoBatches_CaseInsensitiveGo()
    {
        var script = "SELECT 1\ngo\nSELECT 2";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void SplitIntoBatches_GoWithWhitespace()
    {
        var script = "SELECT 1\n  GO  \nSELECT 2";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void SplitIntoBatches_UnterminatedString_Throws()
    {
        var script = "SELECT 'unterminated";

        Assert.Throws<Exception>(() => SqlHelpers.SplitIntoBatches(script));
    }

    [Test]
    public void SplitIntoBatches_UnterminatedMultilineComment_Throws()
    {
        var script = "SELECT /* unterminated";

        Assert.Throws<Exception>(() => SqlHelpers.SplitIntoBatches(script));
    }

    [Test]
    public void SplitIntoBatches_WindowsLineEndings_Handled()
    {
        var script = "SELECT 1\r\nGO\r\nSELECT 2";
        var batches = SqlHelpers.SplitIntoBatches(script);

        Assert.That(batches, Has.Count.EqualTo(2));
    }
}
