// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.Linq;
using NSubstitute;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

public class ForgeKindlerTests
{
    [Test]
    public void GetKindlingScriptNames_SqlServer_ReturnsExpectedScripts()
    {
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.SqlServer);
        Assert.That(scripts, Is.Not.Empty);
        Assert.That(scripts, Does.Contain("Kindling_SchemaSmith_Schema.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.BootstrapTableQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.fn_StripParenWrapping.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.fn_StripBracketWrapping.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.fn_SafeBracketWrap.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.PrintWithNoWait.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.MissingTableAndColumnQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ModifiedTableQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.MissingIndexesAndConstraintsQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ForeignKeyQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.TableQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.IndexOnlyQuench.sql"));
        Assert.That(scripts, Does.Contain("Kindling_CompletedMigrationScripts_Table.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.fn_FormatJson.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.GenerateTableJson.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ValidateIndexedViewOwnership.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.FixupIndexedViewOwnership.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.IndexedViewQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.GenerateIndexedViewJson.sql"));
    }

    [Test]
    public void GetKindlingScriptNames_SqlServer_BootstrapAndKindlingTableComeBeforeTableQuench()
    {
        // BootstrapTableQuench creates the proc; the kindling table call uses it.
        // Both must come before TableQuench (which doesn't depend on either) so the
        // pipeline shape is: schema -> bootstrap proc -> kindling tables -> utility procs.
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.SqlServer);
        var bootstrapIdx = Array.IndexOf(scripts, "SchemaSmith.BootstrapTableQuench.sql");
        var kindlingTableIdx = Array.IndexOf(scripts, "Kindling_CompletedMigrationScripts_Table.sql");
        var tableQuenchIdx = Array.IndexOf(scripts, "SchemaSmith.TableQuench.sql");

        Assert.That(bootstrapIdx, Is.GreaterThanOrEqualTo(0), "BootstrapTableQuench must be in the script list.");
        Assert.That(bootstrapIdx, Is.LessThan(kindlingTableIdx),
            "BootstrapTableQuench must be created before the kindling table that uses it.");
        Assert.That(kindlingTableIdx, Is.LessThan(tableQuenchIdx),
            "Kindling table must be created via Bootstrap before TableQuench is created.");
    }

    [Test]
    public void GetKindlingScriptNames_PostgreSQL_ReturnsExpectedScripts()
    {
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.PostgreSQL);
        Assert.That(scripts, Is.Not.Empty);
        Assert.That(scripts, Does.Contain("Kindling_SchemaSmith_Schema.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.BootstrapTableQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ExecuteOrDebug.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.QuoteColumnList.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.QuoteIndexColumnList.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.StripParenWrapping.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ValidateTableOwnership.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.FixupTableOwnership.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.FixupIndexOwnership.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.MissingTableAndColumnQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ModifiedTableQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.MissingIndexesAndConstraintsQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ForeignKeyQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.TableQuench.sql"));
        Assert.That(scripts, Does.Contain("Kindling_ProductOwnership_Table.sql"));
        Assert.That(scripts, Does.Contain("Kindling_CompletedMigrationScripts_Table.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.IndexOnlyQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.FormatJson.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.GenerateTableJson.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.ValidateMaterializedViewOwnership.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.FixupMaterializedViewOwnership.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.MissingMaterializedViewIndexesQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith.MaterializedViewQuench.sql"));
    }

    [Test]
    public void GetKindlingScriptNames_PostgreSQL_BootstrapAndKindlingTablesComeBeforeOwnershipProcs()
    {
        // ProductOwnership table must be created before ValidateTableOwnership (which reads it).
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.PostgreSQL);
        var bootstrapIdx = Array.IndexOf(scripts, "SchemaSmith.BootstrapTableQuench.sql");
        var productOwnershipIdx = Array.IndexOf(scripts, "Kindling_ProductOwnership_Table.sql");
        var validateOwnershipIdx = Array.IndexOf(scripts, "SchemaSmith.ValidateTableOwnership.sql");

        Assert.That(bootstrapIdx, Is.LessThan(productOwnershipIdx),
            "BootstrapTableQuench must be created before the ProductOwnership kindling call.");
        Assert.That(productOwnershipIdx, Is.LessThan(validateOwnershipIdx),
            "ProductOwnership table must exist before ValidateTableOwnership proc is created.");
    }

    [Test]
    public void GetKindlingScriptNames_MySQL_ReturnsExpectedScripts()
    {
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.MySQL);
        Assert.That(scripts, Is.Not.Empty);
        Assert.That(scripts, Does.Contain("SchemaSmith_BootstrapTableQuench.sql"));
        Assert.That(scripts, Does.Contain("Kindling_CompletedMigrationScripts_Table.sql"));
        Assert.That(scripts, Does.Contain("Kindling_ProductOwnership_Table.sql"));
        Assert.That(scripts, Does.Contain("Kindling_StatusMessages_Table.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_QuoteIdentifier.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_StripBacktickWrapping.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_SafeBacktickWrap.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_NormalizeIndexColumns.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_GenerateTableJson.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_ParseTableJson.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_MissingTableAndColumnQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_ModifiedTableQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_MissingIndexesAndConstraintsQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_ForeignKeyQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_IndexOnlyQuench.sql"));
        Assert.That(scripts, Does.Contain("SchemaSmith_TableQuench.sql"));
    }

    [Test]
    public void GetKindlingScriptNames_MySQL_DoesNotContainDeletedAlterScript()
    {
        // BootstrapTableQuench's ADD COLUMN IF NOT EXISTS / CREATE INDEX IF NOT EXISTS pattern
        // subsumed Kindling_AlterCompletedMigrationScripts.sql. The file must be gone from the
        // ordered kindling script list.
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.MySQL);
        Assert.That(scripts, Does.Not.Contain("Kindling_AlterCompletedMigrationScripts.sql"),
            "Kindling_AlterCompletedMigrationScripts.sql was deleted by the BootstrapTableQuench refactor.");
    }

    [Test]
    public void GetKindlingScriptNames_MySQL_BootstrapPrecedesAllKindlingTables()
    {
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.MySQL);
        var bootstrapIdx = Array.IndexOf(scripts, "SchemaSmith_BootstrapTableQuench.sql");
        var completedIdx = Array.IndexOf(scripts, "Kindling_CompletedMigrationScripts_Table.sql");
        var ownershipIdx = Array.IndexOf(scripts, "Kindling_ProductOwnership_Table.sql");
        var statusIdx = Array.IndexOf(scripts, "Kindling_StatusMessages_Table.sql");

        Assert.That(bootstrapIdx, Is.LessThan(completedIdx),
            "BootstrapTableQuench must precede CompletedMigrationScripts kindling.");
        Assert.That(bootstrapIdx, Is.LessThan(ownershipIdx),
            "BootstrapTableQuench must precede ProductOwnership kindling.");
        Assert.That(bootstrapIdx, Is.LessThan(statusIdx),
            "BootstrapTableQuench must precede StatusMessages kindling.");
    }

    [Test]
    public void GetKindlingScriptNames_AllPlatforms_HaveUniqueScripts()
    {
        var sqlServer = ForgeKindler.GetKindlingScriptNames(Platform.SqlServer);
        var postgres = ForgeKindler.GetKindlingScriptNames(Platform.PostgreSQL);
        var mysql = ForgeKindler.GetKindlingScriptNames(Platform.MySQL);

        // SqlServer: 20 = 19 prior + 1 for KindleStamp.
        Assert.That(sqlServer.Length, Is.EqualTo(20));
        // PostgreSQL: 24 = 23 prior + 1 for KindleStamp.
        Assert.That(postgres.Length, Is.EqualTo(24));
        // MySQL: 17 = 16 prior + 1 for KindleStamp.
        Assert.That(mysql.Length, Is.EqualTo(17));
    }

    [Test]
    public void GetKindlingScripts_KindleStamp_FollowsBootstrapAndCarriesTableDef()
    {
        foreach (var platform in new[] { Platform.SqlServer, Platform.PostgreSQL, Platform.MySQL })
        {
            var scripts = ForgeKindler.GetKindlingScripts(platform);
            var names = scripts.Select(s => s.FileName).ToArray();
            var bootstrapIdx = Array.FindIndex(names, n => n.Contains("BootstrapTableQuench"));
            var stampIdx = Array.IndexOf(names, "Kindling_KindleStamp_Table.sql");

            Assert.That(stampIdx, Is.GreaterThanOrEqualTo(0), $"KindleStamp must be kindled on {platform}.");
            Assert.That(bootstrapIdx, Is.LessThan(stampIdx),
                $"BootstrapTableQuench must precede the KindleStamp table call on {platform}.");
            Assert.That(scripts[stampIdx].ReplaceTableDef, Is.True,
                $"KindleStamp table call must substitute its sibling JSON on {platform}.");
        }
    }

    [Test]
    public void KindleTheForge_ThrowsForUnsupportedPlatform()
    {
        var mockCmd = Substitute.For<IDbCommand>();
        // Cast an invalid int to Platform to test the default case
        var invalidPlatform = (Platform)99;
        Assert.Throws<ArgumentException>(() => ForgeKindler.KindleTheForge(mockCmd, invalidPlatform));
    }

    [Test]
    public void KindleOneFile_WrapsExceptionWithFileName()
    {
        var mockCmd = Substitute.For<IDbCommand>();
        // This will fail because the embedded resource won't be found for a fake file name
        var ex = Assert.Throws<Exception>(() =>
            ForgeKindler.KindleOneFile(mockCmd, "NonExistentScript.sql", Platform.SqlServer));
        Assert.That(ex.Message, Does.Contain("Error occurred while kindling 'NonExistentScript.sql'"));
    }

    [Test]
    public void KindleOneFile_ThrowsWhenScriptNotFound()
    {
        var mockCmd = Substitute.For<IDbCommand>();
        var ex = Assert.Throws<Exception>(() =>
            ForgeKindler.KindleOneFile(mockCmd, "NotAReal_Script_File.sql", Platform.PostgreSQL));
        Assert.That(ex.Message, Does.Contain("kindling"));
    }

    [Test]
    public void GetSiblingTableDefJson_SqlServer_LoadsCompletedMigrationScriptsJson()
    {
        // The sibling JSON for Kindling_CompletedMigrationScripts_Table.sql lives next to
        // it under Schema/Scripts/SqlServer/Kindling_CompletedMigrationScripts.json.
        var json = ForgeKindler.GetSiblingTableDefJson(
            "Kindling_CompletedMigrationScripts_Table.sql", Platform.SqlServer);
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("\"Schema\""));
        Assert.That(json, Does.Contain("[CompletedMigrationScripts]"));
        Assert.That(json, Does.Contain("[IX_CompletedMigrationScripts_Slot_Scope]"),
            "Secondary index from Commit B must live in the shared JSON.");
    }

    [Test]
    public void GetSiblingTableDefJson_PostgreSQL_LoadsCompletedMigrationScriptsJson()
    {
        var json = ForgeKindler.GetSiblingTableDefJson(
            "Kindling_CompletedMigrationScripts_Table.sql", Platform.PostgreSQL);
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("CompletedMigrationScripts"));
        Assert.That(json, Does.Contain("ix_completedmigrationscripts_slot_scope"),
            "Secondary index from Commit B must live in the shared JSON.");
    }

    [Test]
    public void GetSiblingTableDefJson_PostgreSQL_LoadsProductOwnershipJson()
    {
        var json = ForgeKindler.GetSiblingTableDefJson(
            "Kindling_ProductOwnership_Table.sql", Platform.PostgreSQL);
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("ProductOwnership"));
        Assert.That(json, Does.Contain("template_name"),
            "Slice-3 template_name column must live in the shared JSON.");
    }

    [Test]
    public void GetSiblingTableDefJson_MySQL_LoadsAllThreeKindlingJsons()
    {
        var completed = ForgeKindler.GetSiblingTableDefJson(
            "Kindling_CompletedMigrationScripts_Table.sql", Platform.MySQL);
        Assert.That(completed, Does.Contain("SchemaSmith_CompletedMigrationScripts"));
        Assert.That(completed, Does.Contain("ix_completedmigrationscripts_slot_scope"),
            "Commit B's secondary index must live in the shared JSON (not in a deleted Alter script).");

        var ownership = ForgeKindler.GetSiblingTableDefJson(
            "Kindling_ProductOwnership_Table.sql", Platform.MySQL);
        Assert.That(ownership, Does.Contain("SchemaSmith_ProductOwnership"));

        var status = ForgeKindler.GetSiblingTableDefJson(
            "Kindling_StatusMessages_Table.sql", Platform.MySQL);
        Assert.That(status, Does.Contain("SchemaSmith_StatusMessages"));
    }

    [Test]
    public void GetSiblingTableDefJson_ThrowsWhenJsonMissing()
    {
        // Any kindling script whose name doesn't match a real sibling JSON resource should
        // throw (the message names both files so the maintainer can fix it fast).
        var ex = Assert.Throws<Exception>(() =>
            ForgeKindler.GetSiblingTableDefJson("Kindling_NoSuchThing_Table.sql", Platform.SqlServer));
        Assert.That(ex.Message, Does.Contain("Kindling_NoSuchThing.json"));
    }

    [Test]
    public void GetKindlingScripts_SqlServer_FlagsTableQuenchForParseJsonSubstitution()
    {
        var scripts = ForgeKindler.GetKindlingScripts(Platform.SqlServer);
        var tableQuench = scripts.Single(s => s.FileName == "SchemaSmith.TableQuench.sql");
        Assert.That(tableQuench.ReplaceParseJson, Is.True);
        Assert.That(tableQuench.ReplaceTableDef, Is.False);

        var kindlingTable = scripts.Single(s => s.FileName == "Kindling_CompletedMigrationScripts_Table.sql");
        Assert.That(kindlingTable.ReplaceTableDef, Is.True);
        Assert.That(kindlingTable.ReplaceParseJson, Is.False);
    }

    [Test]
    public void GetKindlingScripts_TableQuenchParseJsonFlag_IsSetForPgButNotMySql()
    {
        var pg = ForgeKindler.GetKindlingScripts(Platform.PostgreSQL)
            .Single(s => s.FileName == "SchemaSmith.TableQuench.sql");
        Assert.That(pg.ReplaceParseJson, Is.True, "PostgreSQL TableQuench embeds the ParseJson body.");
        Assert.That(pg.ReplaceTableDef, Is.False);

        var mysql = ForgeKindler.GetKindlingScripts(Platform.MySQL)
            .Single(s => s.FileName == "SchemaSmith_TableQuench.sql");
        Assert.That(mysql.ReplaceParseJson, Is.False, "MySQL TableQuench has no ParseJson token.");
        Assert.That(mysql.ReplaceTableDef, Is.False);
    }

    [Test]
    public void GetKindlingScriptNames_IsDerivedFromDescriptors()
    {
        foreach (var platform in new[] { Platform.SqlServer, Platform.PostgreSQL, Platform.MySQL })
        {
            var names = ForgeKindler.GetKindlingScriptNames(platform);
            var descriptorNames = ForgeKindler.GetKindlingScripts(platform).Select(s => s.FileName).ToArray();
            Assert.That(names, Is.EqualTo(descriptorNames), $"Names must derive from descriptors for {platform}.");
        }
    }

    [Test]
    public void KindleOneFile_WithTableDefToken_PullsContentFromSiblingJsonResource()
    {
        // Smoke test: when replaceTableDefToken is true, the loader must substitute the
        // sibling JSON content. We exercise this by attempting to kindle the real
        // Kindling_CompletedMigrationScripts_Table.sql against a mock command; the call
        // builds the SQL string but EXEC fails (no real DB) — we catch the wrapping
        // exception and assert the JSON content was substituted in.
        var mockCmd = Substitute.For<IDbCommand>();
        string capturedSql = null;
        mockCmd.When(c => c.ExecuteNonQuery()).Do(_ => capturedSql = mockCmd.CommandText);

        ForgeKindler.KindleOneFile(mockCmd, "Kindling_CompletedMigrationScripts_Table.sql",
            Platform.SqlServer, replaceTableDefToken: true);

        Assert.That(capturedSql, Is.Not.Null);
        Assert.That(capturedSql, Does.Not.Contain("{{TableDef}}"),
            "Token must be substituted out of the executed SQL.");
        Assert.That(capturedSql, Does.Contain("[CompletedMigrationScripts]"),
            "Substituted SQL must contain the table name from the sibling JSON.");
        Assert.That(capturedSql, Does.Contain("[IX_CompletedMigrationScripts_Slot_Scope]"),
            "Substituted SQL must include the Commit-B secondary index name from the JSON.");
    }

    [Test]
    public void ResolveKindleScript_TableQuench_EmbedsParseJsonSource()
    {
        var resolved = ForgeKindler.ResolveKindleScript(
            "SchemaSmith.TableQuench.sql", Platform.SqlServer, replaceParseJson: true, replaceTableDef: false);

        Assert.That(resolved, Does.Not.Contain("{{ParseJson}}"), "Token must be substituted out.");
        // A distinctive line that only exists in ParseTableJsonIntoTempTables.sql:
        Assert.That(resolved, Does.Contain("Parse Tables from Json"),
            "Resolved TableQuench must contain the ParseJson source body, so a change there changes the hash.");
    }
}
