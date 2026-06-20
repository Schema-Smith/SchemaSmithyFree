// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using NSubstitute;
using Schema.Domain;
using Schema.Isolators;
using Schema.Delivery;

namespace Schema.UnitTests.Domain;

[TestFixture]
public class SqlScriptTests
{
    #region TokenReplace — Basic behavior

    [Test]
    public void TokenReplace_ReplacesTokenWithValue()
    {
        var script = "SELECT * FROM {{MainDB}}.dbo.Users";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("MainDB", "Production")
        };

        var result = SqlScript.TokenReplace(script, tokens);

        Assert.That(result, Is.EqualTo("SELECT * FROM Production.dbo.Users"));
    }

    [Test]
    public void TokenReplace_ReplacesMultipleTokens()
    {
        var script = "SELECT * FROM {{MainDB}}.{{Schema}}.Users";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("MainDB", "Production"),
            new("Schema", "dbo")
        };

        var result = SqlScript.TokenReplace(script, tokens);

        Assert.That(result, Is.EqualTo("SELECT * FROM Production.dbo.Users"));
    }

    [Test]
    public void TokenReplace_NoTokens_ReturnsUnchanged()
    {
        var script = "SELECT 1";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("MainDB", "Production")
        };

        var result = SqlScript.TokenReplace(script, tokens);

        Assert.That(result, Is.EqualTo("SELECT 1"));
    }

    [Test]
    public void TokenReplace_CaseInsensitive()
    {
        var script = "SELECT * FROM {{maindb}}.dbo.Users";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("MainDB", "Production")
        };

        var result = SqlScript.TokenReplace(script, tokens);

        Assert.That(result, Is.EqualTo("SELECT * FROM Production.dbo.Users"));
    }

    #endregion

    #region TokenReplace — Unresolved tokens

    [Test]
    public void TokenReplace_UnresolvedToken_TrackedInRemainingTokens()
    {
        var script = "SELECT * FROM {{MainDB}}.{{MissingToken}}.Users";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("MainDB", "Production")
        };
        var remaining = new List<string>();

        SqlScript.TokenReplace(script, tokens, remainingTokens: remaining);

        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining, Does.Contain("MissingToken"));
    }

    [Test]
    public void TokenReplace_AllResolved_RemainingTokensEmpty()
    {
        var script = "SELECT * FROM {{MainDB}}.dbo.Users";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("MainDB", "Production")
        };
        var remaining = new List<string>();

        SqlScript.TokenReplace(script, tokens, remainingTokens: remaining);

        Assert.That(remaining, Is.Empty);
    }

    #endregion

    #region TokenReplace — Single quote escaping

    [Test]
    public void TokenReplace_InsideSqlString_EscapesSingleQuotes()
    {
        var script = "DECLARE @v NVARCHAR(MAX) = '{{data}}'";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("data", "[{\"Name\":\"O'Brien\"}]")
        };

        var result = SqlScript.TokenReplace(script, tokens);

        Assert.That(result, Does.Contain("O''Brien"));
    }

    [Test]
    public void TokenReplace_OutsideSqlString_DoesNotEscapeQuotes()
    {
        var script = "-- {{comment}}";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("comment", "it's a comment")
        };

        var result = SqlScript.TokenReplace(script, tokens);

        Assert.That(result, Is.EqualTo("-- it's a comment"));
    }

    [Test]
    public void TokenReplace_SqlServer_DoesNotEscapeBackslashes()
    {
        var script = "DECLARE @v NVARCHAR(MAX) = '{{data}}'";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("data", "[{\"Path\":\"C:\\\\Users\\\\docs\"}]")
        };

        var result = SqlScript.TokenReplace(script, tokens, Platform.SqlServer);

        // Backslashes should NOT be doubled for SQL Server
        Assert.That(result, Does.Contain("C:\\\\Users\\\\docs"));
        Assert.That(result, Does.Not.Contain("C:\\\\\\\\Users"));
    }

    [Test]
    public void TokenReplace_PostgreSQL_DoesNotEscapeBackslashes()
    {
        var script = "v_json JSON = '{{data}}'";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("data", "[{\"Text\":\"line1\\r\\nline2\"}]")
        };

        var result = SqlScript.TokenReplace(script, tokens, Platform.PostgreSQL);

        // Backslashes should NOT be doubled for PostgreSQL
        Assert.That(result, Does.Contain("line1\\r\\nline2"));
        Assert.That(result, Does.Not.Contain("line1\\\\r\\\\nline2"));
    }

    [Test]
    public void TokenReplace_MySQL_EscapesBackslashes()
    {
        var script = "SET @json_data = '{{data}}'";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("data", "[{\"Text\":\"line1\\r\\nline2\"}]")
        };

        var result = SqlScript.TokenReplace(script, tokens, Platform.MySQL);

        // Backslashes MUST be doubled for MySQL
        Assert.That(result, Does.Contain("line1\\\\r\\\\nline2"));
    }

    [Test]
    public void TokenReplace_MySQL_EscapesBackslashesBeforeQuotes()
    {
        // Backslash escaping must happen before quote escaping to avoid
        // double-escaping the backslash in escaped quotes
        var script = "SET @json_data = '{{data}}'";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("data", "value with \\' tricky sequence")
        };

        var result = SqlScript.TokenReplace(script, tokens, Platform.MySQL);

        // \' → \\ (backslash doubled) then '' (quote doubled) = \\''
        Assert.That(result, Does.Contain("\\\\''"));
    }

    #endregion

    #region Outcome — Default and settability

    [Test]
    public void NewScript_DefaultsToAppliedOutcome()
    {
        var script = new SqlScript();
        Assert.That(script.Outcome, Is.EqualTo(ScriptOutcome.Applied));
    }

    [Test]
    public void Outcome_IsSettable()
    {
        var script = new SqlScript { Outcome = ScriptOutcome.Skipped };
        Assert.That(script.Outcome, Is.EqualTo(ScriptOutcome.Skipped));
    }

    #endregion

    #region Load — Batch splitting before token resolution

    [Test]
    public void Load_SplitsBatchesBeforeTokenResolution()
    {
        var scriptContent = "SELECT '{{Token1}}'\nGO\nSELECT '{{Token2}}'";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("Token1", "value with GO in it"),
            new("Token2", "another value")
        };

        var mockFile = Substitute.For<IFile>();
        mockFile.Exists("test.sql").Returns(true);
        mockFile.ReadAllText("test.sql").Returns(scriptContent);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFile);
            var script = SqlScript.Load("test.sql", tokens, Platform.SqlServer);

            // Should split into 2 batches on the template GO, not on "GO" inside token value
            Assert.That(script.Batches, Has.Count.EqualTo(2));
            Assert.That(script.Batches[0], Does.Contain("value with GO in it"));
            Assert.That(script.Batches[1], Does.Contain("another value"));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void Load_SingleBatch_ResolvesTokenCorrectly()
    {
        var scriptContent = "DECLARE @v NVARCHAR(MAX) = '{{data}}'; SELECT @v;";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("data", "[{\"Id\":1}]")
        };

        var mockFile = Substitute.For<IFile>();
        mockFile.Exists("test.sql").Returns(true);
        mockFile.ReadAllText("test.sql").Returns(scriptContent);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFile);
            var script = SqlScript.Load("test.sql", tokens, Platform.SqlServer);

            Assert.That(script.Batches, Has.Count.EqualTo(1));
            Assert.That(script.Batches[0], Does.Contain("[{\"Id\":1}]"));
            FactoryContainer.Clear();
        }
    }

    [Test]
    public void Load_MultiBatchWithQuoteEscaping_EscapesPerBatch()
    {
        var scriptContent = "DECLARE @v1 NVARCHAR(MAX) = '{{data}}'\nGO\nDECLARE @v2 NVARCHAR(MAX) = '{{data}}'";
        var tokens = new List<KeyValuePair<string, string>>
        {
            new("data", "it's here")
        };

        var mockFile = Substitute.For<IFile>();
        mockFile.Exists("test.sql").Returns(true);
        mockFile.ReadAllText("test.sql").Returns(scriptContent);

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFile);
            var script = SqlScript.Load("test.sql", tokens, Platform.SqlServer);

            Assert.That(script.Batches, Has.Count.EqualTo(2));
            // Both batches should have escaped quotes
            Assert.That(script.Batches[0], Does.Contain("it''s here"));
            Assert.That(script.Batches[1], Does.Contain("it''s here"));
            FactoryContainer.Clear();
        }
    }

    #endregion
}
