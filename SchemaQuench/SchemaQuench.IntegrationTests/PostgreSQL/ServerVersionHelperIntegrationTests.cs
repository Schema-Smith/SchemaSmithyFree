// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL
{
    [TestFixture]
    [Category("PostgreSQL")]
    public class ServerVersionHelperIntegrationTests : BaseTableQuenchTests
    {
        [Test]
        public void ServerVersionNum_ReturnsRealMajor_AtOrAboveFloor()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT \"SchemaSmith\".\"ServerVersionNum\"()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.GreaterThanOrEqualTo(15)); // PostgreSQL 15 floor
        }

        [Test]
        public void ServerVersionNum_HonorsGucOverride()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SET schemasmith.version_override = '16'";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT \"SchemaSmith\".\"ServerVersionNum\"()";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(16));

            cmd.CommandText = "RESET schemasmith.version_override";
            cmd.ExecuteNonQuery();
        }
    }
}
