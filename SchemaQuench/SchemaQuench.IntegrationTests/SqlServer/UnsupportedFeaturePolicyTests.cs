// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

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
    }
}
