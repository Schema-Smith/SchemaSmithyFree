// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests.MySQL;
using Schema.Isolators;
using Schema.Utility;
using SchemaQuench.Validation;
using SchemaQuench.Validation.Checks;

namespace SchemaTongs.IntegrationTests.MySQL;

/// <summary>
/// MySQL guard for the extraction / <c>SS-FILE-NAME-003</c> reconciliation (#270 / #343 pre-merge).
/// MySQL has no schema-inside-a-database concept, so extracted table content carries no Schema and
/// the file must be the bare, schema-less <c>&lt;table&gt;.json</c> — never a leading-dot
/// <c>.&lt;table&gt;.json</c>. Confirms the shared content-identity resolver produces a schema-less
/// canonical name when the (content) schema is empty, so MySQL extraction output passes its own
/// <c>--Validate</c> table-file-name check.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class TableFileNameReconciliationTests
{
    private const string TemplateName = "Main";
    private const string ProductName = "ReconProductMy";

    private string _integrationDb = "";
    private string _connectionString = "";
    private string _tempProductPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        FixtureSetup.EnsureInitialized();
        _connectionString = FixtureSetup.ConnectionString;
        _integrationDb = GenerateUniqueDBName("TongsFileName");
        CreateTestDatabase();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        DropTestDatabase();
        if (!string.IsNullOrEmpty(_tempProductPath) && Directory.Exists(_tempProductPath))
        {
            try { Directory.Delete(_tempProductPath, recursive: true); }
            catch (IOException) { /* best effort — temp cleanup */ }
        }
        FactoryContainer.Clear();
        LogFactory.Clear();
    }

    [Test]
    public void Extraction_NamesTableFileSchemaLess_PassesTableFileNameCheck()
    {
        _tempProductPath = Path.Join(Path.GetTempPath(), $"SchemaTongsFileNameMy_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempProductPath);

        var errorLog = Substitute.For<ILog>();
        var progressLog = Substitute.For<ILog>();
        var environment = Substitute.For<IEnvironment>();
        var config = BuildConfig();

        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            LogFactory.Clear();
            FactoryContainer.Register<IConfigurationRoot>(config);
            FactoryContainer.Register(environment);
            LogFactory.Register("ErrorLog", errorLog);
            LogFactory.Register("ProgressLog", progressLog);

            try
            {
                var tongs = new SchemaTongs(Platform.MySQL);
                tongs.CastTemplate();

                var tablesDir = Path.Join(_tempProductPath, "Templates", TemplateName, "Tables");
                Assert.That(File.Exists(Path.Join(tablesDir, "TestTable.json")), Is.True,
                    "MySQL table must be emitted schema-less as TestTable.json.");
                Assert.That(File.Exists(Path.Join(tablesDir, ".TestTable.json")), Is.False,
                    "MySQL table file must not carry a leading-dot (empty-schema) prefix.");

                var ctx = new ValidationContext(
                    new Product { Name = ProductName, Platform = Platform.MySQL },
                    new List<Template>(),
                    _tempProductPath);
                var fileNameFindings = new TableFileNameCheck().Run(ctx)
                    .Where(f => f.Code == "SS-FILE-NAME-003")
                    .ToList();

                Assert.That(fileNameFindings, Is.Empty,
                    "MySQL extraction output must not trip SS-FILE-NAME-003: "
                    + string.Join("; ", fileNameFindings.Select(f => f.Message)));
            }
            finally
            {
                FactoryContainer.Clear();
                LogFactory.Clear();
            }
        }
    }

    // ----- setup helpers ---------------------------------------------------------------------

    private void CreateTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL)
            .GetDbConnection(_connectionString + "Database=information_schema;");
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_integrationDb}`;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = $"USE `{_integrationDb}`;";
        cmd.ExecuteNonQuery();

        ForgeKindler.KindleTheForge(cmd, Platform.MySQL);

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS TestTable (
    Column1 INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Column2 VARCHAR(200) NULL
);";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private void DropTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL)
            .GetDbConnection(_connectionString + "Database=information_schema;");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{_integrationDb}`;";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private IConfigurationRoot BuildConfig()
    {
        var rootConfig = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var connProps = ConnectionString.ReadProperties(rootConfig, "MySQL:ConnectionProperties");

        var values = new Dictionary<string, string>
        {
            ["Source:Server"] = rootConfig["MySQL:Server"] ?? "127.0.0.1",
            ["Source:Port"] = rootConfig["MySQL:Port"],
            ["Source:User"] = rootConfig["MySQL:User"],
            ["Source:Password"] = rootConfig["MySQL:Password"],
            ["Source:Database"] = _integrationDb,
            ["Product:Path"] = _tempProductPath,
            ["Product:Name"] = ProductName,
            ["Template:Name"] = TemplateName,
            ["ShouldCast:Tables"] = "true",
            ["ShouldCast:Views"] = "false",
            ["ShouldCast:Procedures"] = "false",
            ["ShouldCast:Functions"] = "false",
            ["ShouldCast:TableTriggers"] = "false",
            ["ShouldCast:Events"] = "false"
        };
        foreach (var prop in connProps)
            values[$"Source:ConnectionProperties:{prop.Key}"] = prop.Value;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static string GenerateUniqueDBName(string prefix)
    {
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{prefix}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }
}
