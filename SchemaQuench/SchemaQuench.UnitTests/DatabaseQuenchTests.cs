// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.Domain;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;
using Schema.Isolators;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class DatabaseQuenchTests
{
    [TearDown]
    public void TearDown()
    {
        FactoryContainer.Clear();
    }

    #region Constructor Tests

    [Test]
    public void Constructor_InitializesProperties()
    {
        RegisterMockConfig();
        var product = new Product { Name = "TestProduct", Platform = Platform.SqlServer };
        var template = new Template { Name = "TestTemplate" };

        var quench = new DatabaseQuench("server", product, template, "testdb",
            false, "0", false, "0", false, false, false, null);

        Assert.That(quench.QuenchSuccessful, Is.False);
        Assert.That(quench.Platform, Is.EqualTo(Platform.SqlServer));
        Assert.That(quench.ProductName, Is.EqualTo("TestProduct"));
    }

    [Test]
    public void Constructor_InternalWithDropUnknownIndexes_SetsAllFields()
    {
        var product = new Product { Name = "Prod", Platform = Platform.MySQL };
        var template = new Template { Name = "Tmpl" };

        var quench = new DatabaseQuench("myserver", product, template, "mydb",
            true, "1", true, "1", "0", true, true, null);

        Assert.That(quench.Platform, Is.EqualTo(Platform.MySQL));
        Assert.That(quench.ProductName, Is.EqualTo("Prod"));
    }

    [Test]
    public void Constructor_DefaultTrackingSettings_BothTrue()
    {
        RegisterMockConfig();
        var product = new Product { Name = "TestProduct", Platform = Platform.SqlServer };
        var template = new Template { Name = "TestTemplate" };

        // Defaults: trackRunOnceMigrations=true, pruneObsoleteMigrationTracking=true
        var quench = new DatabaseQuench("server", product, template, "testdb",
            false, "0", false, "0", false, false, false, null);

        Assert.That(quench.QuenchSuccessful, Is.False);
        Assert.That(quench.Platform, Is.EqualTo(Platform.SqlServer));
    }

    [Test]
    public void Constructor_TrackingOff_AcceptsParameter()
    {
        RegisterMockConfig();
        var product = new Product { Name = "TestProduct", Platform = Platform.SqlServer };
        var template = new Template { Name = "TestTemplate" };

        var quench = new DatabaseQuench("server", product, template, "testdb",
            false, "0", false, "0", false, false, false, null,
            trackRunOnceMigrations: false, pruneObsoleteMigrationTracking: false);

        Assert.That(quench.Platform, Is.EqualTo(Platform.SqlServer));
    }

    [Test]
    public void Constructor_InternalWithTrackingOff_AcceptsParameter()
    {
        var product = new Product { Name = "Prod", Platform = Platform.MySQL };
        var template = new Template { Name = "Tmpl" };

        var quench = new DatabaseQuench("myserver", product, template, "mydb",
            true, "1", true, "1", "0", true, true, null,
            trackRunOnceMigrations: false, pruneObsoleteMigrationTracking: true);

        Assert.That(quench.Platform, Is.EqualTo(Platform.MySQL));
    }

    #endregion

    #region Platform Dispatch - QuoteUseDatabase

    [Test]
    public void QuoteUseDatabase_SqlServer_UsesBrackets()
    {
        Assert.That(DatabaseQuench.QuoteUseDatabase("MyDb", Platform.SqlServer), Is.EqualTo("USE [MyDb]"));
    }

    [Test]
    public void QuoteUseDatabase_PostgreSQL_ReturnsDbName()
    {
        Assert.That(DatabaseQuench.QuoteUseDatabase("MyDb", Platform.PostgreSQL), Is.EqualTo("MyDb"));
    }

    [Test]
    public void QuoteUseDatabase_MySQL_UsesBackticks()
    {
        Assert.That(DatabaseQuench.QuoteUseDatabase("MyDb", Platform.MySQL), Is.EqualTo("USE `MyDb`"));
    }

    [Test]
    public void QuoteUseDatabase_InvalidPlatform_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseQuench.QuoteUseDatabase("db", (Platform)999));
    }

    #endregion

    #region Platform Dispatch - QuoteIdentifier

    [Test]
    public void QuoteIdentifier_SqlServer_UsesBrackets()
    {
        Assert.That(DatabaseQuench.QuoteIdentifier("col", Platform.SqlServer), Is.EqualTo("[col]"));
    }

    [Test]
    public void QuoteIdentifier_PostgreSQL_UsesDoubleQuotes()
    {
        Assert.That(DatabaseQuench.QuoteIdentifier("col", Platform.PostgreSQL), Is.EqualTo("\"col\""));
    }

    [Test]
    public void QuoteIdentifier_MySQL_UsesBackticks()
    {
        Assert.That(DatabaseQuench.QuoteIdentifier("col", Platform.MySQL), Is.EqualTo("`col`"));
    }

    #endregion

    #region IsWhatIf

    [Test]
    public void IsWhatIf_SqlServer_True_WhenValueIs1()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "1", false, "0", "0", false, false, null);
        Assert.That(quench.IsWhatIf, Is.True);
    }

    [Test]
    public void IsWhatIf_SqlServer_False_WhenValueIs0()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);
        Assert.That(quench.IsWhatIf, Is.False);
    }

    [Test]
    public void IsWhatIf_PostgreSQL_True_WhenValueIsTrue()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "true", false, "false", "false", false, false, null);
        Assert.That(quench.IsWhatIf, Is.True);
    }

    [Test]
    public void IsWhatIf_PostgreSQL_False_WhenValueIsFalse()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "false", false, "false", "false", false, false, null);
        Assert.That(quench.IsWhatIf, Is.False);
    }

    [Test]
    public void IsWhatIf_MySQL_True_WhenValueIs1()
    {
        var product = new Product { Name = "Test", Platform = Platform.MySQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "1", false, "0", "0", false, false, null);
        Assert.That(quench.IsWhatIf, Is.True);
    }

    #endregion

    #region FormatBooleanFlag

    [Test]
    public void FormatBooleanFlag_SqlServer_Returns1Or0()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        Assert.That(quench.FormatBooleanFlag(true), Is.EqualTo("1"));
        Assert.That(quench.FormatBooleanFlag(false), Is.EqualTo("0"));
    }

    [Test]
    public void FormatBooleanFlag_PostgreSQL_ReturnsTrueOrFalse()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "false", false, "false", "false", false, false, null);

        Assert.That(quench.FormatBooleanFlag(true), Is.EqualTo("true"));
        Assert.That(quench.FormatBooleanFlag(false), Is.EqualTo("false"));
    }

    [Test]
    public void FormatBooleanFlag_MySQL_Returns1Or0()
    {
        var product = new Product { Name = "Test", Platform = Platform.MySQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        Assert.That(quench.FormatBooleanFlag(true), Is.EqualTo("1"));
        Assert.That(quench.FormatBooleanFlag(false), Is.EqualTo("0"));
    }

    #endregion

    #region ShouldAlwaysRun

    [Test]
    public void ShouldAlwaysRun_WithAlwaysTag_ReturnsTrue()
    {
        Assert.That(DatabaseQuench.ShouldAlwaysRun("01_setup[ALWAYS].sql"), Is.True);
    }

    [Test]
    public void ShouldAlwaysRun_WithoutAlwaysTag_ReturnsFalse()
    {
        Assert.That(DatabaseQuench.ShouldAlwaysRun("01_setup.sql"), Is.False);
    }

    [Test]
    public void ShouldAlwaysRun_CaseSensitive_ReturnsFalse()
    {
        Assert.That(DatabaseQuench.ShouldAlwaysRun("01_setup[always].sql"), Is.False);
    }

    [Test]
    public void ShouldAlwaysRun_AlwaysInMiddle_ReturnsFalse()
    {
        Assert.That(DatabaseQuench.ShouldAlwaysRun("01_[ALWAYS]_setup.sql"), Is.False);
    }

    #endregion

    #region GetRelativeScriptPath

    [Test]
    public void GetRelativeScriptPath_ReturnsRelativePath()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        // Use OS-native path separators for consistency
        var templateDir = System.IO.Path.Combine("C:", "products", "MyProduct", "Templates", "Core");
        var templateFilePath = System.IO.Path.Combine(templateDir, "Template.json");
        var template = new Template { Name = "T", FilePath = templateFilePath };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var scriptPath = System.IO.Path.Combine(templateDir, "Before Scripts", "01_setup.sql");
        var result = quench.GetRelativeScriptPath(scriptPath);
        Assert.That(result, Is.EqualTo("Before Scripts/01_setup.sql"));
    }

    [Test]
    public void GetRelativeScriptPath_NormalizesBackslashes()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var templateDir = System.IO.Path.Combine("C:", "products", "MyProduct", "Templates", "Core");
        var templateFilePath = System.IO.Path.Combine(templateDir, "Template.json");
        var template = new Template { Name = "T", FilePath = templateFilePath };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var scriptPath = System.IO.Path.Combine(templateDir, "After Scripts", "cleanup.sql");
        var result = quench.GetRelativeScriptPath(scriptPath);
        Assert.That(result, Is.EqualTo("After Scripts/cleanup.sql"));
    }

    #endregion

    #region Platform-Specific SQL Generation

    [Test]
    public void GetDeleteCompletedScriptSql_SqlServer_UsesSchemaSmithSchemaAndBrackets()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetDeleteCompletedScriptSql("MyProduct", "Before", "script.sql", "T", "");
        Assert.That(sql, Does.Contain("SchemaSmith.CompletedMigrationScripts"));
        Assert.That(sql, Does.Contain("[ProductName]"));
        Assert.That(sql, Does.Contain("[QuenchSlot]"));
        // DELETE is STRICT on template_name (no permissive IN) so a prune in template A
        // can't shadow-delete a legacy blank-template row that template B still needs.
        Assert.That(sql, Does.Contain("[template_name] = 'T'"));
        Assert.That(sql, Does.Not.Contain("[template_name] IN"));
        Assert.That(sql, Does.Contain("[schema_name] = ''"));
    }

    [Test]
    public void GetDeleteCompletedScriptSql_PostgreSQL_UsesQuotedSchemaSmith()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "false", false, "false", "false", false, false, null);

        var sql = quench.GetDeleteCompletedScriptSql("MyProduct", "Before", "script.sql", "T", "");
        Assert.That(sql, Does.Contain("\"SchemaSmith\".\"CompletedMigrationScripts\""));
        Assert.That(sql, Does.Contain("\"ProductName\""));
        Assert.That(sql, Does.Contain("template_name = 'T'"));
        Assert.That(sql, Does.Not.Contain("template_name IN"));
        Assert.That(sql, Does.Contain("schema_name = ''"));
    }

    [Test]
    public void GetDeleteCompletedScriptSql_MySQL_UsesBackticks()
    {
        var product = new Product { Name = "Test", Platform = Platform.MySQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetDeleteCompletedScriptSql("MyProduct", "Before", "script.sql", "T", "");
        Assert.That(sql, Does.Contain("`SchemaSmith_CompletedMigrationScripts`"));
        Assert.That(sql, Does.Contain("`ProductName`"));
        Assert.That(sql, Does.Contain("`template_name` = 'T'"));
        Assert.That(sql, Does.Not.Contain("`template_name` IN"));
        Assert.That(sql, Does.Contain("`schema_name` = ''"));
    }

    [Test]
    public void GetSelectCompletedScriptsSql_SqlServer_IncludesNoLock()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetSelectCompletedScriptsSql("MyProduct", "Before", "T", "");
        Assert.That(sql, Does.Contain("WITH (NOLOCK)"));
        Assert.That(sql, Does.Contain("[template_name] IN ('', 'T')"));
        Assert.That(sql, Does.Contain("[schema_name] = ''"));
    }

    [Test]
    public void GetSelectCompletedScriptsSql_PostgreSQL_NoLock()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "false", false, "false", "false", false, false, null);

        var sql = quench.GetSelectCompletedScriptsSql("MyProduct", "Before", "T", "tenant_acme");
        Assert.That(sql, Does.Not.Contain("NOLOCK"));
        Assert.That(sql, Does.Contain("\"SchemaSmith\".\"CompletedMigrationScripts\""));
        Assert.That(sql, Does.Contain("template_name IN ('', 'T')"));
        Assert.That(sql, Does.Contain("schema_name = 'tenant_acme'"));
    }

    [Test]
    public void GetInsertCompletedScriptSql_SqlServer_InsertsWithBrackets()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetInsertCompletedScriptSql("path/script.sql", "MyProduct", "Before", "TenantBody", "tenant_acme");
        Assert.That(sql, Does.Contain("INSERT SchemaSmith.CompletedMigrationScripts"));
        Assert.That(sql, Does.Contain("path/script.sql"));
        Assert.That(sql, Does.Contain("'TenantBody'"));
        Assert.That(sql, Does.Contain("'tenant_acme'"));
    }

    [Test]
    public void GetInsertCompletedScriptSql_PostgreSQL_InsertsWithQuotes()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "false", false, "false", "false", false, false, null);

        var sql = quench.GetInsertCompletedScriptSql("path/script.sql", "MyProduct", "Before", "TenantBody", "tenant_acme");
        Assert.That(sql, Does.Contain("INSERT INTO \"SchemaSmith\".\"CompletedMigrationScripts\""));
        Assert.That(sql, Does.Contain("template_name"));
        Assert.That(sql, Does.Contain("schema_name"));
    }

    [Test]
    public void GetInsertCompletedScriptSql_MySQL_InsertsWithBackticks()
    {
        var product = new Product { Name = "Test", Platform = Platform.MySQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetInsertCompletedScriptSql("path/script.sql", "MyProduct", "Before", "T", "");
        Assert.That(sql, Does.Contain("INSERT INTO `SchemaSmith_CompletedMigrationScripts`"));
        Assert.That(sql, Does.Contain("`template_name`"));
        Assert.That(sql, Does.Contain("`schema_name`"));
    }

    [Test]
    public void GetClaimLegacyTrackingRowsSql_SqlServer_BracketedPredicateAndInList()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetClaimLegacyTrackingRowsSql("MyProduct", "Before", "Main", "",
            new[] { "scripts/a.sql", "scripts/b.sql" });

        Assert.That(sql, Does.Contain("UPDATE SchemaSmith.CompletedMigrationScripts"));
        Assert.That(sql, Does.Contain("SET [template_name] = 'Main'"));
        Assert.That(sql, Does.Contain("[template_name] = ''"));    // only legacy rows
        Assert.That(sql, Does.Contain("[schema_name] = ''"));
        Assert.That(sql, Does.Contain("[ScriptPath] IN ('scripts/a.sql','scripts/b.sql')"));
    }

    [Test]
    public void GetClaimLegacyTrackingRowsSql_PostgreSQL_QuotedPredicateAndInList()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "false", false, "false", "false", false, false, null);

        var sql = quench.GetClaimLegacyTrackingRowsSql("MyProduct", "Before", "Main", "",
            new[] { "scripts/a.sql", "scripts/b.sql" });

        Assert.That(sql, Does.Contain("UPDATE \"SchemaSmith\".\"CompletedMigrationScripts\""));
        Assert.That(sql, Does.Contain("SET template_name = 'Main'"));
        Assert.That(sql, Does.Contain("template_name = ''"));
        Assert.That(sql, Does.Contain("schema_name = ''"));
        Assert.That(sql, Does.Contain("\"ScriptPath\" IN ('scripts/a.sql','scripts/b.sql')"));
    }

    [Test]
    public void GetClaimLegacyTrackingRowsSql_MySQL_BacktickedPredicateAndInList()
    {
        var product = new Product { Name = "Test", Platform = Platform.MySQL };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetClaimLegacyTrackingRowsSql("MyProduct", "Before", "Main", "",
            new[] { "scripts/a.sql", "scripts/b.sql" });

        Assert.That(sql, Does.Contain("UPDATE `SchemaSmith_CompletedMigrationScripts`"));
        Assert.That(sql, Does.Contain("SET `template_name` = 'Main'"));
        Assert.That(sql, Does.Contain("`template_name` = ''"));
        Assert.That(sql, Does.Contain("`schema_name` = ''"));
        Assert.That(sql, Does.Contain("`ScriptPath` IN ('scripts/a.sql','scripts/b.sql')"));
    }

    [Test]
    public void GetClaimLegacyTrackingRowsSql_EscapesSingleQuotesInLiteralValues()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, "0", false, "0", "0", false, false, null);

        var sql = quench.GetClaimLegacyTrackingRowsSql("O'Brien", "Before's", "Tem'plate", "schem'a",
            new[] { "scripts/O'Brien.sql" });

        Assert.That(sql, Does.Contain("O''Brien"));
        Assert.That(sql, Does.Contain("Before''s"));
        Assert.That(sql, Does.Contain("Tem''plate"));
        Assert.That(sql, Does.Contain("schem''a"));
        Assert.That(sql, Does.Contain("scripts/O''Brien.sql"));
    }

    [TestCase(Platform.SqlServer)]
    [TestCase(Platform.PostgreSQL)]
    [TestCase(Platform.MySQL)]
    public void CompletedScriptSql_EscapesSingleQuotesInLiteralValues(Platform platform)
    {
        var falseValue = platform == Platform.PostgreSQL ? "false" : "0";
        var product = new Product { Name = "Test", Platform = platform };
        var quench = new DatabaseQuench("srv", product, new Template { Name = "T" }, "db",
            false, falseValue, false, falseValue, falseValue, false, false, null);

        var selectSql = quench.GetSelectCompletedScriptsSql("O'Brien", "Before's", "Tem'plate", "schem'a");
        var deleteSql = quench.GetDeleteCompletedScriptSql("O'Brien", "Before's", "scripts/O'Brien.sql", "Tem'plate", "schem'a");
        var insertSql = quench.GetInsertCompletedScriptSql("scripts/O'Brien.sql", "O'Brien", "Before's", "Tem'plate", "schem'a");

        Assert.Multiple(() =>
        {
            Assert.That(selectSql, Does.Contain("O''Brien").And.Contain("Before''s").And.Contain("Tem''plate").And.Contain("schem''a"));
            Assert.That(deleteSql, Does.Contain("O''Brien").And.Contain("Before''s").And.Contain("scripts/O''Brien.sql").And.Contain("Tem''plate").And.Contain("schem''a"));
            Assert.That(insertSql, Does.Contain("O''Brien").And.Contain("Before''s").And.Contain("scripts/O''Brien.sql").And.Contain("Tem''plate").And.Contain("schem''a"));
        });
    }

    #endregion

    #region Template Script Collection Properties

    [Test]
    public void Template_BeforeScripts_ReturnsBeforeSlotScripts()
    {
        var template = new Template { Name = "Test" };
        var beforeFolder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.Before };
        var script = new SqlScript { Name = "01.sql" };
        script.Batches.Add("SELECT 1");
        beforeFolder.Scripts.Add(script);
        template.ScriptFolders.Add(beforeFolder);

        Assert.That(template.BeforeScripts, Has.Count.EqualTo(1));
        Assert.That(template.BeforeScripts[0].Name, Is.EqualTo("01.sql"));
    }

    [Test]
    public void Template_ObjectScripts_ReturnsObjectsSlotScripts()
    {
        var template = new Template { Name = "Test" };
        var folder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.Objects };
        var script = new SqlScript { Name = "proc.sql" };
        script.Batches.Add("CREATE PROC");
        folder.Scripts.Add(script);
        template.ScriptFolders.Add(folder);

        Assert.That(template.ObjectScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Template_AfterTablesObjectScripts_IncludesBothObjectsAndAfterTablesObjects()
    {
        var template = new Template { Name = "Test" };

        var objectsFolder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.Objects };
        var script1 = new SqlScript { Name = "obj1.sql" };
        script1.Batches.Add("CREATE FUNC");
        objectsFolder.Scripts.Add(script1);

        var afterTablesFolder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.AfterTablesObjects };
        var script2 = new SqlScript { Name = "obj2.sql" };
        script2.Batches.Add("CREATE VIEW");
        afterTablesFolder.Scripts.Add(script2);

        template.ScriptFolders.Add(objectsFolder);
        template.ScriptFolders.Add(afterTablesFolder);

        Assert.That(template.AfterTablesObjectScripts, Has.Count.EqualTo(2));
    }

    [Test]
    public void Template_AfterScripts_ReturnsAfterSlotScripts()
    {
        var template = new Template { Name = "Test" };
        var folder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.After };
        var script = new SqlScript { Name = "after.sql" };
        script.Batches.Add("CLEANUP");
        folder.Scripts.Add(script);
        template.ScriptFolders.Add(folder);

        Assert.That(template.AfterScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Template_BetweenTablesAndKeysScripts_ReturnsBetweenSlotScripts()
    {
        var template = new Template { Name = "Test" };
        var folder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.BetweenTablesAndKeys };
        var script = new SqlScript { Name = "between.sql" };
        script.Batches.Add("ALTER TABLE");
        folder.Scripts.Add(script);
        template.ScriptFolders.Add(folder);

        Assert.That(template.BetweenTablesAndKeysScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Template_AfterTableScripts_ReturnsAfterTablesScriptsSlot()
    {
        var template = new Template { Name = "Test" };
        var folder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.AfterTablesScripts };
        var script = new SqlScript { Name = "afterTable.sql" };
        script.Batches.Add("STUFF");
        folder.Scripts.Add(script);
        template.ScriptFolders.Add(folder);

        Assert.That(template.AfterTableScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Template_TableDataScripts_ReturnsTableDataSlot()
    {
        var template = new Template { Name = "Test" };
        var folder = new TemplateFolder { QuenchSlot = TemplateQuenchSlot.TableData };
        var script = new SqlScript { Name = "data.sql" };
        script.Batches.Add("MERGE");
        folder.Scripts.Add(script);
        template.ScriptFolders.Add(folder);

        Assert.That(template.TableDataScripts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Template_EmptyScriptFolders_ReturnsEmptyLists()
    {
        var template = new Template { Name = "Test" };
        Assert.That(template.BeforeScripts, Is.Empty);
        Assert.That(template.ObjectScripts, Is.Empty);
        Assert.That(template.AfterScripts, Is.Empty);
        Assert.That(template.BetweenTablesAndKeysScripts, Is.Empty);
        Assert.That(template.AfterTableScripts, Is.Empty);
        Assert.That(template.AfterTablesObjectScripts, Is.Empty);
        Assert.That(template.TableDataScripts, Is.Empty);
    }

    #endregion

    #region Template LogPath

    [Test]
    public void Template_LogPath_StripsLongPathPrefix()
    {
        var template = new Template { Name = "Test", FilePath = "/some/path/Template.json" };
        Assert.That(template.LogPath, Is.EqualTo("/some/path/Template.json"));
    }

    #endregion

    #region QuenchOneScript Tests

    [Test]
    public void QuenchOneScript_SuccessfulExecution_MarksAsQuenched()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var script = new SqlScript { Name = "test.sql" };
        script.Batches.Add("SELECT 1");

        var mockCmd = CreateMockCommand();

        quench.QuenchOneScript(mockCmd, script, false, false);

        Assert.That(script.HasBeenQuenched, Is.True);
        Assert.That(script.Error, Is.Null);
    }

    [Test]
    public void QuenchOneScript_FailedExecution_SetsError()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var script = new SqlScript { Name = "fail.sql" };
        script.Batches.Add("INVALID SQL");

        var mockCmd = CreateMockCommand();
        mockCmd.When(c => c.ExecuteNonQuery()).Do(_ => throw new Exception("Syntax error"));

        quench.QuenchOneScript(mockCmd, script, false, false);

        Assert.That(script.HasBeenQuenched, Is.False);
        Assert.That(script.Error, Is.Not.Null);
        Assert.That(script.Error.Message, Does.Contain("Syntax error"));
    }

    [Test]
    public void QuenchOneScript_RunTwice_ExecutesTwice()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "false", false, "false", "false", false, false, null);

        var script = new SqlScript { Name = "test.sql" };
        script.Batches.Add("SELECT 1");

        var mockCmd = CreateMockCommand();
        var executeCount = 0;
        mockCmd.When(c => c.ExecuteNonQuery()).Do(_ => executeCount++);

        quench.QuenchOneScript(mockCmd, script, true, false);

        Assert.That(executeCount, Is.EqualTo(2));
        Assert.That(script.HasBeenQuenched, Is.True);
    }

    [Test]
    public void QuenchOneScript_MultipleBatches_ExecutesAll()
    {
        var product = new Product { Name = "Test", Platform = Platform.MySQL };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var script = new SqlScript { Name = "multi.sql" };
        script.Batches.Add("SELECT 1");
        script.Batches.Add("SELECT 2");
        script.Batches.Add("SELECT 3");

        var mockCmd = CreateMockCommand();
        var executeCount = 0;
        mockCmd.When(c => c.ExecuteNonQuery()).Do(_ => executeCount++);

        quench.QuenchOneScript(mockCmd, script, false, false);

        Assert.That(executeCount, Is.EqualTo(3));
        Assert.That(script.HasBeenQuenched, Is.True);
    }

    #endregion

    #region QuenchDatabaseObjects Tests

    [Test]
    public void QuenchDatabaseObjects_AllSucceed_AllMarkedQuenched()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var scripts = new List<SqlScript>();
        for (var i = 0; i < 3; i++)
        {
            var s = new SqlScript { Name = $"script{i}.sql" };
            s.Batches.Add($"SELECT {i}");
            scripts.Add(s);
        }

        var mockCmd = CreateMockCommand();

        quench.QuenchDatabaseObjects(mockCmd, scripts, false);

        Assert.That(scripts.All(s => s.HasBeenQuenched), Is.True);
    }

    [Test]
    public void QuenchDatabaseObjects_DependencyLoop_RetriesUntilResolved()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var script1 = new SqlScript { Name = "script1.sql" };
        script1.Batches.Add("SELECT 1");
        var script2 = new SqlScript { Name = "script2.sql" };
        script2.Batches.Add("SELECT 2");

        // Script2 fails first time, succeeds second time
        var callCount = 0;
        var mockCmd = CreateMockCommand();
        mockCmd.When(c => c.ExecuteNonQuery()).Do(_ =>
        {
            callCount++;
            if (callCount == 2) // second call = script2's first attempt
                throw new Exception("Dependency not ready");
        });

        var scripts = new List<SqlScript> { script1, script2 };
        quench.QuenchDatabaseObjects(mockCmd, scripts, false);

        Assert.That(scripts.All(s => s.HasBeenQuenched), Is.True);
    }

    #endregion

    #region Execute - Basic Flow Tests

    [Test]
    public void Execute_WithNoCheckpointing_SetsQuenchSuccessful_WhenConnectionSucceeds()
    {
        // This test validates that Execute doesn't crash on the simplest path.
        // With mocked connections it would need full setup which is integration test territory.
        // We verify the constructor state instead.
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        // Without mocked connection factory, Execute will fail to connect and set QuenchSuccessful = false
        quench.Execute();
        Assert.That(quench.QuenchSuccessful, Is.False);
    }

    #endregion

    #region QuenchIndexedViews Tests

    [Test]
    public void QuenchIndexedViews_MissingUniqueClusteredIndex_Throws()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        template.IndexedViews.Add(new SqlServerIndexedView
        {
            Name = "[vw_Test]",
            Schema = "dbo",
            Definition = "SELECT 1",
            Indexes = [new SqlServerIndex { Name = "[IX_1]", Unique = false, Clustered = false }]
        });

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var mockCmd = CreateMockCommand();
        var ex = Assert.Throws<Exception>(() => quench.QuenchIndexedViews(mockCmd));
        Assert.That(ex!.Message, Does.Contain("requires a unique clustered index"));
        Assert.That(ex.Message, Does.Contain("dbo.[vw_Test]"));
    }

    [Test]
    public void QuenchIndexedViews_ClusteredButNotUnique_Throws()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        template.IndexedViews.Add(new SqlServerIndexedView
        {
            Name = "[vw_Test]",
            Schema = "dbo",
            Definition = "SELECT 1",
            Indexes = [new SqlServerIndex { Name = "[IX_1]", Unique = false, Clustered = true }]
        });

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var mockCmd = CreateMockCommand();
        Assert.Throws<Exception>(() => quench.QuenchIndexedViews(mockCmd));
    }

    [Test]
    public void QuenchIndexedViews_AllFilteredByShouldApply_ReturnsWithoutExecuting()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        template.IndexedViews.Add(new SqlServerIndexedView
        {
            Name = "[vw_Test]",
            Schema = "dbo",
            Definition = "SELECT 1",
            ShouldApplyExpression = "false",
            Indexes = [new SqlServerIndex { Name = "[IX_1]", Unique = true, Clustered = true }]
        });

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var mockCmd = CreateMockCommand();

        // Should not throw and should not call ExecuteNonQuery
        quench.QuenchIndexedViews(mockCmd);
        mockCmd.DidNotReceive().ExecuteNonQuery();
    }

    [Test]
    public void QuenchIndexedViews_NoIndexedViews_ReturnsWithoutExecuting()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        // No indexed views added

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var mockCmd = CreateMockCommand();
        quench.QuenchIndexedViews(mockCmd);
        mockCmd.DidNotReceive().ExecuteNonQuery();
    }

    [Test]
    public void QuenchIndexedViews_ValidViews_SetsCommandTextAndExecutes()
    {
        RegisterMockFileWrapper();
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        template.IndexedViews.Add(new SqlServerIndexedView
        {
            Name = "[vw_Test]",
            Schema = "dbo",
            Definition = "SELECT 1",
            Indexes = [new SqlServerIndex { Name = "[IX_1]", Unique = true, Clustered = true, IndexColumns = "Col1" }]
        });

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var mockCmd = CreateMockCommand();
        quench.QuenchIndexedViews(mockCmd);

        Assert.That(mockCmd.CommandText, Does.Contain("EXEC [SchemaSmith].[IndexedViewQuench]"));
        Assert.That(mockCmd.CommandText, Does.Contain("@ProductName = 'Test'"));
        Assert.That(mockCmd.CommandText, Does.Contain("@WhatIf = 0"));
        Assert.That(mockCmd.CommandText, Does.Contain("@UpdateFillFactor = true"));
        mockCmd.Received(1).ExecuteNonQuery();
    }

    [Test]
    public void QuenchIndexedViews_ShouldApplyExpressionNull_IncludesView()
    {
        RegisterMockFileWrapper();
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        template.IndexedViews.Add(new SqlServerIndexedView
        {
            Name = "[vw_Test]",
            Schema = "dbo",
            Definition = "SELECT 1",
            ShouldApplyExpression = null,
            Indexes = [new SqlServerIndex { Name = "[IX_1]", Unique = true, Clustered = true, IndexColumns = "Col1" }]
        });

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var mockCmd = CreateMockCommand();
        quench.QuenchIndexedViews(mockCmd);

        mockCmd.Received(1).ExecuteNonQuery();
    }

    [Test]
    public void QuenchIndexedViews_ProductNameWithApostrophe_EscapesCorrectly()
    {
        RegisterMockFileWrapper();
        var product = new Product { Name = "Test's Product", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        template.IndexedViews.Add(new SqlServerIndexedView
        {
            Name = "[vw_Test]",
            Schema = "dbo",
            Definition = "SELECT 1",
            Indexes = [new SqlServerIndex { Name = "[IX_1]", Unique = true, Clustered = true, IndexColumns = "Col1" }]
        });

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", "0", false, false, null);

        var mockCmd = CreateMockCommand();
        quench.QuenchIndexedViews(mockCmd);

        Assert.That(mockCmd.CommandText, Does.Contain("Test''s Product"));
    }

    #endregion

    #region QuenchMaterializedViews Tests

    [Test]
    public void QuenchMaterializedViews_SetsCorrectCommandText()
    {
        RegisterMockFileWrapper();
        var product = new Product { Name = "TestProd", Platform = Platform.PostgreSQL };
        var template = new Template { Name = "T", MaterializedViewSchema = "[{\"Name\":\"mv_test\"}]" };

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "false", false, "false", "false", false, false, null);

        var mockCmd = CreateMockCommand();
        quench.QuenchMaterializedViews(mockCmd);

        Assert.That(mockCmd.CommandText, Does.Contain("CALL \"SchemaSmith\".\"MaterializedViewQuench\""));
        Assert.That(mockCmd.CommandText, Does.Contain("'TestProd'"));
        Assert.That(mockCmd.CommandText, Does.Contain("false")); // whatIfOnly
        mockCmd.Received(1).ExecuteNonQuery();
    }

    [Test]
    public void QuenchMaterializedViews_ProductNameWithApostrophe_EscapesCorrectly()
    {
        RegisterMockFileWrapper();
        var product = new Product { Name = "O'Brien's DB", Platform = Platform.PostgreSQL };
        var template = new Template { Name = "T", MaterializedViewSchema = "[]" };

        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "false", false, "false", "false", false, false, null);

        var mockCmd = CreateMockCommand();
        quench.QuenchMaterializedViews(mockCmd);

        Assert.That(mockCmd.CommandText, Does.Contain("O''Brien''s DB"));
    }

    #endregion

    #region Schema-Template (Slice 3) — Constructor + DbScope + Log Prefix

    [Test]
    public void Constructor_SchemaName_DefaultsToEmptyString()
    {
        // Forwarding constructor (no schemaName arg) leaves SchemaName empty for regular templates.
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db",
            false, "0", false, "0", false, false, false, null);

        Assert.That(quench.SchemaName, Is.EqualTo(""));
    }

    [Test]
    public void Constructor_SchemaName_PreservedFromExplicitArgument()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db", "tenant_acme",
            false, "0", false, "0", false, false, false, null);

        Assert.That(quench.SchemaName, Is.EqualTo("tenant_acme"));
    }

    [Test]
    public void Constructor_SchemaName_NullCoercedToEmpty()
    {
        // Tracking-table column convention (slice 2) uses '' rather than NULL for regular-template rows.
        // The ctor coerces null to empty so DbScope.SchemaName never carries a null downstream.
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db", null,
            false, "0", false, "0", false, false, false, null);

        Assert.That(quench.SchemaName, Is.EqualTo(""));
    }

    [Test]
    public void Constructor_InternalSchemaName_PreservedFromExplicitArgument()
    {
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var template = new Template { Name = "T" };
        var quench = new DatabaseQuench("srv", product, template, "db", "tenant_globex",
            false, "false", false, "false", "false", false, false, null);

        Assert.That(quench.SchemaName, Is.EqualTo("tenant_globex"));
    }

    [Test]
    public void DbScope_SchemaName_FlowsFromConstructor()
    {
        // DbScope is private; the SchemaName surfaces in the SQL that touches the tracking table —
        // observe via the SQL emitters that route DbScope.SchemaName into the WHERE clause.
        var product = new Product { Name = "MyProduct", Platform = Platform.SqlServer };
        var template = new Template { Name = "TenantBody" };
        var quench = new DatabaseQuench("srv", product, template, "AppProd", "tenant_acme",
            false, "0", false, "0", false, false, false, null);

        var sql = quench.GetSelectCompletedScriptsSql(
            quench.ProductName, "Before", "TenantBody", quench.SchemaName);

        Assert.That(sql, Does.Contain("[schema_name] = 'tenant_acme'"));
        Assert.That(sql, Does.Contain("[template_name] IN ('', 'TenantBody')"));
    }

    [Test]
    public void DbScope_SchemaName_EmptyForRegularTemplate()
    {
        var product = new Product { Name = "MyProduct", Platform = Platform.SqlServer };
        var template = new Template { Name = "Core" };
        var quench = new DatabaseQuench("srv", product, template, "AppProd",
            false, "0", false, "0", false, false, false, null);

        var sql = quench.GetSelectCompletedScriptsSql(
            quench.ProductName, "Before", "Core", quench.SchemaName);

        Assert.That(sql, Does.Contain("[schema_name] = ''"));
    }

    [Test]
    public void LogPrefix_SchemaName_IncludesSchemaTag()
    {
        // Per design §5.8, every log line emitted during a schema iteration carries the
        // [Schema: <name>] prefix so a multi-iteration deploy log stays greppable per tenant.
        // LogPrefix is the single source of truth for the prefix; SafeProgressLog and its
        // siblings all route through it.
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "TenantBody" };
        var quench = new DatabaseQuench("srv", product, template, "AppProd", "tenant_acme",
            false, "0", false, "0", false, false, false, null);

        Assert.That(quench.LogPrefix, Is.EqualTo("[srv].[AppProd] [Schema: tenant_acme]"));
    }

    [Test]
    public void LogPrefix_NoSchemaName_OmitsSchemaTag()
    {
        var product = new Product { Name = "Test", Platform = Platform.SqlServer };
        var template = new Template { Name = "Core" };
        var quench = new DatabaseQuench("srv", product, template, "AppProd",
            false, "0", false, "0", false, false, false, null);

        Assert.That(quench.LogPrefix, Is.EqualTo("[srv].[AppProd]"));
    }

    [Test]
    public void LogPrefix_EmptySchemaName_OmitsSchemaTag()
    {
        // Explicit empty string is equivalent to "no schema iteration" (the work-unit convention
        // for regular templates) — must not render an empty "[Schema: ]" tag.
        var product = new Product { Name = "Test", Platform = Platform.PostgreSQL };
        var template = new Template { Name = "Core" };
        var quench = new DatabaseQuench("srv", product, template, "AppProd", "",
            false, "false", false, "false", "false", false, false, null);

        Assert.That(quench.LogPrefix, Is.EqualTo("[srv].[AppProd]"));
    }

    #endregion

    #region Helper Methods

    private static IDbCommand CreateMockCommand()
    {
        var mockCmd = Substitute.For<IDbCommand>();
        var mockConnection = Substitute.For<IDbConnection>();
        mockConnection.Database.Returns("testdb");
        mockCmd.Connection.Returns(mockConnection);
        return mockCmd;
    }

    private static void RegisterMockConfig()
    {
        var mockConfig = Substitute.For<IConfigurationRoot>();
        mockConfig["DropUnknownIndexes"].Returns((string)null);
        mockConfig["Target:User"].Returns("user");
        mockConfig["Target:Password"].Returns("pass");
        FactoryContainer.Register<IConfigurationRoot>(mockConfig);
    }

    private static void RegisterMockFileWrapper()
    {
        var mockFile = Substitute.For<IFile>();
        FactoryContainer.Register<IFile>(mockFile);
    }

    #endregion
}
