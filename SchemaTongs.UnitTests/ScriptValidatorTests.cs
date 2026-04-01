// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using SchemaTongs;

namespace SchemaTongs.UnitTests;

public class ScriptValidatorTests
{
    [Test]
    public void RewriteWithTempName_CreateOrAlterView_ReplacesName()
    {
        var script = "CREATE OR ALTER VIEW [dbo].[MyView]\r\nAS\r\nSELECT 1 AS Col";
        var result = ScriptValidator.RewriteWithTempName(script);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Script, Does.Not.Contain("[MyView]"));
        Assert.That(result.Script, Does.Match(@"\[dbo\]\.\[vw_[a-f0-9]+\]"));
    }

    [Test]
    public void RewriteWithTempName_CreateFunction_ReplacesName()
    {
        var script = "CREATE FUNCTION [dbo].[fn_Test](@x INT)\r\nRETURNS INT\r\nAS\r\nBEGIN RETURN @x END";
        var result = ScriptValidator.RewriteWithTempName(script);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Script, Does.Not.Contain("[fn_Test]"));
        Assert.That(result.Script, Does.Match(@"\[dbo\]\.\[fn_[a-f0-9]+\]"));
    }

    [Test]
    public void RewriteWithTempName_CreateProcedure_ReplacesName()
    {
        var script = "CREATE PROCEDURE [dbo].[usp_Test]\r\nAS\r\nSELECT 1";
        var result = ScriptValidator.RewriteWithTempName(script);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Script, Does.Not.Contain("[usp_Test]"));
        Assert.That(result.Script, Does.Match(@"\[dbo\]\.\[sp_[a-f0-9]+\]"));
    }

    [Test]
    public void RewriteWithTempName_UnrecognizedPattern_ReturnsFailure()
    {
        var script = "SELECT 1";
        var result = ScriptValidator.RewriteWithTempName(script);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void GenerateParseOnlyWrapper_WrapsScript()
    {
        var script = "CREATE TRIGGER [dbo].[trg_Test] ON [dbo].[Users]\r\nAFTER INSERT\r\nAS\r\nSELECT 1";
        var result = ScriptValidator.GenerateParseOnlyWrapper(script);
        Assert.That(result, Does.StartWith("SET PARSEONLY ON;"));
        Assert.That(result, Does.Contain(script));
        Assert.That(result, Does.EndWith("SET PARSEONLY OFF;"));
    }
}
