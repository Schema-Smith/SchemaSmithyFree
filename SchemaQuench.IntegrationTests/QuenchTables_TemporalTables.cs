using System;
using Schema.DataAccess;

namespace SchemaQuench.IntegrationTests;

[Parallelizable(scope: ParallelScope.All)]
public class QuenchTables_TemporalTables : BaseQuenchTablesTests
{
    [Test]
    public void QuenchTables_ShouldCreateNewTemporalTable()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(COLUMNPROPERTY(OBJECT_ID('dbo.MyNewTeporalTable'), 'ValidFrom', 'GeneratedAlwaysType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(1));

        cmd.CommandText = "SELECT CAST(COLUMNPROPERTY(OBJECT_ID('dbo.MyNewTeporalTable'), 'ValidTo', 'GeneratedAlwaysType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(2));

        cmd.CommandText = "SELECT CAST(OBJECTPROPERTY(OBJECT_ID('dbo.MyNewTeporalTable'), 'TableTemporalType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(2));

        cmd.CommandText = "SELECT CAST(OBJECTPROPERTY(OBJECT_ID('dbo.MyNewTeporalTable_Hist'), 'TableTemporalType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(1));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldRemoveTemporalTracking()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(COLUMNPROPERTY(OBJECT_ID('dbo.MyOldTeporalTable'), 'ValidFrom', 'GeneratedAlwaysType') AS INT)";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(DBNull.Value));

        cmd.CommandText = "SELECT CAST(COLUMNPROPERTY(OBJECT_ID('dbo.MyOldTeporalTable'), 'ValidTo', 'GeneratedAlwaysType') AS INT)";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(DBNull.Value));

        cmd.CommandText = "SELECT CAST(OBJECTPROPERTY(OBJECT_ID('dbo.MyOldTeporalTable'), 'TableTemporalType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(0));

        cmd.CommandText = "SELECT CAST(OBJECTPROPERTY(OBJECT_ID('dbo.MyOldTeporalTable_Hist'), 'TableTemporalType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(0));

        conn.Close();
    }

    [Test]
    public void QuenchTables_ShouldMakeExistingTableTemporal()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT CAST(COLUMNPROPERTY(OBJECT_ID('dbo.MyUpdatedTeporalTable'), 'ValidFrom', 'GeneratedAlwaysType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(1));

        cmd.CommandText = "SELECT CAST(COLUMNPROPERTY(OBJECT_ID('dbo.MyUpdatedTeporalTable'), 'ValidTo', 'GeneratedAlwaysType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(2));

        cmd.CommandText = "SELECT CAST(OBJECTPROPERTY(OBJECT_ID('dbo.MyUpdatedTeporalTable'), 'TableTemporalType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(2));

        cmd.CommandText = "SELECT CAST(OBJECTPROPERTY(OBJECT_ID('dbo.MyUpdatedTeporalTable_Hist'), 'TableTemporalType') AS INT)";
        Assert.That(cmd.ExecuteScalar() as int?, Is.EqualTo(1));

        conn.Close();
    }

    [Test]
    public void ShouldAlterTemporalTable()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        Assert.That(GetColumnDataType(cmd, "MyAlteredTemporalTable", "Somedata"), Is.EqualTo("VARCHAR(500)"));
        Assert.That(GetColumnDataType(cmd, "MyAlteredTemporalTable_Hist", "Somedata"), Is.EqualTo("VARCHAR(500)"));

        Assert.That(GetColumnDataType(cmd, "MyAlteredTemporalTable", "NewCol"), Is.EqualTo("INT"));
        Assert.That(GetColumnDataType(cmd, "MyAlteredTemporalTable_Hist", "NewCol"), Is.EqualTo("INT"));

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
--QuenchTables_ShouldRemoveTemporalTracking
CREATE TABLE [dbo].[MyOldTeporalTable] 
  ([Id] INT NOT NULL, 
   [Somedata] VARCHAR(500) NOT NULL, 
   [ValidFrom] DATETIME2(7) GENERATED ALWAYS AS ROW START NOT NULL,
   [ValidTo] DATETIME2(7) GENERATED ALWAYS AS ROW END NOT NULL,
   CONSTRAINT [PK_MyOldTeporalTable] PRIMARY KEY NONCLUSTERED  ([Id]),
   PERIOD FOR SYSTEM_TIME ([ValidFrom], [ValidTo])
  ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[MyOldTeporalTable_Hist]))
--QuenchTables_ShouldMakeExistingTableTemporal
CREATE TABLE [dbo].[MyUpdatedTeporalTable] 
  ([Id] INT NOT NULL, 
   [Somedata] VARCHAR(500) NOT NULL, 
   CONSTRAINT [PK_MyUpdatedTeporalTable] PRIMARY KEY NONCLUSTERED  ([Id])
  )
INSERT [dbo].[MyUpdatedTeporalTable] ([Id], [SomeData]) VALUES(1, 'xx'), (2, 'yy')
--ShouldAlterTemporalTable
CREATE TABLE [dbo].[MyAlteredTemporalTable] 
  ([Id] INT NOT NULL, 
   [Somedata] VARCHAR(200) NOT NULL, 
   [ValidFrom] DATETIME2(7) GENERATED ALWAYS AS ROW START NOT NULL,
   [ValidTo] DATETIME2(7) GENERATED ALWAYS AS ROW END NOT NULL,
   CONSTRAINT [PK_MyAlteredTemporalTable] PRIMARY KEY NONCLUSTERED  ([Id]),
   PERIOD FOR SYSTEM_TIME ([ValidFrom], [ValidTo])
  ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[MyAlteredTemporalTable_Hist]))
""";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Schema": "[dbo]",
                "Name": "[MyNewTeporalTable]",
                "IsTemporal": true,
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Somedata]",
                      "DataType": "VARCHAR(500)",
                      "Nullable": false
                    }
                            ],
                "Indexes": [
                    {
                      "Name": "[PK_MyNewTeporalTable]",
                      "PrimaryKey": true,
                      "IndexColumns": "[Id]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[MyOldTeporalTable]",
                "IsTemporal": false,
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Somedata]",
                      "DataType": "VARCHAR(500)",
                      "Nullable": false
                    }
                            ],
                "Indexes": [
                    {
                      "Name": "[PK_MyOldTeporalTable]",
                      "PrimaryKey": true,
                      "IndexColumns": "[Id]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[MyUpdatedTeporalTable]",
                "IsTemporal": true,
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Somedata]",
                      "DataType": "VARCHAR(500)",
                      "Nullable": false
                    }
                            ],
                "Indexes": [
                    {
                      "Name": "[PK_MyUpdatedTeporalTable]",
                      "PrimaryKey": true,
                      "IndexColumns": "[Id]"
                    }
                ]
            },
            {
                "Schema": "[dbo]",
                "Name": "[MyAlteredTemporalTable]",
                "IsTemporal": true,
                "Columns": [
                    {
                      "Name": "[Id]",
                      "DataType": "INT",
                      "Nullable": false
                    },
                    {
                      "Name": "[Somedata]",
                      "DataType": "VARCHAR(500)",
                      "Nullable": false
                    },
                    {
                      "Name": "[NewCol]",
                      "DataType": "INT",
                      "Nullable": false
                    }
                            ],
                "Indexes": [
                    {
                      "Name": "[PK_MyAlteredTemporalTable]",
                      "PrimaryKey": true,
                      "IndexColumns": "[Id]"
                    }
                ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json);
    }
}
