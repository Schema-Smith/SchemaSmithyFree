// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Schema.Domain;

namespace SchemaTongs.UnitTests;

[TestFixture]
public class ScriptValidatorTests
{
    private ScriptValidator _validator;

    [SetUp]
    public void SetUp()
    {
        _validator = new ScriptValidator();
    }

    #region RewriteWithTempName — SQL Server

    [Test]
    public void RewriteWithTempName_SqlServer_Function_RewritesCorrectly()
    {
        var script = "CREATE FUNCTION [dbo].[fn_MyFunc](@x INT)\nRETURNS INT\nAS\nBEGIN RETURN @x END";
        var result = _validator.RewriteWithTempName(script, Platform.SqlServer);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE FUNCTION \[dbo\]\.\[_validate_[a-f0-9]+\]"));
        Assert.That(result.RewrittenScript, Does.Contain("RETURNS INT"));
        Assert.That(result.TempName, Does.Match(@"_validate_[a-f0-9]+"));
    }

    [Test]
    public void RewriteWithTempName_SqlServer_OrAlterView_RewritesCorrectly()
    {
        var script = "CREATE OR ALTER VIEW [dbo].[vw_MyView]\nAS\nSELECT 1 AS Col";
        var result = _validator.RewriteWithTempName(script, Platform.SqlServer);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE OR ALTER VIEW \[dbo\]\.\[_validate_[a-f0-9]+\]"));
        Assert.That(result.RewrittenScript, Does.Contain("SELECT 1 AS Col"));
    }

    [Test]
    public void RewriteWithTempName_SqlServer_Procedure_RewritesCorrectly()
    {
        var script = "CREATE PROCEDURE [dbo].[usp_MyProc]\nAS\nSELECT 1";
        var result = _validator.RewriteWithTempName(script, Platform.SqlServer);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE PROCEDURE \[dbo\]\.\[_validate_[a-f0-9]+\]"));
        Assert.That(result.RewrittenScript, Does.Contain("SELECT 1"));
    }

    [Test]
    public void RewriteWithTempName_SqlServer_NoSchema_RewritesCorrectly()
    {
        var script = "CREATE VIEW [MyView]\nAS\nSELECT 1 AS Col";
        var result = _validator.RewriteWithTempName(script, Platform.SqlServer);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE VIEW \[_validate_[a-f0-9]+\]"));
        Assert.That(result.RewrittenScript, Does.Not.Contain("[MyView]"));
    }

    #endregion

    #region RewriteWithTempName — PostgreSQL

    [Test]
    public void RewriteWithTempName_PostgreSQL_OrReplaceFunction_RewritesCorrectly()
    {
        var script = "CREATE OR REPLACE FUNCTION \"public\".\"my_func\"(x integer)\nRETURNS integer\nAS $$ BEGIN RETURN x; END; $$ LANGUAGE plpgsql;";
        var result = _validator.RewriteWithTempName(script, Platform.PostgreSQL);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE OR REPLACE FUNCTION ""public""\.""" + @"_validate_[a-f0-9]+"""));
        Assert.That(result.RewrittenScript, Does.Contain("RETURNS integer"));
    }

    [Test]
    public void RewriteWithTempName_PostgreSQL_View_RewritesCorrectly()
    {
        var script = "CREATE VIEW \"public\".\"my_view\"\nAS\nSELECT 1 AS col;";
        var result = _validator.RewriteWithTempName(script, Platform.PostgreSQL);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE VIEW ""public""\.""" + @"_validate_[a-f0-9]+"""));
        Assert.That(result.RewrittenScript, Does.Contain("SELECT 1 AS col;"));
    }

    #endregion

    #region RewriteWithTempName — MySQL

    [Test]
    public void RewriteWithTempName_MySQL_Function_RewritesCorrectly()
    {
        var script = "CREATE FUNCTION `my_func`(x INT)\nRETURNS INT\nBEGIN RETURN x; END";
        var result = _validator.RewriteWithTempName(script, Platform.MySQL);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE FUNCTION `_validate_[a-f0-9]+`"));
        Assert.That(result.RewrittenScript, Does.Contain("RETURNS INT"));
    }

    [Test]
    public void RewriteWithTempName_MySQL_DefinerClause_RewritesCorrectly()
    {
        var script = "CREATE DEFINER=`root`@`%` FUNCTION `my_func`(x INT)\nRETURNS INT\nBEGIN RETURN x; END";
        var result = _validator.RewriteWithTempName(script, Platform.MySQL);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"CREATE DEFINER=`root`@`%` FUNCTION `_validate_[a-f0-9]+`"));
        Assert.That(result.RewrittenScript, Does.Contain("RETURNS INT"));
    }

    #endregion

    #region RewriteWithTempName — Edge Cases

    [Test]
    public void RewriteWithTempName_MultiLine_NameOnNextLine_RewritesCorrectly()
    {
        var script = "CREATE FUNCTION\n[dbo].[fn_Foo](@x INT)\nRETURNS INT\nAS\nBEGIN RETURN @x END";
        var result = _validator.RewriteWithTempName(script, Platform.SqlServer);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Match(@"\[dbo\]\.\[_validate_[a-f0-9]+\]"));
        Assert.That(result.RewrittenScript, Does.Contain("RETURNS INT"));
    }

    [Test]
    public void RewriteWithTempName_PreservesScriptBody()
    {
        var body = "(@Param1 INT, @Param2 VARCHAR(100))\nRETURNS TABLE\nAS\nRETURN SELECT * FROM dbo.SomeTable WHERE Id = @Param1";
        var script = $"CREATE FUNCTION [dbo].[fn_Test]{body}";
        var result = _validator.RewriteWithTempName(script, Platform.SqlServer);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RewrittenScript, Does.Contain(body));
    }

    [Test]
    public void RewriteWithTempName_UnrecognizedPattern_ReturnsFailure()
    {
        var script = "SELECT 1";
        var result = _validator.RewriteWithTempName(script, Platform.SqlServer);

        Assert.That(result.Success, Is.False);
    }

    #endregion

    #region GenerateParseOnlyWrapper

    [Test]
    public void GenerateParseOnlyWrapper_SqlServer_Trigger_WrapsWithParseOnly()
    {
        var script = "CREATE TRIGGER [dbo].[trg_Insert] ON [dbo].[MyTable] AFTER INSERT AS SELECT 1";
        var result = _validator.GenerateParseOnlyWrapper(script, Platform.SqlServer);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.StartWith("SET PARSEONLY ON;"));
        Assert.That(result, Does.Contain(script));
        Assert.That(result, Does.EndWith("SET PARSEONLY OFF;"));
    }

    [Test]
    public void GenerateParseOnlyWrapper_PostgreSQL_Trigger_ReturnsNull()
    {
        var script = "CREATE TRIGGER my_trigger AFTER INSERT ON my_table FOR EACH ROW EXECUTE FUNCTION my_func();";
        var result = _validator.GenerateParseOnlyWrapper(script, Platform.PostgreSQL);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GenerateParseOnlyWrapper_MySQL_Trigger_GeneratesPrepare()
    {
        var script = "CREATE TRIGGER my_trigger AFTER INSERT ON my_table FOR EACH ROW BEGIN INSERT INTO log VALUES(NEW.id); END";
        var result = _validator.GenerateParseOnlyWrapper(script, Platform.MySQL);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("PREPARE _validate_stmt FROM"));
        Assert.That(result, Does.Contain("DEALLOCATE PREPARE _validate_stmt;"));
    }

    [Test]
    public void GenerateParseOnlyWrapper_MySQL_EscapesSingleQuotes()
    {
        var script = "CREATE TRIGGER trg AFTER INSERT ON t FOR EACH ROW BEGIN INSERT INTO log VALUES('test'); END";
        var result = _validator.GenerateParseOnlyWrapper(script, Platform.MySQL);

        Assert.That(result, Does.Contain("''test''"));
    }

    #endregion

    #region ValidateScript

    [Test]
    public void ValidateScript_ValidScript_ReturnsIsValid()
    {
        var connection = Substitute.For<IDbConnection>();
        var transaction = Substitute.For<IDbTransaction>();
        var command = Substitute.For<IDbCommand>();
        connection.BeginTransaction().Returns(transaction);
        connection.CreateCommand().Returns(command);

        var script = "CREATE VIEW [dbo].[vw_Test]\nAS\nSELECT 1 AS Col";
        var result = _validator.ValidateScript(connection, script, ScriptObjectType.Views, Platform.SqlServer);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateScript_InvalidScript_ReturnsError()
    {
        var connection = Substitute.For<IDbConnection>();
        var transaction = Substitute.For<IDbTransaction>();
        var command = Substitute.For<IDbCommand>();
        connection.BeginTransaction().Returns(transaction);
        connection.CreateCommand().Returns(command);
        command.When(c => c.ExecuteNonQuery()).Do(_ => throw new Exception("Invalid SQL syntax"));

        var script = "CREATE VIEW [dbo].[vw_Test]\nAS\nSELECT * FROM NonExistentTable";
        var result = _validator.ValidateScript(connection, script, ScriptObjectType.Views, Platform.SqlServer);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid SQL syntax"));
    }

    [Test]
    public void ValidateScript_SkippedObjectType_ReturnsValid()
    {
        var connection = Substitute.For<IDbConnection>();

        var result = _validator.ValidateScript(connection, "CREATE SCHEMA [test]", ScriptObjectType.Schemas, Platform.SqlServer);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Skipped, Is.True);
        connection.DidNotReceive().BeginTransaction();
    }

    [Test]
    public void ValidateScript_TransactionAlwaysRolledBack()
    {
        var connection = Substitute.For<IDbConnection>();
        var transaction = Substitute.For<IDbTransaction>();
        var command = Substitute.For<IDbCommand>();
        connection.BeginTransaction().Returns(transaction);
        connection.CreateCommand().Returns(command);

        var script = "CREATE VIEW [dbo].[vw_Test]\nAS\nSELECT 1 AS Col";
        _validator.ValidateScript(connection, script, ScriptObjectType.Views, Platform.SqlServer);

        transaction.Received(1).Rollback();
        transaction.DidNotReceive().Commit();
    }

    [Test]
    public void ValidateScript_TransactionRolledBack_EvenOnError()
    {
        var connection = Substitute.For<IDbConnection>();
        var transaction = Substitute.For<IDbTransaction>();
        var command = Substitute.For<IDbCommand>();
        connection.BeginTransaction().Returns(transaction);
        connection.CreateCommand().Returns(command);
        command.When(c => c.ExecuteNonQuery()).Do(_ => throw new Exception("Error"));

        var script = "CREATE VIEW [dbo].[vw_Test]\nAS\nSELECT 1 AS Col";
        _validator.ValidateScript(connection, script, ScriptObjectType.Views, Platform.SqlServer);

        transaction.Received(1).Rollback();
    }

    [Test]
    public void ValidateScript_ParseOnlyType_UsesParseOnlyWrapper()
    {
        var connection = Substitute.For<IDbConnection>();
        var transaction = Substitute.For<IDbTransaction>();
        var command = Substitute.For<IDbCommand>();
        connection.BeginTransaction().Returns(transaction);
        connection.CreateCommand().Returns(command);

        var script = "CREATE TRIGGER [dbo].[trg_Test] ON [dbo].[MyTable] AFTER INSERT AS SELECT 1";
        _validator.ValidateScript(connection, script, ScriptObjectType.Triggers, Platform.SqlServer);

        command.Received().CommandText = Arg.Is<string>(s => s.Contains("SET PARSEONLY ON;"));
    }

    [Test]
    public void ValidateScript_PostgreSQL_ParseOnlyType_SkipsValidation()
    {
        var connection = Substitute.For<IDbConnection>();

        var script = "CREATE TRIGGER my_trigger AFTER INSERT ON my_table FOR EACH ROW EXECUTE FUNCTION my_func();";
        var result = _validator.ValidateScript(connection, script, ScriptObjectType.Triggers, Platform.PostgreSQL);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Skipped, Is.True);
        connection.DidNotReceive().BeginTransaction();
    }

    [Test]
    public void ValidateScript_SkippedTypes_IncludesAllNonValidatable()
    {
        var connection = Substitute.For<IDbConnection>();

        var skippedTypes = new[]
        {
            ScriptObjectType.Schemas, ScriptObjectType.DataTypes, ScriptObjectType.FullTextCatalogs,
            ScriptObjectType.FullTextStopLists, ScriptObjectType.XMLSchemaCollections,
            ScriptObjectType.DomainTypes, ScriptObjectType.EnumTypes, ScriptObjectType.CompositeTypes,
            ScriptObjectType.Sequences, ScriptObjectType.Events, ScriptObjectType.None,
            ScriptObjectType.IndexedViews, ScriptObjectType.MaterializedViews
        };

        foreach (var objectType in skippedTypes)
        {
            var result = _validator.ValidateScript(connection, "script", objectType, Platform.SqlServer);
            Assert.That(result.Skipped, Is.True, $"Expected {objectType} to be skipped");
        }
    }

    #endregion
}
