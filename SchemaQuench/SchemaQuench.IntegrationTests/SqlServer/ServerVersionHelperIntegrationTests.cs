// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    // fn_ServerMajorVersion bakes the C#-detected server major version into its body at KINDLE time (the
    // SS-2008 floor dropped the 2016+ SESSION_CONTEXT transport, which would not CREATE on a genuine pre-2016
    // binary). When nothing is baked (0) it falls back to the real server property, so a default kindle still
    // reports the true version; a baked value wins.
    [TestFixture]
    [Category("SqlServer")]
    public class ServerVersionHelperIntegrationTests : BakedKindleTestBase
    {
        [Test]
        public void FnServerMajorVersion_FallsBackToRealMajor_WhenNothingBaked()
        {
            // _mainDb is kindled by FixtureSetup with no baked version (0) -> SERVERPROPERTY fallback.
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThanOrEqualTo(10)); // SQL Server 2008 (major 10) floor
        }

        [Test]
        public void FnServerMajorVersion_ReturnsBakedVersion_WhenKindledWithOne()
        {
            // Bake an old major (12 = SQL Server 2014) at kindle time; the literal wins over the real server,
            // which is how a genuine pre-2016 binary — where SERVERPROPERTY('ProductMajorVersion') is NULL —
            // still resolves its version.
            using var conn = KindleScratchDatabase("FnVersionBake", serverMajorVersion: 12);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(12));
        }
    }
}
