// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Schema.DataAccess;

﻿using Schema.DataAccess;
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

    // Double-quoted string support (inString2 branch)
    [Test]
    public void ShouldHandleDoubleQuoteStringsSpanningLines()
    {
        // Double-quoted string that spans two lines — the GO on the second
        // line is inside the string so should not split
        const string sql = @"SELECT ""First line
Second line""
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void ShouldHandleDoubleQuoteStringContainingGoOnSameLine()
    {
        // A double-quoted string with GO inside it — must not split
        const string sql = @"PRINT ""GO is just text here""
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void ShouldErrorOnUnterminatedDoubleQuoteString()
    {
        const string sql = @"-- first batch
GO
DECLARE @x NVARCHAR(100) = ""
GO";
        var ex = Assert.Throws<Exception>(() => SqlHelpers.SplitIntoBatches(sql));
        Assert.That(ex!.Message, Contains.Substring("Batch Parsing Failed"));
    }

    [Test]
    public void ShouldHandleDoubleQuoteStringWithEmbeddedSingleLineComment()
    {
        // -- inside a double-quoted string should not be treated as a comment
        const string sql = "PRINT \"-- not a comment\"\nGO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void ShouldHandleDoubleQuoteStringWithEmbeddedMultiLineComment()
    {
        // /* inside a double-quoted string should not be treated as a comment
        const string sql = "PRINT \"/* not a comment */\"\nGO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void ShouldHandleBracketedIdentifierSpanningLines()
    {
        // A bracketed identifier ([ ]) that spans lines — GO on second line is inside bracket
        const string sql = "SELECT [column\nname]\nGO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void ShouldErrorOnUnterminatedBracketedIdentifier()
    {
        const string sql = @"-- first batch
GO
SELECT [unterminated
GO";
        var ex = Assert.Throws<Exception>(() => SqlHelpers.SplitIntoBatches(sql));
        Assert.That(ex!.Message, Contains.Substring("Batch Parsing Failed"));
    }

    [Test]
    public void ShouldHandleEmptyInput()
    {
        var batches = SqlHelpers.SplitIntoBatches("");
        Assert.That(batches, Is.Empty);
    }

    [Test]
    public void ShouldHandleWhitespaceOnlyInput()
    {
        var batches = SqlHelpers.SplitIntoBatches("   \n   \n   ");
        Assert.That(batches, Is.Empty);
    }

    [Test]
    public void ShouldHandleMultilineCommentSpanningLinesWithoutEndOnSameLine()
    {
        // Multi-line comment where the closing */ is on a later line
        // This ensures the inMultiLine=true && no-*/ branch (cleanLine = "") is hit
        const string sql = @"SELECT 1
/* This comment
   spans three
   lines */
GO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }

    [Test]
    public void ShouldHandleSingleQuoteStringSpanningMultipleLinesWithNoQuoteOnMiddleLine()
    {
        // inString=true and cleanLine has no ' — exercises the cleanLine="" branch on line 54
        const string sql = "DECLARE @x VARCHAR(MAX) = 'line one\nno quote here\nend quote'\nGO";
        var batches = SqlHelpers.SplitIntoBatches(sql);
        Assert.That(batches, Has.Count.EqualTo(1));
    }
}
