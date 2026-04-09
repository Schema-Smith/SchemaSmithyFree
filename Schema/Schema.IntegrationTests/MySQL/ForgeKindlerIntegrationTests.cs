// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;

using NUnit.Framework;
using Schema.Utility;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for ForgeKindler against a live MySQL database.
/// Uses dynamically created test databases via FixtureSetup.
/// Note: ForgeKindler is deployed once by FixtureSetup - these tests verify the deployment.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class ForgeKindlerIntegrationTests
{
    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public void KindleTheForge_CreatesCompletedMigrationScriptsTable()
    {
        // ForgeKindler is already deployed by FixtureSetup - verify the table exists in target database
        using var command = _connection.CreateCommand();

        command.CommandText = $@"
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
            AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'";
        var result = command.ExecuteScalar();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ToString(), Is.EqualTo("SchemaSmith_CompletedMigrationScripts"));
    }

    [Test]
    public void KindleTheForge_CreatesGenerateTableJSONProcedure()
    {
        // ForgeKindler is already deployed by FixtureSetup - verify the procedure exists in target database
        using var command = _connection.CreateCommand();

        command.CommandText = $@"
            SELECT ROUTINE_NAME
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_SCHEMA = '{FixtureSetup.MainDb}'
            AND ROUTINE_NAME = 'SchemaSmith_GenerateTableJSON'
            AND ROUTINE_TYPE = 'PROCEDURE'";
        var result = command.ExecuteScalar();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ToString(), Is.EqualTo("SchemaSmith_GenerateTableJSON"));
    }

    [Test]
    public void KindleTheForge_CanBeRunMultipleTimes()
    {
        // TestUser has SYSTEM_USER privilege via docker init script
        using var freshConnection = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        freshConnection.Open();
        using var command = freshConnection.CreateCommand();

        // Should not throw on multiple runs (idempotent)
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(command, Platform.MySQL));
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(command, Platform.MySQL));
    }
}
