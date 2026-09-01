// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.SqlServer;

/// <summary>
/// A column change on a table that a SCHEMABINDING module references — gap item I1, the last of the five
/// under <see href="https://github.com/Schema-Smith/SchemaSmith/issues/323">#323</see>.
/// <para>SQL Server refuses the ALTER with <b>4922</b> ("one or more objects access this column"), and
/// <c>sys.sql_expression_dependencies</c> can enumerate the blockers before the attempt. SchemaSmith
/// already drops and recreates full-text indexes, indexes and foreign keys around column changes, and
/// <c>IndexedViewQuench</c> handles schemabound indexed views — so this is generalising an existing
/// pattern rather than inventing one.</para>
/// <para>This fixture establishes what actually happens today, so the fix is measured against observed
/// behaviour rather than an assumption about it.</para>
/// </summary>
[Category("SqlServer")]
[Category("Integration")]
[TestFixture]
[NonParallelizable]
public class SchemaBoundDependentTests
{
    private IDbConnection _connection;
    private string _db;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _db = $"SchemaBound_{Guid.NewGuid():N}"[..40];
        _connection = DbConnectionFactory.ForPlatform(Platform.SqlServer)
            .GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();

        _connection.ChangeDatabase("master");
        Exec($"CREATE DATABASE [{_db}]");
        _connection.ChangeDatabase(_db);

        using var cmd = _connection.CreateCommand();
        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (_connection == null) return;
        try
        {
            _connection.ChangeDatabase("master");
            Exec($"ALTER DATABASE [{_db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
            Exec($"DROP DATABASE IF EXISTS [{_db}]");
        }
        finally
        {
            _connection.Close();
            _connection.Dispose();
        }
    }

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();
    }

    private int Scalar(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var r = cmd.ExecuteScalar();
        return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
    }

    private void Deploy(string json)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandTimeout = 300;
        // Each test deploys a single-table package, so drop-by-absence would otherwise remove the
        // PREVIOUS test's table -- and a schemabound view legitimately blocks that, which is fixture
        // noise rather than anything about the column change under test.
        cmd.CommandText = "EXEC SchemaSmith.TableQuench @ProductName = 'SchemaBoundTest', "
                          + $"@TableDefinitions = N'{json.Replace("'", "''")}', "
                          + "@DropTablesRemovedFromProduct = 0";
        cmd.ExecuteNonQuery();
    }

    private static string Package(string table, string nameLength) =>
        "[{ \"Schema\": \"[dbo]\", \"Name\": \"[" + table + "]\", \"Columns\": ["
        + " { \"Name\": \"[Id]\", \"DataType\": \"INT\", \"Nullable\": false },"
        + " { \"Name\": \"[Label]\", \"DataType\": \"VARCHAR(" + nameLength + ")\", \"Nullable\": false } ], \"Indexes\": ["
        + " { \"Name\": \"[PK_" + table + "]\", \"IndexColumns\": \"[Id]\", \"PrimaryKey\": true, \"Unique\": true } ] }]";

    [Test]
    public void AColumnChange_BlockedByASchemaBoundView_IsRefusedWithTheModuleNamed()
    {
        Deploy(Package("SbTable", "50"));
        Exec("CREATE VIEW dbo.SbView WITH SCHEMABINDING AS SELECT Id, Label FROM dbo.SbTable");

        // Widening the column is refused by SQL Server while the schemabound view exists.
        var ex = Assert.Catch(() => Deploy(Package("SbTable", "100")));

        Assert.That(ex, Is.Not.Null, "the change genuinely cannot be applied while the module exists");
        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("SbView"),
                "the message must NAME the blocking module -- SQL Server's own 4922 says only that "
                + "'one or more objects' access the column, leaving the reader to go and find out "
                + "which. " + ex.Message);
            Assert.That(ex.Message, Does.Contain("SbTable"), "and the table it blocks");
            Assert.That(ex.Message, Does.Contain("AfterTablesObjects"),
                "and the remedy -- moving the module into a schema-bound object folder is the "
                + "supported fix, so the message has to point at it rather than leave the user "
                + "stuck. " + ex.Message);
        });
    }

    [Test]
    public void TheBlockingDependents_AreEnumerableBeforeTheAttempt()
    {
        // The basis for any fix: the catalog knows which modules block the change, and whether each one
        // is scriptable. WITH ENCRYPTION returns NULL from OBJECT_DEFINITION and can never be recreated,
        // which is why refusal has to exist alongside any drop-and-recreate.
        Deploy(Package("SbEnum", "50"));
        Exec("CREATE VIEW dbo.SbEnumView WITH SCHEMABINDING AS SELECT Id, Label FROM dbo.SbEnum");

        Assert.Multiple(() =>
        {
            Assert.That(Scalar(@"
                SELECT COUNT(*)
                  FROM sys.sql_expression_dependencies d
                  JOIN sys.sql_modules m ON m.[object_id] = d.referencing_id
                 WHERE d.referenced_id = OBJECT_ID('dbo.SbEnum') AND m.is_schema_bound = 1"),
                Is.GreaterThan(0),
                "sys.sql_expression_dependencies must name the blocker before the ALTER is attempted");

            Assert.That(Scalar("SELECT CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.SbEnumView')) IS NULL "
                               + "THEN 0 ELSE 1 END"), Is.EqualTo(1),
                "and an unencrypted module's definition is recoverable, which is what makes a "
                + "drop-and-recreate safe for it");
        });
    }

    private void DeployWithDrop(string json)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "EXEC SchemaSmith.TableQuench @ProductName = 'SchemaBoundTest', "
                          + $"@TableDefinitions = N'{json.Replace("'", "''")}', "
                          + "@DropTablesRemovedFromProduct = 0, @DropSchemaBoundDependents = 1";
        cmd.ExecuteNonQuery();
    }

    [Test]
    public void WithTheOptionOn_TheBlockingModuleIsDroppedAndTheColumnChangeApplies()
    {
        Deploy(Package("SbDrop", "50"));
        Exec("CREATE VIEW dbo.SbDropView WITH SCHEMABINDING AS SELECT Id, Label FROM dbo.SbDrop");

        DeployWithDrop(Package("SbDrop", "100"));

        Assert.Multiple(() =>
        {
            Assert.That(Scalar("SELECT CONVERT(INT, c.max_length) FROM sys.columns c "
                               + "WHERE c.[object_id] = OBJECT_ID('dbo.SbDrop') AND c.name = 'Label'"),
                Is.EqualTo(100),
                "the whole point of the option is that the column change actually lands");
            Assert.That(Scalar("SELECT COUNT(*) FROM sys.objects WHERE name = 'SbDropView'"), Is.Zero,
                "and the blocking module is gone -- the package's after-tables object pass is what puts "
                + "it back, which is why it has to be declared there");
        });
    }

    [Test]
    public void WithTheOptionOff_TheModuleSurvivesAndTheChangeIsStillRefused()
    {
        // The negative half, and it matters: a drop that fired regardless of the flag would destroy a
        // schema-bound module in every package that never opted in, and the user would have no script
        // to recreate it from.
        Deploy(Package("SbKeep", "50"));
        Exec("CREATE VIEW dbo.SbKeepView WITH SCHEMABINDING AS SELECT Id, Label FROM dbo.SbKeep");

        Assert.Catch(() => Deploy(Package("SbKeep", "100")));

        Assert.That(Scalar("SELECT COUNT(*) FROM sys.objects WHERE name = 'SbKeepView'"), Is.EqualTo(1),
            "opting out has to mean the module is left alone, not dropped and not recreated");
    }

    [Test]
    public void AnEncryptedModule_IsRefusedEvenWithTheOptionOn()
    {
        // WITH ENCRYPTION returns NULL from OBJECT_DEFINITION, so nothing -- not SchemaSmith, not the
        // user's own package -- can put it back from the server. Dropping it would be unrecoverable, so
        // the option deliberately does not extend to it.
        Deploy(Package("SbEnc", "50"));
        Exec("CREATE VIEW dbo.SbEncView WITH SCHEMABINDING, ENCRYPTION AS SELECT Id, Label FROM dbo.SbEnc");

        var ex = Assert.Catch(() => DeployWithDrop(Package("SbEnc", "100")));

        Assert.That(ex, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("SbEncView"), "the message must name it. " + ex.Message);
            Assert.That(Scalar("SELECT COUNT(*) FROM sys.objects WHERE name = 'SbEncView'"), Is.EqualTo(1),
                "and it must still be there -- refusing after dropping would be the worst outcome");
        });
    }


    [Test]
    public void ANestedSchemaBoundChain_IsDroppedDeepestFirst()
    {
        // sys.sql_expression_dependencies reports only the DIRECT dependent of the table, so a flat drop
        // list contains VA but not VB -- and "DROP VIEW VA" then fails with "because it is being
        // referenced by object VB". Verified against a live server before writing this. The chain has to
        // be walked transitively and dropped deepest-first, which is the ordering #323 asked for.
        Deploy(Package("SbNest", "50"));
        Exec("CREATE VIEW dbo.SbNestA WITH SCHEMABINDING AS SELECT Id, Label FROM dbo.SbNest");
        Exec("CREATE VIEW dbo.SbNestB WITH SCHEMABINDING AS SELECT Id, Label FROM dbo.SbNestA");

        DeployWithDrop(Package("SbNest", "100"));

        Assert.Multiple(() =>
        {
            Assert.That(Scalar("SELECT CONVERT(INT, c.max_length) FROM sys.columns c "
                               + "WHERE c.[object_id] = OBJECT_ID('dbo.SbNest') AND c.name = 'Label'"),
                Is.EqualTo(100), "the column change has to land through a two-deep chain");
            Assert.That(Scalar("SELECT COUNT(*) FROM sys.objects WHERE name IN ('SbNestA', 'SbNestB')"),
                Is.Zero,
                "both levels must be gone -- dropping only the direct dependent cannot succeed, and "
                + "leaving the outer one would block the next deploy instead");
        });
    }

}
