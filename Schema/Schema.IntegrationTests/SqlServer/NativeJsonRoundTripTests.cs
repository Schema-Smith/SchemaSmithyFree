// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.SqlServer;

// Does SQL Server 2025's native `json` type survive a round trip through the generic DataType
// passthrough? Certification, not a feature: the answer decides whether the type needs any work at all.
//
// SchemaSmith does not enumerate types. `DataType` is free text, so a `json` column deploys by simply
// being written down -- but extraction re-derives the type string from the catalog, and the shaping CASE
// in GenerateTableJson only special-cases CHAR/BINARY, NUMERIC/DECIMAL, DATETIME2, XML and
// UNIQUEIDENTIFIER. If `json` comes back as something else, an extracted package silently stops being
// the package that was deployed, and the next deploy sees drift forever.
//
// [Explicit] with no Category: the type is 2025-only and every CI engine job runs 2022. Point it at a
// 2025 instance with the usual SmithySettings_SqlServer__* env vars. It Ignores below 2025 rather than
// failing, so an explicit run against the wrong target says why.
[Explicit("Requires SQL Server 2025 (major 17); run manually via the SmithySettings_SqlServer__* env vars.")]
[TestFixture]
public class NativeJsonRoundTripTests
{
    private IDbConnection _connection;
    private string _db;
    private bool _is2025;

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

        _is2025 = Scalar("SELECT CONVERT(INT, PARSENAME(CONVERT(VARCHAR(64), SERVERPROPERTY('ProductVersion')), 4))") >= 17;
        if (!_is2025) return;

        _db = $"SchemaJson_{Guid.NewGuid():N}"[..30];
        Exec($"CREATE DATABASE [{_db}]");
        _connection.ChangeDatabase(_db);

        using var cmd = _connection.CreateCommand();
        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer);
    }

    [SetUp]
    public void Require2025()
    {
        if (!_is2025)
            Assert.Ignore("The native json type is SQL Server 2025 (major 17) only; nothing here applies below it.");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (_connection == null) return;
        try
        {
            if (_db != null)
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
        cmd.CommandTimeout = 300;
        return cmd.ExecuteScalar() as string ?? "";
    }

    /// <summary>
    /// Reads a whole FOR JSON result. SQL Server splits FOR JSON output into 2033-character chunks
    /// across rows, so ExecuteScalar returns only the first fragment -- literally "{" here, which is how
    /// this fixture first failed for a reason that had nothing to do with the json type.
    /// </summary>
    private string ReadAll(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        using var reader = cmd.ExecuteReader();
        var sb = new System.Text.StringBuilder();
        while (reader.Read())
            if (!reader.IsDBNull(0)) sb.Append(reader.GetString(0));
        return sb.ToString();
    }

    private void Deploy(string json)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "EXEC SchemaSmith.TableQuench @ProductName = 'JsonTest', "
                          + $"@TableDefinitions = N'{json.Replace("'", "''")}'";
        cmd.ExecuteNonQuery();
    }

    private static string Package(string table) =>
        "[{ \"Schema\": \"[dbo]\", \"Name\": \"[" + table + "]\", \"Columns\": ["
        + " { \"Name\": \"[Id]\", \"DataType\": \"INT\", \"Nullable\": false },"
        + " { \"Name\": \"[Doc]\", \"DataType\": \"JSON\", \"Nullable\": true } ], \"Indexes\": ["
        + " { \"Name\": \"[PK_" + table + "]\", \"IndexColumns\": \"[Id]\", \"PrimaryKey\": true, \"Unique\": true } ] }]";

    [Test]
    public void ANativeJsonColumn_Deploys()
    {
        Deploy(Package("JsonDeploy"));

        Assert.That(ScalarString("SELECT t.name FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id "
                                 + "WHERE c.[object_id] = OBJECT_ID('dbo.JsonDeploy') AND c.name = 'Doc'"),
            Is.EqualTo("json").IgnoreCase,
            "the free-text DataType passthrough should deploy a native json column with no special "
            + "handling at all -- if this fails, the type needs real work rather than a certification");
    }

    [Test]
    public void ANativeJsonColumn_ExtractsAsJson_NotAsSomethingElse()
    {
        Deploy(Package("JsonExtract"));

        var extracted = ReadAll("EXEC SchemaSmith.GenerateTableJson @p_Schema = 'dbo', @p_Table = 'JsonExtract'");

        Assert.That(extracted, Does.Contain("\"DataType\": \"json\"").IgnoreCase,
            "extraction re-derives the type string from the catalog rather than echoing what was "
            + "declared. If json comes back as anything else, every extracted package silently stops "
            + "being the package that was deployed.\nExtracted: " + extracted);
    }

    [Test]
    public void ANativeJsonColumn_IsIdempotent_OnRedeploy()
    {
        // The assertion that actually matters. A type that deploys and extracts but does not COMPARE
        // equal produces a table that is altered on every single run -- green, silent, and permanent.
        Deploy(Package("JsonIdempotent"));
        var firstModify = ScalarString(
            "SELECT CONVERT(VARCHAR(40), modify_date, 121) FROM sys.tables WHERE name = 'JsonIdempotent'");

        Deploy(Package("JsonIdempotent"));
        var secondModify = ScalarString(
            "SELECT CONVERT(VARCHAR(40), modify_date, 121) FROM sys.tables WHERE name = 'JsonIdempotent'");

        Assert.That(secondModify, Is.EqualTo(firstModify),
            "an unchanged package must not touch the table. A changed modify_date means the column was "
            + "altered again, which is the signature of a type the drift comparison cannot match against "
            + "itself -- invisible in a green run and permanent.");
    }
}
