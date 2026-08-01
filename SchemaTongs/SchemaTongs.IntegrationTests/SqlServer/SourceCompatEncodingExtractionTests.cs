// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs.IntegrationTests.SqlServer;

/// <summary>
/// C3 — SchemaTongs source-side XML extraction (schema-model encoding). Proves the production wiring:
/// when <c>Source:CompatEncoding=legacy</c>, <see cref="SchemaTongs"/> kindles the XML twins and extracts
/// tables via <c>GenerateTableXml</c> + indexed views via <c>GenerateIndexedViewXml</c> (converted back
/// through <c>ModelXmlSerializer.FromIngestXml</c>), producing the SAME package as the default JSON
/// extraction — minus the <c>Extensions</c> bag, which the legacy encoding drops by design.
///
/// <para>Runs on the modern container: <c>FOR JSON</c> works at any compat there, so
/// <c>Source:CompatEncoding=legacy</c> is the override lever that forces the XML path (mirroring the
/// ingest-side B3 legacy-override test). The RED lever is the extended property: the JSON path carries it
/// in <c>Extensions</c>, the XML path drops it — so a legacy extraction that still contains the extended
/// property proves the XML wiring did not run.</para>
/// </summary>
[Category("SqlServer")]
public class SourceCompatEncodingExtractionTests
{
    private const string TemplateName = "LegacyExtract";
    private const string ProductName = "SourceEncodingProduct";
    private const string ExtendedPropertyValue = "A rich table";

    private string _integrationDb = "";
    private string _connectionString;
    private string _server;
    private readonly List<string> _tempDirs = new();

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var connProps = ConnectionString.ReadProperties(config, "SqlServer:ConnectionProperties");
        _server = config["SqlServer:Server"];
        _connectionString = ConnectionString.Build(Platform.SqlServer, _server, "master",
            config["SqlServer:User"], config["SqlServer:Password"], config["SqlServer:Port"], connProps);
        _integrationDb = GenerateUniqueDBName("TongsSourceEnc");

        CreateSourceDatabase();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        DropTestDatabase();
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* best effort — temp cleanup */ }
            catch (UnauthorizedAccessException) { /* best effort — temp cleanup */ }
        }
        FactoryContainer.Clear();
        LogFactory.Clear();
    }

    [Test]
    public void LegacyEncoding_ExtractsSameSchemaModelAsModern_MinusExtensions()
    {
        string modernPath, legacyPath;

        lock (FactoryContainer.SharedLockObject)
        {
            try
            {
                modernPath = Extract("modern");
                legacyPath = Extract("legacy");
            }
            finally
            {
                FactoryContainer.Clear();
                LogFactory.Clear();
            }
        }

        // ----- RED lever: the XML (legacy) encoding drops Extensions; the JSON (modern) path keeps it. -----
        // The extended property lands in the table's Extensions bag on the JSON path only. A legacy
        // extraction that still contains it proves the XML wiring was skipped (Json kindle + GenerateTableJSON).
        var modernRich = ReadTableFile(modernPath, "Rich");
        var legacyRich = ReadTableFile(legacyPath, "Rich");
        Assert.That(modernRich, Does.Contain(ExtendedPropertyValue),
            "The modern (JSON) extraction must carry the extended property in Extensions.");
        Assert.That(legacyRich, Does.Not.Contain(ExtendedPropertyValue),
            "The legacy (XML) extraction must drop Extensions by design — proving the XML encoding path ran.");

        // ----- Correctness: every emitted table + indexed view is model-equal minus Extensions. -----
        var modernFiles = ReadPackageObjects(modernPath);
        var legacyFiles = ReadPackageObjects(legacyPath);

        Assert.That(legacyFiles.Keys, Is.EquivalentTo(modernFiles.Keys),
            "Legacy and modern extraction must emit the same set of table + indexed-view files.");
        Assert.That(modernFiles.Keys.Any(k => k.StartsWith("Tables/") && k.EndsWith("Rich.json")), Is.True,
            "The rich table must be extracted (sanity — the comparison below would be vacuous otherwise).");
        Assert.That(modernFiles.Keys.Any(k => k.StartsWith("Indexed Views/")), Is.True,
            "The indexed view must be extracted (covers the GenerateIndexedViewXml edit).");

        foreach (var key in modernFiles.Keys)
            Assert.That(legacyFiles[key], Is.EqualTo(modernFiles[key]),
                $"Legacy (XML) and modern (JSON) extraction of {key} must be model-equal minus Extensions.");
    }

    // ----- Extraction -------------------------------------------------------------------------

    private string Extract(string compatEncoding)
    {
        var productPath = Path.Join(Path.GetTempPath(), $"SrcEnc_{compatEncoding}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(productPath);
        _tempDirs.Add(productPath);

        FactoryContainer.Clear();
        LogFactory.Clear();
        FactoryContainer.Register<IConfigurationRoot>(BuildConfig(productPath, compatEncoding));
        FactoryContainer.Register(Substitute.For<IEnvironment>());
        LogFactory.Register("ErrorLog", Substitute.For<ILog>());
        LogFactory.Register("ProgressLog", Substitute.For<ILog>());

        // Mirror the CLI sequence (Program.cs): pre-flight resolves the source version + extraction
        // encoding, then CastTemplate extracts. On the modern container the real compat (150) clears the
        // floor guard; Source:CompatEncoding=legacy is what forces the XML path.
        var tongs = new SchemaTongs(Platform.SqlServer);
        tongs.PreFlightSourceVersion();
        tongs.CastTemplate();
        return productPath;
    }

    private IConfigurationRoot BuildConfig(string productPath, string compatEncoding)
    {
        var rootConfig = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var connProps = ConnectionString.ReadProperties(rootConfig, "SqlServer:ConnectionProperties");

        var values = new Dictionary<string, string>
        {
            ["Source:Server"] = rootConfig["SqlServer:Server"],
            ["Source:Port"] = rootConfig["SqlServer:Port"],
            ["Source:User"] = rootConfig["SqlServer:User"],
            ["Source:Password"] = rootConfig["SqlServer:Password"],
            ["Source:Database"] = _integrationDb,
            ["Source:CompatEncoding"] = compatEncoding,
            ["Product:Path"] = productPath,
            ["Product:Name"] = ProductName,
            ["Template:Name"] = TemplateName,
            ["ShouldCast:Tables"] = "true",
            ["ShouldCast:IndexedViews"] = "true",
            ["ShouldCast:Views"] = "false",
            ["ShouldCast:Procedures"] = "false",
            ["ShouldCast:Functions"] = "false",
            ["ShouldCast:UserDefinedTypes"] = "false",
            ["ShouldCast:TableTriggers"] = "false",
            ["ShouldCast:Catalogs"] = "false",
            ["ShouldCast:StopLists"] = "false",
            ["ShouldCast:DDLTriggers"] = "false",
            ["ShouldCast:XMLSchemaCollections"] = "false",
            ["ShouldCast:Schemas"] = "false",
            ["ShouldCast:ValidateScripts"] = "false"
        };
        foreach (var prop in connProps)
            values[$"Source:ConnectionProperties:{prop.Key}"] = prop.Value;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // ----- Package readers --------------------------------------------------------------------

    private static string ReadTableFile(string productPath, string tableName)
    {
        // Non-schema-template extraction keeps the schema prefix, so the file is "dbo.Rich.json".
        var file = Directory.GetFiles(productPath, "*.json", SearchOption.AllDirectories)
            .Single(f => Path.GetFileName(Path.GetDirectoryName(f)) == "Tables"
                         && (Path.GetFileName(f) == $"{tableName}.json"
                             || Path.GetFileName(f).EndsWith($".{tableName}.json", StringComparison.Ordinal)));
        return File.ReadAllText(file);
    }

    // Every emitted table + indexed-view file, keyed by "<folder>/<file>", normalized to strip the
    // Extensions bag (dropped on the legacy encoding) and empty/null scalars (the XML encoding maps an
    // empty value to an omitted element; "" / null / absent are semantically identical for the placeholder
    // fields — OldName, Collation, mask/encryption sentinels). Both encodings write the file through the
    // same JsonHelper serialization off the deserialized model, so model-equality is file-equality here.
    private static Dictionary<string, string> ReadPackageObjects(string productPath)
    {
        var result = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(productPath, "*.json", SearchOption.AllDirectories))
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(file));
            if (folder != "Tables" && folder != "Indexed Views") continue;
            var token = JToken.Parse(File.ReadAllText(file));
            Normalize(token);
            result[$"{folder}/{Path.GetFileName(file)}"] = token.ToString(Formatting.Indented);
        }
        return result;
    }

    private static void Normalize(JToken node)
    {
        switch (node)
        {
            case JObject o:
                foreach (var p in o.Properties().ToList())
                {
                    if (p.Name == "Extensions"
                        || p.Value.Type == JTokenType.Null
                        || (p.Value is JValue { Type: JTokenType.String } v && (string)v.Value! == ""))
                        p.Remove();
                    else
                        Normalize(p.Value);
                }
                break;
            case JArray a:
                foreach (var item in a) Normalize(item);
                break;
        }
    }

    // ----- Source database --------------------------------------------------------------------

    private void CreateSourceDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{_integrationDb}];";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationDb);
        ForgeKindler.KindleTheForge(cmd, Platform.SqlServer);

        Exec(cmd, "CREATE FULLTEXT CATALOG [FT_Catalog];");
        Exec(cmd, "CREATE FULLTEXT STOPLIST [SL_Test];");

        // A rich table exercising every array container (mirrors GenerateTableXmlEquivalenceTests):
        // identity, computed+persisted, default, NULL/NOT NULL mix, PK clustered, a nonclustered index
        // with a DESC key + INCLUDE + filter, an FK with a referential action, a user statistic, an XML
        // column with primary + secondary XML indexes, a table-level check, and a full-text index.
        Exec(cmd, "CREATE TABLE dbo.Parent (Id INT NOT NULL PRIMARY KEY, Code VARCHAR(20) NOT NULL)");
        Exec(cmd, "CREATE UNIQUE INDEX UX_Parent_Code ON dbo.Parent (Code)");
        Exec(cmd, @"
CREATE TABLE dbo.Rich (
    Id INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Amount DECIMAL(10,2) NULL CONSTRAINT DF_Rich_Amount DEFAULT 0,
    ParentCode VARCHAR(20) NULL,
    Computed AS (Amount * 2) PERSISTED,
    Flag BIT NOT NULL,
    Doc XML NULL,
    CONSTRAINT PK_Rich PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_Rich_Amount CHECK (Amount >= 0),
    CONSTRAINT CK_Rich_Table CHECK (Amount < 100000 OR Name IS NOT NULL),
    CONSTRAINT FK_Rich_Parent FOREIGN KEY (ParentCode) REFERENCES dbo.Parent (Code) ON DELETE SET NULL
)");
        Exec(cmd, "CREATE NONCLUSTERED INDEX IX_Rich_Name ON dbo.Rich (Name DESC) INCLUDE (Amount) WHERE Flag = 1");
        Exec(cmd, "CREATE STATISTICS ST_Rich_Amount ON dbo.Rich (Amount)");
        Exec(cmd, "CREATE PRIMARY XML INDEX XI_Primary_Doc ON dbo.Rich (Doc)");
        Exec(cmd, "CREATE XML INDEX XI_Secondary_Doc_Path ON dbo.Rich (Doc) USING XML INDEX XI_Primary_Doc FOR PATH");
        Exec(cmd, "CREATE FULLTEXT INDEX ON dbo.Rich (Name) KEY INDEX PK_Rich ON FT_Catalog WITH CHANGE_TRACKING = AUTO, STOPLIST = SL_Test");

        // Extended property → captured in Extensions on the JSON path, dropped on the XML path. The RED lever.
        Exec(cmd, $"EXEC sys.sp_addextendedproperty 'MS_Description', '{ExtendedPropertyValue}', 'SCHEMA', [dbo], 'TABLE', [Rich], NULL, NULL");

        // An indexed view (covers the GenerateIndexedViewXml extraction edit). Its base table is a plain
        // table; both are extracted. Shape mirrors GenerateIndexedViewXmlEquivalenceTests (proven valid).
        Exec(cmd, "CREATE TABLE dbo.IvSource (Id INT NOT NULL, Name VARCHAR(100) NOT NULL, Amount DECIMAL(10,2) NOT NULL)");
        Exec(cmd, "CREATE UNIQUE CLUSTERED INDEX UDX_IvSource ON dbo.IvSource (Id)");
        Exec(cmd, @"CREATE VIEW dbo.vRichSummary WITH SCHEMABINDING AS
SELECT Id, Name, COUNT_BIG(*) AS Cnt, SUM(Amount) AS TotalAmount
FROM dbo.IvSource
GROUP BY Id, Name");
        Exec(cmd, "CREATE UNIQUE CLUSTERED INDEX IX_vRichSummary_Id ON dbo.vRichSummary (Id)");
        Exec(cmd, "CREATE NONCLUSTERED INDEX IX_vRichSummary_Name ON dbo.vRichSummary (Name) WITH (FILLFACTOR = 90)");

        conn.Close();
    }

    private static void Exec(IDbCommand cmd, string sql)
    {
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void DropTestDatabase()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
IF DB_ID('{_integrationDb}') IS NOT NULL
  ALTER DATABASE [{_integrationDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{_integrationDb}];";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private static string GenerateUniqueDBName(string prefix)
    {
        var uniqueSegment = Guid.NewGuid().ToString().Replace("-", "_").Substring(0, 8);
        return $"{prefix}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }
}
