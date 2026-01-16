using Schema.DataAccess;
using System;

namespace Schema.UnitTests;

public class SqlHelpersTests
{
    [Test]
    public void ShouldSplitBatchCorrectlyWithGOsEmbeddedInString()
    {
        const string sql = @"-- This is the first batch
GO
-- This is the second batch
GO
-- This is the third batch with embedded GOs in a string
DECLARE @SQL VARCHAR(MAX) = '
-- First dynamic batch
Go

-- Second dynamic batch
Go

-- Third dynamic batch
'
GO
-- Fourth batch
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(4));
    }

    [Test]
    public void ShouldSplitBatchCorrectlyWithGOsEmbeddedInMultiLineComment()
    {
        const string sql = @"-- This is the first batch
gO
-- This is the second batch
Go
/* This is the third batch with embedded GOs in a MultiLine Comment
-- First commented batch
GO

-- Second commented batch
Go

-- Third commented batch
*/
GO
-- Fourth batch
go";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(4));
    }

    [Test]
    public void ShouldSplitBatchCorrectlyWithSingleQuoteInsideSingleLineComment()
    {
        const string sql = @"-- This is the first batch
GO
-- It's the Second batch
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void ShouldSplitBatchCorrectlyWithSingleQuoteInsideMultiLineComment()
    {
        const string sql = @"-- This is the first batch
GO
/* 
   It's the Second batch 
*/
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void ShouldErrorOnUnterminatedString()
    {
        const string sql = @"-- This is the first batch
GO
DECLARE @x VARCHAR(100) = '
GO";
        var ex = Assert.Throws<Exception>(() => SqlHelpers.SplitIntoBatches(sql));
        Assert.That(ex!.Message, Contains.Substring("Batch Parsing Failed"));
    }

    [Test]
    public void ShouldErrorOnUnterminatedComment()
    {
        const string sql = @"-- This is the first batch
GO
/* Unterminated comment
GO";
        var ex = Assert.Throws<Exception>(() => SqlHelpers.SplitIntoBatches(sql));
        Assert.That(ex!.Message, Contains.Substring("Batch Parsing Failed"));
    }

    [Test]
    public void ShouldHandleEmbeddedMultiLineCommentsProperly()
    {
        const string sql = @"-- This is the first batch
GO
/* First multiline */ /* Second multiline */ /*
This one wraps */ 
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void ShouldHandleSingleLineCommentMarkerInsideMultilineComment()
    {
        const string sql = @"-- This is the first batch
GO
/* Should ignore this -- */
/* And this ' */
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void ShouldHandleSingleLineCommentMarkerInsideString()
    {
        const string sql = @"-- This is the first batch
GO
DECLARE @x VARCHAR(100) = '-- Junk

'
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void ShouldHandleMultiLineCommentMarkerInsideString()
    {
        const string sql = @"-- This is the first batch
GO
DECLARE @x VARCHAR(100) = '/* Junk

'
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void ShouldHandleSingLineCommentAfterGO()
    {
        const string sql = @"-- This is the first batch
GO--Ignore Me
DECLARE @x VARCHAR(100) = '/* Junk

'
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void ShouldHandleBracketedIdentifiers()
    {
        const string sql = @"-- This is the first batch
GO--Ignore Me
SELECT '/* Junk

' AS [Embedded ' "" --]
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(2));
    }

    [Test]
    public void FigureOutBatchParseIssue()
    {
        const string sql = @"
CREATE PROCEDURE dbo.MyProcedure (
        @P_FilePath  NVARCHAR(500)  = 'D:\Temp\',/* path on the SQL Server machine -- MUST exist - e.g. 'C:\Temp\' */
        @P_Transfer  BIT = 1
) AS
DECLARE @Junk INT;
GO
";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }
}
