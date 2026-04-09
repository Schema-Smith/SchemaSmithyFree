// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// Integration tests for MergeScriptHelper against PostgreSQL.
/// Uses dynamically created test databases via FixtureSetup.
/// Tests special data type handling: JSON, JSONB, XML.
/// </summary>
[Category("PostgreSQL")]
[TestFixture]
[Category("Integration")]
public class MergeScriptHelperIntegrationTests
{
    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    #region JSON Tests

    [Test]
    public void BuildMergeScript_JsonColumn_RoundTrip()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_json_{Guid.NewGuid():N}"[..40];

        try
        {
            command.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""metadata"" JSON NOT NULL
)";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""metadata"":{""key"":""value"",""nested"":{""a"":1}}}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command,
                "public", tableName, tableData, @"""id""",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // JSON columns should use ::text cast for comparison
            Assert.That(script, Does.Contain("::text"));

            command.CommandText = script;
            command.ExecuteNonQuery();

            command.CommandText = $@"SELECT ""metadata""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            var result = command.ExecuteScalar()?.ToString();
            Assert.That(result, Does.Contain("key"));
            Assert.That(result, Does.Contain("value"));
        }
        finally
        {
            command.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            command.ExecuteNonQuery();
        }
    }

    #endregion

    #region JSONB Tests

    [Test]
    public void BuildMergeScript_JsonbColumn_ScriptContainsJsonbCast()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_jsonb_{Guid.NewGuid():N}"[..40];

        try
        {
            command.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""settings"" JSONB NOT NULL
)";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""settings"":{""z_key"":""last"",""a_key"":""first""}}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command,
                "public", tableName, tableData, @"""id""",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // JSONB columns should use ::jsonb cast for comparison (not ::text, since JSONB normalizes key order)
            Assert.That(script, Does.Contain("::jsonb"));

            command.CommandText = script;
            command.ExecuteNonQuery();

            command.CommandText = $@"SELECT ""settings""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            var result = command.ExecuteScalar()?.ToString();
            // JSONB normalizes key ordering alphabetically
            Assert.That(result, Does.Contain("a_key"));
            Assert.That(result, Does.Contain("z_key"));
        }
        finally
        {
            command.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            command.ExecuteNonQuery();
        }
    }

    #endregion

    #region XML Tests

    [Test]
    public void BuildMergeScript_XmlColumn_RoundTripWithTextCast()
    {
        using var command = _connection.CreateCommand();
        var tableName = $"_test_xml_{Guid.NewGuid():N}"[..40];

        try
        {
            command.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""config"" XML NOT NULL
)";
            command.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""config"":""<root><item key=\""a\"" value=\""1\"" /></root>""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, command,
                "public", tableName, tableData, @"""id""",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            // XML columns use ::text cast for comparison
            Assert.That(script, Does.Contain("::text"));

            command.CommandText = script;
            command.ExecuteNonQuery();

            command.CommandText = $@"SELECT ""config""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            var result = command.ExecuteScalar()?.ToString();
            Assert.That(result, Does.Contain("<root>"));
        }
        finally
        {
            command.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            command.ExecuteNonQuery();
        }
    }

    #endregion

    #region Network Type Verification Tests

    [Test]
    public void BuildMergeScript_NetworkTypes_CanBeExecuted_Verification()
    {
        using var cmd = _connection.CreateCommand();
        var tableName = $"_test_net_{Guid.NewGuid():N}"[..30];

        try
        {
            cmd.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""ip_addr"" INET,
    ""network"" CIDR,
    ""mac"" MACADDR
)";
            cmd.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""ip_addr"":""192.168.1.1/24"",""network"":""10.0.0.0/8"",""mac"":""08:00:2b:01:02:03""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
                "public", tableName, tableData, @"""id""",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            cmd.CommandText = script;
            cmd.ExecuteNonQuery();

            cmd.CommandText = $@"SELECT COUNT(*) FROM ""public"".""{tableName}""";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));

            cmd.CommandText = $@"SELECT ""ip_addr""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("192.168.1.1/24"));

            cmd.CommandText = $@"SELECT ""network""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("10.0.0.0/8"));

            cmd.CommandText = $@"SELECT ""mac""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("08:00:2b:01:02:03"));
        }
        finally
        {
            cmd.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            cmd.ExecuteNonQuery();
        }
    }

    #endregion

    #region Range Type Verification Tests

    [Test]
    public void BuildMergeScript_RangeTypes_CanBeExecuted_Verification()
    {
        using var cmd = _connection.CreateCommand();
        var tableName = $"_test_rng_{Guid.NewGuid():N}"[..30];

        try
        {
            cmd.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""int_range"" INT4RANGE,
    ""ts_range"" TSRANGE
)";
            cmd.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""int_range"":""[1,10)"",""ts_range"":""[2024-01-01 00:00:00,2024-12-31 23:59:59)""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
                "public", tableName, tableData, @"""id""",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            cmd.CommandText = script;
            cmd.ExecuteNonQuery();

            cmd.CommandText = $@"SELECT COUNT(*) FROM ""public"".""{tableName}""";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));

            cmd.CommandText = $@"SELECT ""int_range""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("[1,10)"));
        }
        finally
        {
            cmd.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            cmd.ExecuteNonQuery();
        }
    }

    #endregion

    #region Multirange Type Verification Tests

    [Test]
    public void BuildMergeScript_MultirangeType_CanBeExecuted_Verification()
    {
        using var cmd = _connection.CreateCommand();
        var tableName = $"_test_mrng_{Guid.NewGuid():N}"[..30];

        try
        {
            cmd.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""int_multirange"" INT4MULTIRANGE
)";
            cmd.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""int_multirange"":""{[1,5),[10,20)}""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
                "public", tableName, tableData, @"""id""",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            cmd.CommandText = script;
            cmd.ExecuteNonQuery();

            cmd.CommandText = $@"SELECT COUNT(*) FROM ""public"".""{tableName}""";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));

            cmd.CommandText = $@"SELECT ""int_multirange""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("{[1,5),[10,20)}"));
        }
        finally
        {
            cmd.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            cmd.ExecuteNonQuery();
        }
    }

    #endregion

    #region Interval Type Verification Tests

    [Test]
    public void BuildMergeScript_IntervalType_CanBeExecuted_Verification()
    {
        using var cmd = _connection.CreateCommand();
        var tableName = $"_test_intv_{Guid.NewGuid():N}"[..30];

        try
        {
            cmd.CommandText = $@"CREATE TABLE ""public"".""{tableName}"" (
    ""id"" INT PRIMARY KEY,
    ""duration"" INTERVAL
)";
            cmd.ExecuteNonQuery();

            var tableData = @"[{""id"":1,""duration"":""1 year 2 mons 3 days 04:05:06""}]";
            var script = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
                "public", tableName, tableData, @"""id""",
                mergeUpdate: true, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null);

            cmd.CommandText = script;
            cmd.ExecuteNonQuery();

            cmd.CommandText = $@"SELECT COUNT(*) FROM ""public"".""{tableName}""";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));

            cmd.CommandText = $@"SELECT ""duration""::text FROM ""public"".""{tableName}"" WHERE ""id"" = 1";
            var result = cmd.ExecuteScalar()?.ToString();
            Assert.That(result, Does.Contain("1 year"));
            Assert.That(result, Does.Contain("2 mons"));
            Assert.That(result, Does.Contain("3 days"));
            Assert.That(result, Does.Contain("04:05:06"));
        }
        finally
        {
            cmd.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
            cmd.ExecuteNonQuery();
        }
    }

    #endregion
}
