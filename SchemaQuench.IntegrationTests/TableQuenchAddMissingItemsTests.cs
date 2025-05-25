using Schema.DataAccess;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class TableQuenchAddMissingItemsTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_ShouldAddMissingIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.AddMyIndex'), 'IDX_NewIndex', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        // Make sure the index gets the ProductName extended property
        cmd.CommandText = @"
SELECT CONVERT(VARCHAR(50), x.[value]) AS [value]
  FROM fn_listextendedproperty(default, 'Schema', 'dbo', 'Table', 'AddMyIndex', 'Index', default) x
  WHERE objname COLLATE DATABASE_DEFAULT = 'IDX_NewIndex'
    AND x.[Name] COLLATE DATABASE_DEFAULT = 'ProductName'
";
        Assert.That(cmd.ExecuteScalar() as string, Is.EqualTo(_productName));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingColumns()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AddMyColumn'), 'NewColumn', 'ColumnId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AddMyColumn'), 'CollatedColumn', 'ColumnId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingDefault()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.fn_StripParenWrapping(COLUMN_DEFAULT) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_Name = 'AddMyDefault' AND COLUMN_NAME = 'Id'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("0"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingColumnLevelCheckConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.fn_StripParenWrapping([definition]) FROM sys.check_constraints ck WITH (NOLOCK) WHERE ck.[parent_object_id] = OBJECT_ID('dbo.AddMyColumnCheck')  AND COL_NAME(ck.parent_object_id, ck.parent_column_id) = 'Id'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("[Id]<(10)"));
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingTableLevelCheckConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.CHK_AddMyTableCheck_MyCheck') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingForeignKey()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.FK_AddMyFK_SelfRef') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingStatistics()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN EXISTS (SELECT * FROM sys.stats si WITH (NOLOCK) WHERE si.[object_id] = OBJECT_ID('dbo.AddMyStatistics') AND si.[Name] = 'ST_NewStats') THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldAddMissingFullTextIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AddMyFullTextIndex'), 'Column1', 'AllowsNull') = 0 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.AddMyFullTextIndex'), 'Column2', 'AllowsNull') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN EXISTS (SELECT * FROM sys.fulltext_indexes fi WITH (NOLOCK) WHERE fi.[object_id] = OBJECT_ID('dbo.AddMyFullTextIndex')) THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void ShouldAddMissingXmlIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT [Name] FROM sys.xml_indexes WITH (NOLOCK) WHERE [object_id] = OBJECT_ID('dbo.AddXmlIndex') AND xml_index_type = 0";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("XI_Primary"));

        cmd.CommandText = "SELECT [Name] FROM sys.xml_indexes WITH (NOLOCK) WHERE [object_id] = OBJECT_ID('dbo.AddXmlIndex') AND xml_index_type > 0";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("XI_Secondary_Path"));

        conn.Close();
    }

    [Test]
    public void ShouldAddMissingClusteredColumnStoreIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT [Name] FROM sys.indexes WITH (NOLOCK) WHERE [object_id] = OBJECT_ID('dbo.AddClusteredlColumnStoreIndex') AND [type] = 5";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("cci_ColumnStore"));

        conn.Close();
    }


    [Test]
    public void ShouldAddMissingNonClusteredColumnStoreIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT [Name] FROM sys.indexes WITH (NOLOCK) WHERE [object_id] = OBJECT_ID('dbo.AddNonClusteredlColumnStoreIndex') AND [type] = 6";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("nci_ColumnStore"));

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
--TableQuench_ShouldAddMissingIndex
CREATE TABLE dbo.AddMyIndex (Id INT NOT NULL)
--TableQuench_ShouldAddMissingColumn
CREATE TABLE dbo.AddMyColumn (Id INT NOT NULL)
--TableQuench_ShouldAddMissingDefault
CREATE TABLE dbo.AddMyDefault (Id INT NOT NULL)
--TableQuench_ShouldAddMissingColumnLevelCheckConstraint
CREATE TABLE dbo.AddMyColumnCheck (Id INT NOT NULL)
--TableQuench_ShouldAddMissingTableLevelCheckConstraint
CREATE TABLE dbo.AddMyTableCheck (Id INT NOT NULL, Col2 INT)
--TableQuench_ShouldAddMissingForeignKey
CREATE TABLE dbo.AddMyFK (Id INT NOT NULL PRIMARY KEY, Col2 INT)
--TableQuench_ShouldAddMissingStatistics
CREATE TABLE dbo.AddMyStatistics (Id INT NOT NULL)
--TableQuench_ShouldAddMissingFullTextIndex
CREATE TABLE dbo.AddMyFullTextIndex (Column1 INT NOT NULL, Column2 VARCHAR(200) NULL)
CREATE UNIQUE INDEX UDX_Key ON dbo.AddMyFullTextIndex ([Column1])
--ShouldAddMissingXmlIndex
CREATE TABLE dbo.AddXmlIndex (Column1 INT NOT NULL, Column2 VARCHAR(200) NULL, Column3 XML NULL, CONSTRAINT PK_AddXmlIndex PRIMARY KEY CLUSTERED (Column1))
--ShouldAddMissingClusteredColumnStoreIndex
CREATE TABLE dbo.AddClusteredlColumnStoreIndex (Column1 INT NOT NULL, Column2 VARCHAR(200) NULL, Column3 INT NULL, Column4 VARCHAR(100) NULL, Column5 INT NOT NULL)
--ShouldAddMissingNonClusteredColumnStoreIndex
CREATE TABLE dbo.AddNonClusteredlColumnStoreIndex (Column1 INT NOT NULL, Column2 VARCHAR(200) NULL, Column3 INT NULL, Column4 VARCHAR(100) NULL, Column5 INT NOT NULL)
";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Schema": "[dbo]",
                "Name": "[AddMyIndex]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[IDX_NewIndex]",
                      "IndexColumns": "[Id]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddMyColumn]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[NewColumn]",
                      "DataType": "VARCHAR(10)",
                      "Nullable": true,
                      "Sparse": true
                    },
                    {
                      "Name": "[CollatedColumn]",
                      "DataType": "VARCHAR(10)",
                      "Nullable": true,
                      "Collation": "Latin1_General_CS_AS",
                      "DataMaskFunction": "default()"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddMyDefault]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false,
                      "Default": "0"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddMyColumnCheck]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false,
                      "CheckExpression": "[Id]<10"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddMyTableCheck]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "Nullable": false,
                      "DataType": "INT"
                    },
                    {
                      "Name": "[Col2]",
                      "DataType": "INT",
                      "Nullable": true
                    }
                ],
                "CheckConstraints": [
                    {
                      "Name": "CHK_AddMyTableCheck_MyCheck",
                      "Expression": "[Id]<[Col2]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddMyFK]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Col2]",
                      "DataType": "INT",
                      "Nullable": true
                    }
                ],
                "ForeignKeys": [
                    {
                      "Name": "[FK_AddMyFK_SelfRef]",
                      "Columns": "[Col2]",
                      "RelatedTableSchema": "dbo",
                      "RelatedTable": "[AddMyFK]",
                      "RelatedColumns": "[Id]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddMyStatistics]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Statistics": [
                    {
                       "Name": "ST_NewStats",
                       "Columns": "[Id]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddMyFullTextIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "VARCHAR(200)",
                      "Nullable": true
                    }
                ],
                "FullTextIndex": {
                    "FullTextCatalog": "FT_Catalog",
                    "KeyIndex": "UDX_Key",
                    "ChangeTracking": "OFF",
                    "Columns": "[Column2]"
                }
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddXmlIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "BIGINT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "VARCHAR(200)",
                      "Nullable": true
                    },
                    {
                      "Name": "[Column3]",
                      "DataType": "XML",
                      "Nullable": true
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[PK_AddXmlIndex]",
                      "IndexColumns": "[Column1]",
                      "Clustered": true,
                      "PrimaryKey": true,
                      "Unique": true
                    }
                ],
                "XmlIndexes": [
                    {
                      "Name": "[XI_Primary]",
                      "Column": "[Column3]",
                      "IsPrimary": true
                    },
                    {
                      "Name": "[XI_Secondary_Path]",
                      "Column": "[Column3]",
                      "IsPrimary": false,
                      "PrimaryIndex": "[XI_Primary]",
                      "SecondaryIndexType": "PATH"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddClusteredlColumnStoreIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "VARCHAR(200)",
                      "Nullable": true
                    },
                    {
                      "Name": "[Column3]",
                      "DataType": "INT",
                      "Nullable": true
                    },
                    {
                      "Name": "[Column4]",
                      "DataType": "VARCHAR(100)",
                      "Nullable": true
                    },
                    {
                      "Name": "[Column5]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[cci_ColumnStore]",
                      "Clustered": true,
                      "ColumnStore": true,
                      "PrimaryKey": false,
                      "Unique": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AddNonClusteredlColumnStoreIndex]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "VARCHAR(200)",
                      "Nullable": true
                    },
                    {
                      "Name": "[Column3]",
                      "DataType": "INT",
                      "Nullable": true
                    },
                    {
                      "Name": "[Column4]",
                      "DataType": "VARCHAR(100)",
                      "Nullable": true
                    },
                    {
                      "Name": "[Column5]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[nci_ColumnStore]",
                      "Clustered": false,
                      "ColumnStore": true,
                      "PrimaryKey": false,
                      "Unique": false,
                      "IncludeColumns": "[Column2],[Column3],[Column4]"
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json);
        conn.Close();
    }
}
