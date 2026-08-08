// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    /// <summary>
    /// Genuine-old-binary milestone proof for the SQL Server emit-guards (Slice C). The baked-version
    /// UnsupportedFeaturePolicyTests force the &lt; 13 branch on the MODERN container, which proves the degrade
    /// LOGIC but cannot prove the two things only a real pre-2016 binary can: (1) the below-cliff XML ingest
    /// path runs the degrade code (compat-100 cannot OPENJSON, so the deploy goes through ParseTableXml /
    /// IndexOnlyXmlQuench), and (2) the suppressed emits actually leave nothing the old server would reject.
    ///
    /// [Explicit] — requires the on-demand SQL2008R2 instance (major 10 / compat 100) at 127.0.0.1,14330,
    /// started via C:\temp\sqlserver-oldbinaries\start-oldsql.ps1. Not run in CI (no genuine old binary there).
    /// Kindles with IngestEncoding.Xml + baked major 10 (SERVERPROPERTY('ProductMajorVersion') is NULL pre-2016,
    /// so the C#-baked value is authoritative — the whole reason fn_ServerMajorVersion bakes at kindle time).
    /// Assertions avoid 2016-only catalogs (sys.masked_columns, sys.columns.encryption_type) which do not exist
    /// on 2008; degrade is proven by deploy-success + the downgrade manifest + structural absence.
    /// </summary>
    [TestFixture]
    [Category("SqlServer")]
    [Explicit("Requires the genuine SQL2008R2 instance at 127.0.0.1,14330 (start-oldsql.ps1); not run in CI.")]
    public class GenuineSql2008EmitGuardCertTests
    {
        private const string MasterConn =
            "Server=127.0.0.1,14330;Database=master;User Id=sa;Password=SchemaSmith!Old2026;Encrypt=False;TrustServerCertificate=True";

        private readonly List<string> _createdDbs = new();

        // A product declaring all four above-2008 features across three tables: a temporal table, a table with
        // a masked column and an Always Encrypted column, and a table with a clustered columnstore index.
        private static string ProbeJson() => """
[
  {
    "Schema": "[dbo]", "Name": "[Cert_Temporal]", "IsTemporal": true,
    "Columns": [
      {"Name": "[Id]", "DataType": "INT", "Nullable": false, "PrimaryKey": true},
      {"Name": "[Val]", "DataType": "NVARCHAR(50)", "Nullable": false}
    ]
  },
  {
    "Schema": "[dbo]", "Name": "[Cert_Cols]",
    "Columns": [
      {"Name": "[Id]", "DataType": "INT", "Nullable": false},
      {"Name": "[Email]", "DataType": "NVARCHAR(100)", "Nullable": false, "DataMaskFunction": "email()"},
      {"Name": "[SSN]", "DataType": "NVARCHAR(11)", "Nullable": false, "EncryptionType": "DETERMINISTIC", "EncryptionKey": "[TestCEK]", "EncryptionAlgorithm": "AEAD_AES_256_CBC_HMAC_SHA_256"}
    ]
  },
  {
    "Schema": "[dbo]", "Name": "[Cert_Cci]",
    "Columns": [
      {"Name": "[Id]", "DataType": "INT", "Nullable": false},
      {"Name": "[Val]", "DataType": "NVARCHAR(50)", "Nullable": false}
    ],
    "Indexes": [
      {"Name": "[cci_Cert]", "Clustered": true, "ColumnStore": true, "PrimaryKey": false, "Unique": false}
    ]
  }
]
""";

        private IDbConnection KindleScratch2008(string prefix, string policy)
        {
            var db = $"{prefix}_{Guid.NewGuid():N}";
            using (var master = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(MasterConn))
            {
                master.Open();
                using var createCmd = master.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE [{db}]";
                createCmd.CommandTimeout = 300;
                createCmd.ExecuteNonQuery();
            }
            _createdDbs.Add(db);

            var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(
                $"Server=127.0.0.1,14330;Database={db};User Id=sa;Password=SchemaSmith!Old2026;Encrypt=False;TrustServerCertificate=True");
            conn.Open();
            using (var kindleCmd = conn.CreateCommand())
            {
                kindleCmd.CommandTimeout = 600;
                // The genuine below-cliff path: XML ingest encoding, real detected major version (10) baked in.
                ForgeKindler.KindleTheForge(kindleCmd, Platform.SqlServer, forceReKindle: true,
                    IngestEncoding.Xml, serverMajorVersion: 10, policy: policy);
            }
            return conn;
        }

        private static void DeployProbe(IDbCommand cmd, string policyProduct)
        {
            var xml = ModelXmlSerializer.ToIngestXml(ProbeJson(), "Tables", "Table");
            cmd.CommandTimeout = 600;
            cmd.CommandText =
                $"EXEC SchemaSmith.TableQuench @ProductName = '{policyProduct}', @TableDefinitions = @xml, " +
                "@WhatIf = 0, @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0";
            var p = cmd.CreateParameter();
            p.ParameterName = "@xml";
            p.Value = xml;
            p.DbType = DbType.String;
            cmd.Parameters.Add(p);
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
        }

        private static int Scalar(IDbCommand cmd, string sql)
        {
            cmd.CommandText = sql;
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        [OneTimeTearDown]
        public void DropScratchDatabases()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(MasterConn);
            conn.Open();
            using var cmd = conn.CreateCommand();
            foreach (var db in _createdDbs)
            {
                cmd.CommandText = $@"
IF DB_ID('{db}') IS NOT NULL
BEGIN
  ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [{db}];
END";
                cmd.ExecuteNonQuery();
            }
        }

        // warn (default): all four features deploy CLEANLY on a genuine 2008 R2 target via the XML path — the
        // 2016/2012-2014 DDL is never emitted, the objects come out plain, and one downgrade manifest row per
        // feature is recorded (surfaced in the run summary's Unsupported Feature Downgrades section).
        [Test]
        public void AllFourFeatures_OnGenuine2008_WarnPolicy_DeployCleanly_AndRecordDowngrades()
        {
            using var conn = KindleScratch2008("Cert2008Warn", policy: "warn");
            using var cmd = conn.CreateCommand();

            Assert.DoesNotThrow(() => DeployProbe(cmd, "Cert2008Warn"),
                "a product declaring temporal/masking/AE/columnstore must deploy cleanly on genuine SQL 2008 R2 (features degraded)");

            // All three tables created.
            Assert.That(Scalar(cmd, "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Cert_Temporal','Cert_Cols','Cert_Cci')"),
                Is.EqualTo(3), "all three tables must be created");

            // Temporal turn-on suppressed: no synthesized period columns (2016-only OBJECTPROPERTY/COLUMNPROPERTY
            // are unavailable on 2008, so assert the structural absence of ValidFrom/ValidTo instead).
            Assert.That(Scalar(cmd, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cert_Temporal') AND name IN ('ValidFrom','ValidTo')"),
                Is.EqualTo(0), "no period columns may be added below 2016 (temporal turn-on suppressed)");

            // Columnstore skipped: no clustered/nonclustered columnstore index (sys.indexes.type 5/6 exist on
            // 2008 as a column; there simply are no such rows).
            Assert.That(Scalar(cmd, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Cert_Cci') AND type IN (5,6)"),
                Is.EqualTo(0), "no columnstore index may exist below 2012/2014");

            // The masked/encrypted columns exist as plain columns (2016 catalogs to prove "not masked/encrypted"
            // don't exist on 2008; their absence is guaranteed by the engine — deploy success is the proof).
            Assert.That(Scalar(cmd, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cert_Cols') AND name IN ('Email','SSN')"),
                Is.EqualTo(2), "the masked + encrypted columns must be created (as plaintext)");

            // The decisive cross-feature proof: one downgrade manifest row per feature on the XML/compat-100 path.
            foreach (var objectType in new[]
                     {
                         "temporal (SQL Server 2016)", "data masking (SQL Server 2016)",
                         "Always Encrypted (SQL Server 2016)", "columnstore index (SQL Server 2012/2014)"
                     })
            {
                Assert.That(Scalar(cmd, $"SELECT COUNT(*) FROM SchemaSmith.ChangeAudit WHERE ActionType = 'downgraded' AND ObjectType = '{objectType}'"),
                    Is.GreaterThanOrEqualTo(1), $"a downgrade manifest row must be recorded for {objectType}");
            }
        }

        // fail (opt-in): the same product on genuine 2008 R2 aborts with a "requires SQL Server 2016" message
        // (data masking is the first degrade reached, in MissingTableAndColumnQuench).
        [Test]
        public void AllFourFeatures_OnGenuine2008_FailPolicy_Aborts()
        {
            using var conn = KindleScratch2008("Cert2008Fail", policy: "fail");
            using var cmd = conn.CreateCommand();

            var ex = Assert.Catch(() => DeployProbe(cmd, "Cert2008Fail"));
            Assert.That(ex!.Message, Does.Contain("requires SQL Server"),
                "the fail policy must abort naming the required version on a genuine old target");
        }
    }
}
