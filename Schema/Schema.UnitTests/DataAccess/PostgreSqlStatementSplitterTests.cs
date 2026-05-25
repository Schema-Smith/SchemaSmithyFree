// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;

namespace Schema.UnitTests.DataAccess;

public class PostgreSqlStatementSplitterTests
{
    [Test]
    public void Split_SingleStatement_ReturnsSingleItem()
    {
        var result = PostgreSqlStatementSplitter.Split("SELECT 1;");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("SELECT 1;"));
    }

    [Test]
    public void Split_DropAndCreateProcedure_ReturnsTwoStatements()
    {
        var script = @"DROP PROCEDURE IF EXISTS ""SchemaSmith"".""TestProc"" (TEXT);

CREATE OR REPLACE PROCEDURE ""SchemaSmith"".""TestProc""(p_Input TEXT)
    LANGUAGE plpgsql
AS $$
BEGIN
    RAISE NOTICE 'hello';
    SELECT 1;
END $$;";

        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Does.StartWith("DROP PROCEDURE"));
        Assert.That(result[1], Does.StartWith("CREATE OR REPLACE"));
    }

    [Test]
    public void Split_SemicolonsInsideDollarQuotes_NotSplitPoints()
    {
        var script = @"CREATE OR REPLACE PROCEDURE ""SchemaSmith"".""TestProc""()
    LANGUAGE plpgsql
AS $$
DECLARE
    sql_script TEXT = '';
BEGIN
    sql_script = 'SELECT 1; SELECT 2;';
    EXECUTE sql_script;
END $$;";

        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void Split_DropFunction_HandlesCorrectly()
    {
        var script = @"DROP FUNCTION IF EXISTS ""SchemaSmith"".""QuoteColumnList""(TEXT);

CREATE OR REPLACE FUNCTION ""SchemaSmith"".""QuoteColumnList""(p_List TEXT)
    RETURNS TEXT
    LANGUAGE plpgsql
AS $$
BEGIN
    RETURN p_List;
END $$;";

        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void Split_EmptyInput_ReturnsEmpty()
    {
        Assert.That(PostgreSqlStatementSplitter.Split(""), Is.Empty);
        Assert.That(PostgreSqlStatementSplitter.Split("   "), Is.Empty);
    }

    [Test]
    public void Split_NoSemicolonAtEnd_StillReturnsStatement()
    {
        var result = PostgreSqlStatementSplitter.Split("SELECT 1");
        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void Split_MultipleStatementsNoDollarQuotes_SplitsCorrectly()
    {
        var script = "CREATE SCHEMA IF NOT EXISTS \"SchemaSmith\";\nCREATE TABLE IF NOT EXISTS \"SchemaSmith\".\"Test\" (id INT);";
        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void Split_KindlingSchemaScript_SplitsCorrectly()
    {
        var script = @"CREATE SCHEMA IF NOT EXISTS ""SchemaSmith"";
CREATE TABLE IF NOT EXISTS ""SchemaSmith"".""CompletedMigrationScripts"" (""ScriptName"" TEXT NOT NULL);";
        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void Split_NamedDollarQuoteTag_SemicolonsInsideNotSplitPoints()
    {
        var script = @"CREATE OR REPLACE FUNCTION ""SchemaSmith"".""TestFn""(p_Schema TEXT)
  RETURNS text
  LANGUAGE plpgsql
AS $function$
DECLARE result_string TEXT;
BEGIN
    SELECT 'hello; world' INTO result_string;
    RETURN result_string;
END $function$";

        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void Split_DropAndCreateWithNamedDollarQuote_ReturnsTwoStatements()
    {
        var script = @"DROP FUNCTION IF EXISTS ""SchemaSmith"".""TestFn""(TEXT);

CREATE OR REPLACE FUNCTION ""SchemaSmith"".""TestFn""(p_Schema TEXT)
  RETURNS text
  LANGUAGE plpgsql
AS $function$
DECLARE result_string TEXT;
BEGIN
    SELECT 'hello; world' INTO result_string;
    RETURN result_string;
END $function$;";

        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Does.StartWith("DROP FUNCTION"));
        Assert.That(result[1], Does.StartWith("CREATE OR REPLACE"));
    }

    [Test]
    public void Split_BodyDollarQuoteTag_SemicolonsInsideNotSplitPoints()
    {
        var script = @"CREATE OR REPLACE PROCEDURE ""SchemaSmith"".""TestProc""()
    LANGUAGE plpgsql
AS $body$
BEGIN
    EXECUTE 'SELECT 1; SELECT 2;';
END $body$;";

        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(1));
    }

    // --- Line comment handling (-- outside dollar quotes) ---

    [Test]
    public void Split_SemicolonInsideLineComment_NotSplitPoint()
    {
        // A semicolon inside a -- comment must not be treated as a statement boundary.
        var script = @"-- pre-statement comment with ; embedded
SELECT 1;
SELECT 2;";
        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Does.Contain("SELECT 1"));
        Assert.That(result[1], Does.Contain("SELECT 2"));
    }

    [Test]
    public void Split_LineCommentBeforeDollarQuotedBlock_NotSplitPoint()
    {
        // The original repro: -- comment with ; just before AS $$ opens.
        // Without the fix, the splitter would split on the ; inside the comment,
        // leaving the AS $$ ... $$ block starting mid-comment and producing garbage.
        var script = @"-- this function does work; it also has a ; in this comment
CREATE OR REPLACE FUNCTION ""SchemaSmith"".""TestFn""()
  RETURNS TEXT
  LANGUAGE plpgsql
AS $$
BEGIN
    RETURN 'hello';
END $$;";
        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Does.Contain("CREATE OR REPLACE FUNCTION"));
        Assert.That(result[0], Does.Contain("RETURN 'hello'"));
    }

    [Test]
    public void Split_DoubleDashInsideDollarQuote_NotTreatedAsLineComment()
    {
        // Inside a dollar-quoted block, -- is procedure-body syntax (a plpgsql comment).
        // The splitter must NOT engage its line-comment skip logic inside dollar quotes,
        // or the procedure body's contents get warped.
        var script = @"CREATE OR REPLACE FUNCTION ""SchemaSmith"".""TestFn""()
  RETURNS TEXT
  LANGUAGE plpgsql
AS $$
BEGIN
    -- plpgsql comment with ; semicolon inside dollar-quote
    RETURN 'hello';
END $$;
SELECT 1;";
        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Does.StartWith("CREATE OR REPLACE"));
        Assert.That(result[0], Does.Contain("-- plpgsql comment with ; semicolon inside dollar-quote"));
        Assert.That(result[1], Does.StartWith("SELECT 1"));
    }

    [Test]
    public void Split_LineCommentText_PreservedInStatementOutput()
    {
        // Contract: the fix removes a parser limitation but does not change the
        // statement text that flows downstream. Comments stay in the output.
        var script = @"-- pre-statement comment with ; embedded
SELECT 1;";
        var result = PostgreSqlStatementSplitter.Split(script);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Does.Contain("-- pre-statement comment with ; embedded"));
        Assert.That(result[0], Does.Contain("SELECT 1"));
    }
}
