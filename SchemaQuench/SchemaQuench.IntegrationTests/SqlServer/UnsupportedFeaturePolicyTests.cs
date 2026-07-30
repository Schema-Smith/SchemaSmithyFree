// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    [TestFixture]
    [Category("SqlServer")]
    public class UnsupportedFeaturePolicyTests : BaseTableQuenchTests
    {
        [Test]
        public void UnsupportedFeaturePolicy_DefaultsToWarn_WhenSessionContextUnset()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("warn"));
        }

        [Test]
        public void UnsupportedFeaturePolicy_ReturnsFail_WhenSessionContextIsFail()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "EXEC sp_set_session_context N'schemasmith.unsupported_policy', N'fail'";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("fail"));
        }

        [Test]
        public void UnsupportedFeaturePolicy_ResolvesToWarn_ForAnyNonFailValue()
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "EXEC sp_set_session_context N'schemasmith.unsupported_policy', N'bogus'";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("warn"));
        }
    }
}
