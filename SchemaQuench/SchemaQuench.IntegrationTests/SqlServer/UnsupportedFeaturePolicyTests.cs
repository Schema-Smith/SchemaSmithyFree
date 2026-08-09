// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    // UnsupportedFeaturePolicy bakes the resolved policy ('warn' default | 'fail') into its body at KINDLE
    // time from Target:UnsupportedFeaturePolicy (the SS-2008 floor dropped the 2016+ SESSION_CONTEXT
    // transport, unavailable on a genuine pre-2016 binary). Any value other than an explicit 'fail'
    // resolves to the safe 'warn' default.
    [TestFixture]
    [Category("SqlServer")]
    public class UnsupportedFeaturePolicyTests : BakedKindleTestBase
    {
        [Test]
        public void UnsupportedFeaturePolicy_DefaultsToWarn_WhenNothingBaked()
        {
            // _mainDb is kindled by FixtureSetup with the default policy ('warn').
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("warn"));
        }

        [Test]
        public void UnsupportedFeaturePolicy_ReturnsFail_WhenKindledWithFail()
        {
            using var conn = KindleScratchDatabase("PolicyFailBake", policy: "fail");
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("fail"));
        }

        [Test]
        public void UnsupportedFeaturePolicy_ResolvesToWarn_ForAnyNonFailValue()
        {
            // The function's defensive CASE resolves anything other than an explicit 'fail' to 'warn' even if
            // an un-normalized value were ever baked (the C# caller normalizes to 'warn'/'fail' beforehand).
            using var conn = KindleScratchDatabase("PolicyBogusBake", policy: "bogus");
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("warn"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Temporal (SYSTEM_VERSIONING / PERIOD FOR SYSTEM_TIME) — SQL Server 2016 (major 13). Below the floor
        // the turn-on emit (MissingIndexesAndConstraintsQuench) is suppressed, so a declared temporal table
        // deploys as a plain table (warn, default) or aborts (fail) instead of hard-failing on the 2016-only
        // DDL. The scratch DB bakes fn_ServerMajorVersion() = 10 (SQL 2008 R2) to force the < 13 branch on the
        // modern container — the SS analogue of PostgreSQL's schemasmith.version_override.
        // ---------------------------------------------------------------------------------------------------

        private const string TemporalObjectType = "temporal (SQL Server 2016)";

        private static string TemporalTableJson(string tableName) => $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "IsTemporal": true,
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false, "PrimaryKey": true},
        {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": false}
    ]
}
""";

        private static int TableTemporalType(IDbCommand cmd, string tableName)
        {
            cmd.CommandText = $"SELECT CAST(OBJECTPROPERTY(OBJECT_ID('dbo.{tableName}'), 'TableTemporalType') AS INT)";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static int DowngradeRowCount(IDbCommand cmd, string objectType, string objectName)
        {
            cmd.CommandText = $@"SELECT COUNT(*) FROM SchemaSmith.ChangeAudit
                                 WHERE ActionType = 'downgraded'
                                   AND ObjectType = '{objectType}'
                                   AND ObjectName = '{objectName}'";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // warn (default): a table declared temporal on a < 2016 target is created as a plain (non-temporal)
        // table, the SYSTEM_VERSIONING/PERIOD emit suppressed, and a downgrade manifest row names the table.
        [Test]
        public void Temporal_BelowSql2016_WarnPolicy_DeploysNonTemporal_AndRecordsDowngrade()
        {
            var tableName = $"WarnTemporal_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("TemporalWarnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, TemporalTableJson(tableName), productName: tableName),
                "a temporal table must degrade (emit suppressed) below SQL Server 2016, not hard-fail on SYSTEM_VERSIONING");

            cmd.CommandText = $"SELECT OBJECT_ID('dbo.{tableName}')";
            Assert.That(cmd.ExecuteScalar(), Is.Not.EqualTo(DBNull.Value), "the table must still be created");
            Assert.That(TableTemporalType(cmd, tableName), Is.EqualTo(0), "the table must be plain (non-temporal) below 2016");
            Assert.That(DowngradeRowCount(cmd, TemporalObjectType, $"[dbo].[{tableName}]"), Is.EqualTo(1),
                "a downgrade manifest row must name the table that lost temporal tracking");
        }

        // No phantom churn: a second quench of the same temporal-declared table on a < 2016 target must not
        // error and must leave the table plain (the turn-on stays suppressed; nothing re-detects it modified).
        [Test]
        public void Temporal_BelowSql2016_WarnPolicy_SecondQuench_StaysNonTemporal()
        {
            var tableName = $"NoChurnTemporal_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("TemporalChurnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            RunTableQuenchProc(cmd, TemporalTableJson(tableName), productName: tableName);
            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, TemporalTableJson(tableName), productName: tableName),
                "a repeat quench below 2016 must stay idempotent");
            Assert.That(TableTemporalType(cmd, tableName), Is.EqualTo(0), "the table must remain plain after a second quench");
        }

        // fail (opt-in): a < 2016 target with a declared temporal table aborts with a clear "requires SQL
        // Server 2016" message rather than silently degrading.
        [Test]
        public void Temporal_BelowSql2016_FailPolicy_AbortsWithRequiresSql2016()
        {
            var tableName = $"FailTemporal_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("TemporalFailBake", serverMajorVersion: 10, policy: "fail");
            using var cmd = conn.CreateCommand();

            var ex = Assert.Catch(() => RunTableQuenchProc(cmd, TemporalTableJson(tableName), productName: tableName));
            Assert.That(ex!.Message, Does.Contain("requires SQL Server 2016"),
                "the fail policy must abort naming the required version");
        }

        // ---------------------------------------------------------------------------------------------------
        // Dynamic data masking (MASKED WITH) — SQL Server 2016 (major 13). Below the floor the column emit
        // (CREATE + ALTER paths) is suppressed and the modified-column detection ignores the mask diff, so a
        // masked column deploys unmasked (warn) or the quench aborts (fail) instead of hard-failing on the
        // 2016-only clause.
        // ---------------------------------------------------------------------------------------------------

        private const string DataMaskingObjectType = "data masking (SQL Server 2016)";

        private static string MaskedTableJson(string tableName) => $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[Email]", "DataType": "NVARCHAR(200)", "Nullable": false, "DataMaskFunction": "email()"}
    ]
}
""";

        private static int MaskedColumnCount(IDbCommand cmd, string tableName)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM sys.masked_columns WHERE [object_id] = OBJECT_ID('dbo.{tableName}')";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // warn (default): a masked column on a < 2016 target is created unmasked, the MASKED WITH emit
        // suppressed, and a downgrade manifest row names the column.
        [Test]
        public void DataMasking_BelowSql2016_WarnPolicy_DeploysUnmasked_AndRecordsDowngrade()
        {
            var tableName = $"WarnMask_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("MaskWarnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, MaskedTableJson(tableName), productName: tableName),
                "a masked column must degrade (emit suppressed) below SQL Server 2016, not hard-fail on MASKED WITH");

            cmd.CommandText = $"SELECT OBJECT_ID('dbo.{tableName}')";
            Assert.That(cmd.ExecuteScalar(), Is.Not.EqualTo(DBNull.Value), "the table must still be created");
            Assert.That(MaskedColumnCount(cmd, tableName), Is.EqualTo(0), "no column may be masked below 2016");
            Assert.That(DowngradeRowCount(cmd, DataMaskingObjectType, $"[dbo].[{tableName}].[Email]"), Is.EqualTo(1),
                "a downgrade manifest row must name the column that lost masking");
        }

        // No phantom churn: a second quench of the same masked-declared column on a < 2016 target must not
        // error and must leave the column unmasked (the mask diff is ignored in modified-column detection).
        [Test]
        public void DataMasking_BelowSql2016_WarnPolicy_SecondQuench_StaysUnmasked()
        {
            var tableName = $"NoChurnMask_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("MaskChurnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            RunTableQuenchProc(cmd, MaskedTableJson(tableName), productName: tableName);
            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, MaskedTableJson(tableName), productName: tableName),
                "a repeat quench below 2016 must stay idempotent");
            Assert.That(MaskedColumnCount(cmd, tableName), Is.EqualTo(0), "the column must remain unmasked after a second quench");
        }

        // fail (opt-in): a < 2016 target with a declared masked column aborts with "requires SQL Server 2016".
        [Test]
        public void DataMasking_BelowSql2016_FailPolicy_AbortsWithRequiresSql2016()
        {
            var tableName = $"FailMask_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("MaskFailBake", serverMajorVersion: 10, policy: "fail");
            using var cmd = conn.CreateCommand();

            var ex = Assert.Catch(() => RunTableQuenchProc(cmd, MaskedTableJson(tableName), productName: tableName));
            Assert.That(ex!.Message, Does.Contain("requires SQL Server 2016"),
                "the fail policy must abort naming the required version");
        }

        // ---------------------------------------------------------------------------------------------------
        // Always Encrypted (ENCRYPTED WITH) — SQL Server 2016 (major 13). Below the floor the column emit is
        // suppressed and the encryption diff is ignored in modified-column detection (so no swap-guard trip),
        // so an encrypted column deploys plaintext (warn) or the quench aborts (fail). The below-13 case never
        // references the CEK, so the throwaway kindle DB needs no Always Encrypted key infrastructure.
        // ---------------------------------------------------------------------------------------------------

        private const string AlwaysEncryptedObjectType = "Always Encrypted (SQL Server 2016)";

        private static string EncryptedTableJson(string tableName) => $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[SSN]", "DataType": "NVARCHAR(11)", "Nullable": false,
         "EncryptionType": "DETERMINISTIC", "EncryptionKey": "[TestCEK]", "EncryptionAlgorithm": "AEAD_AES_256_CBC_HMAC_SHA_256"}
    ]
}
""";

        private static int EncryptedColumnCount(IDbCommand cmd, string tableName)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM sys.columns WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND encryption_type IS NOT NULL";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // warn (default): an encrypted column on a < 2016 target is created plaintext, the ENCRYPTED WITH emit
        // suppressed (the CEK is never referenced), and a downgrade manifest row names the column.
        [Test]
        public void AlwaysEncrypted_BelowSql2016_WarnPolicy_DeploysPlaintext_AndRecordsDowngrade()
        {
            var tableName = $"WarnEnc_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("EncWarnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, EncryptedTableJson(tableName), productName: tableName),
                "an encrypted column must degrade (emit suppressed) below SQL Server 2016, not hard-fail on ENCRYPTED WITH");

            cmd.CommandText = $"SELECT OBJECT_ID('dbo.{tableName}')";
            Assert.That(cmd.ExecuteScalar(), Is.Not.EqualTo(DBNull.Value), "the table must still be created");
            Assert.That(EncryptedColumnCount(cmd, tableName), Is.EqualTo(0), "no column may be encrypted below 2016");
            Assert.That(DowngradeRowCount(cmd, AlwaysEncryptedObjectType, $"[dbo].[{tableName}].[SSN]"), Is.EqualTo(1),
                "a downgrade manifest row must name the column that lost encryption");
        }

        // No phantom churn / no swap-guard trip: a second quench of the same encrypted-declared column on a
        // < 2016 target must not error and must leave the column plaintext (the encryption diff is ignored in
        // modified-column detection, so MustSwapColumn is never set and the AE fail-closed guard is not reached).
        [Test]
        public void AlwaysEncrypted_BelowSql2016_WarnPolicy_SecondQuench_StaysPlaintext()
        {
            var tableName = $"NoChurnEnc_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("EncChurnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            RunTableQuenchProc(cmd, EncryptedTableJson(tableName), productName: tableName);
            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, EncryptedTableJson(tableName), productName: tableName),
                "a repeat quench below 2016 must stay idempotent (no swap-guard trip)");
            Assert.That(EncryptedColumnCount(cmd, tableName), Is.EqualTo(0), "the column must remain plaintext after a second quench");
        }

        // fail (opt-in): a < 2016 target with a declared encrypted column aborts with "requires SQL Server 2016".
        [Test]
        public void AlwaysEncrypted_BelowSql2016_FailPolicy_AbortsWithRequiresSql2016()
        {
            var tableName = $"FailEnc_{Guid.NewGuid().ToString("N")[..8]}";
            using var conn = KindleScratchDatabase("EncFailBake", serverMajorVersion: 10, policy: "fail");
            using var cmd = conn.CreateCommand();

            var ex = Assert.Catch(() => RunTableQuenchProc(cmd, EncryptedTableJson(tableName), productName: tableName));
            Assert.That(ex!.Message, Does.Contain("requires SQL Server 2016"),
                "the fail policy must abort naming the required version");
        }

        // ---------------------------------------------------------------------------------------------------
        // Columnstore indexes — NONCLUSTERED requires SQL Server 2012 (major 11), CLUSTERED requires 2014
        // (major 12). Below its intro version a columnstore index cannot exist, so it is dropped from the
        // working set entirely (not just clause-suppressed): the table deploys without the index (warn) or the
        // quench aborts (fail). Baking major 11 proves the nonclustered/clustered split -- an NCCI is created
        // while a CCI still degrades.
        // ---------------------------------------------------------------------------------------------------

        private const string ColumnStoreObjectType = "columnstore index (SQL Server 2012/2014)";

        private static string ClusteredColumnStoreJson(string tableName, string indexName) => $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": false}
    ],
    "Indexes": [
        {"Name": "[{{indexName}}]", "Clustered": true, "ColumnStore": true, "PrimaryKey": false, "Unique": false}
    ]
}
""";

        private static string NonclusteredColumnStoreJson(string tableName, string indexName) => $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": false}
    ],
    "Indexes": [
        {"Name": "[{{indexName}}]", "Clustered": false, "ColumnStore": true, "PrimaryKey": false, "Unique": false, "IncludeColumns": "[Val]"}
    ]
}
""";

        // sys.indexes.type: 5 = clustered columnstore, 6 = nonclustered columnstore.
        private static int ColumnStoreIndexCount(IDbCommand cmd, string tableName)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM sys.indexes WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [type] IN (5, 6)";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // warn (default): a clustered columnstore index on a < 2014 target is skipped (the table deploys as a
        // rowstore heap) and a downgrade manifest row names the index.
        [Test]
        public void ColumnStore_BelowSql2014_WarnPolicy_SkipsIndex_AndRecordsDowngrade()
        {
            var tableName = $"WarnCci_{Guid.NewGuid().ToString("N")[..8]}";
            var indexName = $"cci_{tableName}";
            using var conn = KindleScratchDatabase("CciWarnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, ClusteredColumnStoreJson(tableName, indexName), productName: tableName),
                "a columnstore index must degrade (skipped) below its intro version, not hard-fail on COLUMNSTORE");

            cmd.CommandText = $"SELECT OBJECT_ID('dbo.{tableName}')";
            Assert.That(cmd.ExecuteScalar(), Is.Not.EqualTo(DBNull.Value), "the table must still be created");
            Assert.That(ColumnStoreIndexCount(cmd, tableName), Is.EqualTo(0), "no columnstore index may exist below the floor");
            Assert.That(DowngradeRowCount(cmd, ColumnStoreObjectType, $"[dbo].[{tableName}].[{indexName}]"), Is.EqualTo(1),
                "a downgrade manifest row must name the skipped columnstore index");
        }

        // No phantom churn: a second quench of the same columnstore-declared table on a below-floor target must
        // not error and must still have no columnstore index (it was dropped from the working set, not left
        // "missing" every run).
        [Test]
        public void ColumnStore_BelowSql2014_WarnPolicy_SecondQuench_StaysSkipped()
        {
            var tableName = $"NoChurnCci_{Guid.NewGuid().ToString("N")[..8]}";
            var indexName = $"cci_{tableName}";
            using var conn = KindleScratchDatabase("CciChurnBake", serverMajorVersion: 10, policy: "warn");
            using var cmd = conn.CreateCommand();

            RunTableQuenchProc(cmd, ClusteredColumnStoreJson(tableName, indexName), productName: tableName);
            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, ClusteredColumnStoreJson(tableName, indexName), productName: tableName),
                "a repeat quench below the floor must stay idempotent");
            Assert.That(ColumnStoreIndexCount(cmd, tableName), Is.EqualTo(0), "the table must remain columnstore-free after a second quench");
        }

        // fail (opt-in): a below-floor target with a declared columnstore index aborts naming the required version.
        [Test]
        public void ColumnStore_BelowSql2014_FailPolicy_Aborts()
        {
            var tableName = $"FailCci_{Guid.NewGuid().ToString("N")[..8]}";
            var indexName = $"cci_{tableName}";
            using var conn = KindleScratchDatabase("CciFailBake", serverMajorVersion: 10, policy: "fail");
            using var cmd = conn.CreateCommand();

            var ex = Assert.Catch(() => RunTableQuenchProc(cmd, ClusteredColumnStoreJson(tableName, indexName), productName: tableName));
            Assert.That(ex!.Message, Does.Contain("Columnstore indexes require SQL Server 2012"),
                "the fail policy must abort naming the required version");
        }

        // Granularity: a NONCLUSTERED columnstore is supported at major 11 (2012), so baking 11 must CREATE it
        // (no downgrade) -- proving the emit-guard is not over-degrading NCCIs at the 2012 floor.
        [Test]
        public void ColumnStore_Nonclustered_AtSql2012_IsCreated()
        {
            var tableName = $"Ncci11_{Guid.NewGuid().ToString("N")[..8]}";
            var indexName = $"ncci_{tableName}";
            using var conn = KindleScratchDatabase("NcciBake11", serverMajorVersion: 11, policy: "warn");
            using var cmd = conn.CreateCommand();

            RunTableQuenchProc(cmd, NonclusteredColumnStoreJson(tableName, indexName), productName: tableName);

            Assert.That(ColumnStoreIndexCount(cmd, tableName), Is.EqualTo(1), "a nonclustered columnstore index must be created at major 11");
            Assert.That(DowngradeRowCount(cmd, ColumnStoreObjectType, $"[dbo].[{tableName}].[{indexName}]"), Is.EqualTo(0),
                "no downgrade may be recorded for a supported nonclustered columnstore at major 11");
        }

        // The clustered/nonclustered split: a CLUSTERED columnstore needs major 12 (2014), so baking 11 must
        // still degrade it even though a nonclustered one would be created at the same version.
        [Test]
        public void ColumnStore_Clustered_AtSql2012_StillDegrades()
        {
            var tableName = $"Cci11_{Guid.NewGuid().ToString("N")[..8]}";
            var indexName = $"cci_{tableName}";
            using var conn = KindleScratchDatabase("CciBake11", serverMajorVersion: 11, policy: "warn");
            using var cmd = conn.CreateCommand();

            RunTableQuenchProc(cmd, ClusteredColumnStoreJson(tableName, indexName), productName: tableName);

            Assert.That(ColumnStoreIndexCount(cmd, tableName), Is.EqualTo(0), "a clustered columnstore must not be created below major 12");
            Assert.That(DowngradeRowCount(cmd, ColumnStoreObjectType, $"[dbo].[{tableName}].[{indexName}]"), Is.EqualTo(1),
                "a clustered columnstore must degrade at major 11 (needs 12)");
        }

        // ---------------------------------------------------------------------------------------------------
        // XML ingest path (compat-100 / below the OPENJSON cliff) — CI coverage. A genuine pre-2016 target
        // deploys through ParseTableXml / IndexOnlyXmlQuench, so the degrade must fire on that path too. Kindle
        // the scratch DB with IngestEncoding.Xml + baked major 10 on the MODERN container to exercise the exact
        // below-cliff code path in CI (the genuine-binary GenuineSql2008EmitGuardCertTests are the [Explicit]
        // release-time recheck — a real old binary's catalog shape is only reproduced there, not by baking).
        // ---------------------------------------------------------------------------------------------------

        private static string AllFeaturesJson(string temporal, string cols, string cci) => $$"""
[
  {"Schema": "[dbo]", "Name": "[{{temporal}}]", "IsTemporal": true,
   "Columns": [{"Name": "[Id]", "DataType": "INT", "Nullable": false, "PrimaryKey": true}, {"Name": "[Val]", "DataType": "NVARCHAR(50)", "Nullable": false}]},
  {"Schema": "[dbo]", "Name": "[{{cols}}]",
   "Columns": [{"Name": "[Id]", "DataType": "INT", "Nullable": false},
               {"Name": "[Email]", "DataType": "NVARCHAR(100)", "Nullable": false, "DataMaskFunction": "email()"},
               {"Name": "[SSN]", "DataType": "NVARCHAR(11)", "Nullable": false, "EncryptionType": "DETERMINISTIC", "EncryptionKey": "[TestCEK]", "EncryptionAlgorithm": "AEAD_AES_256_CBC_HMAC_SHA_256"}]},
  {"Schema": "[dbo]", "Name": "[{{cci}}]",
   "Columns": [{"Name": "[Id]", "DataType": "INT", "Nullable": false}, {"Name": "[Val]", "DataType": "NVARCHAR(50)", "Nullable": false}],
   "Indexes": [{"Name": "[cci_{{cci}}]", "Clustered": true, "ColumnStore": true, "PrimaryKey": false, "Unique": false}]}
]
""";

        private static void DeployXml(IDbCommand cmd, string tablesJson, string productName)
        {
            var xml = ModelXmlSerializer.ToIngestXml(tablesJson, "Tables", "Table");
            cmd.CommandTimeout = 300;
            cmd.CommandText = $"EXEC SchemaSmith.TableQuench @ProductName = '{productName}', @TableDefinitions = @xml, " +
                              "@WhatIf = 0, @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0";
            var p = cmd.CreateParameter();
            p.ParameterName = "@xml";
            p.Value = xml;
            p.DbType = DbType.String;
            cmd.Parameters.Add(p);
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
        }

        // warn (default) on the XML ingest path: all four features degrade — the table deploys plain, columns
        // unmasked/plaintext, no columnstore — and one downgrade manifest row per feature is recorded.
        [Test]
        public void XmlEncoding_BelowSql2016_WarnPolicy_AllFeaturesDegradeCleanly()
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            string temporal = $"XmlT_{id}", cols = $"XmlC_{id}", cci = $"XmlI_{id}";
            using var conn = KindleScratchDatabase("XmlWarnBake", serverMajorVersion: 10, policy: "warn", encoding: IngestEncoding.Xml);
            using var cmd = conn.CreateCommand();

            Assert.DoesNotThrow(() => DeployXml(cmd, AllFeaturesJson(temporal, cols, cci), "XmlWarn"),
                "all four features must degrade cleanly on the XML ingest path below 2016");

            Assert.That(TableTemporalType(cmd, temporal), Is.EqualTo(0), "temporal turn-on suppressed on the XML path");
            Assert.That(MaskedColumnCount(cmd, cols), Is.EqualTo(0), "no masking on the XML path below 2016");
            Assert.That(EncryptedColumnCount(cmd, cols), Is.EqualTo(0), "no encryption on the XML path below 2016");
            Assert.That(ColumnStoreIndexCount(cmd, cci), Is.EqualTo(0), "no columnstore on the XML path below the floor");
            Assert.That(DowngradeRowCount(cmd, TemporalObjectType, $"[dbo].[{temporal}]"), Is.EqualTo(1), "temporal downgrade row (XML path)");
            Assert.That(DowngradeRowCount(cmd, DataMaskingObjectType, $"[dbo].[{cols}].[Email]"), Is.EqualTo(1), "masking downgrade row (XML path)");
            Assert.That(DowngradeRowCount(cmd, AlwaysEncryptedObjectType, $"[dbo].[{cols}].[SSN]"), Is.EqualTo(1), "AE downgrade row (XML path)");
            Assert.That(DowngradeRowCount(cmd, ColumnStoreObjectType, $"[dbo].[{cci}].[cci_{cci}]"), Is.EqualTo(1), "columnstore downgrade row (XML path)");
        }

        // fail (opt-in) on the XML ingest path: the quench aborts naming the required version.
        [Test]
        public void XmlEncoding_BelowSql2016_FailPolicy_Aborts()
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            using var conn = KindleScratchDatabase("XmlFailBake", serverMajorVersion: 10, policy: "fail", encoding: IngestEncoding.Xml);
            using var cmd = conn.CreateCommand();

            var ex = Assert.Catch(() => DeployXml(cmd, AllFeaturesJson($"XmlFT_{id}", $"XmlFC_{id}", $"XmlFI_{id}"), "XmlFail"));
            Assert.That(ex!.Message, Does.Contain("requires SQL Server"),
                "the fail policy must abort on the XML ingest path too");
        }
    }
}
