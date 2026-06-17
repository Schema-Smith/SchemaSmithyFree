// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// Validates the slice-2 migration of SchemaSmith_CompletedMigrationScripts on MySQL. MySQL is
/// excluded from the schema-template fan-out feature itself, but its tracking table still gains
/// the two new columns so the codebase only carries one shape of the table.
/// </summary>
[Category("MySQL")]
[Category("Integration")]
[TestFixture]
public class MigrationTrackingBackwardCompatTests
{
    private IDbConnection _connection = null!;
    private IDbCommand _command = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.MySQL)
            .GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
        _command = _connection.CreateCommand();

        DropAndRecreateLegacyTable();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            _command.CommandText = "DROP TABLE IF EXISTS `SchemaSmith_CompletedMigrationScripts`";
            _command.ExecuteNonQuery();
            // force: version-gated kindle would otherwise skip after the fixture's initial kindle
            ForgeKindler.KindleTheForge(_command, Platform.MySQL, forceReKindle: true);
        }
        finally
        {
            _command.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }

    private void DropAndRecreateLegacyTable()
    {
        _command.CommandText = "DROP TABLE IF EXISTS `SchemaSmith_CompletedMigrationScripts`";
        _command.ExecuteNonQuery();
        _command.CommandText = @"
            CREATE TABLE `SchemaSmith_CompletedMigrationScripts` (
                `Id` INT AUTO_INCREMENT PRIMARY KEY,
                `ProductName` VARCHAR(100) NOT NULL,
                `QuenchSlot` VARCHAR(50) NOT NULL,
                `ScriptPath` VARCHAR(500) NOT NULL,
                `CompletedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY `uk_script` (`ProductName`, `QuenchSlot`, `ScriptPath`(255))
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
        _command.ExecuteNonQuery();
    }

    [Test]
    public void KindleTheForge_LegacyTableMissingColumns_AddsTemplateNameAndSchemaName()
    {
        _command.CommandText = @"
            INSERT INTO `SchemaSmith_CompletedMigrationScripts` (`ProductName`, `QuenchSlot`, `ScriptPath`)
            VALUES ('Demo', 'Before', 'Before Scripts/Migration_001.sql')";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.MySQL, forceReKindle: true);

        _command.CommandText = $@"
            SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
              AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'
              AND COLUMN_NAME IN ('template_name', 'schema_name')
            ORDER BY COLUMN_NAME";
        using var reader = _command.ExecuteReader();
        var found = new System.Collections.Generic.List<string>();
        while (reader.Read()) found.Add(reader.GetString(0));
        reader.Close();
        Assert.That(found, Is.EquivalentTo(new[] { "schema_name", "template_name" }));
    }

    [Test]
    public void KindleTheForge_LegacyRow_HasBlankTemplateNameAndSchemaName_AfterUpgrade()
    {
        _command.CommandText = @"
            INSERT INTO `SchemaSmith_CompletedMigrationScripts` (`ProductName`, `QuenchSlot`, `ScriptPath`)
            VALUES ('Demo', 'Before', 'Before Scripts/Migration_001.sql')";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.MySQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT template_name, schema_name FROM `SchemaSmith_CompletedMigrationScripts`
            WHERE `ScriptPath` = 'Before Scripts/Migration_001.sql'";
        using var reader = _command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetString(0), Is.EqualTo(""));
        Assert.That(reader.GetString(1), Is.EqualTo(""));
    }

    [Test]
    public void PermissiveTemplateLookup_LegacyRow_MatchesNewSameTemplateRead()
    {
        _command.CommandText = @"
            INSERT INTO `SchemaSmith_CompletedMigrationScripts` (`ProductName`, `QuenchSlot`, `ScriptPath`)
            VALUES ('Demo', 'Before', 'Before Scripts/Migration_001.sql')";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.MySQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT COUNT(*) FROM `SchemaSmith_CompletedMigrationScripts`
            WHERE `ProductName` = 'Demo'
              AND `QuenchSlot` = 'Before'
              AND `ScriptPath` = 'Before Scripts/Migration_001.sql'
              AND template_name IN ('', 'Main')
              AND schema_name = ''";
        Assert.That(System.Convert.ToInt64(_command.ExecuteScalar()), Is.EqualTo(1));
    }

    [Test]
    public void KindleTheForge_RunTwice_RemainsIdempotent()
    {
        _command.CommandText = @"
            INSERT INTO `SchemaSmith_CompletedMigrationScripts` (`ProductName`, `QuenchSlot`, `ScriptPath`)
            VALUES ('Demo', 'Before', 'Before Scripts/Migration_001.sql')";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.MySQL, forceReKindle: true);
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(_command, Platform.MySQL));

        _command.CommandText = $@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = '{FixtureSetup.MainDb}'
              AND TABLE_NAME = 'SchemaSmith_CompletedMigrationScripts'
              AND COLUMN_NAME IN ('template_name', 'schema_name')";
        Assert.That(System.Convert.ToInt64(_command.ExecuteScalar()), Is.EqualTo(2));

        _command.CommandText = @"
            SELECT COUNT(*) FROM `SchemaSmith_CompletedMigrationScripts`
            WHERE `ScriptPath` = 'Before Scripts/Migration_001.sql'";
        Assert.That(System.Convert.ToInt64(_command.ExecuteScalar()), Is.EqualTo(1));
    }
}
