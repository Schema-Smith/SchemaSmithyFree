// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;

namespace Schema.IntegrationTests.Shared;

/// <summary>
/// Shared table/column/index COMMENT round-trip and drift tests for the MySQL/MariaDb family. Covers
/// the defect where extraction emitted COMMENT (SchemaSmith_GenerateTableJson.sql) but the deploy side
/// had nowhere to put it -- the parser temp tables carried no Comment column, so a declared comment was
/// silently discarded on deploy with no drift ever detected. The MySQL and MariaDb subclasses supply the
/// platform + fixture accessors; every [Test] body here runs on both engines (no MariaDb override exists
/// for any of the parse/quench scripts these tests exercise, so behavior is identical on both).
/// </summary>
public abstract class CommentQuenchSharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string MainDb { get; }
    protected abstract string MainConnectionString { get; }

    private const string TableName = "comment_quench_test";
    private const string PkIndexName = "pk_comment_quench_test";
    private const string SecondaryIndexName = "ix_val_comment_quench_test";
    private const string Product = "CommentQuenchProduct";

    private IDbConnection _connection = null!;
    private string _testDb = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDb = MainDb;
        _connection = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(MainConnectionString);
        _connection.Open();
        // ForgeKindler is already deployed into MainDb by the fixture.
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        DropTestTable();
        // ChangeAudit persists across the fixture's shared connection; scope each test to its own rows.
        Exec($"DELETE FROM SchemaSmith_ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");
    }

    [TearDown]
    public void TearDown() => DropTestTable();

    // ---- helpers -----------------------------------------------------------

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
    }

    private string ScalarStr(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    private void DropTestTable() => Exec($"DROP TABLE IF EXISTS `{_testDb}`.`{TableName}`");

    private static string BuildTableJson(string tableComment, string columnComment, string indexComment)
    {
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Comment = tableComment,
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn { Name = "`val`", DataType = "INT", Nullable = false, Default = "0", Comment = columnComment }
            ],
            Indexes =
            [
                new MySqlIndex { Name = $"`{PkIndexName}`", PrimaryKey = true, Unique = true, IndexColumns = "`id`" },
                new MySqlIndex { Name = $"`{SecondaryIndexName}`", IndexColumns = "`val`", Comment = indexComment }
            ]
        };
        return "[" + JsonConvert.SerializeObject(table) + "]";
    }

    private void Deploy(string tableComment, string columnComment, string indexComment)
    {
        var json = BuildTableJson(tableComment, columnComment, indexComment).Replace("'", "''");
        Exec($"CALL SchemaSmith_TableQuench('{Product}', '{_testDb}', '{json}', 0, 0, 0)");
    }

    private MySqlTable Extract()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{_testDb}', '{TableName}')";
        var json = "";
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                json += reader[0];
        }
        return PlatformDeserializer.DeserializeTable(json, Platform) as MySqlTable;
    }

    private string TableComment() => ScalarStr(
        $@"SELECT TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}'");

    private string ColumnComment() => ScalarStr(
        $@"SELECT COLUMN_COMMENT FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND COLUMN_NAME = 'val'");

    private string IndexComment() => ScalarStr(
        $@"SELECT MAX(INDEX_COMMENT) FROM INFORMATION_SCHEMA.STATISTICS
           WHERE TABLE_SCHEMA = '{_testDb}' AND TABLE_NAME = '{TableName}' AND INDEX_NAME = '{SecondaryIndexName}'");

    private long ColumnModifiedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'modified' AND ObjectName LIKE '%{TableName}.val%'");

    private long IndexCreatedAuditCount() => Scalar(
        $@"SELECT COUNT(*) FROM SchemaSmith_ChangeAudit
           WHERE ActionType = 'created' AND ObjectType = 'index' AND ObjectName LIKE '%{TableName}.{SecondaryIndexName}%'");

    // ---- round-trip: comment is applied and survives extraction -------------

    [Test]
    public void TableColumnAndIndexComments_AreAppliedOnCreate_AndRoundTripThroughExtraction()
    {
        Deploy(tableComment: "table comment", columnComment: "column comment", indexComment: "index comment");

        Assert.Multiple(() =>
        {
            Assert.That(TableComment(), Is.EqualTo("table comment"), "The declared table COMMENT must be applied on CREATE TABLE.");
            Assert.That(ColumnComment(), Is.EqualTo("column comment"), "The declared column COMMENT must be applied on CREATE TABLE.");
            Assert.That(IndexComment(), Is.EqualTo("index comment"), "The declared index COMMENT must be applied on CREATE INDEX.");
        });

        var table = Extract();
        Assert.That(table, Is.Not.Null);
        var val = (MySqlColumn)table!.Columns.Find(c => c.Name.Contains("val"));
        var idx = (MySqlIndex)table.Indexes.Find(i => i.Name.Contains(SecondaryIndexName));

        Assert.Multiple(() =>
        {
            Assert.That(table.Comment, Is.EqualTo("table comment"), "Extraction must round-trip the declared table Comment.");
            Assert.That(val, Is.Not.Null);
            Assert.That(val!.Comment, Is.EqualTo("column comment"), "Extraction must round-trip the declared column Comment.");
            Assert.That(idx, Is.Not.Null);
            Assert.That(idx!.Comment, Is.EqualTo("index comment"), "Extraction must round-trip the declared index Comment.");
        });
    }

    // ---- drift: a comment CHANGE (nothing else differs) is detected + applied

    [Test]
    public void TableCommentChange_DifferingOnlyInComment_IsDetectedAndApplied()
    {
        Deploy(tableComment: "before", columnComment: null, indexComment: null);
        Assert.That(TableComment(), Is.EqualTo("before"), "Sanity: table comment starts at the first declared value.");

        Deploy(tableComment: "after", columnComment: null, indexComment: null);

        Assert.That(TableComment(), Is.EqualTo("after"), "A table comment change (nothing else differs) must be detected and applied.");
    }

    [Test]
    public void ColumnCommentChange_DifferingOnlyInComment_IsDetectedAndApplied()
    {
        Deploy(tableComment: null, columnComment: "before", indexComment: null);
        Assert.That(ColumnComment(), Is.EqualTo("before"), "Sanity: column comment starts at the first declared value.");

        Deploy(tableComment: null, columnComment: "after", indexComment: null);

        Assert.Multiple(() =>
        {
            Assert.That(ColumnComment(), Is.EqualTo("after"), "A column comment change (nothing else differs) must be detected and applied.");
            Assert.That(ColumnModifiedAuditCount(), Is.GreaterThanOrEqualTo(1), "The comment-only change must record a 'modified' manifest row.");
        });
    }

    [Test]
    public void IndexCommentChange_DifferingOnlyInComment_IsDetectedAndApplied()
    {
        Deploy(tableComment: null, columnComment: null, indexComment: "before");
        Assert.That(IndexComment(), Is.EqualTo("before"), "Sanity: index comment starts at the first declared value.");

        Deploy(tableComment: null, columnComment: null, indexComment: "after");

        Assert.Multiple(() =>
        {
            Assert.That(IndexComment(), Is.EqualTo("after"), "An index comment change (nothing else differs) must be detected and applied.");
            Assert.That(IndexCreatedAuditCount(), Is.GreaterThanOrEqualTo(1),
                "The comment-only change is applied via the existing drop+recreate path for modified indexes, which must record the recreate.");
        });
    }

    // ---- escaping: a comment containing a single quote survives -------------

    [Test]
    public void CommentsContainingASingleQuote_SurviveRoundTrip()
    {
        const string quoted = "Bob's data";
        Deploy(tableComment: quoted, columnComment: quoted, indexComment: quoted);

        Assert.Multiple(() =>
        {
            Assert.That(TableComment(), Is.EqualTo(quoted), "A table comment containing a single quote must round-trip through the DDL's own escaping.");
            Assert.That(ColumnComment(), Is.EqualTo(quoted), "A column comment containing a single quote must round-trip through the DDL's own escaping.");
            Assert.That(IndexComment(), Is.EqualTo(quoted), "An index comment containing a single quote must round-trip through the DDL's own escaping.");
        });

        var table = Extract();
        var val = (MySqlColumn)table!.Columns.Find(c => c.Name.Contains("val"));
        var idx = (MySqlIndex)table.Indexes.Find(i => i.Name.Contains(SecondaryIndexName));

        Assert.Multiple(() =>
        {
            Assert.That(table.Comment, Is.EqualTo(quoted));
            Assert.That(val!.Comment, Is.EqualTo(quoted));
            Assert.That(idx!.Comment, Is.EqualTo(quoted));
        });
    }

    // ---- a table with no comments is unaffected, and stays idempotent -------

    [Test]
    public void TableWithNoComments_IsUnaffected_AndRedeployIsIdempotent()
    {
        Deploy(tableComment: null, columnComment: null, indexComment: null);

        Assert.Multiple(() =>
        {
            Assert.That(string.IsNullOrEmpty(TableComment()), Is.True, "No declared table comment must leave TABLE_COMMENT empty.");
            Assert.That(string.IsNullOrEmpty(ColumnComment()), Is.True, "No declared column comment must leave COLUMN_COMMENT empty.");
            Assert.That(string.IsNullOrEmpty(IndexComment()), Is.True, "No declared index comment must leave INDEX_COMMENT empty.");
        });

        Exec($"DELETE FROM SchemaSmith_ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");

        Deploy(tableComment: null, columnComment: null, indexComment: null);

        Assert.Multiple(() =>
        {
            Assert.That(string.IsNullOrEmpty(TableComment()), Is.True, "A second no-comment deploy must not introduce a comment.");
            Assert.That(ColumnModifiedAuditCount(), Is.EqualTo(0), "A second no-comment deploy must not record a spurious 'modified' row (non-idempotency bug).");
            Assert.That(IndexCreatedAuditCount(), Is.EqualTo(0), "A second no-comment deploy must not drop+recreate the unchanged index (non-idempotency bug).");
        });
    }

    // ---- SchemaSmith_IndexOnlyQuench is a distinct procedure carrying its own
    // copy of the create/modify-index logic; exercise it directly (not just via
    // SchemaSmith_TableQuench, which routes indexes through
    // SchemaSmith_MissingIndexesAndConstraintsQuench instead).

    [Test]
    public void IndexOnlyQuench_CreatesIndexWithComment_AndDetectsCommentChange()
    {
        // Base table + PK only, no secondary index yet -- created via the normal TableQuench path.
        var baseTable = new MySqlTable
        {
            Name = $"`{TableName}`",
            Engine = "InnoDB",
            Columns =
            [
                new MySqlColumn { Name = "`id`", DataType = "INT", Nullable = false, AutoIncrement = true },
                new MySqlColumn { Name = "`val`", DataType = "INT", Nullable = false, Default = "0" }
            ],
            Indexes =
            [
                new MySqlIndex { Name = $"`{PkIndexName}`", PrimaryKey = true, Unique = true, IndexColumns = "`id`" }
            ]
        };
        var baseJson = ("[" + JsonConvert.SerializeObject(baseTable) + "]").Replace("'", "''");
        Exec($"CALL SchemaSmith_TableQuench('{Product}', '{_testDb}', '{baseJson}', 0, 0, 0)");

        DeployIndexOnly(indexComment: "index-only before");
        Assert.That(IndexComment(), Is.EqualTo("index-only before"), "SchemaSmith_IndexOnlyQuench must apply a declared index Comment on create.");

        DeployIndexOnly(indexComment: "index-only after");
        Assert.That(IndexComment(), Is.EqualTo("index-only after"),
            "SchemaSmith_IndexOnlyQuench must detect and apply an index comment change (nothing else differs) the same way SchemaSmith_MissingIndexesAndConstraintsQuench does.");
    }

    private void DeployIndexOnly(string indexComment)
    {
        // IndexOnlyQuench reads the _SchemaSmith_Indexes temp table populated by ParseTableJson, so the
        // two calls run back-to-back on the same connection/session, exactly as the product code does.
        var table = new MySqlTable
        {
            Name = $"`{TableName}`",
            Indexes =
            [
                new MySqlIndex { Name = $"`{SecondaryIndexName}`", IndexColumns = "`val`", Comment = indexComment }
            ]
        };
        var json = ("[" + JsonConvert.SerializeObject(table) + "]").Replace("'", "''");
        Exec($"CALL SchemaSmith_ParseTableJson('{_testDb}', '{json}')");
        Exec($"CALL SchemaSmith_IndexOnlyQuench('{Product}', '{_testDb}', 0, 0, 0)");
    }
}
