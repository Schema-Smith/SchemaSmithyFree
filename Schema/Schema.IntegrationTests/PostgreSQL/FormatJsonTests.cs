// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Utility;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// <c>SchemaSmith.FormatJson</c> — the pretty-printer every PostgreSQL extraction function returns through.
/// <para>It drops keys whose value is null, and it decided where to put a separator from a count of ALL
/// keys rather than of the keys it would actually emit. So whenever the trailing keys of an object were
/// all null it emitted a separator after the last surviving key — <c>{ "a": 1, }</c> — which is not JSON,
/// and the cast on the way out rejected it with <c>invalid input syntax for type json</c>.</para>
/// <para><b>Tested here rather than through the extraction that exposed it.</b> The defect belongs to
/// FormatJson and reaches every caller: tables, materialized views, indexed views, and any property added
/// later that happens to serialize last and null. A test sitting on the replica-identity extraction would
/// have covered one caller and left the rest to rediscover it.</para>
/// </summary>
[Category("PostgreSQL")]
[Category("Integration")]
[TestFixture]
public class FormatJsonTests
{
    private string _server = "", _user = "", _password = "", _port = "";
    private Dictionary<string, string> _props = new();
    private string _db = "";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        _server = config["PostgreSQL:Server"] ?? "127.0.0.1";
        _user = config["PostgreSQL:User"];
        _password = config["PostgreSQL:Password"];
        _port = config["PostgreSQL:Port"];
        _props = ConnectionString.ReadProperties(config, "PostgreSQL:ConnectionProperties");

        _db = $"ss_fj_{Guid.NewGuid():N}"[..28].ToLowerInvariant();
        using var maint = Open("postgres");
        Exec(maint, $"DROP DATABASE IF EXISTS \"{_db}\"");
        Exec(maint, $"CREATE DATABASE \"{_db}\"");

        using var c = Open(_db);
        using var cmd = c.CreateCommand();
        ForgeKindler.KindleTheForge(cmd, Platform.PostgreSQL);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try
        {
            using var maint = Open("postgres");
            Exec(maint, $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_db}' AND pid <> pg_backend_pid()");
            Exec(maint, $"DROP DATABASE IF EXISTS \"{_db}\"");
        }
        catch (DbException) { /* teardown must not mask an assertion */ }
    }

    private IDbConnection Open(string database)
    {
        var c = DbConnectionFactory.ForPlatform(Platform.PostgreSQL)
            .GetDbConnection(ConnectionString.Build(Platform.PostgreSQL, _server, database, _user, _password, _port, _props));
        c.Open();
        return c;
    }

    private static void Exec(IDbConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();
    }

    private string Format(string json)
    {
        using var c = Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "SELECT \"SchemaSmith\".\"FormatJson\"($ss$" + json + "$ss$::JSON)::TEXT";
        return Convert.ToString(cmd.ExecuteScalar());
    }

    [Test]
    public void ATrailingNullKey_StillProducesValidJson()
    {
        // The exact shape that broke: the last key is null, so it is dropped, and the separator after the
        // key before it has nothing left to separate.
        var formatted = Format("{\"a\": 1, \"b\": null}");

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("\"a\""), formatted);
            Assert.That(formatted, Does.Not.Contain("\"b\""), "a null value is dropped, as before");
            Assert.That(formatted.Replace(" ", "").Replace("\n", "").Replace("\r", ""),
                Does.Not.Contain(",}"), "a dangling separator is what made the result unparseable\n" + formatted);
        });
    }

    [Test]
    public void SeveralTrailingNullKeys_StillProduceValidJson()
    {
        var formatted = Format("{\"a\": 1, \"b\": null, \"c\": null, \"d\": null}");

        Assert.That(formatted.Replace(" ", "").Replace("\n", "").Replace("\r", ""),
            Does.Not.Contain(",}"), formatted);
    }

    [Test]
    public void AnAllNullObject_StillProducesValidJson()
    {
        var formatted = Format("{\"a\": null, \"b\": null}");

        Assert.That(formatted.Replace(" ", "").Replace("\n", "").Replace("\r", ""),
            Is.EqualTo("{}"), formatted);
    }

    [Test]
    public void NullKeysBetweenLiveOnes_DoNotLoseTheSeparator()
    {
        // The opposite failure to guard against while fixing the first: over-correcting into dropping a
        // separator that IS needed leaves two keys jammed together, equally unparseable.
        var formatted = Format("{\"a\": 1, \"b\": null, \"c\": 2}");
        var tight = formatted.Replace(" ", "").Replace("\n", "").Replace("\r", "");

        Assert.Multiple(() =>
        {
            Assert.That(tight, Does.Contain("\"a\":1,"), formatted);
            Assert.That(tight, Does.Contain("\"c\":2"), formatted);
            Assert.That(tight, Does.Not.Contain(",}"), formatted);
        });
    }

    [Test]
    public void NestedObjectsWithTrailingNulls_AreAlsoValid()
    {
        // FormatJson recurses, so a nested object hits the same counter independently.
        var formatted = Format("{\"outer\": {\"x\": 1, \"y\": null}, \"z\": null}");
        var tight = formatted.Replace(" ", "").Replace("\n", "").Replace("\r", "");

        Assert.Multiple(() =>
        {
            Assert.That(tight, Does.Contain("\"x\":1"), formatted);
            Assert.That(tight, Does.Not.Contain(",}"), formatted);
        });
    }
}
