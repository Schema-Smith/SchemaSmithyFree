// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.Text;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.SqlServer;

/// <summary>
/// Declaring a SQL Server graph table — gap item J2.
/// <para>A graph table is an ordinary table that SQL Server gives system-generated pseudo-columns to, so
/// the whole feature is one property plus a create-time clause. The half with any difficulty in it —
/// keeping those pseudo-columns out of an extracted package — shipped separately as
/// <see href="https://github.com/Schema-Smith/SchemaSmith/issues/402">#402</see>.</para>
/// <para><b>Create-time only, verified against the engine:</b> <c>ALTER TABLE … SET (AS NODE)</c> is not
/// syntax at all (error 156), so a table cannot become a node or edge after the fact. Changing
/// <c>GraphType</c> on a deployed table is therefore refused by name rather than attempted — the same
/// posture SchemaSmith takes toward moving a table to a different filegroup.</para>
/// </summary>
[Category("SqlServer")]
[Category("Integration")]
[TestFixture]
[NonParallelizable]
public class GraphTableDeployTests
{
    private IDbConnection _connection;
    private string _db;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _db = $"SchemaGraphDeploy_{Guid.NewGuid():N}"[..40];
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
        cmd.CommandText = "EXEC SchemaSmith.TableQuench @ProductName = 'GraphTest', "
                          + $"@TableDefinitions = N'{json.Replace("'", "''")}'";
        cmd.ExecuteNonQuery();
    }

    private static string Package(string table, string graphType) =>
        "[{ \"Schema\": \"[dbo]\", \"Name\": \"[" + table + "]\""
        + (graphType == null ? "" : ", \"GraphType\": \"" + graphType + "\"")
        + ", \"Columns\": [ { \"Name\": \"[Id]\", \"DataType\": \"INT\", \"Nullable\": false },"
        + " { \"Name\": \"[Label]\", \"DataType\": \"NVARCHAR(50)\", \"Nullable\": true } ], \"Indexes\": ["
        + " { \"Name\": \"[PK_" + table + "]\", \"IndexColumns\": \"[Id]\", \"PrimaryKey\": true, \"Unique\": true } ] }]";

    private string Extract(string table)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = $"EXEC SchemaSmith.GenerateTableJson @p_Schema = 'dbo', @p_Table = '{table}'";
        using var reader = cmd.ExecuteReader();
        var sb = new StringBuilder();
        while (reader.Read())
            if (!reader.IsDBNull(0)) sb.Append(reader.GetString(0));
        return sb.ToString();
    }

    [Test]
    public void DeclaringNode_CreatesANodeTable()
    {
        Deploy(Package("GNode", "Node"));

        Assert.Multiple(() =>
        {
            Assert.That(Scalar("SELECT CONVERT(INT, is_node) FROM sys.tables WHERE name = 'GNode'"), Is.EqualTo(1),
                "the table has to actually be a node table -- deploying it as an ordinary table would be a "
                + "green run that silently ignored the declaration");
            Assert.That(Scalar("SELECT COUNT(*) FROM sys.columns WHERE [object_id] = OBJECT_ID('dbo.GNode') "
                               + "AND graph_type IS NULL"), Is.EqualTo(2),
                "and its two declared columns are still there alongside the generated ones");
        });
    }

    [Test]
    public void DeclaringEdge_CreatesAnEdgeTable()
    {
        Deploy(Package("GEdge", "Edge"));

        Assert.That(Scalar("SELECT CONVERT(INT, is_edge) FROM sys.tables WHERE name = 'GEdge'"), Is.EqualTo(1));
    }

    [Test]
    public void NotDeclaringGraphType_CreatesAnOrdinaryTable()
    {
        // The negative half. Without it, an emit that appended AS NODE unconditionally would pass every
        // assertion above while turning every table in every package into a graph table.
        Deploy(Package("GPlain", null));

        Assert.That(Scalar("SELECT CONVERT(INT, is_node) + CONVERT(INT, is_edge) FROM sys.tables "
                           + "WHERE name = 'GPlain'"), Is.Zero);
    }

    [Test]
    public void AGraphTable_IsIdempotent()
    {
        // The second deploy is the one that finds bugs: if GraphType does not compare equal against the
        // live table, every run tries to change it -- and there is no ALTER for it, so it would fail.
        Deploy(Package("GTwice", "Node"));
        Deploy(Package("GTwice", "Node"));

        Assert.That(Scalar("SELECT CONVERT(INT, is_node) FROM sys.tables WHERE name = 'GTwice'"), Is.EqualTo(1));
    }

    [Test]
    public void AGraphTable_RoundTripsThroughExtraction()
    {
        Deploy(Package("GRoundTrip", "Node"));

        var json = Extract("GRoundTrip");

        Assert.That(json, Does.Contain("\"GraphType\": \"Node\"").IgnoreCase,
            "an extracted package that drops GraphType re-deploys the table as an ordinary one -- the same "
            + "silent round-trip loss #369 fixed for system versioning.\n" + json);
    }

    [Test]
    public void ChangingGraphType_OnADeployedTable_IsRefusedByName()
    {
        // SQL Server has no ALTER for this at all -- ALTER TABLE ... SET (AS NODE) is error 156, not even
        // syntax. So the only honest options are refuse or rebuild, and refusing says which.
        Deploy(Package("GChange", null));

        var ex = Assert.Catch(() => Deploy(Package("GChange", "Node")));

        Assert.That(ex, Is.Not.Null, "silently leaving the table ordinary would be the worse outcome");
        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("GChange"), "the message must name the table");
            Assert.That(ex.Message, Does.Contain("GraphType").IgnoreCase,
                "and the property, so the reader knows what to change rather than reading SQL Server's "
                + "syntax error");
        });
    }
}
