// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    [TestFixture]
    [Category("SqlServer")]
    public class ServerVersionHelperIntegrationTests : BaseTableQuenchTests
    {
        [Test]
        public void FnServerMajorVersion_ReturnsRealMajor_AtOrAboveFloor()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThanOrEqualTo(14)); // SQL Server 2017 floor
        }

        [Test]
        public void FnServerMajorVersion_HonorsSessionOverride()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "EXEC sp_set_session_context N'schemasmith.version_override', 15";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT SchemaSmith.fn_ServerMajorVersion()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(15));
        }
    }
}
