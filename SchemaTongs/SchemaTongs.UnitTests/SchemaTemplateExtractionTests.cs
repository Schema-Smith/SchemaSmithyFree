// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using log4net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs.UnitTests;

/// <summary>
/// Schema-template extraction mode tests (design §7, plan §10.2). When
/// <c>Source.Schema</c> is non-empty in <c>SchemaTongs.settings.json</c>,
/// SchemaTongs produces a schema-template package: unqualified filenames,
/// omitted <c>Schema</c> fields, schema-aware <c>RelatedTableSchema</c> stripping,
/// a <c>Template.json</c> stub with the schema-template fields, and forced-off
/// database-scoped <c>ShouldCast</c> flags.
/// </summary>
[TestFixture]
public class SchemaTemplateExtractionTests
{
    private const string SourceSchema = "tenant_seed";
    private const string TemplateName = "TenantBody";

    private ILog _progressLog;
    private ILog _errorLog;
    private IEnvironment _environment;
    private IDbConnectionFactory _connectionFactory;
    private IDbConnection _connection;
    private IDbCommand _command;
    private IDbConnection _connectionJson;
    private IDbCommand _commandJson;
    private IFile _fileWrapper;
    private IDirectory _directoryWrapper;

    // Captures Template.json content as written by RepositoryHelper.UpdateOrInitTemplate.
    private string _writtenTemplateJson;

    private void SetUpMocks()
    {
        _progressLog = Substitute.For<ILog>();
        _errorLog = Substitute.For<ILog>();
        _environment = Substitute.For<IEnvironment>();
        _connectionFactory = Substitute.For<IDbConnectionFactory>();
        _connection = Substitute.For<IDbConnection>();
        _command = Substitute.For<IDbCommand>();
        _connectionJson = Substitute.For<IDbConnection>();
        _commandJson = Substitute.For<IDbCommand>();
        _fileWrapper = Substitute.For<IFile>();
        _directoryWrapper = Substitute.For<IDirectory>();

        _connection.CreateCommand().Returns(_command);
        _connectionJson.CreateCommand().Returns(_commandJson);
        _directoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        _directoryWrapper.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>()).Returns(Array.Empty<string>());
        _fileWrapper.Exists(Arg.Any<string>()).Returns(false);

        // First GetDbConnection call serves the main command; subsequent calls return the JSON connection
        // (used by table-JSON extraction). Match the pattern in SchemaTongsTests.
        var callCount = 0;
        _connectionFactory.GetDbConnection(Arg.Any<string>()).Returns(_ =>
        {
            callCount++;
            return callCount <= 1 ? _connection : _connectionJson;
        });

        // Capture Template.json write to inspect the generated payload.
        _writtenTemplateJson = null;
        _fileWrapper.When(f => f.WriteAllText(Arg.Is<string>(p => p.EndsWith("Template.json")), Arg.Any<string>()))
            .Do(ci => _writtenTemplateJson = ci.ArgAt<string>(1));
    }

    /// <summary>
    /// Stub <paramref name="mockCmd"/>.ExecuteScalar() so that KindleTheForge takes the
    /// skip path on MySQL without touching the DB. Lock-acquire returns 1L (success),
    /// the KindleStamp existence check returns 1L (table present), and the stamp SELECT
    /// returns the current computed stamp so the gate detects "already current" and returns
    /// without executing any kindle DDL. Non-MySQL kindle queries fall through to null,
    /// preserving default NSubstitute behaviour for SQL Server and PostgreSQL tests.
    /// </summary>
    private void StubMySqlKindleGate() => KindleGateTestHelpers.StubMySqlKindleGate(_command);

    private void RegisterConfig(Platform platform, Dictionary<string, string> overrides = null)
    {
        var configValues = new Dictionary<string, string>
        {
            ["Source:Server"] = "localhost",
            ["Source:Database"] = "testdb",
            ["Source:User"] = "testuser",
            ["Source:Password"] = "testpass",
            ["Target:Platform"] = platform.ToString(),
            ["Product:Path"] = Path.GetTempPath(),
            ["Product:Name"] = "TestProduct",
            ["Template:Name"] = TemplateName,
            ["Source:Schema"] = SourceSchema
        };

        if (overrides != null)
        {
            foreach (var kv in overrides)
                configValues[kv.Key] = kv.Value;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        FactoryContainer.Register<IConfigurationRoot>(config);
        FactoryContainer.Register(_environment);
        FactoryContainer.Register(_fileWrapper);
        FactoryContainer.Register(_directoryWrapper);
        FactoryContainer.Register(_connectionFactory);
        LogFactory.Register("ProgressLog", _progressLog);
        LogFactory.Register("ErrorLog", _errorLog);

        // Make Product.json appear to already exist with a minimal valid body so SchemaTongs picks up
        // the configured platform; same convention as SchemaTongsTests.
        _fileWrapper.Exists(Arg.Is<string>(s => s.Contains("Product.json"))).Returns(true);
        _fileWrapper.ReadAllText(Arg.Is<string>(s => s.Contains("Product.json"))).Returns(
            $"{{\"Name\":\"TestProduct\",\"Platform\":\"{platform}\",\"TemplateOrder\":[\"{TemplateName}\"],\"ScriptTokens\":{{}},\"ScriptFolders\":[]}}");
    }

    // NSubstitute "last call" tracking is thread-local — calling Returns(builderHelper())
    // would conflate the helper's own Returns() calls with the outer assignment. These helpers
    // therefore return the substitute WITHOUT the outer Returns wiring; callers assign the
    // reader to the command via `var r = Helper(...); _command.StubReaders(r);`.

    private static IDataReader EmptyReader()
    {
        var r = Substitute.For<IDataReader>();
        r.Read().Returns(false);
        return r;
    }

    private static IDataReader SingleSqlServerTableListReader(string schema, string table)
    {
        var r = Substitute.For<IDataReader>();
        var calls = 0;
        r.Read().Returns(_ => calls++ < 1, _ => false);
        r["TABLE_SCHEMA"].Returns(schema);
        r["TABLE_NAME"].Returns(table);
        return r;
    }

    private static IDataReader SingleColumnReader(string columnZero, params string[] additionalRows)
    {
        var r = Substitute.For<IDataReader>();
        var rowCount = 1 + additionalRows.Length;
        var idx = 0;
        r.Read().Returns(_ => idx++ < rowCount, _ => false);
        var queue = new Queue<string>();
        queue.Enqueue(columnZero);
        foreach (var s in additionalRows) queue.Enqueue(s);
        r[0].Returns(_ => queue.Dequeue());
        return r;
    }

    // Returns the platform-appropriate set of ShouldCast:* flags all set to "false". Tests then
    // override only the flag(s) they're exercising, so each test's intent (which flag is on) is
    // visible without scrolling past a full dictionary of "false" boilerplate.
    private static Dictionary<string, string> ShouldCastAllFalse(Platform platform) => platform switch
    {
        Platform.SqlServer => new Dictionary<string, string>
        {
            ["ShouldCast:Tables"] = "false",
            ["ShouldCast:Schemas"] = "false",
            ["ShouldCast:UserDefinedTypes"] = "false",
            ["ShouldCast:Functions"] = "false",
            ["ShouldCast:Views"] = "false",
            ["ShouldCast:Procedures"] = "false",
            ["ShouldCast:TableTriggers"] = "false",
            ["ShouldCast:Catalogs"] = "false",
            ["ShouldCast:StopLists"] = "false",
            ["ShouldCast:DDLTriggers"] = "false",
            ["ShouldCast:XMLSchemaCollections"] = "false",
            ["ShouldCast:IndexedViews"] = "false"
        },
        Platform.PostgreSQL => new Dictionary<string, string>
        {
            ["ShouldCast:Tables"] = "false",
            ["ShouldCast:Schemas"] = "false",
            ["ShouldCast:DomainTypes"] = "false",
            ["ShouldCast:EnumTypes"] = "false",
            ["ShouldCast:CompositeTypes"] = "false",
            ["ShouldCast:Functions"] = "false",
            ["ShouldCast:Aggregates"] = "false",
            ["ShouldCast:Procedures"] = "false",
            ["ShouldCast:Sequences"] = "false",
            ["ShouldCast:Rules"] = "false",
            ["ShouldCast:TableTriggers"] = "false",
            ["ShouldCast:Views"] = "false",
            ["ShouldCast:MaterializedViews"] = "false"
        },
        Platform.MySQL => new Dictionary<string, string>
        {
            ["ShouldCast:Tables"] = "false",
            ["ShouldCast:Functions"] = "false",
            ["ShouldCast:Views"] = "false",
            ["ShouldCast:Procedures"] = "false",
            ["ShouldCast:TableTriggers"] = "false",
            ["ShouldCast:Events"] = "false"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
    };

    #region §10.2 — Filename transformation

    [Test]
    public void SchemaTemplate_SqlServer_Table_FilenameUnqualified()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            // Only tables, to keep the test minimal.
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // Main command: one row listing tenant_seed.Customers
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"Columns\":[],\"ForeignKeys\":[],\"OldName\":\"\"}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            // Unqualified filename — Customers.json, not tenant_seed.Customers.json
            _fileWrapper.Received().WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json") && !s.EndsWith($"{SourceSchema}.Customers.json")),
                Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region §10.2 — JSON Schema field omitted

    [Test]
    public void SchemaTemplate_SqlServer_Table_JsonOmitsSchemaField()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"Columns\":[],\"ForeignKeys\":[],\"OldName\":\"\"}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null, "table JSON should have been written");
            var parsed = JObject.Parse(capturedJson);
            Assert.That(parsed["Schema"], Is.Null, "schema-template table JSON must omit the Schema field");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region §10.2 — RelatedTableSchema stripping (same source) and preservation (cross-schema)

    [Test]
    public void SchemaTemplate_SqlServer_Fk_SameSchemaRelatedTableSchema_Stripped()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // FK whose RelatedTableSchema matches the source schema — should be omitted.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Orders");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Orders\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"FK_Orders_Customers\",\"Columns\":\"CustomerId\",\"RelatedTable\":\"Customers\",\"RelatedColumns\":\"Id\",\"RelatedTableSchema\":\"tenant_seed\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Orders.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var fks = (JArray)parsed["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"], Is.Null,
                "Same-source RelatedTableSchema should be stripped (engine defaults to {{SchemaName}})");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Fk_BracketedSameSchemaRelatedTableSchema_Stripped()
    {
        // Issue #272: bracketed RelatedTableSchema "[tenant_seed]" must be omitted the same as
        // the bare "tenant_seed" form — this is the form GenerateTableJson actually emits
        // ('[' + OBJECT_SCHEMA_NAME(...) + ']'). Pins the scrub LOGIC; the empirical throwaway-DB
        // extraction (2026-06-18) confirmed real same-schema FK extraction omits RelatedTableSchema
        // on current main (fresh + re-extraction). Anchor test for the reporter's #272 FK case.
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Orders");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Orders\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"FK_Orders_Customers\",\"Columns\":\"CustomerId\",\"RelatedTable\":\"Customers\",\"RelatedColumns\":\"Id\",\"RelatedTableSchema\":\"[tenant_seed]\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Orders.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var fks = (JArray)parsed["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"], Is.Null,
                "Bracketed same-source RelatedTableSchema ([tenant_seed]) should be stripped (issue #272)");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Fk_CrossSchemaRelatedTableSchema_Preserved()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // FK whose RelatedTableSchema points OUT of the source schema (dbo.Countries) — must be preserved.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"FK_Customers_Country\",\"Columns\":\"CountryId\",\"RelatedTable\":\"Countries\",\"RelatedColumns\":\"Id\",\"RelatedTableSchema\":\"dbo\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var fks = (JArray)parsed["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"]?.ToString(), Is.EqualTo("dbo"),
                "Cross-schema RelatedTableSchema must be preserved");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Fk_SameSchema_BracketWrapped_RelatedSchemaIsNulled()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // Reviewer scenario from #256: extraction produces bracket-delimited
            // RelatedTableSchema; the configured source schema is the bare name.
            // The scrub must recognize them as equivalent and null the related schema.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Orders");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Orders\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"FK_Orders_Customers\",\"Columns\":\"CustomerId\",\"RelatedTable\":\"Customers\",\"RelatedColumns\":\"Id\",\"RelatedTableSchema\":\"[tenant_seed]\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Orders.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var fks = (JArray)parsed["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"], Is.Null,
                "Bracket-delimited same-source RelatedTableSchema should be stripped (#256)");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Fk_CrossSchema_BracketWrapped_RelatedSchemaPreserved()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // Companion to the bug fix: bracket-delimited FK schema that is NOT the
            // source schema stays as-is (it's a real cross-schema reference).
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"FK_Customers_Country\",\"Columns\":\"CountryId\",\"RelatedTable\":\"Countries\",\"RelatedColumns\":\"Id\",\"RelatedTableSchema\":\"[other_schema]\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var fks = (JArray)parsed["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"]?.ToString(), Is.EqualTo("[other_schema]"),
                "Bracket-delimited cross-schema RelatedTableSchema must be preserved verbatim");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_PostgreSQL_Fk_SameSchema_QuoteWrapped_RelatedSchemaIsNulled()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.PostgreSQL);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.PostgreSQL, overrides);

            // PG equivalent of the #256 bug shape: extraction-emitted quoted identifier
            // should match the bare configured source schema.
            var pgTableListReader = Substitute.For<IDataReader>();
            var calls = 0;
            pgTableListReader.Read().Returns(_ => calls++ < 1, _ => false);
            pgTableListReader["schemaname"].Returns(SourceSchema);
            pgTableListReader["tablename"].Returns("orders");
            pgTableListReader["relkind"].Returns("r");
            pgTableListReader["relispartition"].Returns(false);
            _command.StubReaders(pgTableListReader);
            var ordersJson =
                "{\"Name\":\"orders\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"fk_orders_customers\",\"Columns\":\"customer_id\",\"RelatedTable\":\"customers\",\"RelatedColumns\":\"id\",\"RelatedTableSchema\":\"\\\"tenant_seed\\\"\"}]}";
            _command.ExecuteScalar().Returns(_ =>
                KindleGateTestHelpers.IsReadOnlyProbe(_command.CommandText) ? (object)0
                : _command.CommandText?.Contains("pg_catalog.pg_class") == true
                    ? (object)0L
                    : (object)ordersJson);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("orders.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var fks = (JArray)parsed["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"], Is.Null,
                "Double-quote-delimited same-source RelatedTableSchema should be stripped (#256)");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region §10.2 — Template.json generation

    [Test]
    public void SchemaTemplate_SqlServer_TemplateJson_ContainsSchemaTemplateFields()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            // No-op extraction — we're only checking Template.json content.
            RegisterConfig(Platform.SqlServer, ShouldCastAllFalse(Platform.SqlServer));

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(_writtenTemplateJson, Is.Not.Null, "Template.json should have been written");
            var parsed = JObject.Parse(_writtenTemplateJson);

            // The fan-out script — stub when user didn't provide one — must include the source schema name.
            Assert.That(parsed["SchemaIdentificationScript"], Is.Not.Null,
                "Template.json must carry SchemaIdentificationScript in schema-template mode");
            Assert.That(parsed["SchemaIdentificationScript"].ToString(), Does.Contain(SourceSchema));

            // CreateSchemaIfMissing must be false (or absent — Newtonsoft omits default-value bools).
            var createIfMissing = parsed["CreateSchemaIfMissing"];
            if (createIfMissing != null)
                Assert.That(createIfMissing.Value<bool>(), Is.False);

            // AllowParallel default true → omitted by JsonHelper (DefaultValueHandling.Ignore).
            // ContinueOnSchemaFailure default true → likewise omitted.
            // Both are read-back as default-true on Template.Load, so the omission is correct behavior.
            Assert.That(parsed["AllowParallel"]?.Value<bool>() ?? true, Is.True);
            Assert.That(parsed["ContinueOnSchemaFailure"]?.Value<bool>() ?? true, Is.True);

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_TemplateJson_UsesUserProvidedScriptVerbatim()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            const string userScript = "SELECT schema_name FROM SchemaSmith.fn_GetActiveTenants()";

            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["Template:SchemaIdentificationScript"] = userScript;
            RegisterConfig(Platform.SqlServer, overrides);

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(_writtenTemplateJson, Is.Not.Null);
            var parsed = JObject.Parse(_writtenTemplateJson);
            Assert.That(parsed["SchemaIdentificationScript"]?.ToString(), Is.EqualTo(userScript));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_TemplateJson_OmitsDatabaseScopedFolders()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            RegisterConfig(Platform.SqlServer, ShouldCastAllFalse(Platform.SqlServer));

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(_writtenTemplateJson, Is.Not.Null);
            var parsed = JObject.Parse(_writtenTemplateJson);
            var folders = (JArray)parsed["ScriptFolders"];
            Assert.That(folders, Is.Not.Null, "ScriptFolders must be present");

            var folderTypes = new HashSet<string>();
            foreach (var f in folders)
            {
                var ot = f["ObjectType"]?.ToString();
                if (!string.IsNullOrEmpty(ot)) folderTypes.Add(ot);
            }

            Assert.That(folderTypes, Does.Not.Contain("Schemas"));
            Assert.That(folderTypes, Does.Not.Contain("DDLTriggers"));
            Assert.That(folderTypes, Does.Not.Contain("FullTextCatalogs"));
            Assert.That(folderTypes, Does.Not.Contain("FullTextStopLists"));

            // Sanity: something must remain (Functions, Procedures, Views, Tables, etc.)
            Assert.That(folders.Count, Is.GreaterThan(0));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region §10.2 — ShouldCast force-false + warning

    [Test]
    public void SchemaTemplate_SqlServer_ShouldCastSchemas_ForcedFalse_WithWarning()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            // User explicitly set ShouldCast.Schemas true — must be forced false with warning.
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Schemas"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            // The "Casting Schema Scripts" log message would only appear if extraction ran — assert it didn't.
            _progressLog.DidNotReceive().Info(Arg.Is<string>(s => s.Contains("Casting Schema Scripts")));

            // A warning that names the forced-false flag must be emitted.
            _progressLog.Received().Warn(Arg.Is<string>(s =>
                s.Contains("Schemas") &&
                (s.Contains("ignored", StringComparison.OrdinalIgnoreCase)
                 || s.Contains("forced", StringComparison.OrdinalIgnoreCase)
                 || s.Contains("schema-template", StringComparison.OrdinalIgnoreCase))));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_ShouldCastDDLTriggers_ForcedFalse_WithWarning()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:DDLTriggers"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            _progressLog.DidNotReceive().Info(Arg.Is<string>(s => s.Contains("Casting DDL Trigger Scripts")));
            _progressLog.Received().Warn(Arg.Is<string>(s => s.Contains("DDLTriggers")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_ShouldCastFullTextCatalogs_ForcedFalse_WithWarning()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Catalogs"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            _progressLog.DidNotReceive().Info(Arg.Is<string>(s => s.Contains("Casting Full-Text Catalog")));
            _progressLog.Received().Warn(Arg.Is<string>(s =>
                s.Contains("FullTextCatalogs", StringComparison.OrdinalIgnoreCase)
                || s.Contains("Catalogs", StringComparison.OrdinalIgnoreCase)));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_ShouldCastFullTextStopLists_ForcedFalse_WithWarning()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:StopLists"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            _progressLog.DidNotReceive().Info(Arg.Is<string>(s => s.Contains("Casting Full-Text Stop List")));
            _progressLog.Received().Warn(Arg.Is<string>(s =>
                s.Contains("FullTextStopLists", StringComparison.OrdinalIgnoreCase)
                || s.Contains("StopLists", StringComparison.OrdinalIgnoreCase)));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region Regular mode unchanged (regression sentinel)

    [Test]
    public void RegularMode_SourceSchemaEmpty_ProducesSchemaQualifiedFilenames()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["Source:Schema"] = "";   // ← regular mode
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader("dbo", "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"dbo\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            // Regular mode → schema-prefixed filename.
            _fileWrapper.Received().WriteAllText(
                Arg.Is<string>(s => s.EndsWith("dbo.Customers.json")),
                Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region §7.2 — PostgreSQL ShouldCast.Schemas force-false + warning

    [Test]
    public void SchemaTemplate_PostgreSQL_ShouldCastSchemas_ForcedFalse_WithWarning()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            // User explicitly set ShouldCast.Schemas true — must be forced false with warning.
            // PG has only this one database-scoped flag in schema-template mode (design §7.2);
            // DDLTriggers / FullTextCatalogs / FullTextStopLists are SQL Server concepts and
            // do not apply here — the test must NOT extend to those.
            var overrides = ShouldCastAllFalse(Platform.PostgreSQL);
            overrides["ShouldCast:Schemas"] = "true";
            RegisterConfig(Platform.PostgreSQL, overrides);

            var emptyMain = EmptyReader();
            var emptyJson = EmptyReader();
            _command.StubReaders(emptyMain);
            _commandJson.StubReaders(emptyJson);

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            // CastPostgreSqlSchemas emits "Casting Schemas" — that must NOT have run.
            _progressLog.DidNotReceive().Info(Arg.Is<string>(s => s.Contains("Casting Schemas")));

            // A warning naming the forced-false flag must be emitted.
            _progressLog.Received().Warn(Arg.Is<string>(s =>
                s.Contains("Schemas") &&
                (s.Contains("ignored", StringComparison.OrdinalIgnoreCase)
                 || s.Contains("forced", StringComparison.OrdinalIgnoreCase)
                 || s.Contains("schema-template", StringComparison.OrdinalIgnoreCase))));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region §7.3 — Expression rewriting in table JSON (slice 6 follow-up)

    // Slice 6 of schema-templates rewrites source-schema-qualified identifiers in `.sql` file
    // bodies. The same rewrite must apply to SQL expression strings that live AS PROPERTIES
    // inside table JSON — table-level check constraints, column defaults, computed columns,
    // generated-column expressions, filtered-index expressions, and PG exclude-constraint
    // filter expressions. A user extracting from a working source DB whose computed columns
    // and defaults reference `tenant_seed.fn_*` would otherwise see those literals survive
    // into the template and bind to the wrong schema on the next iteration's quench.

    [Test]
    public void SchemaTemplate_SqlServer_TableLevelCheckConstraint_Expression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[]," +
                "\"CheckConstraints\":[{\"Name\":\"CK_Email_Domain\",\"Expression\":\"([tenant_seed].[fn_IsValidEmail](Email) = 1)\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var checks = (JArray)parsed["CheckConstraints"];
            Assert.That(checks, Is.Not.Null);
            Assert.That(checks.Count, Is.EqualTo(1));
            var expr = checks[0]["Expression"]?.ToString();
            Assert.That(expr, Does.Contain("{{SchemaName}}"),
                "Source-schema-qualified ref in CheckConstraint.Expression must be rewritten to {{SchemaName}}");
            Assert.That(expr, Does.Not.Contain("[tenant_seed]"),
                "Original source schema literal must NOT survive into the template");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_CheckConstraint_CrossSchemaReference_Preserved()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // Cross-schema reference (dbo.fn_*) must be preserved verbatim per design §7.3.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[]," +
                "\"CheckConstraints\":[{\"Name\":\"CK_Shared\",\"Expression\":\"([dbo].[fn_Validate](Code) = 1)\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var checks = (JArray)parsed["CheckConstraints"];
            Assert.That(checks[0]["Expression"]?.ToString(), Does.Contain("[dbo]"),
                "Cross-schema reference must be preserved verbatim");
            Assert.That(checks[0]["Expression"]?.ToString(), Does.Not.Contain("{{SchemaName}}"));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Column_DefaultExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Orders");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Orders\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Columns\":[{\"Name\":\"OrderNumber\",\"DataType\":\"int\",\"Nullable\":false," +
                "\"Default\":\"(NEXT VALUE FOR [tenant_seed].[OrderNumberSeq])\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Orders.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var cols = (JArray)parsed["Columns"];
            var def = cols[0]["Default"]?.ToString();
            Assert.That(def, Does.Contain("{{SchemaName}}"),
                "Source-schema-qualified ref in Column.Default must be rewritten");
            Assert.That(def, Does.Not.Contain("[tenant_seed]"));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Column_ComputedExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Invoices");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Invoices\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Columns\":[{\"Name\":\"Total\",\"DataType\":\"decimal(18,2)\",\"Nullable\":true," +
                "\"ComputedExpression\":\"([tenant_seed].[fn_CalcTotal](Lines))\",\"Persisted\":true}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Invoices.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var cols = (JArray)parsed["Columns"];
            var computed = cols[0]["ComputedExpression"]?.ToString();
            Assert.That(computed, Does.Contain("{{SchemaName}}"));
            Assert.That(computed, Does.Not.Contain("[tenant_seed]"));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Column_CheckExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Columns\":[{\"Name\":\"Email\",\"DataType\":\"varchar(200)\",\"Nullable\":false," +
                "\"CheckExpression\":\"([tenant_seed].[fn_IsValidEmail](Email) = 1)\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var cols = (JArray)parsed["Columns"];
            Assert.That(cols[0]["CheckExpression"]?.ToString(), Does.Contain("{{SchemaName}}"));
        }

        FactoryContainer.Clear();
        LogFactory.Clear();
    }

    [Test]
    public void SchemaTemplate_SqlServer_Index_FilterExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Indexes\":[{\"Name\":\"IX_Active\",\"IndexColumns\":\"Email\"," +
                "\"FilterExpression\":\"([tenant_seed].[fn_IsActive](Id) = 1)\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var idxs = (JArray)parsed["Indexes"];
            Assert.That(idxs[0]["FilterExpression"]?.ToString(), Does.Contain("{{SchemaName}}"));
            Assert.That(idxs[0]["FilterExpression"]?.ToString(), Does.Not.Contain("[tenant_seed]"));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_PostgreSQL_Column_GenerationExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.PostgreSQL);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.PostgreSQL, overrides);

            // PG path uses ExecuteScalar then list-reader for table names; mock the list reader to
            // return one row, then ExecuteScalar to return the JSON.
            var pgTableListReader = Substitute.For<IDataReader>();
            var calls = 0;
            pgTableListReader.Read().Returns(_ => calls++ < 1, _ => false);
            pgTableListReader["schemaname"].Returns(SourceSchema);
            pgTableListReader["tablename"].Returns("invoices");
            pgTableListReader["relkind"].Returns("r");
            pgTableListReader["relispartition"].Returns(false);
            _command.StubReaders(pgTableListReader);
            // Discriminate by CommandText: the PG kindle-gate pg_class existence check must return
            // 0L (table absent → stamp null → DDL runs via ExecuteNonQuery, which is fine for
            // unit tests). All other ExecuteScalar calls (GenerateTableJSON) return the table JSON.
            var invoicesJson =
                "{\"Name\":\"invoices\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Columns\":[{\"Name\":\"total\",\"DataType\":\"numeric\",\"Nullable\":true," +
                "\"GenerationExpression\":\"(tenant_seed.fn_calc_total(lines))\",\"Generated\":\"ALWAYS\"}]}";
            _command.ExecuteScalar().Returns(_ =>
                KindleGateTestHelpers.IsReadOnlyProbe(_command.CommandText) ? (object)0
                : _command.CommandText?.Contains("pg_catalog.pg_class") == true
                    ? (object)0L
                    : (object)invoicesJson);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("invoices.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var cols = (JArray)parsed["Columns"];
            var gen = cols[0]["GenerationExpression"]?.ToString();
            Assert.That(gen, Does.Contain("{{SchemaName}}"));
            // Bare `tenant_seed.fn_calc_total` should have been collapsed.
            Assert.That(gen, Does.Not.Match(@"\btenant_seed\b\s*\."));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_PostgreSQL_Index_FilterExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.PostgreSQL);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.PostgreSQL, overrides);

            var pgTableListReader = Substitute.For<IDataReader>();
            var calls = 0;
            pgTableListReader.Read().Returns(_ => calls++ < 1, _ => false);
            pgTableListReader["schemaname"].Returns(SourceSchema);
            pgTableListReader["tablename"].Returns("customers");
            pgTableListReader["relkind"].Returns("r");
            pgTableListReader["relispartition"].Returns(false);
            _command.StubReaders(pgTableListReader);
            var customersJson =
                "{\"Name\":\"customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Indexes\":[{\"Name\":\"ix_active\",\"IndexColumns\":\"email\"," +
                "\"FilterExpression\":\"(tenant_seed.fn_is_active(id))\"}]}";
            _command.ExecuteScalar().Returns(_ =>
                KindleGateTestHelpers.IsReadOnlyProbe(_command.CommandText) ? (object)0
                : _command.CommandText?.Contains("pg_catalog.pg_class") == true
                    ? (object)0L
                    : (object)customersJson);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var idxs = (JArray)parsed["Indexes"];
            var filter = idxs[0]["FilterExpression"]?.ToString();
            Assert.That(filter, Does.Contain("{{SchemaName}}"));
            Assert.That(filter, Does.Not.Match(@"\btenant_seed\b\s*\."));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_PostgreSQL_ExcludeConstraint_FilterExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.PostgreSQL);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.PostgreSQL, overrides);

            var pgTableListReader = Substitute.For<IDataReader>();
            var calls = 0;
            pgTableListReader.Read().Returns(_ => calls++ < 1, _ => false);
            pgTableListReader["schemaname"].Returns(SourceSchema);
            pgTableListReader["tablename"].Returns("rooms");
            pgTableListReader["relkind"].Returns("r");
            pgTableListReader["relispartition"].Returns(false);
            _command.StubReaders(pgTableListReader);
            var roomsJson =
                "{\"Name\":\"rooms\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"ExcludeConstraints\":[{\"Name\":\"no_overlap\",\"ExcludeColumns\":[{\"Column\":\"during\",\"Operator\":\"&&\"}]," +
                "\"FilterExpression\":\"(tenant_seed.fn_is_bookable(room_id))\"}]}";
            _command.ExecuteScalar().Returns(_ =>
                KindleGateTestHelpers.IsReadOnlyProbe(_command.CommandText) ? (object)0
                : _command.CommandText?.Contains("pg_catalog.pg_class") == true
                    ? (object)0L
                    : (object)roomsJson);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("rooms.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var excludes = (JArray)parsed["ExcludeConstraints"];
            var filter = excludes[0]["FilterExpression"]?.ToString();
            Assert.That(filter, Does.Contain("{{SchemaName}}"));
            Assert.That(filter, Does.Not.Match(@"\btenant_seed\b\s*\."));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_CheckConstraint_UnqualifiedRef_EmitsAuditEntry()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // A check constraint that references an unqualified table-position identifier
            // (`FROM SomeTable`) should be flagged in the unqualified-identifier audit so
            // the user knows to review it — §7.4 attribution shape for JSON property hits.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[]," +
                "\"CheckConstraints\":[{\"Name\":\"CK_Reachable\",\"Expression\":\"(EXISTS (SELECT 1 FROM ReachableSet))\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            // The audit warning should mention the file AND the property name (constraint name)
            // so the reviewer can navigate to the offending JSON property.
            _progressLog.Received().Warn(Arg.Is<string>(s =>
                s.Contains("Customers.json")
                && s.Contains("CK_Reachable")));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Statistic_FilterExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // Filtered statistic whose expression references the source schema — must be rewritten
            // to {{SchemaName}} so the template doesn't bind to tenant_seed on next quench.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Statistics\":[{\"Name\":\"ST_Active\",\"Columns\":\"Email\"," +
                "\"FilterExpression\":\"([tenant_seed].[fn_IsActive](Id) = 1)\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var stats = (JArray)parsed["Statistics"];
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.Count, Is.EqualTo(1));
            var filter = stats[0]["FilterExpression"]?.ToString();
            Assert.That(filter, Does.Contain("{{SchemaName}}"),
                "Source-schema-qualified ref in Statistic.FilterExpression must be rewritten to {{SchemaName}}");
            Assert.That(filter, Does.Not.Contain("[tenant_seed]"),
                "Original source schema literal must NOT survive into the template");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Statistic_CrossSchemaFilterExpression_Preserved()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // Cross-schema reference (dbo.fn_*) inside a statistic filter must be preserved verbatim.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[],\"CheckConstraints\":[]," +
                "\"Statistics\":[{\"Name\":\"ST_Shared\",\"Columns\":\"Code\"," +
                "\"FilterExpression\":\"([dbo].[fn_IsActive](Id) = 1)\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var stats = (JArray)parsed["Statistics"];
            Assert.That(stats[0]["FilterExpression"]?.ToString(), Does.Contain("[dbo]"),
                "Cross-schema reference must be preserved verbatim");
            Assert.That(stats[0]["FilterExpression"]?.ToString(), Does.Not.Contain("{{SchemaName}}"));

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_IndexedView_Index_FilterExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            // Isolate indexed-view extraction: only IndexedViews enabled. CastSqlServerIndexedViews
            // uses the main command for both list (ExecuteReader) and per-view JSON (ExecuteScalar),
            // so there's no _commandJson involvement here.
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:IndexedViews"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var viewListReader = Substitute.For<IDataReader>();
            var calls = 0;
            viewListReader.Read().Returns(_ => calls++ < 1, _ => false);
            viewListReader["SchemaName"].Returns(SourceSchema);
            viewListReader["ViewName"].Returns("ActiveCustomers");
            _command.StubReaders(viewListReader);
            var activeCustomersJson =
                "{\"Name\":\"ActiveCustomers\",\"Schema\":\"tenant_seed\",\"Definition\":\"SELECT 1\"," +
                "\"Indexes\":[{\"Name\":\"IX_ActiveCustomers\",\"IndexColumns\":\"Id\"," +
                "\"FilterExpression\":\"([tenant_seed].[fn_IsActive](Id) = 1)\"}]}";
            _command.ExecuteScalar().Returns(_ =>
                KindleGateTestHelpers.IsReadOnlyProbe(_command.CommandText) ? (object)0 : activeCustomersJson);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("ActiveCustomers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null,
                "indexed view JSON should have been written to ActiveCustomers.json");
            var parsed = JObject.Parse(capturedJson);
            var idxs = (JArray)parsed["Indexes"];
            Assert.That(idxs, Is.Not.Null);
            Assert.That(idxs.Count, Is.EqualTo(1));
            var filter = idxs[0]["FilterExpression"]?.ToString();
            Assert.That(filter, Does.Contain("{{SchemaName}}"),
                "Source-schema-qualified ref in IndexedView.Indexes[].FilterExpression must be rewritten");
            Assert.That(filter, Does.Not.Contain("[tenant_seed]"),
                "Original source schema literal must NOT survive into the template");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_PostgreSQL_MaterializedView_Index_FilterExpression_Rewritten()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            // Isolate materialized-view extraction: only MaterializedViews enabled. The PG mat-view
            // path uses the main command for both list (ExecuteReader on matviewname) and per-view
            // JSON (ExecuteScalar).
            var overrides = ShouldCastAllFalse(Platform.PostgreSQL);
            overrides["ShouldCast:MaterializedViews"] = "true";
            RegisterConfig(Platform.PostgreSQL, overrides);

            var viewListReader = Substitute.For<IDataReader>();
            var calls = 0;
            viewListReader.Read().Returns(_ => calls++ < 1, _ => false);
            viewListReader["schemaname"].Returns(SourceSchema);
            viewListReader["matviewname"].Returns("active_customers");
            _command.StubReaders(viewListReader);
            var activeCustomersJson =
                "{\"Name\":\"active_customers\",\"Schema\":\"tenant_seed\",\"Definition\":\"SELECT 1\",\"WithData\":true," +
                "\"Indexes\":[{\"Name\":\"ix_active\",\"IndexColumns\":\"id\"," +
                "\"FilterExpression\":\"(tenant_seed.fn_is_active(id))\"}]}";
            _command.ExecuteScalar().Returns(_ =>
                KindleGateTestHelpers.IsReadOnlyProbe(_command.CommandText) ? (object)0 :
                _command.CommandText?.Contains("pg_catalog.pg_class") == true
                    ? (object)0L
                    : (object)activeCustomersJson);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("active_customers.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null,
                "materialized view JSON should have been written to active_customers.json");
            var parsed = JObject.Parse(capturedJson);
            var idxs = (JArray)parsed["Indexes"];
            Assert.That(idxs, Is.Not.Null);
            Assert.That(idxs.Count, Is.EqualTo(1));
            var filter = idxs[0]["FilterExpression"]?.ToString();
            Assert.That(filter, Does.Contain("{{SchemaName}}"),
                "Source-schema-qualified ref in MaterializedView.Indexes[].FilterExpression must be rewritten");
            Assert.That(filter, Does.Not.Match(@"\btenant_seed\b\s*\."),
                "Original source schema literal must NOT survive into the template");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region #249 — Platform-aware schema-name comparison

    // SQL Server identifiers are case-insensitive by default. A source schema configured as
    // "tenant_seed" must still match metadata reads that return "Tenant_Seed" (or any other casing).
    // PostgreSQL stores identifiers in their declared case (quoted-identifier semantics matter),
    // so case differences are real differences and must be preserved.

    [Test]
    public void SchemaTemplate_SqlServer_TableExtractedDespiteSchemaNameCaseMismatch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // Metadata returns the schema with different casing than the config — this is the
            // realistic case-insensitive SQL Server scenario. Pre-fix the Ordinal compare
            // skipped the table entirely.
            var tableListReader = SingleSqlServerTableListReader("Tenant_Seed", "Customers");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Customers\",\"Schema\":\"Tenant_Seed\",\"Columns\":[],\"ForeignKeys\":[],\"OldName\":\"\"}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            // Table must be extracted despite the casing difference.
            _fileWrapper.Received().WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Customers.json")),
                Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_SqlServer_Fk_SameSchemaCaseMismatch_Stripped()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            // FK's RelatedTableSchema casing differs from _sourceSchema. On SQL Server this is the
            // same schema and the related-table-schema should be stripped (defaults to {{SchemaName}}
            // at deploy time). Pre-fix the Ordinal compare preserved the literal "Tenant_Seed",
            // leaving tenant deployments with FKs pointing back at the seed schema.
            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Orders");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Orders\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"FK_Orders_Customers\",\"Columns\":\"CustomerId\",\"RelatedTable\":\"Customers\",\"RelatedColumns\":\"Id\",\"RelatedTableSchema\":\"Tenant_Seed\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Orders.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Not.Null);
            var parsed = JObject.Parse(capturedJson);
            var fks = (JArray)parsed["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"], Is.Null,
                "Case-insensitive SQL Server: 'Tenant_Seed' is the same schema as configured 'tenant_seed' and should be stripped.");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void SchemaTemplate_PostgreSQL_TableSkippedWhenSchemaCaseMismatch()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            var overrides = ShouldCastAllFalse(Platform.PostgreSQL);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.PostgreSQL, overrides);

            // PG: metadata returns "Tenant_Seed" but config is "tenant_seed". These are DIFFERENT
            // schemas in PG (quoted-identifier semantics). The table must NOT be extracted.
            var pgTableListReader = Substitute.For<IDataReader>();
            var calls = 0;
            pgTableListReader.Read().Returns(_ => calls++ < 1, _ => false);
            pgTableListReader["schemaname"].Returns("Tenant_Seed");
            pgTableListReader["tablename"].Returns("invoices");
            _command.StubReaders(pgTableListReader);

            string capturedJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("invoices.json")),
                Arg.Any<string>())).Do(ci => capturedJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.PostgreSQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedJson, Is.Null,
                "PostgreSQL must preserve exact-case schema matching — 'Tenant_Seed' is a different schema than configured 'tenant_seed'.");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region §7.1 — MySQL: Source.Schema ignored (not schema-template activation)

    [Test]
    public void SchemaTemplate_MySQL_SourceSchema_DoesNotActivateTemplateMode()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            StubMySqlKindleGate();
            // Source.Schema passes through RegisterConfig defaults. On MySQL, it must not activate schema-template mode (design §2).
            RegisterConfig(Platform.MySQL, ShouldCastAllFalse(Platform.MySQL));

            var tongs = new SchemaTongs(Platform.MySQL);
            Assert.DoesNotThrow(() => tongs.CastTemplate(),
                "MySQL with Source.Schema set must run in regular mode, not throw.");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion

    #region Round-2 combined fix: bracket-wrapped FK null (#256) + inverted TemplateOrder partition (#258)

    [Test]
    public void Round2_CombinedFix_BracketWrappedFkAndInvertedTemplateOrder_BothRepairedInOneRun()
    {
        // Exercises both round-2 fixes in a single test:
        //   #256 — bracket-delimited RelatedTableSchema matching the source schema is nulled.
        //   #258 — TemplateOrder stable-partitions so regular templates precede schema-templates
        //           even when the Product.json was written with the schema-template first.
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();

            // --- Part 1: partition fix (#258) ---
            // Product.json already has "TenantSchema" (schema-template) as the only entry —
            // the inverted-order shape from the reviewer scenario.
            const string partitionProductPath = @"C:\fake\partition-test";
            var partitionProductFile = Path.Combine(partitionProductPath, "Product.json");
            var tenantSchemaTemplateFile = Path.Combine(partitionProductPath, "Templates", "TenantSchema", "Template.json");
            var tenantSchemaTemplate = new Template
            {
                Name = "TenantSchema",
                SchemaIdentificationScript = "SELECT 'tenant_seed' AS SchemaName"
            };

            // Register the file/directory mocks so RepositoryHelper picks up _fileWrapper
            // instead of creating a real FileWrapper. RegisterConfig is not called for Part 1
            // since we're testing UpdateOrInitRepository directly.
            FactoryContainer.Register<IFile>(_fileWrapper);
            FactoryContainer.Register<IDirectory>(_directoryWrapper);

            _fileWrapper.Exists(Arg.Is<string>(s => s == partitionProductFile)).Returns(true);
            _fileWrapper.ReadAllText(Arg.Is<string>(s => s == partitionProductFile)).Returns(
                JsonHelper.Serialize(new Product
                {
                    Name = "TestProduct",
                    Platform = Platform.SqlServer,
                    TemplateOrder = new List<string> { "TenantSchema" }
                }));
            _fileWrapper.Exists(Arg.Is<string>(s => s == tenantSchemaTemplateFile)).Returns(true);
            _fileWrapper.ReadAllText(Arg.Is<string>(s => s == tenantSchemaTemplateFile)).Returns(JsonHelper.Serialize(tenantSchemaTemplate));

            string writtenProductJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s == partitionProductFile), Arg.Any<string>()))
                .Do(ci => writtenProductJson = ci.ArgAt<string>(1));

            RepositoryHelper.UpdateOrInitRepository(
                partitionProductPath, productName: "TestProduct", templateName: "Shared", dbName: "shared_db",
                platform: Platform.SqlServer, isSchemaTemplate: false);

            Assert.That(writtenProductJson, Is.Not.Null, "Product.json should have been written");
            var writtenProduct = Newtonsoft.Json.JsonConvert.DeserializeObject<Product>(writtenProductJson);
            Assert.That(writtenProduct.TemplateOrder, Is.EqualTo(new[] { "Shared", "TenantSchema" }),
                "#258: regular template must be placed before schema-template regardless of prior TemplateOrder");

            // --- Part 2: bracket-wrapped FK schema null (#256) ---
            // Fresh SchemaTongs run in schema-template mode; table extraction returns a bracket-
            // delimited RelatedTableSchema that matches the source schema — must be stripped.
            FactoryContainer.Clear();
            LogFactory.Clear();
            SetUpMocks();

            var overrides = ShouldCastAllFalse(Platform.SqlServer);
            overrides["ShouldCast:Tables"] = "true";
            RegisterConfig(Platform.SqlServer, overrides);

            var tableListReader = SingleSqlServerTableListReader(SourceSchema, "Orders");
            var jsonReader = SingleColumnReader(
                "{\"Name\":\"Orders\",\"Schema\":\"tenant_seed\",\"OldName\":\"\",\"Columns\":[],\"ForeignKeys\":[" +
                "{\"Name\":\"FK_Orders_Customers\",\"Columns\":\"CustomerId\",\"RelatedTable\":\"Customers\"," +
                "\"RelatedColumns\":\"Id\",\"RelatedTableSchema\":\"[tenant_seed]\"}]}");
            _command.StubReaders(tableListReader);
            _commandJson.StubReaders(jsonReader);

            string capturedTableJson = null;
            _fileWrapper.When(f => f.WriteAllText(
                Arg.Is<string>(s => s.EndsWith("Orders.json")),
                Arg.Any<string>())).Do(ci => capturedTableJson = ci.ArgAt<string>(1));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate());

            Assert.That(capturedTableJson, Is.Not.Null, "Orders.json should have been written");
            var parsedTable = JObject.Parse(capturedTableJson);
            var fks = (JArray)parsedTable["ForeignKeys"];
            Assert.That(fks, Is.Not.Null);
            Assert.That(fks.Count, Is.EqualTo(1));
            Assert.That(fks[0]["RelatedTableSchema"], Is.Null,
                "#256: bracket-delimited same-source RelatedTableSchema must be stripped");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    #endregion
}
