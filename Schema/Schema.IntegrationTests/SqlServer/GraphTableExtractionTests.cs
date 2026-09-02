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
/// Extracting a SQL Server graph table must not emit its system-generated pseudo-columns.
/// <para>Graph tables (<c>AS NODE</c> / <c>AS EDGE</c>) are listed as UNSUPPORTED, but "unsupported"
/// should mean SchemaSmith does not help you declare one — not that extracting a database containing one
/// produces a package that is silently wrong. A node table with two real columns extracts as four; an
/// edge table with one real column extracts as nine.</para>
/// <para><b>Why the existing filter misses them.</b> Extraction iterates
/// <c>INFORMATION_SCHEMA.COLUMNS</c> and excludes only <c>generated_always_type &lt;&gt; 0</c>, which is
/// what removes temporal period columns. Every graph pseudo-column reports
/// <c>generated_always_type = 0</c> / <c>NOT_APPLICABLE</c>, so none of them is caught — and
/// <c>is_hidden</c> does not cover them either: <c>$node_id</c>, <c>$edge_id</c>, <c>$from_id</c> and
/// <c>$to_id</c> are all <c>is_hidden = 0</c>.</para>
/// <para>The names carry a per-table GUID suffix (<c>$node_id_A08D8E3E345948…</c>), so the emitted
/// package cannot be redeployed anywhere — not even back to the database it came from. This is the same
/// class of round-trip loss as #369.</para>
/// </summary>
[Category("SqlServer")]
[Category("Integration")]
[TestFixture]
[NonParallelizable]
public class GraphTableExtractionTests
{
    private IDbConnection _connection;
    private string _db;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _db = $"SchemaGraph_{Guid.NewGuid():N}"[..40];
        _connection = DbConnectionFactory.ForPlatform(Platform.SqlServer)
            .GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();

        _connection.ChangeDatabase("master");
        Exec($"CREATE DATABASE [{_db}]");
        _connection.ChangeDatabase(_db);

        using var cmd = _connection.CreateCommand();
        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer);

        Exec("CREATE TABLE dbo.GraphPerson (Id INT NOT NULL PRIMARY KEY, Name NVARCHAR(50) NULL) AS NODE");
        Exec("CREATE TABLE dbo.GraphKnows (Since DATE NULL) AS EDGE");
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

    /// <summary>FOR JSON splits across rows at 2033 characters, so the whole reader has to be drained.</summary>
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
    public void ExtractingANodeTable_OmitsTheGeneratedPseudoColumns()
    {
        var json = Extract("GraphPerson");

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("[Id]"), "the real columns must still be there");
            Assert.That(json, Does.Contain("[Name]"));

            Assert.That(json, Does.Not.Contain("node_id"),
                "$node_id carries a per-table GUID suffix, so a package containing it cannot be deployed "
                + "anywhere -- including back to the database it was extracted from.\n" + json);
            Assert.That(json, Does.Not.Contain("graph_id"),
                "graph_id is hidden but still reaches INFORMATION_SCHEMA.COLUMNS, which is what extraction "
                + "iterates.\n" + json);
        });
    }

    [Test]
    public void ExtractingAnEdgeTable_OmitsTheGeneratedPseudoColumns()
    {
        // The edge table is the worse case: one real column, eight generated ones.
        var json = Extract("GraphKnows");

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("[Since]"), "the one real column must still be there");

            foreach (var pseudo in new[] { "edge_id", "from_id", "to_id", "from_obj_id", "to_obj_id", "graph_id" })
                Assert.That(json, Does.Not.Contain(pseudo),
                    $"'{pseudo}' is a system-generated graph column and must not be emitted as a user "
                    + "column.\n" + json);
        });
    }

    [Test]
    public void AnOrdinaryTable_IsUnaffected()
    {
        // The negative half: whatever excludes graph columns must not start dropping real ones. Without
        // this, an over-broad filter would pass both tests above while quietly emptying every package.
        Exec("CREATE TABLE dbo.PlainTable (Id INT NOT NULL PRIMARY KEY, node_id_like NVARCHAR(10) NULL)");

        var json = Extract("PlainTable");

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("[Id]"));
            Assert.That(json, Does.Contain("[node_id_like]"),
                "a user column whose name merely resembles a graph pseudo-column must survive -- the "
                + "exclusion has to key off the catalog, not off the name");
        });
    }
}
