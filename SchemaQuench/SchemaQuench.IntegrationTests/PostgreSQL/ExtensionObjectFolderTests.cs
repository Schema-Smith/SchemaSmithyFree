// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests;
using Schema.Isolators;
using Schema.Utility;
using System;
using System.Data;
using System.IO;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

// Can a PostgreSQL extension be deployed today, with no new SchemaSmith mechanism?
//
// This started as gap item G2, sized as a new domain type plus quench and generate scripts. The design
// review cut it down: SchemaSmith only drops tables, table components and materialized views, so an
// extension -- database-scoped, and a component of nothing -- is a scripted object like a schema or a
// collation. Scripted objects are deployed and never dropped, which is the entire posture G2 needed.
//
// This test exists to prove that claim rather than assert it, because the two possible answers put G2 in
// completely different buckets: if a plain object folder already works, G2 is documentation and a
// recipe; if it does not, G2 is code.
[Category("PostgreSQL")]
[NonParallelizable]
public class ExtensionObjectFolderTests
{
    private readonly ILog _errorLog = Substitute.For<ILog>();
    private readonly ILog _progressLog = Substitute.For<ILog>();
    private readonly IEnvironment _environment = Substitute.For<IEnvironment>();
    private readonly string _connectionString;
    private readonly string _mainDb;

    public ExtensionObjectFolderTests()
    {
        var config = FactoryContainer.Resolve<IConfigurationRoot>();
        var connProps = ConnectionString.ReadProperties(config, "Target:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform.PostgreSQL, config["Target:Server"], "postgres",
            config["Target:User"], config["Target:Password"], config["Target:Port"], connProps);
        _mainDb = config["ScriptTokens:MainDB"];
    }

    [Test]
    public void AnExtensionDeploysFromAPlainObjectFolder_AndIsIdempotent()
    {
        var temp = Path.Join(Path.GetTempPath(), $"ExtFolder_{Guid.NewGuid():N}");

        lock (FactoryContainer.SharedLockObject)
        {
            SetupSharedMocks();

            using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;

            try
            {
                DropExtension(cmd);
                Assert.That(ExtensionInstalled(cmd), Is.False,
                    "premise: citext must not already be installed, or this proves nothing");

                CopyFixtureTo("ExtensionObjectFolder", temp);
                FactoryContainer.Resolve<IConfigurationRoot>()["SchemaPackagePath"] = temp;

                RunSchemaQuench();

                Assert.Multiple(() =>
                {
                    _environment.DidNotReceive().Exit(2);
                    _environment.DidNotReceive().Exit(3);
                    Assert.That(ExtensionInstalled(cmd), Is.True,
                        "a folder in the Objects slot with no ObjectType is enough to deploy an extension "
                        + "-- which is what makes G2 a documented recipe rather than a new mechanism");
                });

                // Object scripts run on EVERY quench, so the recipe only holds if the script is
                // idempotent. CREATE EXTENSION without IF NOT EXISTS would fail here on the second run,
                // and that is the whole reason the recipe specifies it.
                _environment.ClearReceivedCalls();
                RunSchemaQuench();

                Assert.Multiple(() =>
                {
                    _environment.DidNotReceive().Exit(2);
                    _environment.DidNotReceive().Exit(3);
                    Assert.That(ExtensionInstalled(cmd), Is.True, "and it survives the second run");
                });
            }
            finally
            {
                try { DropExtension(cmd); } catch { /* teardown must not mask the assertions */ }
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }
    }

    private static bool ExtensionInstalled(IDbCommand cmd)
    {
        cmd.CommandText = "SELECT COUNT(*) FROM pg_extension WHERE extname = 'citext'";
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static void DropExtension(IDbCommand cmd)
    {
        cmd.CommandText = "DROP EXTENSION IF EXISTS citext";
        cmd.ExecuteNonQuery();
    }

    private void SetupSharedMocks()
    {
        _progressLog.ClearReceivedCalls();
        _errorLog.ClearReceivedCalls();
        _environment.ClearReceivedCalls();
        FactoryContainer.Register(_environment);
        LogFactory.Register("ErrorLog", _errorLog);
        LogFactory.Register("ProgressLog", _progressLog);
    }

    private void RunSchemaQuench() => Program.Main(["SkipKindlingForge"]);

    private static void CopyFixtureTo(string productName, string dest)
    {
        var src = TestHelper.GetTestProductPath("PostgreSQL", productName);
        CopyDirectory(src, dest);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Join(dest, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Join(dest, Path.GetFileName(dir)));
    }
}
