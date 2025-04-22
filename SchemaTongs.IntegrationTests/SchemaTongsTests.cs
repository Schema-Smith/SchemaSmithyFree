using System.Data;
using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Schema.DataAccess;
using Schema.Isolators;
using Schema.Utility;
using log4net;
using NSubstitute;

namespace SchemaTongs.IntegrationTests;

public class SchemaTongsTests
{
    private string _integrationDb = "";
    private string _connectionString;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets(null);
        _connectionString = ConnectionString.Build(config["Source:Server"], "master", config["Source:User"], config["Source:Password"]);
        _integrationDb = GenerateUniqueDBName(config["Source:database"] ?? "TongsTest");

        CreateTestDatabases();
    }

    [Test]
    public void ShouldCastTables()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Tables"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Tables", "Test.TestTable.json"))), Arg.Any<string>());

            config["ShouldCast:Tables"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastViews()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Views"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Views", "Test.TestView.sql"))), Arg.Any<string>());

            config["ShouldCast:Views"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }


    [Test]
    public void ShouldCastStoredProcedures()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:StoredProcedures"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Procedures", "Test.TestProcedure.sql"))), Arg.Any<string>());

            config["ShouldCast:StoredProcedures"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastUserDefinedFunctions()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:UserDefinedFunctions"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Functions", "Test.TestFunction.sql"))), Arg.Any<string>());

            config["ShouldCast:UserDefinedFunctions"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastUserDefinedTypes()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:UserDefinedTypes"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Types", "Test.Flag.sql"))), Arg.Any<string>());

            config["ShouldCast:UserDefinedTypes"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastSchemas()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Schemas"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Schemas", "Test.sql"))), Arg.Any<string>());

            config["ShouldCast:Schemas"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastTableTriggers()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:TableTriggers"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("Triggers", "Test.TestTable.TestTrigger.sql"))), Arg.Any<string>());

            config["ShouldCast:TableTriggers"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastCatalogs()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:Catalogs"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("FullTextCatalogs", "FT_Catalog.sql"))), Arg.Any<string>());

            config["ShouldCast:Catalogs"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastStopLists()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:StopLists"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("FullTextStopLists", "SL_Test.sql"))), Arg.Any<string>());

            config["ShouldCast:StopLists"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastDDLTriggers()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:DDLTriggers"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("DDLTriggers", "safety.sql"))), Arg.Any<string>());

            config["ShouldCast:DDLTriggers"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCastXMLSchemaCollections()
    {
        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var file = Substitute.For<IFile>();
        var directory = Substitute.For<IDirectory>();
        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(environment);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            var config = SetupConfig();
            config["ShouldCast:XMLSchemaCollections"] = "true";

            var tongs = new SchemaTongs();
            tongs.CastTemplate();

            file.Received(6).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            file.Received(3).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(".schema")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("product.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase("template.json")), Arg.Any<string>());
            file.Received(1).WriteAllText(Arg.Is<string>(s => s.EndsWithIgnoringCase(Path.Combine("XMLSchemaCollections", "dbo.ManuInstructionsSchemaCollection.sql"))), Arg.Any<string>());

            config["ShouldCast:XMLSchemaCollections"] = "false";
            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
    }

    private IConfigurationRoot SetupConfig()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets(null);
        config["Source:database"] = _integrationDb;

        config["ShouldCast:Tables"] = "false";
        config["ShouldCast:Schemas"] = "false";
        config["ShouldCast:UserDefinedTypes"] = "false";
        config["ShouldCast:UserDefinedFunctions"] = "false";
        config["ShouldCast:Views"] = "false";
        config["ShouldCast:StoredProcedures"] = "false";
        config["ShouldCast:TableTriggers"] = "false";
        config["ShouldCast:Catalogs"] = "false";
        config["ShouldCast:StopLists"] = "false";
        config["ShouldCast:DDLTriggers"] = "false";
        config["ShouldCast:XMLSchemaCollections"] = "false";
        return config;
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
CREATE TYPE [Test].[Flag] FROM [bit] NOT NULL
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE TABLE Test.TestTable (Column1 INT NOT NULL, Column2 VARCHAR(200) NULL, Column3 [Test].[Flag])
CREATE UNIQUE CLUSTERED INDEX UDX_Key ON Test.TestTable ([Column1])
CREATE FULLTEXT INDEX ON Test.TestTable (Column2) KEY INDEX UDX_Key ON [FT_Catalog] WITH CHANGE_TRACKING=AUTO, STOPLIST = [SL_Test]; 
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

        cmd.CommandText = @"
CREATE TRIGGER [safety] ON DATABASE FOR DROP_TABLE
AS   
   INSERT INTO Test.TestLog (Msg) VALUES ('Dropping Tables is bad!');
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE TRIGGER Test.TestTrigger ON Test.TestTable AFTER INSERT
AS
BEGIN
    DECLARE @id INT;
    SELECT @id = Column1 FROM inserted;
    INSERT INTO Test.TestLog (Msg) VALUES ('Trigger fired for ID: ' + CAST(@id AS VARCHAR(10)));
END;
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE VIEW Test.TestView 
AS 
SELECT * 
  FROM Test.TestTable
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE FUNCTION Test.TestFunction(@param INT) RETURNS INT
AS
BEGIN
    RETURN @param;
END;
";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
CREATE PROCEDURE Test.TestProcedure @param INT
AS
BEGIN
    SELECT @param;
END;
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