using Schema.DataAccess;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class QuenchTables_DropColumnsTests : BaseQuenchTablesTests
{
    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnUsedInIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnInIndex'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnInIndex'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.DropColumnInIndex'), 'IDX_Dependency', 'IndexId') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.DropColumnInIndex'), 'IDX_NoDependency', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnUsedInUniqueIndexReferencedByFullTextIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnInUniqueIndexInFTIndex'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnInUniqueIndexInFTIndex'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnInUniqueIndexInFTIndex'), 'Column3', 'AllowsNull') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.DropColumnInUniqueIndexInFTIndex'), 'IDX_Dependency', 'IndexId') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN EXISTS (SELECT * FROM sys.fulltext_indexes fi WITH (NOLOCK) WHERE fi.[object_id] = OBJECT_ID('dbo.DropColumnInUniqueIndexInFTIndex')) THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnUsedInUniqueIndexReferencedByForeignKey()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnInUniqueIndexInFK'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnInUniqueIndexInFK'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.DropColumnInUniqueIndexInFK'), 'IDX_Dependency', 'IndexId') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.FK_DropColumnInUniqueIndexInFK_SelfRef') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithDefault()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithDefault'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithDefault'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithColumnLevelCheckConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithCheckConstraint'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithCheckConstraint'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithTableLevelCheckConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithTableCheckConstraint'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithTableCheckConstraint'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithStatistics()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithStatistics'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithStatistics'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithStatisticsFilterExpression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithStatisticsFilter'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithStatisticsFilter'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithIndexFilterExpression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithIndexFilter'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithIndexFilter'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithFullTextIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithFTIndex'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithFTIndex'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithForeignKey()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithFK'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithFK'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingColumnWithComputedExpression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithComputed'), 'Column2', 'AllowsNull') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.DropColumnWithComputed'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
--QuenchTables_ShouldHandleRemovingColumnUsedInIndex
CREATE TABLE dbo.DropColumnInIndex (Column1 INT NOT NULL, Column2 INT)
CREATE INDEX IDX_NoDependency ON dbo.DropColumnInIndex ([Column1])
CREATE INDEX IDX_Dependency ON dbo.DropColumnInIndex ([Column2])
--QuenchTables_ShouldHandleRemovingColumnUsedInUniqueIndexReferencedByFullTextIndex
CREATE TABLE dbo.DropColumnInUniqueIndexInFTIndex (Column1 INT NOT NULL, Column2 INT NOT NULL, Column3 VARCHAR(200) NULL)
CREATE UNIQUE INDEX IDX_Dependency ON dbo.DropColumnInUniqueIndexInFTIndex ([Column2])
CREATE FULLTEXT INDEX ON dbo.DropColumnInUniqueIndexInFTIndex (Column3) KEY INDEX IDX_Dependency ON FT_Catalog WITH CHANGE_TRACKING = OFF
--QuenchTables_ShouldHandleRemovingColumnUsedInUniqueIndexReferencedByForeignKey
CREATE TABLE dbo.DropColumnInUniqueIndexInFK (Column1 INT NOT NULL, Column2 INT NOT NULL)
CREATE UNIQUE INDEX IDX_Dependency ON dbo.DropColumnInUniqueIndexInFK ([Column2])
ALTER TABLE dbo.DropColumnInUniqueIndexInFK ADD CONSTRAINT FK_DropColumnInUniqueIndexInFK_SelfRef FOREIGN KEY (Column1) REFERENCES dbo.DropColumnInUniqueIndexInFK (Column2)
--QuenchTables_ShouldHandleRemovingColumnWithDefault
CREATE TABLE dbo.DropColumnWithDefault (Column1 INT NOT NULL, Column2 INT DEFAULT 0)
--QuenchTables_ShouldHandleRemovingColumnWithColumnLevelCheckConstraint
CREATE TABLE dbo.DropColumnWithCheckConstraint (Column1 INT NOT NULL, Column2 INT CHECK ([Column2] < 50))
--QuenchTables_ShouldHandleRemovingColumnWithTableLevelCheckConstraint
CREATE TABLE dbo.DropColumnWithTableCheckConstraint (Column1 INT NOT NULL, Column2 INT, CONSTRAINT CK_DropColumnWithTableCheckConstraint_Dependency CHECK (Column2 < Column1))
--QuenchTables_ShouldHandleRemovingColumnWithStatistics
CREATE TABLE dbo.DropColumnWithStatistics (Column1 INT NOT NULL, Column2 INT)
CREATE STATISTICS ST_Dependency ON dbo.DropColumnWithStatistics (Column2)
--QuenchTables_ShouldHandleRemovingColumnWithStatisticsFilterExpression
CREATE TABLE dbo.DropColumnWithStatisticsFilter (Column1 INT NOT NULL, Column2 INT)
CREATE STATISTICS ST_Dependency ON dbo.DropColumnWithStatisticsFilter (Column1) WHERE Column2 < 100
--QuenchTables_ShouldHandleRemovingColumnWithIndexFilterExpression
CREATE TABLE dbo.DropColumnWithIndexFilter (Column1 INT NOT NULL, Column2 INT)
CREATE INDEX IDX_Dependency ON dbo.DropColumnWithIndexFilter (Column1) WHERE Column2 < 100
--QuenchTables_ShouldHandleRemovingColumnWithFullTextIndex
CREATE TABLE dbo.DropColumnWithFTIndex (Column1 INT NOT NULL, Column2 VARCHAR(2000), CONSTRAINT PK_DropColumnWithFTIndex PRIMARY KEY (Column1))
CREATE FULLTEXT INDEX ON dbo.DropColumnWithFTIndex (Column2) KEY INDEX PK_DropColumnWithFTIndex ON FT_Catalog WITH CHANGE_TRACKING = OFF
--QuenchTables_ShouldHandleRemovingColumnWithForeignKey
CREATE TABLE dbo.DropColumnWithFK (Column1 INT NOT NULL, Column2 INT, CONSTRAINT PK_DropColumnWithFK PRIMARY KEY (Column1))
CREATE UNIQUE INDEX UQ_Colum2 ON dbo.DropColumnWithFK (Column2)
CREATE TABLE dbo.DropColumnWithFKRef (Column1 INT NOT NULL, Column2 INT, CONSTRAINT PK_DropColumnWithFKRef PRIMARY KEY (Column1))
ALTER TABLE dbo.DropColumnWithFK ADD CONSTRAINT FK_DropColumnWithFK_SelfRef FOREIGN KEY (Column2) REFERENCES dbo.DropColumnWithFK (Column1)
ALTER TABLE dbo.DropColumnWithFKRef ADD CONSTRAINT FK_DropColumnWithFK_Referenced FOREIGN KEY (Column2) REFERENCES dbo.DropColumnWithFK (Column2)
ALTER TABLE dbo.DropColumnWithFK ADD CONSTRAINT FK_DropColumnWithFK_Referencing FOREIGN KEY (Column2) REFERENCES dbo.DropColumnWithFKRef (Column1)
--QuenchTables_ShouldHandleRemovingColumnWithComputedExpression
CREATE TABLE dbo.DropColumnWithComputed (Column1 INT NOT NULL, Column2 INT, Column3 AS (Column2 * 3))
";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnInIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnInUniqueIndexInFTIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column3]",
                      "DataType": "VARCHAR(200)",
                      "Nullable": true
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[UDX_NewKey]",
                      "IndexColumns": "[Column1]",
                      "Unique": true
                    }
                ],
                "FullTextIndex": {
                    "FullTextCatalog": "FT_Catalog",
                    "KeyIndex": "UDX_NewKey",
                    "ChangeTracking": "OFF",
                    "Columns": "[Column3]"
                }
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnInUniqueIndexInFK]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithDefault]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithCheckConstraint]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithTableCheckConstraint]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithStatistics]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithStatisticsFilter]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithIndexFilter]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithFTIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithFK]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropColumnWithComputed]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column3]",
                      "ComputedExpression": "Column1 * 3"
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json);

        conn.Close();
    }
}
