// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.SqlServer;

/// <summary>
/// SQL Server <c>XML_COMPRESSION</c> — the sibling of <c>DATA_COMPRESSION</c>, and independent of it.
/// <para><b>The version story is asymmetric, and it is the whole reason this was awkward.</b> Verified
/// live rather than read: the clause DEPLOYS from SQL Server 2022, but
/// <c>sys.partitions.xml_compression</c> does not exist there. On 2022 CU25 the column is only on
/// <c>sys.internal_partitions</c>, which reports NULL for an ordinary table; it appears on
/// <c>sys.partitions</c> in 2025. So 2022–2024 honour the setting and can never report it — the same
/// shape as MariaDB application-time periods (declarable 10.4.3, readable 11.4).</para>
/// <para><b>Two consequences fall out of that, and both are tested here.</b> First, a procedure naming
/// the column fails to CREATE on a server that lacks it — binding happens before any runtime <c>IF</c> —
/// so the reference is composed in or out at KINDLE time, when the server version is already known.
/// Second, extraction on 2022 returns nothing for it, so SchemaTongs carries the value forward from the
/// package it is refreshing rather than stripping a property the server is honouring.</para>
/// <para>These tests run against whatever the fixture points at and adapt: the round-trip assertions only
/// bite on 2025+, and the deploy assertions bite from 2022.</para>
/// </summary>
[Category("SqlServer")]
[Category("Integration")]
[TestFixture]
[NonParallelizable]
public class XmlCompressionTests
{
    private const string TableName = "xml_compression_test";
    private IDbConnection _connection = null!;
    private int _major;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
        _major = Convert.ToInt32(Scalar("SELECT CONVERT(INT, SERVERPROPERTY('ProductMajorVersion'))"));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        if (_major < 16)
            Assert.Ignore($"XML_COMPRESSION requires SQL Server 2022; this server is major {_major}. "
                          + "The degrade path is covered by the DegradeUnsupportedFeatures tests.");
        Exec($"DROP TABLE IF EXISTS dbo.{TableName}");
    }

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();
    }

    private object Scalar(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        return cmd.ExecuteScalar();
    }

    private static string Package(bool tableCompressed, bool indexCompressed) =>
        "[{ \"Schema\": \"dbo\", \"Name\": \"" + TableName + "\""
        + (tableCompressed ? ", \"XmlCompression\": true" : "")
        + ", \"Columns\": [ { \"Name\": \"id\", \"DataType\": \"INT\", \"Nullable\": false },"
        + " { \"Name\": \"doc\", \"DataType\": \"XML\", \"Nullable\": true },"
        + " { \"Name\": \"v\", \"DataType\": \"NVARCHAR(50)\", \"Nullable\": true } ],"
        + " \"Indexes\": [ { \"Name\": \"pk_" + TableName + "\", \"PrimaryKey\": true, \"Unique\": true, \"IndexColumns\": \"id\" },"
        + " { \"Name\": \"ix_" + TableName + "\", \"IndexColumns\": \"v\""
        + (indexCompressed ? ", \"XmlCompression\": true" : "") + " } ] }]";

    private void Deploy(bool tableCompressed, bool indexCompressed = false)
    {
        var json = Package(tableCompressed, indexCompressed).Replace("'", "''");
        Exec($"EXEC SchemaSmith.TableQuench @ProductName = 'XmlCompProduct', @TableDefinitions = '{json}'");
    }

    /// <summary>
    /// FOR JSON returns the document in 2033-character CHUNKS, one row each -- ExecuteScalar would take
    /// only the first, which is how this first "passed" while comparing against the string "{".
    /// </summary>
    private string ExtractedJson()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"EXEC SchemaSmith.GenerateTableJSON @p_Schema = 'dbo', @p_Table = '{TableName}'";
        cmd.CommandTimeout = 300;
        using var reader = cmd.ExecuteReader();
        var sb = new System.Text.StringBuilder();
        while (reader.Read()) sb.Append(reader.GetValue(0));
        return sb.ToString();
    }

    /// <summary>Reads the live setting. Only answerable on 2025+; returns null below that.</summary>
    private bool? LiveTableSetting()
    {
        if (_major < 17) return null;
        var v = Scalar($"SELECT MAX(CONVERT(TINYINT, p.xml_compression)) FROM sys.partitions p "
                       + $"WHERE p.object_id = OBJECT_ID('dbo.{TableName}') AND p.index_id < 2");
        return v == null || v == DBNull.Value ? null : Convert.ToInt32(v) == 1;
    }

    [Test]
    public void ADeclaredTableXmlCompression_Deploys()
    {
        Deploy(tableCompressed: true);

        Assert.That(Convert.ToInt32(Scalar($"SELECT COUNT(*) FROM sys.tables WHERE name = '{TableName}'")), Is.EqualTo(1),
            "the table has to deploy at all -- an ungated clause is a parser error below 2022");

        if (_major >= 17)
            Assert.That(LiveTableSetting(), Is.True, "and on 2025 the server must actually report it on");
    }

    [Test]
    public void ADeclaredIndexXmlCompression_Deploys()
    {
        // The property is per-object: an index does not inherit its table's setting.
        Deploy(tableCompressed: false, indexCompressed: true);

        Assert.That(Convert.ToInt32(Scalar(
            $"SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{TableName}') AND name = 'ix_{TableName}'")),
            Is.EqualTo(1));
    }

    [Test]
    public void ATableWithNoXmlColumn_StillAcceptsTheClause()
    {
        // Probed on 2022: unlike TEXTIMAGE_ON, which SQL Server rejects outright on a table with no
        // large-object column (error 1709), XML_COMPRESSION is accepted on a table with no xml column.
        // So no LOB-style guard is needed, and this test is what stops one being added defensively.
        var json = ("[{ \"Schema\": \"dbo\", \"Name\": \"" + TableName + "\", \"XmlCompression\": true,"
                    + " \"Columns\": [ { \"Name\": \"id\", \"DataType\": \"INT\", \"Nullable\": false } ],"
                    + " \"Indexes\": [ { \"Name\": \"pk_" + TableName + "\", \"PrimaryKey\": true, \"Unique\": true, \"IndexColumns\": \"id\" } ] }]")
                   .Replace("'", "''");

        Assert.DoesNotThrow(() => Exec($"EXEC SchemaSmith.TableQuench @ProductName = 'XmlCompProduct', @TableDefinitions = '{json}'"));
    }

    [Test]
    public void RedeployingAnUnchangedDeclaration_IsIdempotent()
    {
        // THE expensive-mistake guard. Below 2025 the catalog cannot report the current value, and a
        // comparison that read "cannot report" as "currently off" would REBUILD every declared-ON table
        // on every deploy -- a full data rebuild, forever. The drift block is version-guarded so it
        // simply does not run there.
        Deploy(tableCompressed: true);
        Exec($"DELETE FROM SchemaSmith.ChangeAudit WHERE ObjectName LIKE '%{TableName}%'");

        Deploy(tableCompressed: true);

        Assert.That(Convert.ToInt32(Scalar(
            $"SELECT COUNT(*) FROM SchemaSmith.ChangeAudit WHERE ActionType = 'modified' AND ObjectName LIKE '%{TableName}%'")),
            Is.Zero, "a second identical deploy must not rebuild the table");
    }

    [Test]
    public void TurningItOff_ConvergesOn2025()
    {
        if (_major < 17)
            Assert.Ignore("Drift detection needs sys.partitions.xml_compression, which arrives in SQL Server 2025. "
                          + "Below it the setting is applied at create and deliberately never re-evaluated.");

        Deploy(tableCompressed: true);
        Assert.That(LiveTableSetting(), Is.True, "precondition");

        Deploy(tableCompressed: false);

        Assert.That(LiveTableSetting(), Is.False, "where the server CAN report the setting, drift must converge");
    }

    [Test]
    public void ItRoundTripsThroughExtraction_On2025()
    {
        if (_major < 17)
            Assert.Ignore("sys.partitions.xml_compression arrives in SQL Server 2025. Below it extraction "
                          + "structurally cannot see the setting; PreserveXmlCompression_WhenSourceCannotReportIt "
                          + "covers what happens instead.");

        Deploy(tableCompressed: true);

        var json = ExtractedJson();
        Assert.That(json, Does.Contain("XmlCompression"), json);
    }

    [Test]
    public void ATableWithoutIt_ExtractsWithoutTheProperty()
    {
        // No-churn: emitting the property for every table would rewrite every committed SQL Server
        // package for a setting nobody declared.
        Deploy(tableCompressed: false);

        var json = ExtractedJson();
        Assert.That(json, Does.Not.Contain("XmlCompression"), json);
    }
}
