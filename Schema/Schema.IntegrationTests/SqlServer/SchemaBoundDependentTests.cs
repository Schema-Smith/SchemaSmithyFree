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
}
