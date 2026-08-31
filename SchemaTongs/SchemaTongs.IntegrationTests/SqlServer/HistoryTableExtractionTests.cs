// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs.IntegrationTests.SqlServer;

/// <summary>
/// A system-managed history table must not end up as a table file in the extracted package.
/// <para>SchemaSmith creates a temporal history table from the versioned table's own declaration —
/// <c>IsTemporal</c> plus the <c>HistoryTable*</c> properties. Extracting it separately writes a second
/// table file that the next deploy then tries to create in its own right, so the package no longer
/// round-trips.</para>
/// <para>Raised while checking ledger (gap item J3) for the failure
/// <see href="https://github.com/Schema-Smith/SchemaSmith/issues/402">#402</see> exposed for graph tables.
/// A ledger table's own generated columns are already excluded — they report
/// <c>generated_always_type</c> 7–10 — but an updatable ledger table also spawns
/// <c>MSSQL_LedgerHistoryFor_&lt;object_id&gt;</c>, whose <c>temporal_type</c> is
/// <c>NON_TEMPORAL_TABLE</c>, so nothing keyed on temporal reaches it.</para>
/// <para><b>Asserted on the files SchemaTongs actually writes</b>, by running a real extraction rather
/// than by re-executing a copy of its table-list query. A test holding its own copy of the query passes
/// happily while the real one drifts — which is the whole failure mode this guards.</para>
/// </summary>
[Category("SqlServer")]
[NonParallelizable]
public class HistoryTableExtractionTests
{
    private const string ProductName = "HistoryTableProduct";
    private const string TemplateName = "Main";

    private string _db = "";
    private string _masterConnectionString = "";
    private string _tempProductPath = "";
    private bool _ledgerSupported;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var connProps = ConnectionString.ReadProperties(config, "SqlServer:ConnectionProperties");
        _masterConnectionString = ConnectionString.Build(Platform.SqlServer, config["SqlServer:Server"], "master",
            config["SqlServer:User"], config["SqlServer:Password"], config["SqlServer:Port"], connProps);
        _db = $"TongsHist_{Guid.NewGuid():N}"[..30];

        Master($"CREATE DATABASE [{_db}]");

        OnDb(cmd =>
        {
            Run(cmd, """
                CREATE TABLE dbo.Versioned (
                    Id INT NOT NULL PRIMARY KEY,
                    V INT NULL,
                    SysStart DATETIME2 GENERATED ALWAYS AS ROW START NOT NULL,
                    SysEnd DATETIME2 GENERATED ALWAYS AS ROW END NOT NULL,
                    PERIOD FOR SYSTEM_TIME (SysStart, SysEnd))
                WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.Versioned_Hist))
                """);

            // Graph tables join the fixture so one round trip covers #402 and #403 together --
            // both were "extraction emits something that cannot be deployed".
            Run(cmd, "CREATE TABLE dbo.GraphPerson (Id INT NOT NULL PRIMARY KEY, Name NVARCHAR(50) NULL) AS NODE");
            Run(cmd, "CREATE TABLE dbo.GraphKnows (Since DATE NULL) AS EDGE");

            cmd.CommandText = "SELECT COUNT(*) FROM sys.all_columns "
                              + "WHERE object_id = OBJECT_ID('sys.tables') AND name = 'ledger_type'";
            _ledgerSupported = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            if (_ledgerSupported)
                Run(cmd, "CREATE TABLE dbo.LedgerAcct (Id INT NOT NULL PRIMARY KEY, Bal DECIMAL(18,2) NULL) "
                         + "WITH (SYSTEM_VERSIONING = ON, LEDGER = ON)");
        });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try
        {
            OnDb(cmd =>
            {
                try { Run(cmd, "ALTER TABLE dbo.Versioned SET (SYSTEM_VERSIONING = OFF)"); } catch { /* best effort */ }
            });
            Master($"ALTER DATABASE [{_db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
            Master($"DROP DATABASE IF EXISTS [{_db}]");
        }
        catch { /* teardown must not mask an assertion */ }

        if (!string.IsNullOrEmpty(_tempProductPath) && Directory.Exists(_tempProductPath))
        {
            try { Directory.Delete(_tempProductPath, recursive: true); } catch { /* best effort */ }
        }
        FactoryContainer.Clear();
        LogFactory.Clear();
    }

    private static void Run(IDbCommand cmd, string sql)
    {
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();
    }

    private void Master(string sql)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        Run(cmd, sql);
    }

    private void OnDb(Action<IDbCommand> act)
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_masterConnectionString);
        conn.Open();
        conn.ChangeDatabase(_db);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        act(cmd);
    }

    [Test]
    public void ARealExtraction_WritesNoHistoryTableFile()
    {
        _tempProductPath = Path.Combine(Path.GetTempPath(), $"TongsHist_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempProductPath);

        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            LogFactory.Clear();
            FactoryContainer.Register<IConfigurationRoot>(BuildConfig());
            FactoryContainer.Register(environment);
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);

            new global::SchemaTongs.SchemaTongs(Platform.SqlServer).CastTemplate();
        }

        var tablesDir = Path.Combine(_tempProductPath, "Templates", TemplateName, "Tables");
        Assert.That(Directory.Exists(tablesDir), Is.True, "extraction must have produced a Tables folder");
        var files = string.Join(";", Directory.GetFiles(tablesDir, "*.json"));

        Assert.Multiple(() =>
        {
            Assert.That(files, Does.Contain("Versioned"),
                "the versioned table itself must still be extracted -- an over-broad exclusion that also "
                + "dropped the real table would otherwise pass every assertion below");
            Assert.That(files, Does.Not.Contain("Versioned_Hist"),
                "the temporal history table is created by the versioned table's own declaration; a file "
                + "for it makes the next deploy try to create it in its own right.\nFiles: " + files);

            if (_ledgerSupported)
                Assert.That(files, Does.Not.Contain("MSSQL_LedgerHistoryFor"),
                    "an updatable ledger table's history is named after an object id on the SOURCE server, "
                    + "so that file could not be deployed anywhere.\nFiles: " + files);
        });
    }

    private IConfigurationRoot BuildConfig()
    {
        var root = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var values = new Dictionary<string, string>
        {
            ["Source:Server"] = root["SqlServer:Server"],
            ["Source:Port"] = root["SqlServer:Port"],
            ["Source:User"] = root["SqlServer:User"],
            ["Source:Password"] = root["SqlServer:Password"],
            ["Source:Database"] = _db,
            ["Target:Server"] = root["SqlServer:Server"],
            ["Target:Port"] = root["SqlServer:Port"],
            ["Target:User"] = root["SqlServer:User"],
            ["Target:Password"] = root["SqlServer:Password"],
            ["ScriptTokens:MainDB"] = _db,
            ["Product:Path"] = _tempProductPath,
            ["Product:Name"] = ProductName,
            ["Template:Name"] = TemplateName,
            ["ShouldCast:Tables"] = "true",
            ["ShouldCast:Views"] = "false",
            ["ShouldCast:Procedures"] = "false",
            ["ShouldCast:Functions"] = "false",
            ["ShouldCast:UserDefinedTypes"] = "false",
            ["ShouldCast:TableTriggers"] = "false",
            ["ShouldCast:Schemas"] = "false",
            ["ShouldCast:IndexedViews"] = "false",
            ["ShouldCast:Sequences"] = "false",
            ["ShouldCast:Synonyms"] = "false",
            ["ShouldCast:XmlSchemaCollections"] = "false",
            ["ShouldCast:FullTextCatalogs"] = "false",
            ["ShouldCast:FullTextStopLists"] = "false",
        };
        // Both halves need them: extraction reads Source:*, the redeploy reads Target:*, and a missing
        // TrustServerCertificate surfaces only as "Error validating configured servers".
        foreach (var kv in ConnectionString.ReadProperties(root, "SqlServer:ConnectionProperties"))
        {
            values[$"Source:ConnectionProperties:{kv.Key}"] = kv.Value;
            values[$"Target:ConnectionProperties:{kv.Key}"] = kv.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
