using System.Data;
using System;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Index = Schema.Domain.Index;

namespace SchemaTongs.IntegrationTests;

public class GenerateTableJsonTests
{
    private string _integrationDb = "";
    private string _connectionString;
    private string _testConnectionString;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets(null);
        _connectionString = ConnectionString.Build(config["Source:Server"], "master", config["Source:User"], config["Source:Password"]);
        _integrationDb = GenerateUniqueDBName("GenerateTableJson");

        CreateTestDatabases();

        _testConnectionString = ConnectionString.Build(config["Source:Server"], _integrationDb, config["Source:User"], config["Source:Password"]);
    }

    [Test]
    public void ShouldGenerateCorrectJsonForXMLIndexes()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE dbo.TestXMLIndexes (
    MyInt INT NOT NULL,
    MyXml XML,
    MyXmlWithSchema XML(ManuInstructionsSchemaCollection) NULL,
    CONSTRAINT [PK_TestXMLIndexes] PRIMARY KEY CLUSTERED (MyInt)
)

CREATE PRIMARY XML INDEX [XI_Primary_MyXml] ON dbo.TestXMLIndexes (MyXml)
CREATE XML INDEX [XI_Secondary_MyXml_Path] ON dbo.TestXMLIndexes (MyXml) USING XML INDEX [XI_Primary_MyXml] FOR PATH 
CREATE PRIMARY XML INDEX [XI_Primary_MyXmlWithSchema] ON dbo.TestXMLIndexes (MyXmlWithSchema)
CREATE XML INDEX [XI_Secondary_MyXmlWithSchema_Path] ON dbo.TestXMLIndexes (MyXmlWithSchema) USING XML INDEX [XI_Primary_MyXmlWithSchema] FOR PATH 
CREATE XML INDEX [XI_Secondary_MyXmlWithSchema_Value] ON dbo.TestXMLIndexes (MyXmlWithSchema) USING XML INDEX [XI_Primary_MyXmlWithSchema] FOR VALUE 
CREATE XML INDEX [XI_Secondary_MyXmlWithSchema_Property] ON dbo.TestXMLIndexes (MyXmlWithSchema) USING XML INDEX [XI_Primary_MyXmlWithSchema] FOR PROPERTY
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, "dbo", "TestXMLIndexes");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Schema, Is.EqualTo("[dbo]"));
        Assert.That(result.Name, Is.EqualTo("[TestXMLIndexes]"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.XmlIndexes, Is.Not.Null);
        Assert.That(result.XmlIndexes, Has.Count.EqualTo(6));

        AssertXmlIndexProperties(result.XmlIndexes[0], "XI_Primary_MyXml", "MyXml", true, null, null);
        AssertXmlIndexProperties(result.XmlIndexes[1], "XI_Primary_MyXmlWithSchema", "MyXmlWithSchema", true, null, null);
        AssertXmlIndexProperties(result.XmlIndexes[2], "XI_Secondary_MyXml_Path", "MyXml", false, "XI_Primary_MyXml", "PATH");
        AssertXmlIndexProperties(result.XmlIndexes[3], "XI_Secondary_MyXmlWithSchema_Path", "MyXmlWithSchema", false, "XI_Primary_MyXmlWithSchema", "PATH");
        AssertXmlIndexProperties(result.XmlIndexes[4], "XI_Secondary_MyXmlWithSchema_Property", "MyXmlWithSchema", false, "XI_Primary_MyXmlWithSchema", "PROPERTY");
        AssertXmlIndexProperties(result.XmlIndexes[5], "XI_Secondary_MyXmlWithSchema_Value", "MyXmlWithSchema", false, "XI_Primary_MyXmlWithSchema", "VALUE");

        conn.Close();
    }

    private void AssertXmlIndexProperties(XmlIndex xmlIndex, string name, string column, bool isPrimary, string primaryIndex, string secondaryIndexType)
    {
        Assert.That(xmlIndex.Name, Is.EqualTo($"[{name}]"), $"Name of {name}");
        Assert.That(xmlIndex.Column, Is.EqualTo($"[{column}]"), $"Column of {name}");
        Assert.That(xmlIndex.IsPrimary, Is.EqualTo(isPrimary), $"IsPrimary of {name}");
        Assert.That(xmlIndex.PrimaryIndex, Is.EqualTo(primaryIndex == null ? null : $"[{primaryIndex}]"), $"PrimaryIndex of {name}");
        Assert.That(xmlIndex.SecondaryIndexType, Is.EqualTo(secondaryIndexType), $"SecondaryIndexType of {name}");
    }

    [Test]
    public void ShouldGenerateCorrectJsonForForeignKeys()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE dbo.MyFKTable (Id INT NOT NULL PRIMARY KEY, Col2 INT, Col3 INT)
CREATE TABLE dbo.MyFKReferencedTable (Id INT NOT NULL PRIMARY KEY, RefCol INT NOT NULL)
CREATE UNIQUE INDEX IDX_RefKey ON dbo.MyFKReferencedTable (RefCol)
ALTER TABLE dbo.MyFKTable ADD CONSTRAINT FK_MyFKTable_Col3_MyFKReferencedTable_Id FOREIGN KEY (Col3) REFERENCES dbo.MyFKReferencedTable (Id) ON DELETE CASCADE
ALTER TABLE dbo.MyFKTable ADD CONSTRAINT FK_MyFKTable_Col2_MyFKReferencedTable_RefCol FOREIGN KEY (Col2) REFERENCES dbo.MyFKReferencedTable (RefCol) ON UPDATE CASCADE
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, "dbo", "MyFKTable");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Schema, Is.EqualTo("[dbo]"));
        Assert.That(result.Name, Is.EqualTo("[MyFKTable]"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.ForeignKeys, Is.Not.Null);
        Assert.That(result.ForeignKeys, Has.Count.EqualTo(2));

        Assert.That(result.ForeignKeys[0].Name, Is.EqualTo("[FK_MyFKTable_Col2_MyFKReferencedTable_RefCol]"));
        Assert.That(result.ForeignKeys[0].Columns, Is.EqualTo("[Col2]"));
        Assert.That(result.ForeignKeys[0].RelatedTableSchema, Is.EqualTo("[dbo]"));
        Assert.That(result.ForeignKeys[0].RelatedTable, Is.EqualTo("[MyFKReferencedTable]"));
        Assert.That(result.ForeignKeys[0].RelatedColumns, Is.EqualTo("[RefCol]"));
        Assert.That(result.ForeignKeys[0].CascadeOnDelete, Is.False);
        Assert.That(result.ForeignKeys[0].CascadeOnUpdate, Is.True);
        Assert.That(result.ForeignKeys[1].Name, Is.EqualTo("[FK_MyFKTable_Col3_MyFKReferencedTable_Id]"));
        Assert.That(result.ForeignKeys[1].Columns, Is.EqualTo("[Col3]"));
        Assert.That(result.ForeignKeys[1].RelatedTableSchema, Is.EqualTo("[dbo]"));
        Assert.That(result.ForeignKeys[1].RelatedTable, Is.EqualTo("[MyFKReferencedTable]"));
        Assert.That(result.ForeignKeys[1].RelatedColumns, Is.EqualTo("[Id]"));
        Assert.That(result.ForeignKeys[1].CascadeOnDelete, Is.True);
        Assert.That(result.ForeignKeys[1].CascadeOnUpdate, Is.False);

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForFullTextIndex()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE dbo.TestFullTextIndex (
    MyInt INT NOT NULL,
    MyBigInt BIGINT NOT NULL,
    MyString VARCHAR(100) NULL,
    CONSTRAINT [PK_TestFullTextIndex] PRIMARY KEY NONCLUSTERED (MyInt)
)

CREATE FULLTEXT INDEX ON dbo.TestFullTextIndex (MyString) KEY INDEX PK_TestFullTextIndex ON FT_Catalog WITH CHANGE_TRACKING = AUTO, STOPLIST = SL_TEST
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, "dbo", "TestFullTextIndex");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Schema, Is.EqualTo("[dbo]"));
        Assert.That(result.Name, Is.EqualTo("[TestFullTextIndex]"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.FullTextIndex, Is.Not.Null);

        Assert.That(result.FullTextIndex.Columns, Is.EqualTo("[MyString]"));
        Assert.That(result.FullTextIndex.KeyIndex, Is.EqualTo("[PK_TestFullTextIndex]"));
        Assert.That(result.FullTextIndex.ChangeTracking, Is.EqualTo("AUTO"));
        Assert.That(result.FullTextIndex.StopList, Is.EqualTo("[SL_Test]"));
        Assert.That(result.FullTextIndex.FullTextCatalog, Is.EqualTo("[FT_Catalog]"));

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForCheckConstraint()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE dbo.TestChecks (
    MyInt INT NOT NULL,
    MyBigInt BIGINT NOT NULL,
    MyString VARCHAR(100) NULL,
    CONSTRAINT [CK_TestChecks_MyInt] CHECK (MyInt < MyBigInt),
    CONSTRAINT [CK_TestChecks_MyBigInt] CHECK (MyBigInt > MyInt)
)
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, "dbo", "TestChecks");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Schema, Is.EqualTo("[dbo]"));
        Assert.That(result.Name, Is.EqualTo("[TestChecks]"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.CheckConstraints, Is.Not.Null);
        Assert.That(result.CheckConstraints, Has.Count.EqualTo(2));

        Assert.That(result.CheckConstraints[0].Name, Is.EqualTo("[CK_TestChecks_MyBigInt]"));
        Assert.That(result.CheckConstraints[0].Expression, Is.EqualTo("[MyBigInt]>[MyInt]"));
        Assert.That(result.CheckConstraints[1].Name, Is.EqualTo("[CK_TestChecks_MyInt]"));
        Assert.That(result.CheckConstraints[1].Expression, Is.EqualTo("[MyInt]<[MyBigInt]"));

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForStatistics()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE dbo.TestStatistics (
    MyInt INT NOT NULL,
    MyBigInt BIGINT NOT NULL,
    MyString VARCHAR(100) NULL
)

CREATE STATISTICS [ST_TestStatistics_MyInt] ON dbo.TestStatistics (MyInt)
CREATE STATISTICS [ST_TestStatistics_MyBigInt_ForNullStrings] ON dbo.TestStatistics (MyBigInt) WHERE MyString IS NULL
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, "dbo", "TestStatistics");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Schema, Is.EqualTo("[dbo]"));
        Assert.That(result.Name, Is.EqualTo("[TestStatistics]"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.Statistics, Is.Not.Null);
        Assert.That(result.Statistics, Has.Count.EqualTo(2));

        AssertStatisticProperties(result.Statistics[0], "ST_TestStatistics_MyBigInt_ForNullStrings", "[MyBigInt]", "[MyString] IS NULL");
        AssertStatisticProperties(result.Statistics[1], "ST_TestStatistics_MyInt", "[MyInt]", null);

        conn.Close();
    }

    private void AssertStatisticProperties(Statistic statistic, string name, string columns, string filterExpression)
    {
        Assert.That(statistic.Name, Is.EqualTo($"[{name}]"), $"Name of {name}");
        Assert.That(statistic.Columns, Is.EqualTo(columns), $"Columns of {name}");
        Assert.That(statistic.FilterExpression, Is.EqualTo(filterExpression), $"FilterExpression of {name}");
    }

    [Test]
    public void ShouldGenerateCorrectJsonForIndexes()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE dbo.TestIndexes (
    MyInt INT NOT NULL,
    MyBigInt BIGINT NOT NULL,
    MyString VARCHAR(100) NULL,
    CONSTRAINT [PK_TestIndexes] PRIMARY KEY NONCLUSTERED (MyInt) WITH (FILLFACTOR = 80, DATA_COMPRESSION = NONE),
    CONSTRAINT [UQ_TestIndexes_MyString] UNIQUE (MyString),
) WITH (DATA_COMPRESSION = NONE)

CREATE UNIQUE NONCLUSTERED INDEX [UX_TestIndexes_MyInt] ON dbo.TestIndexes (MyInt) INCLUDE(MyString, MyBigInt) WITH (FILLFACTOR = 100, DATA_COMPRESSION = ROW)
CREATE NONCLUSTERED INDEX [IX_TestIndexes_MyString] ON dbo.TestIndexes (MyString, MyBigInt) WHERE MyString IS NOT NULL WITH (FILLFACTOR = 90, DATA_COMPRESSION = PAGE)
CREATE CLUSTERED INDEX [CX_TestIndexes_MyBigInt] ON dbo.TestIndexes (MyBigInt) WITH (FILLFACTOR = 100, DATA_COMPRESSION = PAGE)
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, "dbo", "TestIndexes");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Schema, Is.EqualTo("[dbo]"));
        Assert.That(result.Name, Is.EqualTo("[TestIndexes]"));
        Assert.That(result.CompressionType, Is.EqualTo("PAGE")); // will match the clustered index
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.Indexes, Is.Not.Null);
        Assert.That(result.Indexes, Has.Count.EqualTo(5));

        AssertIndexProperties(result.Indexes[0], "CX_TestIndexes_MyBigInt", true, false, false, false, "[MyBigInt]", null, 0, null, "PAGE");
        AssertIndexProperties(result.Indexes[1], "IX_TestIndexes_MyString", false, false, false, false, "[MyString],[MyBigInt]", null, 90, "[MyString] IS NOT NULL", "PAGE");
        AssertIndexProperties(result.Indexes[2], "PK_TestIndexes", false, true, true, false, "[MyInt]", null, 80, null, "NONE");
        AssertIndexProperties(result.Indexes[3], "UQ_TestIndexes_MyString", false, true, false, true, "[MyString]", null, 0, null, "NONE");
        AssertIndexProperties(result.Indexes[4], "UX_TestIndexes_MyInt", false, true, false, false, "[MyInt]", "[MyString],[MyBigInt]", 0, null, "ROW");

        conn.Close();
    }

    private void AssertIndexProperties(Index index, string name, bool isCustered, bool isUnique, bool isPrimaryKey, bool isUniqueConstraint, string columns, string includeColumns, int fillFactor, string filterExpression, string compression)
    {
        Assert.That(index.Name, Is.EqualTo($"[{name}]"), $"Name of {name}");
        Assert.That(index.PrimaryKey, Is.EqualTo(isPrimaryKey), $"PrimaryKey of {name}");
        Assert.That(index.Unique, Is.EqualTo(isUnique), $"Unique of {name}");
        Assert.That(index.Clustered, Is.EqualTo(isCustered), $"Clustered of {name}");
        Assert.That(index.UniqueConstraint, Is.EqualTo(isUniqueConstraint), $"UniqueConstraint of {name}");
        Assert.That(index.CompressionType, Is.EqualTo(compression), $"Compression of {name}");
        Assert.That(index.IndexColumns, Is.EqualTo(columns), $"IndexColumns of {name}");
        Assert.That(index.IncludeColumns, Is.EqualTo(includeColumns), $"IncludeColumns of {name}");
        Assert.That(index.FillFactor, Is.EqualTo(fillFactor), $"FillFactor of {name}");
        Assert.That(index.FilterExpression, Is.EqualTo(filterExpression), $"FilterExpression of {name}");
    }

    [Test]
    public void ShouldGenerateCorrectJsonForColumns()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_testConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE dbo.TestColumns (
    MyBit BIT NOT NULL,
    MyInt INT NULL,
    MyDecimal DECIMAL(10, 2) NOT NULL,
    MyNumeric NUMERIC(5, 3) NULL,
    MyNString NVARCHAR(100) NULL,
    MyString VARCHAR(200) NULL,
    MyDateTime DATETIME NULL,
    MyDateTime2 DATETIME2(4) NULL,
    MyXml XML NULL,
    MyMaxVarchar VARCHAR(MAX) NULL,
    MyMaxNvarchar NVARCHAR(MAX) NULL,
    MyMaxVarbinary VARBINARY(MAX) NULL,
    MyFloat FLOAT NULL,
    MyMoney MONEY NULL,
    MySmallint SMALLINT NULL,
    MyTinyint TINYINT NULL,
    MyBigint BIGINT NULL,
    MyUniqueIdentifier UNIQUEIDENTIFIER NULL,
    MyDate DATE NULL,
    MyTime TIME NULL,
    MySmallDateTime SMALLDATETIME NULL,
    MyBitWithDefault BIT NOT NULL DEFAULT 1,
    MyIntWithDefault INT NOT NULL DEFAULT 42,
    MyDecimalWithDefault DECIMAL(12, 4) NOT NULL DEFAULT 3.14,
    MyFlag [Test].[Flag],
    MySysname SYSNAME NULL,
    MyRowGuidCol UNIQUEIDENTIFIER ROWGUIDCOL NOT NULL,
    MyIdentity INT IDENTITY(13,2) NOT NULL,
    MyXmlWithSchema XML(ManuInstructionsSchemaCollection) NULL,
    MyRowVersion ROWVERSION NULL,
    CHECK (MyMoney > 10)
)
";
        cmd.ExecuteNonQuery();

        var result = GenerateTable(cmd, "dbo", "TestColumns");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Schema, Is.EqualTo("[dbo]"));
        Assert.That(result.Name, Is.EqualTo("[TestColumns]"));
        Assert.That(result.CompressionType, Is.EqualTo("NONE"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(30));
        AssertColumnProperties(result.Columns[0], "MyBigint", "BIGINT", true, null, null);
        AssertColumnProperties(result.Columns[1], "MyBit", "BIT", false, null, null);
        AssertColumnProperties(result.Columns[2], "MyBitWithDefault", "BIT", false, "1", null);
        AssertColumnProperties(result.Columns[3], "MyDate", "DATE", true, null, null);
        AssertColumnProperties(result.Columns[4], "MyDateTime", "DATETIME", true, null, null);
        AssertColumnProperties(result.Columns[5], "MyDateTime2", "DATETIME2(4)", true, null, null);
        AssertColumnProperties(result.Columns[6], "MyDecimal", "DECIMAL(10, 2)", false, null, null);
        AssertColumnProperties(result.Columns[7], "MyDecimalWithDefault", "DECIMAL(12, 4)", false, "3.14", null);
        AssertColumnProperties(result.Columns[8], "MyFlag", "TEST.FLAG", false, null, null);
        AssertColumnProperties(result.Columns[9], "MyFloat", "FLOAT", true, null, null);
        AssertColumnProperties(result.Columns[10], "MyIdentity", "INT IDENTITY(13, 2)", false, null, null);
        AssertColumnProperties(result.Columns[11], "MyInt", "INT", true, null, null);
        AssertColumnProperties(result.Columns[12], "MyIntWithDefault", "INT", false, "42", null);
        AssertColumnProperties(result.Columns[13], "MyMaxNvarchar", "NVARCHAR(MAX)", true, null, null);
        AssertColumnProperties(result.Columns[14], "MyMaxVarbinary", "VARBINARY(MAX)", true, null, null);
        AssertColumnProperties(result.Columns[15], "MyMaxVarchar", "VARCHAR(MAX)", true, null, null);
        AssertColumnProperties(result.Columns[16], "MyMoney", "MONEY", true, null, "[MyMoney]>(10)");
        AssertColumnProperties(result.Columns[17], "MyNString", "NVARCHAR(100)", true, null, null);
        AssertColumnProperties(result.Columns[18], "MyNumeric", "NUMERIC(5, 3)", true, null, null);
        AssertColumnProperties(result.Columns[19], "MyRowGuidCol", "UNIQUEIDENTIFIER ROWGUIDCOL", false, null, null);
        AssertColumnProperties(result.Columns[20], "MyRowVersion", "TIMESTAMP", true, null, null); // we can't seem to get ROWVERSION back from SqlServer even thought they support the type
        AssertColumnProperties(result.Columns[21], "MySmallDateTime", "SMALLDATETIME", true, null, null);
        AssertColumnProperties(result.Columns[22], "MySmallint", "SMALLINT", true, null, null);
        AssertColumnProperties(result.Columns[23], "MyString", "VARCHAR(200)", true, null, null);
        AssertColumnProperties(result.Columns[24], "MySysname", "SYSNAME", true, null, null);
        AssertColumnProperties(result.Columns[25], "MyTime", "TIME", true, null, null);
        AssertColumnProperties(result.Columns[26], "MyTinyint", "TINYINT", true, null, null);
        AssertColumnProperties(result.Columns[27], "MyUniqueIdentifier", "UNIQUEIDENTIFIER", true, null, null);
        AssertColumnProperties(result.Columns[28], "MyXml", "XML", true, null, null);
        AssertColumnProperties(result.Columns[29], "MyXmlWithSchema", "XML([dbo].[ManuInstructionsSchemaCollection])", true, null, null);

        Assert.That(result.CheckConstraints, Is.Not.Null);
        Assert.That(result.CheckConstraints, Has.Count.EqualTo(0));
        Assert.That(result.ForeignKeys, Is.Not.Null);
        Assert.That(result.ForeignKeys, Has.Count.EqualTo(0));
        Assert.That(result.Indexes, Is.Not.Null);
        Assert.That(result.Indexes, Has.Count.EqualTo(0));
        Assert.That(result.Statistics, Is.Not.Null);
        Assert.That(result.Statistics, Has.Count.EqualTo(0));
        Assert.That(result.FullTextIndex, Is.Null);
        Assert.That(result.XmlIndexes, Is.Not.Null);
        Assert.That(result.XmlIndexes, Has.Count.EqualTo(0));
        Assert.That(result.IsTemporal, Is.False);

        conn.Close();
    }

    private void AssertColumnProperties(Column column, string name, string dataType, bool nullable, string defaultValue, string check)
    {
        Assert.That(column.Name, Is.EqualTo($"[{name}]"), $"Name of {name}");
        Assert.That(column.DataType, Is.EqualTo(dataType), $"Type of {name}");
        Assert.That(column.Nullable, Is.EqualTo(nullable), $"Nullability of {name}");
        Assert.That(column.Default, Is.EqualTo(defaultValue), $"Default of {name}");
        Assert.That(column.CheckExpression, Is.EqualTo(check), $"Check of {name}");
    }

    private string GenerateTableJson(IDbCommand cmd, string schema, string table)
    {
        cmd.CommandText = $"EXEC [SchemaSmith].GenerateTableJson @p_Schema = '{schema}', @p_Table = '{table}'";
        using var reader = cmd.ExecuteReader();

        var tableJson = string.Empty;
        while (reader.Read())
        {
            tableJson += $"{reader.GetString(0)}\r\n";
        }

        return tableJson;
    }

    private Table GenerateTable(IDbCommand cmd, string schema, string table)
    {
        return JsonConvert.DeserializeObject<Table>(GenerateTableJson(cmd, schema, table));
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
    }

    private void CreateTestDatabases()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
CREATE DATABASE [{_integrationDb}];
";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationDb);
        ForgeKindler.KindleTheForge(cmd);

        cmd.CommandText = @"
CREATE FULLTEXT CATALOG [FT_Catalog] 
CREATE FULLTEXT STOPLIST [SL_Test];
ALTER FULLTEXT STOPLIST [SL_Test] ADD '$' LANGUAGE 'Neutral';

EXEC('CREATE SCHEMA [Test]')
CREATE TYPE [Test].[Flag] FROM BIT NOT NULL
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE XML SCHEMA COLLECTION ManuInstructionsSchemaCollection AS  
N'<?xml version=""1.0"" encoding=""UTF-16""?>  
<xsd:schema targetNamespace=""https://schemas.microsoft.com/sqlserver/2004/07/adventure-works/ProductModelManuInstructions""   
   xmlns          =""https://schemas.microsoft.com/sqlserver/2004/07/adventure-works/ProductModelManuInstructions""   
   elementFormDefault=""qualified""   
   attributeFormDefault=""unqualified""  
   xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" >  
  
    <xsd:complexType name=""StepType"" mixed=""true"" >  
        <xsd:choice  minOccurs=""0"" maxOccurs=""unbounded"" >   
            <xsd:element name=""tool"" type=""xsd:string"" />  
            <xsd:element name=""material"" type=""xsd:string"" />  
            <xsd:element name=""blueprint"" type=""xsd:string"" />  
            <xsd:element name=""specs"" type=""xsd:string"" />  
            <xsd:element name=""diag"" type=""xsd:string"" />  
        </xsd:choice>   
    </xsd:complexType>  
  
    <xsd:element  name=""root"">  
        <xsd:complexType mixed=""true"">  
            <xsd:sequence>  
                <xsd:element name=""Location"" minOccurs=""1"" maxOccurs=""unbounded"">  
                    <xsd:complexType mixed=""true"">  
                        <xsd:sequence>  
                            <xsd:element name=""step"" type=""StepType"" minOccurs=""1"" maxOccurs=""unbounded"" />  
                        </xsd:sequence>  
                        <xsd:attribute name=""LocationID"" type=""xsd:integer"" use=""required""/>  
                        <xsd:attribute name=""SetupHours"" type=""xsd:decimal"" use=""optional""/>  
                        <xsd:attribute name=""MachineHours"" type=""xsd:decimal"" use=""optional""/>  
                        <xsd:attribute name=""LaborHours"" type=""xsd:decimal"" use=""optional""/>  
                        <xsd:attribute name=""LotSize"" type=""xsd:decimal"" use=""optional""/>  
                    </xsd:complexType>  
                </xsd:element>  
            </xsd:sequence>  
        </xsd:complexType>  
    </xsd:element>  
</xsd:schema>';  
";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace(" - ", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabases()
    {
        using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        DropOneDatabase(cmd, _integrationDb);

        conn.Close();
    }

    private static void DropOneDatabase(IDbCommand cmd, string dbName)
    {
        cmd.CommandText = @$"
IF DB_ID('{dbName}') IS NOT NULL
  ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{dbName}];
";
        cmd.ExecuteNonQuery();
    }
}