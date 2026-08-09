// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Base for the SQL Server helper-function tests that must prove a value baked at KINDLE time (the SS-2008
/// floor dropped the 2016+ SESSION_CONTEXT transport, so fn_ServerMajorVersion / UnsupportedFeaturePolicy
/// now carry their value in the compiled body). Each baked-value assertion needs its own freshly-kindled
/// database — re-kindling the shared main DB with a specific version/policy would contaminate sibling tests —
/// so this creates throwaway databases, kindles the forge into them with the requested values, and drops
/// them on teardown.
/// </summary>
public abstract class BakedKindleTestBase : BaseTableQuenchTests
{
    private string _server = "", _user = "", _password = "", _port = "";
    private Dictionary<string, string> _connProps = new();
    private readonly List<string> _createdDbs = [];

    [OneTimeSetUp]
    public void BakedKindleSetup()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        _server = config["Target:Server"];
        _user = config["Target:User"];
        _password = config["Target:Password"];
        _port = config["Target:Port"];
        _connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
    }

    /// <summary>
    /// Create a throwaway database (at the server's default compatibility level), kindle the SQL Server
    /// helper set into it with the supplied baked server major version + unsupported-feature policy, and
    /// return an OPEN connection to it. The caller disposes the connection; teardown drops the database.
    /// </summary>
    protected IDbConnection KindleScratchDatabase(string prefix, int serverMajorVersion = 0, string policy = "warn",
        IngestEncoding encoding = IngestEncoding.Json)
    {
        var db = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..8]}";
        using (var master = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString))
        {
            master.Open();
            using var createCmd = master.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE [{db}]";
            createCmd.ExecuteNonQuery();
        }
        _createdDbs.Add(db);

        var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(
            ConnectionString.Build(Platform.SqlServer, _server, db, _user, _password, _port, _connProps));
        conn.Open();
        using (var kindleCmd = conn.CreateCommand())
            ForgeKindler.KindleTheForge(kindleCmd, Platform.SqlServer, forceReKindle: true,
                encoding, serverMajorVersion, policy);
        return conn;
    }

    [OneTimeTearDown]
    public void BakedKindleTeardown()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        foreach (var db in _createdDbs)
        {
            cmd.CommandText = $@"
IF DB_ID('{db}') IS NOT NULL
  ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE IF EXISTS [{db}];";
            cmd.ExecuteNonQuery();
        }
    }
}
