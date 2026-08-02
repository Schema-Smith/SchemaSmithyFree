// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Data;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer
{
    // UnsupportedFeaturePolicy bakes the resolved policy ('warn' default | 'fail') into its body at KINDLE
    // time from Target:UnsupportedFeaturePolicy (the SS-2008 floor dropped the 2016+ SESSION_CONTEXT
    // transport, unavailable on a genuine pre-2016 binary). Any value other than an explicit 'fail'
    // resolves to the safe 'warn' default.
    [TestFixture]
    [Category("SqlServer")]
    public class UnsupportedFeaturePolicyTests : BakedKindleTestBase
    {
        [Test]
        public void UnsupportedFeaturePolicy_DefaultsToWarn_WhenNothingBaked()
        {
            // _mainDb is kindled by FixtureSetup with the default policy ('warn').
            using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("warn"));
        }

        [Test]
        public void UnsupportedFeaturePolicy_ReturnsFail_WhenKindledWithFail()
        {
            using var conn = KindleScratchDatabase("PolicyFailBake", policy: "fail");
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("fail"));
        }

        [Test]
        public void UnsupportedFeaturePolicy_ResolvesToWarn_ForAnyNonFailValue()
        {
            // The function's defensive CASE resolves anything other than an explicit 'fail' to 'warn' even if
            // an un-normalized value were ever baked (the C# caller normalizes to 'warn'/'fail' beforehand).
            using var conn = KindleScratchDatabase("PolicyBogusBake", policy: "bogus");
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT SchemaSmith.UnsupportedFeaturePolicy()";
            Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("warn"));
        }
    }
}
