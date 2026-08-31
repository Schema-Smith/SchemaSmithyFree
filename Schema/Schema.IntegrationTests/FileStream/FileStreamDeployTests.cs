// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.FileStream;

// FILESTREAM deployment, against a genuine Windows SQL Server instance with FILESTREAM enabled.
//
// [Explicit] with NO [Category("SqlServer")], so CI's Category=SqlServer filter skips it -- the same
// arrangement OldBinaryXmlKindleTests uses, and for the same reason: the thing under test is not
// reachable from CI. FILESTREAM is unsupported on SQL Server on Linux and every CI engine job is a Linux
// container; Windows containers were tried for this repo and did not work out. No arrangement of CI
// certifies this, so it is certified locally and deliberately.
//
// Run against a Windows instance with FILESTREAM enabled (the FilestreamSettings WMI class, then
// sp_configure 'filestream access level'), pointing the usual SmithySettings_SqlServer__* env vars at
// it. It Ignores rather than fails when FILESTREAM is off, so an explicit run against the wrong target
// says why instead of looking broken.
[Explicit("Requires a Windows SQL Server instance with FILESTREAM enabled; run manually via the SmithySettings_SqlServer__* env vars.")]
[TestFixture]
public class FileStreamDeployTests
{
    private IDbConnection _connection;
    private string _db;
    private bool _fileStreamAvailable;
    private IngestEncoding _encoding = IngestEncoding.Json;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var server = config["SqlServer:Server"] ?? "127.0.0.1";
        var user = config["SqlServer:User"];
        var password = config["SqlServer:Password"];
        var port = config["SqlServer:Port"];
        var props = ConnectionString.ReadProperties(config, "SqlServer:ConnectionProperties");

        _connection = DbConnectionFactory.ForPlatform(Platform.SqlServer)
            .GetDbConnection(ConnectionString.Build(Platform.SqlServer, server, "master", user, password, port, props));
        _connection.Open();

        _fileStreamAvailable = Scalar("SELECT CONVERT(INT, ISNULL(SERVERPROPERTY('FilestreamEffectiveLevel'), 0))") > 0;
        if (!_fileStreamAvailable) return;

        // Its own database, because a FILESTREAM filegroup is a create-time property.
        _db = $"SchemaFs_{Guid.NewGuid():N}"[..30];
        var dataPath = ScalarString("SELECT CONVERT(NVARCHAR(400), SERVERPROPERTY('InstanceDefaultDataPath'))");
        Exec($"CREATE DATABASE [{_db}] ON PRIMARY (NAME = {_db}_d, FILENAME = '{dataPath}{_db}.mdf'), "
             + $"FILEGROUP FsFg CONTAINS FILESTREAM (NAME = {_db}_fs, FILENAME = '{dataPath}{_db}Fs') "
             + $"LOG ON (NAME = {_db}_l, FILENAME = '{dataPath}{_db}.ldf')");
        _connection.ChangeDatabase(_db);

        using var cmd = _connection.CreateCommand();
        // Encoding by server version, not by default. The JSON ingest path uses STRING_AGG (2017+), and
        // the newest Windows instance available here is 2016 -- which is exactly the situation the XML
        // tier exists for. A happy side effect: this certifies FILESTREAM through the legacy XML parse
        // and generate scripts, while the degrade fixture on the modern Linux container certifies the
        // JSON ones.
        var serverMajor = TargetVersionDetector.Detect(cmd, Platform.SqlServer).ServerComparable;
        _encoding = serverMajor < 14 ? IngestEncoding.Xml : IngestEncoding.Json;
        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer, forceReKindle: true, _encoding, serverMajor, "warn");
    }

    [SetUp]
    public void RequireFileStream()
    {
        if (!_fileStreamAvailable)
            Assert.Ignore("FILESTREAM is not enabled on the target instance; nothing here can be proven against it.");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (_connection == null) return;
        try
        {
            if (_db != null && Environment.GetEnvironmentVariable("FS_KEEP_DB") == null)
            {
                _connection.ChangeDatabase("master");
                Exec($"ALTER DATABASE [{_db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
                Exec($"DROP DATABASE IF EXISTS [{_db}]");
            }
        }
        finally
        {
            _connection.Close();
            _connection.Dispose();
        }
    }

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();
    }

    private int Scalar(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var r = cmd.ExecuteScalar();
        return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
    }

    private string ScalarString(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar() as string;
    }

    private void Deploy(string json)
    {
        // The XML tier inlines the XML parser into TableQuench, so @TableDefinitions must be XML there --
        // handing it JSON creates the table and silently drops every column detail, which is exactly how
        // this fixture first failed. ModelXmlSerializer is the same conversion the product uses.
        var definitions = _encoding == IngestEncoding.Xml
            ? ModelXmlSerializer.ToIngestXml(json, "Tables", "Table")
            : json;

        using var cmd = _connection.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "EXEC SchemaSmith.TableQuench @ProductName = 'FsTest', @TableDefinitions = @TableDefinitions";
        var p = cmd.CreateParameter();
        p.ParameterName = "@TableDefinitions";
        p.Value = definitions;
        cmd.Parameters.Add(p);
        cmd.ExecuteNonQuery();
    }

    private static string Package(string table, string guidIndexKind) =>
        "[{ \"Schema\": \"[dbo]\", \"Name\": \"[" + table + "]\", \"Columns\": ["
        + " { \"Name\": \"[Id]\", \"DataType\": \"INT\", \"Nullable\": false },"
        + " { \"Name\": \"[G]\", \"DataType\": \"UNIQUEIDENTIFIER ROWGUIDCOL\", \"Nullable\": false, \"Default\": \"NEWID()\" },"
        + " { \"Name\": \"[Doc]\", \"DataType\": \"VARBINARY(MAX)\", \"Nullable\": true, \"FileStream\": true } ], \"Indexes\": ["
        + " { \"Name\": \"[PK_" + table + "]\", \"IndexColumns\": \"[Id]\", \"PrimaryKey\": true, \"Unique\": true },"
        + " { \"Name\": \"[UQ_" + table + "_G]\", \"IndexColumns\": \"[G]\", " + guidIndexKind + " } ] }]";

    [Test]
    public void AFileStreamColumn_Deploys_WhenTheRowGuidColIsCoveredByAUniqueConstraint()
    {
        Deploy(Package("FsOk", "\"UniqueConstraint\": true"));

        Assert.Multiple(() =>
        {
            Assert.That(Scalar("SELECT CONVERT(INT, is_filestream) FROM sys.columns "
                               + "WHERE [object_id] = OBJECT_ID('dbo.FsOk') AND name = 'Doc'"),
                Is.EqualTo(1), "the column has to actually be FILESTREAM, not merely present");

            Assert.That(Scalar("SELECT COUNT(*) FROM sys.tables WHERE name = 'FsOk' "
                               + "AND filestream_data_space_id IS NOT NULL"),
                Is.EqualTo(1), "and the table is bound to a FILESTREAM filegroup");
        });
    }

    [Test]
    public void AFileStreamColumn_IsIdempotent()
    {
        // The second deploy is the one that finds bugs: a pass that adds the column whenever it is
        // declared, rather than only when new, fails here on a duplicate column.
        Deploy(Package("FsTwice", "\"UniqueConstraint\": true"));
        Deploy(Package("FsTwice", "\"UniqueConstraint\": true"));

        Assert.That(Scalar("SELECT COUNT(*) FROM sys.columns WHERE [object_id] = OBJECT_ID('dbo.FsTwice') "
                           + "AND name = 'Doc'"), Is.EqualTo(1));
    }

    [Test]
    public void AUniqueIndexOnTheRowGuidCol_IsRefusedWithTheFixInTheMessage()
    {
        // The trap this feature had to be probed to discover: 5505 demands a unique CONSTRAINT, and a
        // unique INDEX does not satisfy it. SQL Server's own message never says so, so SchemaSmith must.
        var ex = Assert.Catch(() => Deploy(Package("FsIndexOnly", "\"Unique\": true")));

        Assert.That(ex, Is.Not.Null, "declaring FILESTREAM with only a unique index must not deploy quietly");
        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("ROWGUIDCOL"),
                "the message must name the property that is missing");
            Assert.That(ex.Message, Does.Contain("UniqueConstraint"),
                "and must name the exact package change that fixes it -- naming the problem without the "
                + "remedy just leaves the user with SQL Server's message, which is what this replaces");
        });
    }

    [Test]
    public void AFileStreamColumn_RoundTripsThroughExtraction()
    {
        Deploy(Package("FsRoundTrip", "\"UniqueConstraint\": true"));

        using var cmd = _connection.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = _encoding == IngestEncoding.Xml
            ? "EXEC SchemaSmith.GenerateTableXml @p_Schema = 'dbo', @p_Table = 'FsRoundTrip'"
            : "EXEC SchemaSmith.GenerateTableJson @p_Schema = 'dbo', @p_Table = 'FsRoundTrip'";
        var json = cmd.ExecuteScalar() as string ?? "";

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("FileStream"),
                "an extracted package that drops FileStream re-deploys the column as in-row storage -- a "
                + "silent storage change on every round trip");
            Assert.That(json, Does.Contain("ROWGUIDCOL"),
                "and the ROWGUIDCOL column has to come back carrying it -- it rides the DataType string "
                + "(\"UNIQUEIDENTIFIER ROWGUIDCOL\"), the same way IDENTITY does, so a round trip that "
                + "drops it produces a package that cannot satisfy 5505 on re-deploy");
        });
    }
}
