// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// Validates the slice-2 migration of SchemaSmith.CompletedMigrationScripts on PostgreSQL.
/// </summary>
[Category("PostgreSQL")]
[Category("Integration")]
[TestFixture]
public class MigrationTrackingBackwardCompatTests
{
    private IDbConnection _connection = null!;
    private IDbCommand _command = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.PostgreSQL)
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
            _command.CommandText = "DROP TABLE IF EXISTS \"SchemaSmith\".\"CompletedMigrationScripts\"";
            _command.ExecuteNonQuery();
            // force: version-gated kindle would otherwise skip after the fixture's initial kindle
            ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);
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
        _command.CommandText = @"DROP TABLE IF EXISTS ""SchemaSmith"".""CompletedMigrationScripts""";
        _command.ExecuteNonQuery();
        _command.CommandText = @"
            CREATE TABLE ""SchemaSmith"".""CompletedMigrationScripts"" (
                ""ScriptPath""  VARCHAR(800) NOT NULL,
                ""ProductName"" VARCHAR(100) NOT NULL,
                ""QuenchSlot""  VARCHAR(30)  NOT NULL,
                ""QuenchDate""  TIMESTAMP    NOT NULL DEFAULT NOW(),
                CONSTRAINT ""PK_CompletedMigrationScripts"" PRIMARY KEY (""ScriptPath"", ""ProductName"", ""QuenchSlot"")
            );";
        _command.ExecuteNonQuery();
    }

    [Test]
    public void KindleTheForge_LegacyTableMissingColumns_AddsTemplateNameAndSchemaName()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""CompletedMigrationScripts"" (""ScriptPath"", ""ProductName"", ""QuenchSlot"")
            VALUES ('Before Scripts/Migration_001.sql', 'Demo', 'Before');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'SchemaSmith' AND table_name = 'CompletedMigrationScripts'
              AND column_name IN ('template_name', 'schema_name')
            ORDER BY column_name;";
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
            INSERT INTO ""SchemaSmith"".""CompletedMigrationScripts"" (""ScriptPath"", ""ProductName"", ""QuenchSlot"")
            VALUES ('Before Scripts/Migration_001.sql', 'Demo', 'Before');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT template_name, schema_name FROM ""SchemaSmith"".""CompletedMigrationScripts""
            WHERE ""ScriptPath"" = 'Before Scripts/Migration_001.sql';";
        using var reader = _command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetString(0), Is.EqualTo(""));
        Assert.That(reader.GetString(1), Is.EqualTo(""));
    }

    [Test]
    public void PermissiveTemplateLookup_LegacyRow_MatchesNewSameTemplateRead()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""CompletedMigrationScripts"" (""ScriptPath"", ""ProductName"", ""QuenchSlot"")
            VALUES ('Before Scripts/Migration_001.sql', 'Demo', 'Before');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT COUNT(*) FROM ""SchemaSmith"".""CompletedMigrationScripts""
            WHERE ""ProductName"" = 'Demo'
              AND ""QuenchSlot"" = 'Before'
              AND ""ScriptPath"" = 'Before Scripts/Migration_001.sql'
              AND template_name IN ('', 'Shared')
              AND schema_name = '';";
        var count = System.Convert.ToInt64(_command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(1), "Legacy template_name='' must match permissive IN ('', @template).");
    }

    [Test]
    public void StrictSchemaLookup_LegacyBlankSchemaRow_DoesNotShadowNewTenantLookup()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""CompletedMigrationScripts"" (""ScriptPath"", ""ProductName"", ""QuenchSlot"")
            VALUES ('Before Scripts/Migration_001.sql', 'Demo', 'Before');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT COUNT(*) FROM ""SchemaSmith"".""CompletedMigrationScripts""
            WHERE ""ProductName"" = 'Demo'
              AND ""QuenchSlot"" = 'Before'
              AND ""ScriptPath"" = 'Before Scripts/Migration_001.sql'
              AND template_name IN ('', 'TenantBody')
              AND schema_name = 'tenant_acme';";
        var count = System.Convert.ToInt64(_command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(0),
            "Strict schema_name = @schema must NOT match a legacy blank-schema row when looking up a tenant.");
    }

    [Test]
    public void NewWrites_PopulateActualTemplateAndSchemaValues()
    {
        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""CompletedMigrationScripts""
                (""ScriptPath"", ""ProductName"", ""QuenchSlot"", template_name, schema_name)
            VALUES ('Before Scripts/Migration_001.sql', 'Demo', 'Before', 'TenantBody', 'tenant_acme');";
        _command.ExecuteNonQuery();

        _command.CommandText = @"
            SELECT template_name, schema_name FROM ""SchemaSmith"".""CompletedMigrationScripts""
            WHERE ""ScriptPath"" = 'Before Scripts/Migration_001.sql' AND template_name = 'TenantBody';";
        using var reader = _command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetString(0), Is.EqualTo("TenantBody"));
        Assert.That(reader.GetString(1), Is.EqualTo("tenant_acme"));
    }

    [Test]
    public void KindleTheForge_RunTwice_RemainsIdempotent()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""CompletedMigrationScripts"" (""ScriptPath"", ""ProductName"", ""QuenchSlot"")
            VALUES ('Before Scripts/Migration_001.sql', 'Demo', 'Before');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL));

        _command.CommandText = @"
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = 'SchemaSmith' AND table_name = 'CompletedMigrationScripts'
              AND column_name IN ('template_name', 'schema_name');";
        Assert.That(System.Convert.ToInt64(_command.ExecuteScalar()), Is.EqualTo(2));

        _command.CommandText = @"
            SELECT COUNT(*) FROM ""SchemaSmith"".""CompletedMigrationScripts""
            WHERE ""ScriptPath"" = 'Before Scripts/Migration_001.sql';";
        Assert.That(System.Convert.ToInt64(_command.ExecuteScalar()), Is.EqualTo(1));
    }
}
