using Schema.DataAccess;
using Microsoft.Data.SqlClient;
using System;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class QuenchTables_Miscellaneous : BaseQuenchTablesTests
{
    [Test]
    public void QuenchTables_ShouldRenameIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.RenameMyIndex'), 'IDX_RightName', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.RenameMyIndex'), 'IDX_WrongName', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldRenameUniqueConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.RenameMyUniqueConstraint'), 'UQ_NewName', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.RenameMyUniqueConstraint'), 'UQ_OldName', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);
        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldHandleRemovingForeignKey()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.FK_DropFK_SelfRef') IS NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.FK_DropFK_SelfRef2') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void ShouldHandleRemovingConflictingCustomClusteredIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.DropConflictingClusteredIdx'), 'IDX_NewClusteredIdx', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.DropConflictingClusteredIdx'), 'IDX_Conflict', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);
        conn.Close();
    }

    [Test]
    public void ShouldModifyTableCompression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COALESCE(p.data_compression_desc COLLATE DATABASE_DEFAULT, 'NONE') FROM sys.partitions AS p WITH (NOLOCK) WHERE p.[object_id] = OBJECT_ID('dbo.AlterTableCompression') AND p.index_id < 2";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("PAGE"));
        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexCompression()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COALESCE(p.data_compression_desc COLLATE DATABASE_DEFAULT, 'NONE') FROM sys.partitions AS p WITH (NOLOCK) WHERE p.[object_id] = OBJECT_ID('dbo.AlterIndexCompression') AND p.index_id >= 2";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("PAGE"));
        conn.Close();
    }

    [Test]
    public void ShouldModifyStatisticsWhenFilterExpressionChanges()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith.fn_StripParenWrapping(filter_definition) FROM sys.stats si WITH (NOLOCK) WHERE si.[object_id] = OBJECT_ID('dbo.ModifyStatisticsFilterExpression') AND [Name] = 'ST_Filtered'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("[Column1]>(50)"));

        conn.Close();
    }

    [Test]
    public void ShouldModifyStatisticsWhenColumnListChanges()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COL_NAME(ic.[object_id], ic.column_id) FROM sys.stats si WITH (NOLOCK) JOIN sys.stats_columns ic WITH (NOLOCK) ON ic.[object_id] = si.[object_id] AND ic.[stats_id] = si.[stats_id] WHERE si.[object_id] = OBJECT_ID('dbo.ModifyStatisticsColumnList') AND [Name] = 'ST_ColumnList'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("Column2"));

        conn.Close();
    }

    [Test]
    public void ShouldErrorWhenUpdatingWrongProductTable()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[TableOwnedByOtherProduct]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            }
            """;
        var ex = Assert.Throws<SqlException>(() => RunTableQuenchProc(cmd, json));
        Assert.That(ex!.Message, Contains.Substring("One or more tables in this quench are already owned by another product"));

        conn.Close();
    }

    [Test]
    public void ShouldDropXmlIndexNoLongerPartOfProduct()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.XmlIndexNoLongerInProduct'), 'XI_DropMe', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.XmlIndexNoLongerInProduct'), 'XI_KeepMe', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void ShouldDropIndexNoLongerPartOfProduct()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.IndexNoLongerInProduct'), 'IDX_DropMe', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);

        cmd.CommandText = "SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.IndexNoLongerInProduct'), 'IDX_Custom', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True);

        conn.Close();
    }

    [Test]
    public void ShouldDropTableNoLongerPartOfProduct()
    {
        var productName = Guid.NewGuid().ToString();
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
CREATE TABLE dbo.TableNoLongerInProduct (Column1 INT NOT NULL)
EXEC sp_addextendedproperty @name = N'ProductName', @value = '{productName}', @level0type = N'Schema', @level0name = 'dbo', @level1type = N'Table', @level1name = 'TableNoLongerInProduct'
";
        cmd.ExecuteNonQuery();

        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[TableInProduct]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            }
            """;

        cmd.CommandText = $"EXEC SchemaSmith.TableQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropTablesRemovedFromProduct = 1";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT CAST(CASE WHEN OBJECT_ID('dbo.TableNoLongerInProduct') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False);

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
--QuenchTables_ShouldRenameIndex
CREATE TABLE dbo.RenameMyIndex (Id INT NOT NULL)
CREATE NONCLUSTERED INDEX IDX_WrongName ON dbo.RenameMyIndex (Id)
--QuenchTables_ShouldRenameUniqueConstraint
CREATE TABLE dbo.RenameMyUniqueConstraint (Id INT NOT NULL)
ALTER TABLE dbo.RenameMyUniqueConstraint ADD CONSTRAINT UQ_OldName UNIQUE (Id)
--QuenchTables_ShouldHandleRemovingForeignKey
CREATE TABLE dbo.DropFK (Column1 INT NOT NULL, Column2 INT NOT NULL, CONSTRAINT PK_DropFK PRIMARY KEY (Column1))
CREATE UNIQUE INDEX UQ_Colum2 ON dbo.DropFK (Column2)
ALTER TABLE dbo.DropFK ADD CONSTRAINT FK_DropFK_SelfRef FOREIGN KEY (Column2) REFERENCES dbo.DropFK (Column1)
ALTER TABLE dbo.DropFK ADD CONSTRAINT FK_DropFK_SelfRef2 FOREIGN KEY (Column2) REFERENCES dbo.DropFK (Column1)
--ShouldHandleRemovingConflictingCustomClusteredIndex
CREATE TABLE dbo.DropConflictingClusteredIdx (Id INT NOT NULL)
CREATE CLUSTERED INDEX IDX_Conflict ON dbo.DropConflictingClusteredIdx (Id)
--ShouldModifyTableCompression
CREATE TABLE dbo.AlterTableCompression (Id INT NOT NULL) WITH (DATA_COMPRESSION=NONE)
--ShouldModifyIndexCompression
CREATE TABLE dbo.AlterIndexCompression (Id INT NOT NULL)
CREATE INDEX IDX_CompressionChange ON dbo.AlterIndexCompression (Id) WITH (DATA_COMPRESSION=NONE)
--ShouldModifyStatisticsWhenFilterExpressionChanges
CREATE TABLE dbo.ModifyStatisticsFilterExpression (Column1 INT NOT NULL)
CREATE STATISTICS ST_Filtered ON dbo.ModifyStatisticsFilterExpression ([Column1]) WHERE [Column1]>(20)
--ShouldModifyStatisticsWhenColumnListChanges
CREATE TABLE dbo.ModifyStatisticsColumnList (Column1 INT NOT NULL, Column2 INT NOT NULL)
CREATE STATISTICS ST_ColumnList ON dbo.ModifyStatisticsColumnList ([Column1])
--ShouldDropIndexNoLongerPartOfProduct
CREATE TABLE dbo.IndexNoLongerInProduct (Column1 INT NOT NULL)
CREATE INDEX IDX_DropMe ON IndexNoLongerInProduct (Column1)
CREATE INDEX IDX_Custom ON IndexNoLongerInProduct (Column1)
EXEC sp_addextendedproperty @name = N'ProductName', @value = '{_productName}', @level0type = N'Schema', @level0name = 'dbo', @level1type = N'Table', @level1name = 'IndexNoLongerInProduct'
EXEC sp_addextendedproperty @name = N'ProductName', @value = '{_productName}', @level0type = N'Schema', @level0name = 'dbo', @level1type = N'Table', @level1name = 'IndexNoLongerInProduct', @level2type = N'Index', @level2name = 'IDX_DropMe'
--XmlIndexNoLongerInProduct
CREATE TABLE dbo.XmlIndexNoLongerInProduct (Column1 INT NOT NULL, Column2 VARCHAR(200) NULL, Column3 XML NULL, CONSTRAINT PK_XmlIndexNoLongerInProduct PRIMARY KEY CLUSTERED (Column1))
CREATE PRIMARY XML INDEX [XI_KeepMe] ON dbo.XmlIndexNoLongerInProduct (Column3)
CREATE XML INDEX [XI_DropMe] ON dbo.XmlIndexNoLongerInProduct (Column3) USING XML INDEX [XI_KeepMe] FOR PATH 
EXEC sp_addextendedproperty @name = N'ProductName', @value = '{_productName}', @level0type = N'Schema', @level0name = 'dbo', @level1type = N'Table', @level1name = 'XmlIndexNoLongerInProduct'
EXEC sp_addextendedproperty @name = N'ProductName', @value = '{_productName}', @level0type = N'Schema', @level0name = 'dbo', @level1type = N'Table', @level1name = 'XmlIndexNoLongerInProduct', @level2type = N'Index', @level2name = 'XI_KeepMe'
EXEC sp_addextendedproperty @name = N'ProductName', @value = '{_productName}', @level0type = N'Schema', @level0name = 'dbo', @level1type = N'Table', @level1name = 'XmlIndexNoLongerInProduct', @level2type = N'Index', @level2name = 'XI_DropMe'

-- Exception Cases
CREATE TABLE dbo.TableOwnedByOtherProduct (Column1 INT NOT NULL)
EXEC sp_addextendedproperty @name = N'ProductName', @value = 'OtherProduct', @level0type = N'Schema', @level0name = 'dbo', @level1type = N'Table', @level1name = 'TableOwnedByOtherProduct'
";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Schema": "[dbo]",
                "Name": "[RenameMyIndex]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[IDX_RightName]",
                      "IndexColumns": "[Id]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[RenameMyUniqueConstraint]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[UQ_NewName]",
                      "IndexColumns": "[Id]",
                      "Unique": true,
                      "UniqueConstraint": true
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropFK]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "ForeignKeys": [
                    {
                      "Name": "[FK_DropFK_SelfRef2]",
                      "Columns": "[Column2]",
                      "RelatedTableSchema": "dbo",
                      "RelatedTable": "[DropFK]",
                      "RelatedColumns": "[Column1]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[DropConflictingClusteredIdx]",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[IDX_NewClusteredIdx]",
                      "IndexColumns": "[Id]",
                      "Clustered": true
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterTableCompression]",
                "CompressionType": "PAGE",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[AlterIndexCompression]",
                "CompressionType": "PAGE",
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Indexes": [
                    {
                      "Name": "[IDX_CompressionChange]",
                      "IndexColumns": "[Id]",
                      "CompressionType": "PAGE"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[ModifyStatisticsFilterExpression]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Statistics": [
                    {
                      "Name": "[ST_Filtered]",
                      "Columns": "[Column1]",
                      "FilterExpression": "[Column1]>(50)"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[ModifyStatisticsColumnList]",
                "Columns": [
                    {
                      "Name": "[Column1]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Column2]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                ],
                "Statistics": [
                    {
                      "Name": "[ST_ColumnList]",
                      "Columns": "[Column2]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[IndexNoLongerInProduct]",
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
                "Name": "[XmlIndexNoLongerInProduct]",
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
                      "Name": "[PK_XmlIndexNoLongerInProduct]",
                      "IndexColumns": "[Column1]",
                      "Clustered": true,
                      "PrimaryKey": true,
                      "Unique": true
                    }
                ],
                "XmlIndexes": [
                    {
                      "Name": "[XI_KeepMe]",
                      "Column": "[Column3]",
                      "IsPrimary": true
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json);

        conn.Close();
    }
}
