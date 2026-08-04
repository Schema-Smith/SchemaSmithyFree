// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.GenuineOldBinary;

// Genuine-old-binary RUNTIME deploy test (Slice E). The sibling OldBinaryXmlKindleTests proves the legacy
// (XML) helper set CREATEs on a real pre-2016 binary (the CREATE-time BINDING axis). This fixture proves the
// complementary RUNTIME axis: that actually APPLYING a schema on a genuine old binary works — i.e. the
// runtime catalog reads inside the apply procs (all ≤2008, with the 2016 temporal/masking/Always-Encrypted
// reads staged behind fn_ServerMajorVersion()>=13 dynamic blocks that must SKIP below 2016) execute without
// error, and the schema converges. It kindles the XML helper set, then EXECs SchemaSmith.TableQuench (the
// full-apply orchestrator: Parse → MissingTableAndColumn → Modified → MissingIndexes → ForeignKey) with a
// representative 2008-safe table (columns, clustered index, nonclustered PK, self-referencing FK, check,
// default, statistic), asserts the schema materialised, then re-runs it to prove idempotent convergence
// (the second pass drives ModifiedTableQuench, where the version-gated 2016 reads live).
//
// [Explicit], no [Category("SqlServer")] — CI/normal runs never touch it; run deliberately against a genuine
// instance (Ignores on 2016+):
//   SmithySettings_SqlServer__Server=127.0.0.1 SmithySettings_SqlServer__Port=14330 \
//   SmithySettings_SqlServer__User=sa SmithySettings_SqlServer__Password='SchemaSmith!Old2026' \
//   dotnet test Schema/Schema.IntegrationTests --filter FullyQualifiedName~GenuineOldBinary
[Explicit("Requires a genuine pre-2016 SQL Server instance; run manually via the SmithySettings_SqlServer__* env vars.")]
[TestFixture]
public class OldBinaryXmlDeployTests
{
    private const string ProductName = "OldBinDeploy";

    // Self-contained 2008-safe schema: one table exercising column create, a clustered index on a nullable
    // column, a nonclustered PK, a self-referencing FK (references the PK), a check constraint, a default, and
    // a statistic — i.e. every apply proc's runtime path. No FullTextIndex (its catalog would not exist in a
    // transient DB) and no 2016 features (temporal/masking/encryption/columnstore).
    private const string TableJsonArray = """
        [
          {
            "Schema": "[dbo]",
            "Name": "[OldBinDeployTest]",
            "CompressionType": "NONE",
            "Columns": [
              { "Name": "[TestID]", "DataType": "UNIQUEIDENTIFIER" },
              { "Name": "[ParentID]", "DataType": "UNIQUEIDENTIFIER", "Nullable": true },
              { "Name": "[DateCreated]", "DataType": "DATETIME", "Nullable": true },
              { "Name": "[Status]", "DataType": "TINYINT", "Default": "0" },
              { "Name": "[SomeText]", "DataType": "VARCHAR(2000)", "Nullable": true }
            ],
            "Indexes": [
              { "Name": "[CIX_OldBinDeploy_DateCreated]", "CompressionType": "NONE", "Clustered": true, "FillFactor": 100, "IndexColumns": "[DateCreated]" },
              { "Name": "[PK_OldBinDeploy]", "CompressionType": "NONE", "PrimaryKey": true, "Unique": true, "IndexColumns": "[TestID]" }
            ],
            "ForeignKeys": [
              { "Name": "[FK_OldBinDeploy_Self]", "Columns": "[ParentID]", "RelatedTableSchema": "[dbo]", "RelatedTable": "[OldBinDeployTest]", "RelatedColumns": "[TestID]" }
            ],
            "CheckConstraints": [
              { "Name": "[CK_OldBinDeploy_Status]", "Expression": "[Status]<(20)" }
            ],
            "Statistics": [
              { "Name": "[ST_OldBinDeploy_Status]", "Columns": "[Status]", "SampleSize": 100 }
            ]
          }
        ]
        """;

    private string _masterConnectionString = "";
    private string _server = "", _user = "", _password = "", _port = "";
    private Dictionary<string, string> _connProps = new();
    private int _serverMajor;
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

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        _serverMajor = TargetVersionDetector.Detect(cmd, Platform.SqlServer).ServerComparable;
        conn.Close();
    }

    [Test]
    public void FullTableApply_OnGenuineOldBinary_AtServerDefaultCompat() => DeployAndAssert(setCompat100: false);

    [Test]
    public void FullTableApply_OnGenuineOldBinary_AtCompat100() => DeployAndAssert(setCompat100: true);

    private void DeployAndAssert(bool setCompat100)
    {
        if (_serverMajor >= 13)
            Assert.Ignore($"Detected SQL Server major {_serverMajor} (2016+); this old-binary runtime path only matters below 2016.");

        var db = CreateDatabase(setCompat100 ? "OldBinDeploy100" : "OldBinDeployDflt", setCompat100);
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(DbConnectionString(db));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        // Kindle the legacy helper set (baked major, as production does).
        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer, forceReKindle: true, IngestEncoding.Xml, _serverMajor, "warn");

        var tableXml = ModelXmlSerializer.ToIngestXml(TableJsonArray, "Tables", "Table");

        // First apply: create the whole schema through the runtime apply procs on the genuine old binary.
        Assert.DoesNotThrow(() => RunTableQuench(cmd, tableXml),
            $"TableQuench must apply the schema on SQL Server major {_serverMajor}" + (setCompat100 ? " at compatibility level 100." : "."));

        AssertSchemaConverged(cmd, "after first apply");

        // Second apply (idempotent): drives ModifiedTableQuench over the now-existing objects — where the
        // fn_ServerMajorVersion()>=13-gated temporal/masking/Always-Encrypted reads live and must SKIP below 2016.
        Assert.DoesNotThrow(() => RunTableQuench(cmd, tableXml),
            "A second identical TableQuench (idempotent re-run) must converge without error on the old binary.");

        AssertSchemaConverged(cmd, "after idempotent re-run");

        conn.Close();
    }

    private static void RunTableQuench(IDbCommand cmd, string tableXml)
    {
        cmd.CommandText = "SchemaSmith.TableQuench";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Clear();
        AddParam(cmd, "@ProductName", ProductName);
        AddParam(cmd, "@TableDefinitions", tableXml);
        cmd.ExecuteNonQuery();
        cmd.Parameters.Clear();
        cmd.CommandType = CommandType.Text;
    }

    private static void AddParam(IDbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static void AssertSchemaConverged(IDbCommand cmd, string phase)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ScalarInt(cmd, "SELECT CASE WHEN OBJECT_ID('dbo.OldBinDeployTest','U') IS NULL THEN 0 ELSE 1 END"),
                Is.EqualTo(1), $"table dbo.OldBinDeployTest must exist ({phase})");
            Assert.That(ScalarInt(cmd, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OldBinDeployTest')"),
                Is.EqualTo(5), $"all 5 columns must exist ({phase})");
            Assert.That(ScalarInt(cmd, "SELECT CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.OldBinDeployTest'),'PK_OldBinDeploy','IndexID') IS NULL THEN 0 ELSE 1 END"),
                Is.EqualTo(1), $"PK index must exist ({phase})");
            Assert.That(ScalarInt(cmd, "SELECT CASE WHEN INDEXPROPERTY(OBJECT_ID('dbo.OldBinDeployTest'),'CIX_OldBinDeploy_DateCreated','IndexID') IS NULL THEN 0 ELSE 1 END"),
                Is.EqualTo(1), $"clustered index must exist ({phase})");
            Assert.That(ScalarInt(cmd, "SELECT CASE WHEN OBJECT_ID('dbo.FK_OldBinDeploy_Self','F') IS NULL THEN 0 ELSE 1 END"),
                Is.EqualTo(1), $"self-referencing FK must exist ({phase})");
            Assert.That(ScalarInt(cmd, "SELECT CASE WHEN OBJECT_ID('dbo.CK_OldBinDeploy_Status','C') IS NULL THEN 0 ELSE 1 END"),
                Is.EqualTo(1), $"check constraint must exist ({phase})");
            Assert.That(ScalarInt(cmd, "SELECT COUNT(*) FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('dbo.OldBinDeployTest')"),
                Is.GreaterThanOrEqualTo(1), $"the [Status] default must exist ({phase})");
            Assert.That(ScalarInt(cmd, "SELECT COUNT(*) FROM sys.stats WHERE object_id = OBJECT_ID('dbo.OldBinDeployTest') AND name = 'ST_OldBinDeploy_Status'"),
                Is.EqualTo(1), $"the statistic must exist ({phase})");
        });
    }

    private static int ScalarInt(IDbCommand cmd, string sql)
    {
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private string CreateDatabase(string prefix, bool setCompat100)
    {
        var db = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..8]}";
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{db}];" + (setCompat100 ? $" ALTER DATABASE [{db}] SET COMPATIBILITY_LEVEL = 100;" : "");
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
            // Classic guard, not DROP DATABASE IF EXISTS (2016 syntax) — this fixture targets pre-2016 binaries.
            cmd.CommandText = $@"
IF DB_ID('{db}') IS NOT NULL
BEGIN
  ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [{db}];
END";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }
}
