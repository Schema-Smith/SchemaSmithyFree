// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using NUnit.Framework;
using SchemaTongs;

namespace SchemaTongs.UnitTests;

public class CleanupScriptGeneratorTests
{
    [Test]
    public void GenerateDropStatement_ViewFile_ReturnsDropView()
    {
        var result = CleanupScriptGenerator.GenerateDropStatement("dbo.MyView.sql", "Views");
        Assert.That(result, Is.EqualTo("DROP VIEW IF EXISTS [dbo].[MyView];"));
    }

    [Test]
    public void GenerateDropStatement_ProcedureFile_ReturnsDropProcedure()
    {
        var result = CleanupScriptGenerator.GenerateDropStatement("dbo.usp_GetUsers.sql", "Procedures");
        Assert.That(result, Is.EqualTo("DROP PROCEDURE IF EXISTS [dbo].[usp_GetUsers];"));
    }

    [Test]
    public void GenerateDropStatement_FunctionFile_ReturnsDropFunction()
    {
        var result = CleanupScriptGenerator.GenerateDropStatement("dbo.fn_Calculate.sql", "Functions");
        Assert.That(result, Is.EqualTo("DROP FUNCTION IF EXISTS [dbo].[fn_Calculate];"));
    }

    [Test]
    public void GenerateDropStatement_TriggerFile_ReturnsDropTrigger()
    {
        var result = CleanupScriptGenerator.GenerateDropStatement("dbo.trg_Audit.sql", "Triggers");
        Assert.That(result, Is.EqualTo("DROP TRIGGER IF EXISTS [dbo].[trg_Audit];"));
    }

    [Test]
    public void GenerateDropStatement_UnparseableFilename_ReturnsNull()
    {
        var result = CleanupScriptGenerator.GenerateDropStatement("readme.txt", "Views");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GenerateDropStatement_JsonTableFile_ReturnsNull()
    {
        // JSON orphans don't get cleanup scripts (too destructive)
        var result = CleanupScriptGenerator.GenerateDropStatement("dbo.Users.json", "Tables");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GenerateCleanupScript_MultipleOrphans_GeneratesAllStatements()
    {
        var files = new List<string> { "dbo.View1.sql", "dbo.View2.sql", "dbo.View3.sql" };
        var script = CleanupScriptGenerator.GenerateCleanupScript(files, "Views");
        Assert.That(script, Does.Contain("3 orphaned objects"));
        Assert.That(script, Does.Contain("DROP VIEW IF EXISTS [dbo].[View1];"));
        Assert.That(script, Does.Contain("DROP VIEW IF EXISTS [dbo].[View2];"));
        Assert.That(script, Does.Contain("DROP VIEW IF EXISTS [dbo].[View3];"));
    }

    [Test]
    public void GenerateInvalidObjectCleanupScript_MultipleDroppableObjects_GeneratesHeaderAndDropStatements()
    {
        var invalidObjects = new List<(string Folder, string FileName, string Error)>
        {
            ("Views", "dbo.BadView.sql", "Invalid column name"),
            ("Procedures", "dbo.BrokenProc.sql", "Syntax error near GO")
        };

        var result = CleanupScriptGenerator.GenerateInvalidObjectCleanupScript(invalidObjects);

        Assert.That(result, Does.Contain("2 invalid objects"));
        Assert.That(result, Does.Contain("-- Error: Invalid column name"));
        Assert.That(result, Does.Contain("DROP VIEW IF EXISTS [dbo].[BadView];"));
        Assert.That(result, Does.Contain("-- Error: Syntax error near GO"));
        Assert.That(result, Does.Contain("DROP PROCEDURE IF EXISTS [dbo].[BrokenProc];"));
    }

    [Test]
    public void GenerateInvalidObjectCleanupScript_MixedDroppableAndUndroppable_SkipsUndroppable()
    {
        var invalidObjects = new List<(string Folder, string FileName, string Error)>
        {
            ("Views", "dbo.BadView.sql", "Invalid column"),
            ("Tables", "dbo.SomeTable.json", "Missing column")
        };

        var result = CleanupScriptGenerator.GenerateInvalidObjectCleanupScript(invalidObjects);

        Assert.That(result, Does.Contain("DROP VIEW IF EXISTS [dbo].[BadView];"));
        Assert.That(result, Does.Not.Contain("SomeTable"));
    }

    [Test]
    public void GenerateInvalidObjectCleanupScript_EmptyList_GeneratesHeaderOnly()
    {
        var invalidObjects = new List<(string Folder, string FileName, string Error)>();

        var result = CleanupScriptGenerator.GenerateInvalidObjectCleanupScript(invalidObjects);

        Assert.That(result, Does.Contain("0 invalid objects"));
        Assert.That(result, Does.Not.Contain("DROP"));
    }

    [Test]
    public void GenerateInvalidObjectCleanupScript_SqlerrorExtension_GeneratesDropStatement()
    {
        var invalidObjects = new List<(string Folder, string FileName, string Error)>
        {
            ("Functions", "dbo.fn_Broken.sqlerror", "Parse error")
        };

        var result = CleanupScriptGenerator.GenerateInvalidObjectCleanupScript(invalidObjects);

        Assert.That(result, Does.Contain("DROP FUNCTION IF EXISTS [dbo].[fn_Broken];"));
    }
}
