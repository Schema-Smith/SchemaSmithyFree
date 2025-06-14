using System;
using System.Data;
using Schema.DataAccess;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests;

[SetUpFixture]
public class FixtureSetup
{
    private string _integrationMainDb = "";
    private string _integrationSecondaryDb = "";
    private string _connectionString;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets(null);
        _connectionString = ConnectionString.Build(config["Target:Server"], "master", config["Target:User"], config["Target:Password"]);

        _integrationSecondaryDb = GenerateUniqueDBName(config["ScriptTokens:SecondaryDB"] ?? "TestSecondary");
        config["ScriptTokens:SecondaryDB"] = _integrationSecondaryDb;
        _integrationMainDb = GenerateUniqueDBName(config["ScriptTokens:MainDB"] ?? "TestMain");
        config["ScriptTokens:MainDB"] = _integrationMainDb;

        CreateTestDatabases();
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
CREATE DATABASE [{_integrationSecondaryDb}];
CREATE DATABASE [{_integrationMainDb}];
";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationMainDb);
        ForgeKindler.KindleTheForge(cmd);

        cmd.CommandText = @"
CREATE TYPE [Flag] FROM BIT NOT NULL

CREATE TABLE SchemaSmith.TestLog (Id INT IDENTITY(1,1) NOT NULL, Msg NVARCHAR(2000) NOT NULL)

CREATE FULLTEXT CATALOG [FT_Catalog] 
CREATE FULLTEXT STOPLIST [SL_Test];
ALTER FULLTEXT STOPLIST [SL_Test] ADD '$' LANGUAGE 'Neutral';

CREATE FULLTEXT CATALOG [FT_Catalog2] 
CREATE FULLTEXT STOPLIST [SL_Test2];
ALTER FULLTEXT STOPLIST [SL_Test2] ADD '$' LANGUAGE 'Neutral';
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

        conn.ChangeDatabase(_integrationSecondaryDb);
        ForgeKindler.KindleTheForge(cmd);

        cmd.CommandText = @"
CREATE TABLE SchemaSmith.TestLog (Id INT IDENTITY(1,1) NOT NULL, Msg NVARCHAR(2000) NOT NULL)
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

        DropOneDatabase(cmd, _integrationSecondaryDb);
        DropOneDatabase(cmd, _integrationMainDb);

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
