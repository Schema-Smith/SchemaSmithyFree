// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.MySQL
{
    [TestFixture]
    [Category("MySQL")]
    public class ServerVersionHelperIntegrationTests : BaseTableQuenchTests
    {
        [Test]
        public void ServerVersionNum_ReturnsRealComparable_AtOrAboveFloor()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith_ServerVersionNum()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThanOrEqualTo(800)); // MySQL 8.0 floor
        }

        [Test]
        public void ServerVersionNum_HonorsSessionVariableOverride()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SET @schemasmith_version_override = 804";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT SchemaSmith_ServerVersionNum()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(804));

            cmd.CommandText = "SET @schemasmith_version_override = NULL";
            cmd.ExecuteNonQuery();
        }
    }
}
