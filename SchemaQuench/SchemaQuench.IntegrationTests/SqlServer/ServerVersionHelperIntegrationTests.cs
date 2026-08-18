// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    /// <summary>
    /// The SQL Server analogue of PostgreSQL's <c>schemasmith.version_override</c> GUC and MySQL's
    /// <c>@schemasmith_version_override</c> session variable: a runtime lever that forces a
    /// below-floor degrade branch on a modern binary, so the degrade paths can be exercised without
    /// a genuinely old server.
    /// <para>Transported through <c>CONTEXT_INFO</c> rather than <c>SESSION_CONTEXT</c> —
    /// SESSION_CONTEXT is 2016+ and would fail to CREATE the function on the SQL Server 2008 floor,
    /// while CONTEXT_INFO has existed since SQL Server 2000.</para>
    /// </summary>
    [TestFixture]
    [Category("SqlServer")]
    public class ServerVersionHelperIntegrationTests : BaseTableQuenchTests
    {
        private const string SetOverrideTo10 = "SET CONTEXT_INFO 0x53534F560000000A";
        private const string ClearOverride = "SET CONTEXT_INFO 0x0";

        [Test]
        public void ServerMajorVersion_ReturnsRealMajor_AtOrAboveFloor()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThanOrEqualTo(10)); // SQL Server 2008 floor
        }

        [Test]
        public void ServerMajorVersion_HonorsContextInfoOverride()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            try
            {
                cmd.CommandText = SetOverrideTo10;
                cmd.ExecuteNonQuery();

                cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
                Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(10),
                    "the session override must force the below-floor branch on a modern binary");
            }
            finally
            {
                cmd.CommandText = ClearOverride;
                cmd.ExecuteNonQuery();
            }
        }

        [Test]
        public void ServerMajorVersion_IgnoresForeignContextInfo()
        {
            // CONTEXT_INFO is a single per-session slot that anything may write. Without the
            // 0x53534F56 ('SSOV') marker a value set by other code would be misread as a version —
            // which would silently degrade a modern target. That is the whole reason for the prefix.
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            try
            {
                cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
                var real = Convert.ToInt32(cmd.ExecuteScalar());

                cmd.CommandText = "SET CONTEXT_INFO 0xDEADBEEF0000000A";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
                Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(real),
                    "a CONTEXT_INFO written by anything else must not be read as a version override");
            }
            finally
            {
                cmd.CommandText = ClearOverride;
                cmd.ExecuteNonQuery();
            }
        }

        [Test]
        public void ServerMajorVersion_OverrideIsSessionScoped()
        {
            // A test lever that leaked across connections would silently degrade unrelated work.
            using var overridden = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            overridden.Open();
            overridden.ChangeDatabase(_mainDb);
            using var overriddenCmd = overridden.CreateCommand();
            overriddenCmd.CommandText = SetOverrideTo10;
            overriddenCmd.ExecuteNonQuery();

            using var other = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            other.Open();
            other.ChangeDatabase(_mainDb);
            using var otherCmd = other.CreateCommand();
            otherCmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";

            Assert.That(Convert.ToInt32(otherCmd.ExecuteScalar()), Is.GreaterThan(10),
                "the override must not escape the session that set it");

            overriddenCmd.CommandText = ClearOverride;
            overriddenCmd.ExecuteNonQuery();
        }
    }
}
