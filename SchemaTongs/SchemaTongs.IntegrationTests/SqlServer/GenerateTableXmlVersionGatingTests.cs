// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaTongs.IntegrationTests.SqlServer;

// Slice E1.1: the 2016-era catalog reads in GenerateTableXml (temporal_type, generated_always_type, the
// Always Encrypted metadata, and sys.masked_columns.masking_function) are staged through a
// fn_ServerMajorVersion() >= 13 guarded dynamic block so the procedure CREATEs on a genuine pre-2016 binary.
// The CREATE-time BINDING proof (a static reference would fail to compile below 2016) is inherently a
// genuine-old-binary check — the modern CI container has every column, so it can only exercise the GATING
// LOGIC. This proves that: baking the detected major below 13 skips the dynamic read, so a masked column's
// mask is dropped (as it must be on a target that cannot support masking), while at/above 13 it is emitted.
// Masking is the representative 2016 read here; temporal / Always Encrypted ride the same version guard.
[Category("SqlServer")]
[TestFixture]
public class GenerateTableXmlVersionGatingTests
{
    private string _integrationDb = "";
    private string _connectionString = "";
    private string _testConnectionString = "";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var connProps = ConnectionString.ReadProperties(config, "SqlServer:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.SqlServer, config["SqlServer:Server"], "master", config["SqlServer:User"], config["SqlServer:Password"], config["SqlServer:Port"], connProps);
        _integrationDb = $"GenTableXmlGate_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..8]}";
        _testConnectionString = ConnectionString.Build(Platform.SqlServer, config["SqlServer:Server"], _integrationDb, config["SqlServer:User"], config["SqlServer:Password"], config["SqlServer:Port"], connProps);

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{_integrationDb}];";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationDb);
        // A table carrying a 2016 feature (dynamic data masking) so the version gate has something to drop.
        cmd.CommandText = "CREATE TABLE dbo.Masked (Id INT NOT NULL PRIMARY KEY, Secret VARCHAR(50) MASKED WITH (FUNCTION = 'default()') NULL);";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void GenerateTableXml_GatesMaskingReadBelow2016_EmitsAtOrAbove()
    {
        // At/above 2016 (major 13) the version-gated dynamic block runs, so the mask is read and emitted.
        var maskedAt16 = GenerateWithBakedMajor(16);
        Assert.That(maskedAt16, Does.Contain("default()"),
            "at major 13+ the masking read runs, so DataMaskFunction must be emitted");

        // Below 2016 the guard skips the dynamic read (as it must — a pre-2016 target cannot mask), so #ColMeta
        // stays empty and the mask is dropped. This is the same gate that lets the proc CREATE on the old binary.
        var maskedAt12 = GenerateWithBakedMajor(12);
        Assert.That(maskedAt12, Does.Not.Contain("default()"),
            "below major 13 the masking read is gated out, so DataMaskFunction must be dropped");
    }

    // Re-kindle fn_ServerMajorVersion with the baked major (the JSON kindle installs it on both encodings),
    // (re)create GenerateTableXml from its resource, and run it against the masked table.
    private string GenerateWithBakedMajor(int major)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer, forceReKindle: true, IngestEncoding.Json, serverMajorVersion: major);
        ForgeKindler.KindleOneFile(cmd, "SchemaSmith.GenerateTableXml.sql", Platform.SqlServer);

        cmd.CommandText = "EXEC [SchemaSmith].GenerateTableXml @p_Schema = 'dbo', @p_Table = 'Masked'";
        using var reader = cmd.ExecuteReader();
        var xml = "";
        while (reader.Read()) xml += reader.GetValue(0)?.ToString();
        conn.Close();
        return xml;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @$"
IF DB_ID('{_integrationDb}') IS NOT NULL
  ALTER DATABASE [{_integrationDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{_integrationDb}];";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
