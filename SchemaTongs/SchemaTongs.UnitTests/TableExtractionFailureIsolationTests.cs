// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs.UnitTests;

/// <summary>
/// Task C1-0b: a table extraction failure must degrade to a reported per-table skip, not abort every
/// other table queued behind it and leave a partial package on disk with no record of what's missing.
/// The failure here is constructed as malformed extraction OUTPUT (bad JSON) rather than a thrown
/// ADO.NET exception, so this test needs no SQL Server-specific setup and stays valid regardless of
/// which underlying defect (partitioned tables, or anything else) first exposed the missing catch —
/// it protects the isolation mechanism itself, independent of any one root cause.
/// </summary>
[TestFixture]
public class TableExtractionFailureIsolationTests
{
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

        // First GetDbConnection call serves the main command; every call after that returns the JSON
        // connection (used for both table-JSON extraction and the unused gate connection — mirrors the
        // wiring already established in SchemaTongsTests/SchemaTemplateExtractionTests).
        var callCount = 0;
        _connectionFactory.GetDbConnection(Arg.Any<string>()).Returns(_ =>
        {
            callCount++;
            return callCount <= 1 ? _connection : _connectionJson;
        });
    }

    private void RegisterConfig()
    {
        var configValues = new Dictionary<string, string>
        {
            ["Source:Server"] = "localhost",
            ["Source:Database"] = "testdb",
            ["Source:User"] = "testuser",
            ["Source:Password"] = "testpass",
            ["Target:Platform"] = Platform.SqlServer.ToString(),
            ["Product:Path"] = Path.GetTempPath(),
            ["Product:Name"] = "TestProduct",
            ["Template:Name"] = "TestTemplate",
            ["ShouldCast:Tables"] = "true",
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
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        FactoryContainer.Register<IConfigurationRoot>(config);
        FactoryContainer.Register(_environment);
        FactoryContainer.Register(_fileWrapper);
        FactoryContainer.Register(_directoryWrapper);
        FactoryContainer.Register(_connectionFactory);
        LogFactory.Register("ProgressLog", _progressLog);
        LogFactory.Register("ErrorLog", _errorLog);

        _fileWrapper.Exists(Arg.Is<string>(s => s.Contains("Product.json"))).Returns(true);
        _fileWrapper.ReadAllText(Arg.Is<string>(s => s.Contains("Product.json"))).Returns(
            $"{{\"Name\":\"TestProduct\",\"Platform\":\"{Platform.SqlServer}\",\"TemplateOrder\":[\"TestTemplate\"],\"ScriptTokens\":{{}},\"ScriptFolders\":[]}}");
        _fileWrapper.Exists(Arg.Is<string>(s => s.Contains("Template.json"))).Returns(true);
        _fileWrapper.ReadAllText(Arg.Is<string>(s => s.Contains("Template.json"))).Returns(
            "{\"Name\":\"TestTemplate\",\"ScriptFolders\":[],\"ScriptTokens\":{}}");
    }

    private static IDataReader EmptyReader()
    {
        var r = Substitute.For<IDataReader>();
        r.Read().Returns(false);
        return r;
    }

    private static IDataReader TwoRowTableListReader(string schema, string firstTable, string secondTable)
    {
        var r = Substitute.For<IDataReader>();
        var calls = 0;
        // Single-arg Returns re-evaluates the callback on EVERY call (unlike the multi-arg "queued
        // value, then repeat the last" overload), so this correctly yields true, true, false, false...
        r.Read().Returns(_ => calls++ < 2);
        var schemas = new Queue<string>(new[] { schema, schema });
        var tables = new Queue<string>(new[] { firstTable, secondTable });
        r["TABLE_SCHEMA"].Returns(_ => schemas.Dequeue());
        r["TABLE_NAME"].Returns(_ => tables.Dequeue());
        return r;
    }

    private static IDataReader SingleColumnReader(string columnZero)
    {
        var r = Substitute.For<IDataReader>();
        var calls = 0;
        r.Read().Returns(_ => calls++ < 1);
        r[0].Returns(columnZero);
        return r;
    }

    [Test]
    public void ExtractTableDefinitions_OneTableFailsToDeserialize_SkipsItAndCastsTheRest()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            SetUpMocks();
            RegisterConfig();

            const string schema = "dbo";
            const string goodTable = "GoodTable";
            const string badTable = "BadTable";

            var tableListReader = TwoRowTableListReader(schema, goodTable, badTable);
            // command.ExecuteReader() also serves TargetVersionDetector's version probe (ExecuteScalar,
            // unaffected) and any other reader calls made along the way — only the actual table-list
            // enumeration query gets the two rows; anything else degrades to an empty result set.
            _command.ExecuteReader().Returns(_ =>
                (_command.CommandText ?? "").Contains("TABLE_SCHEMA, TABLE_NAME") ? tableListReader : EmptyReader());

            var goodJson = $"{{\"Name\":\"{goodTable}\",\"Schema\":\"{schema}\",\"Columns\":[],\"ForeignKeys\":[],\"OldName\":\"\"}}";
            // Deliberately malformed — the failure is bad extraction OUTPUT (fails PlatformDeserializer's
            // JsonConvert.DeserializeObject with a JsonException) rather than a thrown ADO.NET exception,
            // so the test needs no SQL Server-specific setup.
            const string badJson = "{ not valid json";
            _commandJson.ExecuteReader().Returns(_ =>
                (_commandJson.CommandText ?? "").Contains($"'{badTable}'") ? SingleColumnReader(badJson) : SingleColumnReader(goodJson));

            var tongs = new SchemaTongs(Platform.SqlServer);
            Assert.DoesNotThrow(() => tongs.CastTemplate(), "one bad table must not abort the whole extraction run");

            _fileWrapper.Received(1).WriteAllText(Arg.Is<string>(s => s.Contains(goodTable)), Arg.Any<string>());
            _fileWrapper.DidNotReceive().WriteAllText(Arg.Is<string>(s => s.Contains(badTable)), Arg.Any<string>());
            _progressLog.Received().Error(Arg.Is<string>(s => s.Contains(badTable)));
            _progressLog.Received().Info(Arg.Is<string>(s => s.Contains("Casting Completed with Errors")));
            Assert.That(tongs.Failed, Is.True, "a skipped table must be visible to the caller (Program.cs's exit code), not just logged");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}
