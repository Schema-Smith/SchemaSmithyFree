// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;

namespace Schema.UnitTests.DataAccess;

[TestFixture]
public class BatchSplitterTests
{
    [Test]
    public void Split_NoDelimiterCommand_ReturnsSingleBatch()
    {
        var script = "SELECT 1;";
        var batches = BatchSplitter.Split(script);

        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0], Is.EqualTo("SELECT 1;"));
    }

    [Test]
    public void Split_WithDelimiterCommand_SplitsCorrectly()
    {
        var script = "DELIMITER //\nCREATE PROCEDURE p1()\nBEGIN\n  SELECT 1;\nEND//\nDELIMITER ;\nSELECT 2;";
        var batches = BatchSplitter.Split(script);

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(batches[0], Does.Contain("CREATE PROCEDURE"));
        Assert.That(batches[1], Does.Contain("SELECT 2;"));
    }

    [Test]
    public void Split_DelimiterChangedBack_HandlesSemicolonBatches()
    {
        var script = "DELIMITER //\nBODY//\nDELIMITER ;\nA;\nB;";
        var batches = BatchSplitter.Split(script);

        Assert.That(batches, Has.Count.EqualTo(3));
    }

    [Test]
    public void Split_CustomDelimiterRemoved_FromOutput()
    {
        var script = "DELIMITER $$\nCREATE FUNCTION f1()$$\nDELIMITER ;";
        var batches = BatchSplitter.Split(script);

        Assert.That(batches[0], Does.Not.EndWith("$$"));
    }

    [Test]
    public void Split_EmptyScript_ReturnsSingleBatch()
    {
        var script = "";
        var batches = BatchSplitter.Split(script);

        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void Split_PendingStatementAtEnd_IsIncluded()
    {
        var script = "DELIMITER //\nSELECT 1";
        var batches = BatchSplitter.Split(script);

        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0], Does.Contain("SELECT 1"));
    }
}
