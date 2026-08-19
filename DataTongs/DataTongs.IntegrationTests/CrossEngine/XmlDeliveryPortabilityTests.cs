// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace DataTongs.IntegrationTests.CrossEngine;

/// <summary>
/// Cross-engine XML delivery portability acceptance test. Extracts with --DeliveryEncoding=Xml from
/// non-SQL-Server engines, then deploys to SQL Server through the same legacy-tier XML shred, proving
/// portability across engines (not just same-engine round-trips). Requires SQL Server, PostgreSQL, and
/// MySQL reachable simultaneously — unavailable in per-engine CI jobs. This is a deliberate local-only
/// convenience check, not a coverage gap: per-engine conversion logic is covered separately by the
/// `XmlDeliveryExtraction_ConvertsExtractedJsonToTheDeliveryXmlShape` tests in
/// `Shared/DataTongsExtractionSharedTests.cs` and `PostgreSQL/DataTongsExtractionTests.cs`, which carry
/// engine categories and run in CI. Run this test locally with `--filter "Category=CrossEngine"` against
/// an environment with all three engines up.
/// </summary>
[Category("CrossEngine")]
[TestFixture]
public class XmlDeliveryPortabilityTests
{
    [Test]
    public void PostgresExtraction_DeliveredAsXml_DeploysToSqlServer_ValuesMatchSource()
    {
        var tableName = $"xmlport_{Guid.NewGuid():N}".Substring(0, 20);
        string xml;

        using (var pg = DbConnectionFactory.ForPlatform(Platform.PostgreSQL)
                   .GetDbConnection(Schema.IntegrationTests.PostgreSQL.FixtureSetup.GetMainDbConnectionString()))
        {
            pg.Open();
            try
            {
                using (var cmd = pg.CreateCommand())
                {
                    cmd.CommandText = $@"
CREATE TABLE public.""{tableName}"" (
    ""code"" VARCHAR(20) NOT NULL PRIMARY KEY, ""flag"" BOOLEAN, ""amount"" DECIMAL(10,2),
    ""note"" VARCHAR(100), ""ts"" TIMESTAMP, ""bin"" BYTEA);
INSERT INTO public.""{tableName}"" VALUES
    ('A001', TRUE, 7.25, 'a & b < c', '2026-08-11 06:00:00', decode('DEADBEEF', 'hex')),
    ('B002', FALSE, NULL, NULL, NULL, NULL);";
                    cmd.ExecuteNonQuery();
                }

                var dataTongs = new global::DataTongs.DataTongs(Platform.PostgreSQL);
                using (var cmd = pg.CreateCommand())
                {
                    var selectColumns = dataTongs.GetSelectColumns(cmd, "public", tableName);
                    var json = dataTongs.GetTableDataJson(cmd, selectColumns, "public", tableName, "\"code\"", null);
                    xml = MergeScriptHelper.JsonPayloadToXml(json);
                }
            }
            finally
            {
                using var drop = pg.CreateCommand();
                drop.CommandText = $"DROP TABLE IF EXISTS public.\"{tableName}\"";
                drop.ExecuteNonQuery();
            }
        }

        DeployXmlToSqlServerAndAssertValues(tableName, xml);
    }

    [Test]
    public void MySqlExtraction_DeliveredAsXml_DeploysToSqlServer_ValuesMatchSource()
    {
        var tableName = $"xmlport_{Guid.NewGuid():N}".Substring(0, 20);
        string xml;
        var mainDb = Schema.IntegrationTests.MySQL.FixtureSetup.MainDb;

        using (var my = DbConnectionFactory.ForPlatform(Platform.MySQL)
                   .GetDbConnection(Schema.IntegrationTests.MySQL.FixtureSetup.GetMainDbConnectionString()))
        {
            my.Open();
            try
            {
                using (var cmd = my.CreateCommand())
                {
                    cmd.CommandText = $@"
CREATE TABLE `{mainDb}`.`{tableName}` (
    `code` VARCHAR(20) NOT NULL PRIMARY KEY, `flag` BIT(1), `amount` DECIMAL(10,2),
    `note` VARCHAR(100), `ts` DATETIME, `bin` VARBINARY(16));
INSERT INTO `{mainDb}`.`{tableName}` VALUES
    ('A001', b'1', 7.25, 'a & b < c', '2026-08-11 06:00:00', UNHEX('DEADBEEF')),
    ('B002', b'0', NULL, NULL, NULL, NULL);";
                    cmd.ExecuteNonQuery();
                }

                var dataTongs = new global::DataTongs.DataTongs(Platform.MySQL);
                using (var cmd = my.CreateCommand())
                {
                    var json = dataTongs.GetTableDataJson(cmd, null, mainDb, tableName, "`code`", null);
                    xml = MergeScriptHelper.JsonPayloadToXml(json);
                }
            }
            finally
            {
                using var drop = my.CreateCommand();
                drop.CommandText = $"DROP TABLE IF EXISTS `{mainDb}`.`{tableName}`";
                drop.ExecuteNonQuery();
            }
        }

        DeployXmlToSqlServerAndAssertValues(tableName, xml);
    }

    // Shared second half of both tests: deploy the extracted-and-converted XML to SQL Server through the
    // legacy-tier shred, then verify every value survived the cross-engine hop -- the acceptance criterion
    // is portability to the SQL Server target, not that the source engine can read its own output back.
    private static void DeployXmlToSqlServerAndAssertValues(string tableName, string xml)
    {
        using var sql = DbConnectionFactory.ForPlatform(Platform.SqlServer)
            .GetDbConnection(Schema.IntegrationTests.SqlServer.FixtureSetup.GetMainDbConnectionString());
        sql.Open();
        try
        {
            using (var cmd = sql.CreateCommand())
            {
                cmd.CommandText = $@"CREATE TABLE [dbo].[{tableName}] (
  [code] VARCHAR(20) NOT NULL PRIMARY KEY, [flag] BIT NULL, [amount] DECIMAL(10,2) NULL,
  [note] NVARCHAR(100) NULL, [ts] DATETIME2 NULL, [bin] VARBINARY(MAX) NULL);";
                cmd.ExecuteNonQuery();
            }
            using (var cmd = sql.CreateCommand())
            {
                var script = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd, "dbo", tableName, xml, "[code]",
                    mergeUpdate: true, mergeDelete: false, disableTriggers: false, tokenizeScripts: false,
                    mergeFilter: null, contentEncoding: "Xml");
                cmd.CommandText = script;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = sql.CreateCommand())
            {
                cmd.CommandText = $"SELECT [flag],[amount],[note],CONVERT(VARCHAR(33),[ts],126),CONVERT(VARCHAR(MAX),[bin],1) FROM [dbo].[{tableName}] WHERE [code]='A001'";
                using var r = cmd.ExecuteReader();
                Assert.That(r.Read(), Is.True);
                Assert.That(r.GetBoolean(0), Is.True);
                Assert.That(r.GetDecimal(1), Is.EqualTo(7.25m));
                Assert.That(r.GetString(2), Is.EqualTo("a & b < c"));
                Assert.That(r.GetString(3), Does.StartWith("2026-08-11T06:00:00"));
                Assert.That(r.GetString(4), Is.EqualTo("0xDEADBEEF"));
            }
            using (var cmd = sql.CreateCommand())
            {
                cmd.CommandText = $"SELECT [flag],[amount],[note],[ts],[bin] FROM [dbo].[{tableName}] WHERE [code]='B002'";
                using var r = cmd.ExecuteReader();
                Assert.That(r.Read(), Is.True);
                Assert.That(r.GetBoolean(0), Is.False);
                Assert.That(r.IsDBNull(1), Is.True, "A NULL amount must round-trip as NULL.");
                Assert.That(r.IsDBNull(2), Is.True);
                Assert.That(r.IsDBNull(3), Is.True);
                Assert.That(r.IsDBNull(4), Is.True);
            }
        }
        finally
        {
            using var drop = sql.CreateCommand();
            drop.CommandText = $"DROP TABLE IF EXISTS [dbo].[{tableName}]";
            drop.ExecuteNonQuery();
        }
    }
}
