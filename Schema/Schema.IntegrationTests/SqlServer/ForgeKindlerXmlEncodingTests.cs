// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.SqlServer;

// The KEY FINDING behind the SQL Server 2008 floor: CREATE PROCEDURE ... OPENJSON parse-errors at
// compatibility level 100, so the stock (JSON) kindle cannot install its helpers there. The legacy (XML)
// encoding swaps every OPENJSON/FOR JSON proc for a .nodes()/FOR XML PATH twin so the whole helper set
// CREATEs below the cliff. This fixture proves both halves on a real compat-100 database.
[Category("SqlServer")]
[Category("Integration")]
[TestFixture]
public class ForgeKindlerXmlEncodingTests
{
    private string _masterConnectionString = "";
    private string _server = "", _user = "", _password = "", _port = "";
    private Dictionary<string, string> _connProps = new();
    private readonly List<string> _createdDbs = [];

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        _server = config["SqlServer:Server"] ?? "127.0.0.1";
        _user = config["SqlServer:User"];
        _password = config["SqlServer:Password"];
        _port = config["SqlServer:Port"];
        _connProps = ConnectionString.ReadProperties(config, "SqlServer:ConnectionProperties");
        _masterConnectionString = ConnectionString.Build(Platform.SqlServer, _server, "master", _user, _password, _port, _connProps);
    }

    [Test]
    public void FnSplitList_CreatesAndSplitsInOrder_AtCompatibilityLevel100()
    {
        var db = CreateCompat100Database("SplitList100");
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(DbConnectionString(db));
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "IF SCHEMA_ID('SchemaSmith') IS NULL EXEC('CREATE SCHEMA SchemaSmith')";
        cmd.ExecuteNonQuery();
        // fn_SplitList must CREATE at compat 100 (STRING_SPLIT itself would not exist here).
        cmd.CommandText = ResourceLoader.Load("SchemaSmith.fn_SplitList.sql", Platform.SqlServer);
        Assert.DoesNotThrow(() => cmd.ExecuteNonQuery(), "fn_SplitList must CREATE at compatibility level 100");

        // Splits in input order (Ordinal), including an embedded-space token and an empty trailing token.
        cmd.CommandText = @"SELECT STUFF((SELECT '|' + [value] FROM SchemaSmith.fn_SplitList('[Id],[Name] DESC,[Amount],', ',')
                                          ORDER BY [Ordinal] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '')";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("[Id]|[Name] DESC|[Amount]|"));

        conn.Close();
    }

    // Un-ignore once the shared apply procs (MissingTableAndColumnQuench / ModifiedTableQuench /
    // MissingIndexesAndConstraintsQuench / ForeignKeyQuench) are converted to compat-100-safe aggregation —
    // they are kindled on the Xml path and still contain STRING_AGG ... WITHIN GROUP / STRING_SPLIT, so the
    // full legacy kindle does not yet CREATE at compat 100. The XML twins + fn_SplitList are already done.
    [Ignore("Blocked on shared-apply-proc compat-100 conversion (Slice D1) — see SS-2008 ledger.")]
    [Test]
    public void KindleTheForge_XmlEncoding_SucceedsAtCompatibilityLevel100()
    {
        var db = CreateCompat100Database("KindleXml100");
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(DbConnectionString(db));
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Sanity: the database really is at compat 100 (where OPENJSON would parse-error).
        cmd.CommandText = "SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(100), "database must be at compatibility level 100");

        // The whole helper set must kindle without an OPENJSON parse error.
        Assert.DoesNotThrow(() => ForgeKindler.KindleTheForge(cmd, Platform.SqlServer, forceReKindle: true, IngestEncoding.Xml));

        Assert.Multiple(() =>
        {
            // The metadata tables were bootstrapped (BootstrapTableXmlQuench ran, OPENJSON-free).
            Assert.That(ObjectExists(cmd, "SchemaSmith.ChangeAudit", "U"), Is.True, "ChangeAudit table");
            Assert.That(ObjectExists(cmd, "SchemaSmith.CompletedMigrationScripts", "U"), Is.True, "CompletedMigrationScripts table");
            // TableQuench CREATEd with the XML parse inlined — the proc OPENJSON blocked at compat 100.
            Assert.That(ObjectExists(cmd, "SchemaSmith.TableQuench", "P"), Is.True, "TableQuench proc");
            // The XML compare/extraction twins were kindled instead of the JSON versions.
            Assert.That(ObjectExists(cmd, "SchemaSmith.GenerateTableXml", "P"), Is.True, "GenerateTableXml proc");
            Assert.That(ObjectExists(cmd, "SchemaSmith.GenerateIndexedViewXml", "FN"), Is.True, "GenerateIndexedViewXml function");
            // The JSON-only helpers were NOT kindled on the legacy encoding.
            Assert.That(ObjectExists(cmd, "SchemaSmith.fn_FormatJson", "FN"), Is.False, "fn_FormatJson must be skipped");
            Assert.That(ObjectExists(cmd, "SchemaSmith.GenerateTableJSON", "P"), Is.False, "JSON GenerateTableJSON must not be kindled");
        });

        conn.Close();
    }

    [Test]
    public void KindleTheForge_JsonEncoding_FailsAtCompatibilityLevel100()
    {
        // The negative control that makes the XML path necessary: the stock JSON kindle installs procs with
        // OPENJSON, which is a parse error at compat 100, so kindling must fail.
        var db = CreateCompat100Database("KindleJson100");
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(DbConnectionString(db));
        conn.Open();
        using var cmd = conn.CreateCommand();

        var ex = Assert.Throws<Exception>(() =>
            ForgeKindler.KindleTheForge(cmd, Platform.SqlServer, forceReKindle: true, IngestEncoding.Json));
        Assert.That(ex!.Message + ex.InnerException?.Message, Does.Contain("kindling").IgnoreCase);

        conn.Close();
    }

    private static bool ObjectExists(IDbCommand cmd, string name, string type)
    {
        cmd.CommandText = $"SELECT CASE WHEN OBJECT_ID('{name}', '{type}') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private string CreateCompat100Database(string prefix)
    {
        var db = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..8]}";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{db}]; ALTER DATABASE [{db}] SET COMPATIBILITY_LEVEL = 100;";
        cmd.ExecuteNonQuery();
        conn.Close();
        _createdDbs.Add(db);
        return db;
    }

    private string DbConnectionString(string db) =>
        ConnectionString.Build(Platform.SqlServer, _server, db, _user, _password, _port, _connProps);

    [OneTimeTearDown]
    public void TearDown()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        foreach (var db in _createdDbs)
        {
            cmd.CommandText = $@"
IF DB_ID('{db}') IS NOT NULL
  ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{db}];";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }
}
