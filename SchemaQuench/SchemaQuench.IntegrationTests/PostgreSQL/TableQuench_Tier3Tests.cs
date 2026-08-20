// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_Tier3Tests : BaseTableQuenchTests
{
    [Test]
    public void ShouldCreateGinIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'Tier3Tests' AND tablename = 'GinIndexTable' AND indexname = 'IDX_GinIndex')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = @"SELECT am.amname FROM pg_index idx
                            JOIN pg_class i ON i.oid = idx.indexrelid
                            JOIN pg_am am ON am.oid = i.relam
                            WHERE i.relname = 'IDX_GinIndex'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("gin"));

        conn.Close();
    }

    [Test]
    public void ShouldCreateGinIndexIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'Tier3Tests' AND tablename = 'GinIndexTableIO' AND indexname = 'IDX_GinIndexIO')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = @"SELECT am.amname FROM pg_index idx
                            JOIN pg_class i ON i.oid = idx.indexrelid
                            JOIN pg_am am ON am.oid = i.relam
                            WHERE i.relname = 'IDX_GinIndexIO'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("gin"));

        conn.Close();
    }

    [Test]
    public void ShouldCreateBrinIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'Tier3Tests' AND tablename = 'BrinIndexTable' AND indexname = 'IDX_BrinIndex')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = @"SELECT am.amname FROM pg_index idx
                            JOIN pg_class i ON i.oid = idx.indexrelid
                            JOIN pg_am am ON am.oid = i.relam
                            WHERE i.relname = 'IDX_BrinIndex'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("brin"));

        conn.Close();
    }

    [Test]
    public void ShouldCreateBrinIndexIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'Tier3Tests' AND tablename = 'BrinIndexTableIO' AND indexname = 'IDX_BrinIndexIO')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = @"SELECT am.amname FROM pg_index idx
                            JOIN pg_class i ON i.oid = idx.indexrelid
                            JOIN pg_am am ON am.oid = i.relam
                            WHERE i.relname = 'IDX_BrinIndexIO'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("brin"));

        conn.Close();
    }

    [Test]
    public void ShouldCreateHashIndexWithFillFactor()
    {
        // hash is a non-btree built-in access method (no extension needed) that accepts fillfactor,
        // exercising the same allow-list gate that excludes gin/brin above without requiring pgvector.
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'Tier3Tests' AND tablename = 'HashIndexTable' AND indexname = 'IDX_HashIndex')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = @"SELECT am.amname FROM pg_index idx
                            JOIN pg_class i ON i.oid = idx.indexrelid
                            JOIN pg_am am ON am.oid = i.relam
                            WHERE i.relname = 'IDX_HashIndex'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("hash"));

        cmd.CommandText = @"SELECT SPLIT_PART(opt, '=', 2) FROM pg_class c, UNNEST(c.reloptions) AS opt
                            WHERE c.relname = 'IDX_HashIndex' AND opt LIKE 'fillfactor=%'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("70"), "hash accepts fillfactor and must not be skipped by the allow-list");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE SCHEMA ""Tier3Tests"";
--ShouldCreateGinIndex
CREATE TABLE ""Tier3Tests"".""GinIndexTable"" (""Tags"" JSONB NOT NULL);
--ShouldCreateBrinIndex
CREATE TABLE ""Tier3Tests"".""BrinIndexTable"" (""CreatedAt"" TIMESTAMP NOT NULL);
--ShouldCreateHashIndexWithFillFactor
CREATE TABLE ""Tier3Tests"".""HashIndexTable"" (""Code"" TEXT NOT NULL);

-- Index Only
--ShouldCreateGinIndexIndexOnly
CREATE TABLE ""Tier3Tests"".""GinIndexTableIO"" (""Tags"" JSONB NOT NULL);
--ShouldCreateBrinIndexIndexOnly
CREATE TABLE ""Tier3Tests"".""BrinIndexTableIO"" (""CreatedAt"" TIMESTAMP NOT NULL);
";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Schema": "Tier3Tests",
                "Name": "GinIndexTable",
                "Columns": [
                    {
                      "Name": "Tags",
                      "DataType": "JSONB",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "IDX_GinIndex",
                      "IndexColumns": "Tags",
                      "AccessMethod": "gin"
                    }
                ]
            },
            {
                "Schema": "Tier3Tests",
                "Name": "BrinIndexTable",
                "Columns": [
                    {
                      "Name": "CreatedAt",
                      "DataType": "TIMESTAMP",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "IDX_BrinIndex",
                      "IndexColumns": "CreatedAt",
                      "AccessMethod": "brin"
                    }
                ]
            },
            {
                "Schema": "Tier3Tests",
                "Name": "HashIndexTable",
                "Columns": [
                    {
                      "Name": "Code",
                      "DataType": "TEXT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "IDX_HashIndex",
                      "IndexColumns": "Code",
                      "AccessMethod": "hash",
                      "FillFactor": 70
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json);
        conn.Close();

        conn.Open();
        // Index Only
        json = """
            [
            {
                "Schema": "Tier3Tests",
                "Name": "GinIndexTableIO",
                "Indexes": [
                    {
                      "Name": "IDX_GinIndexIO",
                      "IndexColumns": "Tags",
                      "AccessMethod": "gin"
                    }
                ]
            },
            {
                "Schema": "Tier3Tests",
                "Name": "BrinIndexTableIO",
                "Indexes": [
                    {
                      "Name": "IDX_BrinIndexIO",
                      "IndexColumns": "CreatedAt",
                      "AccessMethod": "brin"
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json, indexOnly: true);
        conn.Close();
    }
}
