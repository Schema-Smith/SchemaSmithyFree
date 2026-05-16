// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
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
    public void GetKindlingScriptNames_PostgreSQL_ReturnsExpectedScripts()
    {
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.PostgreSQL);
        Assert.That(scripts, Is.Not.Empty);
        Assert.That(scripts, Does.Contain("Kindling_SchemaSmith_Schema.sql"));
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
    public void GetKindlingScriptNames_MySQL_ReturnsExpectedScripts()
    {
        var scripts = ForgeKindler.GetKindlingScriptNames(Platform.MySQL);
        Assert.That(scripts, Is.Not.Empty);
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
    public void GetKindlingScriptNames_AllPlatforms_HaveUniqueScripts()
    {
        var sqlServer = ForgeKindler.GetKindlingScriptNames(Platform.SqlServer);
        var postgres = ForgeKindler.GetKindlingScriptNames(Platform.PostgreSQL);
        var mysql = ForgeKindler.GetKindlingScriptNames(Platform.MySQL);

        Assert.That(sqlServer.Length, Is.EqualTo(18)); // was 14, adding 4 indexed view scripts
        Assert.That(postgres.Length, Is.EqualTo(22));
        Assert.That(mysql.Length, Is.EqualTo(16)); // +1 for Kindling_AlterCompletedMigrationScripts.sql (slice 2)
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
}
