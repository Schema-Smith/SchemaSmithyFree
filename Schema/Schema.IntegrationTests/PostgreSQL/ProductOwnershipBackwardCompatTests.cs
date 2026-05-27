// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// Validates the slice-3 audit B1 migration of SchemaSmith.ProductOwnership on PostgreSQL.
/// The kindling DDL must add the template_name column idempotently to a pre-extension
/// table without losing data, and legacy rows must end up with template_name = ''.
/// </summary>
[Category("PostgreSQL")]
[Category("Integration")]
[TestFixture]
public class ProductOwnershipBackwardCompatTests
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
            _command.CommandText = "DROP TABLE IF EXISTS \"SchemaSmith\".\"ProductOwnership\"";
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
        _command.CommandText = @"DROP TABLE IF EXISTS ""SchemaSmith"".""ProductOwnership""";
        _command.ExecuteNonQuery();
        // Recreate at the pre-slice-3 shape (4-column PK, no template_name column).
        _command.CommandText = @"
            CREATE TABLE ""SchemaSmith"".""ProductOwnership"" (
                ""Schema""      VARCHAR(256) NOT NULL,
                ""TableName""   VARCHAR(256) NOT NULL,
                ""IndexName""   VARCHAR(256) NULL,
                ""ProductName"" VARCHAR(100) NOT NULL,
                CONSTRAINT ""PK_ProductOwnership"" UNIQUE (""Schema"", ""TableName"", ""IndexName"", ""ProductName"")
            );";
        _command.ExecuteNonQuery();
    }

    [Test]
    public void KindleTheForge_LegacyTableMissingColumn_AddsTemplateName()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""ProductOwnership"" (""Schema"", ""TableName"", ""IndexName"", ""ProductName"")
            VALUES ('public', 'legacy_table', NULL, 'Demo');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'SchemaSmith' AND table_name = 'ProductOwnership'
              AND column_name = 'template_name';";
        using var reader = _command.ExecuteReader();
        Assert.That(reader.Read(), Is.True, "template_name column must exist after kindling.");
    }

    [Test]
    public void KindleTheForge_LegacyRow_HasBlankTemplateName_AfterUpgrade()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""ProductOwnership"" (""Schema"", ""TableName"", ""IndexName"", ""ProductName"")
            VALUES ('public', 'legacy_table', NULL, 'Demo');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT template_name FROM ""SchemaSmith"".""ProductOwnership""
            WHERE ""Schema"" = 'public' AND ""TableName"" = 'legacy_table' AND ""ProductName"" = 'Demo';";
        using var reader = _command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetString(0), Is.EqualTo(""));
    }

    [Test]
    public void PermissiveTemplateLookup_LegacyRow_MatchesSameProductReadWithAnyTemplate()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""ProductOwnership"" (""Schema"", ""TableName"", ""IndexName"", ""ProductName"")
            VALUES ('public', 'legacy_table', NULL, 'Demo');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            SELECT COUNT(*) FROM ""SchemaSmith"".""ProductOwnership""
            WHERE ""ProductName"" = 'Demo'
              AND ""Schema"" = 'public'
              AND ""TableName"" = 'legacy_table'
              AND ""IndexName"" IS NULL
              AND template_name IN ('', 'Shared');";
        var count = System.Convert.ToInt64(_command.ExecuteScalar());
        Assert.That(count, Is.EqualTo(1),
            "Legacy template_name='' row must match permissive IN ('', @template) lookup.");
    }

    [Test]
    public void NewWrites_PopulateActualTemplateName()
    {
        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);

        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""ProductOwnership""
                (""Schema"", ""TableName"", ""IndexName"", ""ProductName"", template_name)
            VALUES ('public', 'new_table', NULL, 'Demo', 'Shared');";
        _command.ExecuteNonQuery();

        _command.CommandText = @"
            SELECT template_name FROM ""SchemaSmith"".""ProductOwnership""
            WHERE ""Schema"" = 'public' AND ""TableName"" = 'new_table' AND ""ProductName"" = 'Demo';";
        using var reader = _command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetString(0), Is.EqualTo("Shared"));
    }

    [Test]
    public void KindleTheForge_RunTwice_RemainsIdempotent()
    {
        _command.CommandText = @"
            INSERT INTO ""SchemaSmith"".""ProductOwnership"" (""Schema"", ""TableName"", ""IndexName"", ""ProductName"")
            VALUES ('public', 'legacy_table', NULL, 'Demo');";
        _command.ExecuteNonQuery();

        // force: version-gated kindle would otherwise skip after the fixture's initial kindle
        ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL, forceReKindle: true);
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(_command, Platform.PostgreSQL));

        _command.CommandText = @"
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = 'SchemaSmith' AND table_name = 'ProductOwnership'
              AND column_name = 'template_name';";
        Assert.That(System.Convert.ToInt64(_command.ExecuteScalar()), Is.EqualTo(1));

        _command.CommandText = @"
            SELECT COUNT(*) FROM ""SchemaSmith"".""ProductOwnership""
            WHERE ""Schema"" = 'public' AND ""TableName"" = 'legacy_table' AND ""ProductName"" = 'Demo';";
        Assert.That(System.Convert.ToInt64(_command.ExecuteScalar()), Is.EqualTo(1));
    }
}
