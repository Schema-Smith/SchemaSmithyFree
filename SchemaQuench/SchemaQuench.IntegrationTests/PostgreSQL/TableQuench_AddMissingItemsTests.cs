// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_AddMissingItemsTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_ShouldAddMissingIndex()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'AddMissingItemsTests' AND tablename = 'AddMyIndex' AND indexname = 'IDX_NewIndex')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        // Make sure the index ownership is recorded
        cmd.CommandText = @"SELECT ""ProductName"" FROM ""SchemaSmith"".""ProductOwnership"" WHERE ""Schema"" = 'AddMissingItemsTests' AND ""TableName"" = 'AddMyIndex' AND ""IndexName"" = 'IDX_NewIndex';";
        Assert.That(cmd.ExecuteScalar() as string, Is.EqualTo(_productName));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingIndexForIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'AddMissingItemsTests' AND tablename = 'AddMyIndexIO' AND indexname = 'IDX_NewIndexIO')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        // Make sure the index ownership is recorded
        cmd.CommandText = @"SELECT ""ProductName"" FROM ""SchemaSmith"".""ProductOwnership"" WHERE ""Schema"" = 'AddMissingItemsTests' AND ""TableName"" = 'AddMyIndexIO' AND ""IndexName"" = 'IDX_NewIndexIO';";
        Assert.That(cmd.ExecuteScalar() as string, Is.EqualTo(_productName));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingColumns()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema = 'AddMissingItemsTests' AND table_name = 'AddMyColumn' AND column_name = 'NewColumn')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema = 'AddMissingItemsTests' AND table_name = 'AddMyColumn' AND column_name = 'CollatedColumn')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema = 'AddMissingItemsTests' AND table_name = 'AddMyColumn' AND column_name = 'DontApply')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingDefault()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT \"SchemaSmith\".\"StripParenWrapping\"(column_default) FROM information_schema.columns WHERE table_schema = 'AddMissingItemsTests' AND table_name = 'AddMyDefault' AND column_name = 'Id'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("0"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingTableLevelCheckConstraint()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM pg_constraint WHERE conname = 'CHK_AddMyTableCheck_MyCheck' AND contype = 'c')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingForeignKey()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM pg_constraint WHERE conname = 'FK_AddMyFK_SelfRef' AND contype = 'f')";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingStatistics()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT stxname FROM pg_statistic_ext WHERE stxrelid = ('\"AddMissingItemsTests\".\"AddMyStatistics\"')::regclass AND stxname = 'ST_NewStats'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("ST_NewStats"));

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
CREATE SCHEMA ""AddMissingItemsTests"";
--TableQuench_ShouldAddMissingIndex
CREATE TABLE ""AddMissingItemsTests"".""AddMyIndex"" (""Id"" INT NOT NULL);
--TableQuench_ShouldAddMissingColumn
CREATE TABLE ""AddMissingItemsTests"".""AddMyColumn"" (""Id"" INT NOT NULL);
--TableQuench_ShouldAddMissingDefault
CREATE TABLE ""AddMissingItemsTests"".""AddMyDefault"" (""Id"" INT NOT NULL);
--TableQuench_ShouldAddMissingTableLevelCheckConstraint
CREATE TABLE ""AddMissingItemsTests"".""AddMyTableCheck"" (""Id"" INT NOT NULL, ""Col2"" INT);
--TableQuench_ShouldAddMissingForeignKey
CREATE TABLE ""AddMissingItemsTests"".""AddMyFK"" (""Id"" INT NOT NULL PRIMARY KEY, ""Col2"" INT);
--TableQuench_ShouldAddMissingStatistics
CREATE TABLE ""AddMissingItemsTests"".""AddMyStatistics"" (""Id"" INT NOT NULL);


--Index Only
--TableQuench_ShouldAddMissingIndexForIndexOnly
CREATE TABLE ""AddMissingItemsTests"".""AddMyIndexIO"" (""Id"" INT NOT NULL);
";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Schema": "AddMissingItemsTests",
                "Name": "AddMyIndex",
                "Columns": [
                    {
                      "Name": "Id",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "IDX_NewIndex",
                      "IndexColumns": "Id"
                    }
                ]
            },
            {
                "Schema": "AddMissingItemsTests",
                "Name": "AddMyColumn",
                "Columns": [
                    {
                      "Name": "Id",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "NewColumn",
                      "DataType": "VARCHAR(10)",
                      "Nullable": true,
                      "Sparse": true
                    },
                    {
                      "Name": "CollatedColumn",
                      "DataType": "VARCHAR(10)",
                      "Nullable": true,
                      "Collation": "az-Latn-x-icu"
                    },
                    {
                      "Name": "DontApply",
                      "DataType": "INT",
                      "Nullable": true,
                      "ShouldApplyExpression": "0=1"
                    }
                ]
            },
            {
                "Schema": "AddMissingItemsTests",
                "Name": "AddMyDefault",
                "Columns": [
                    {
                      "Name": "Id",
                      "DataType": "INT",
                      "Nullable": false,
                      "Default": "0"
                    }
                ]
            },
            {
                "Schema": "AddMissingItemsTests",
                "Name": "AddMyTableCheck",
                "Columns": [
                    {
                      "Name": "Id",
                      "Nullable": false,
                      "DataType": "INT"
                    },
                    {
                      "Name": "Col2",
                      "DataType": "INT",
                      "Nullable": true
                    }
                ],
                "CheckConstraints": [
                    {
                      "Name": "CHK_AddMyTableCheck_MyCheck",
                      "Expression": "\"Id\"<\"Col2\""
                    }
                ]
            },
            {
                "Schema": "AddMissingItemsTests",
                "Name": "AddMyFK",
                "Columns": [
                    {
                      "Name": "Id",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "Col2",
                      "DataType": "INT",
                      "Nullable": true
                    }
                ],
                "ForeignKeys": [
                    {
                      "Name": "FK_AddMyFK_SelfRef",
                      "Columns": "Col2",
                      "RelatedTableSchema": "AddMissingItemsTests",
                      "RelatedTable": "AddMyFK",
                      "RelatedColumns": "Id"
                    }
                ]
            },
            {
                "Schema": "AddMissingItemsTests",
                "Name": "AddMyStatistics",
                "Columns": [
                    {
                      "Name": "Id",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Statistics": [
                    {
                       "Name": "ST_NewStats",
                       "StatisticsColumns": "(\"Id\" / 1)"
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json);
        conn.Close();

        conn.Open();
        json = """
            [
            {
                "Schema": "AddMissingItemsTests",
                "Name": "AddMyIndexIO",
                "Indexes": [
                    {
                      "Name": "IDX_NewIndexIO",
                      "IndexColumns": "Id"
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json, indexOnly: true);
        conn.Close();
    }
}
