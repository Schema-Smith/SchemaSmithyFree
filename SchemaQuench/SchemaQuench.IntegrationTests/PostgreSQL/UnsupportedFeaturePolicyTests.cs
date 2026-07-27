// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class UnsupportedFeaturePolicyTests : BaseTableQuenchTests
{
    // The unsupported-feature policy helper: 'warn' by default; 'fail' only when the session setting
    // schemasmith.unsupported_policy is explicitly 'fail'. This is the per-connection lever ProductQuench
    // sets from Target:UnsupportedFeaturePolicy; version-gated emit sites read it to decide degrade-with-
    // warning vs abort.
    [Test]
    public void UnsupportedFeaturePolicy_DefaultsToWarn()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT \"SchemaSmith\".\"UnsupportedFeaturePolicy\"()";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("warn"), "Default policy must be warn");
        conn.Close();
    }

    [Test]
    public void UnsupportedFeaturePolicy_HonorsFailOverride()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SET schemasmith.unsupported_policy = 'fail'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT \"SchemaSmith\".\"UnsupportedFeaturePolicy\"()";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("fail"), "An explicit 'fail' override must be honored");

        // Any other value falls back to the safe default.
        cmd.CommandText = "SET schemasmith.unsupported_policy = 'nonsense'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT \"SchemaSmith\".\"UnsupportedFeaturePolicy\"()";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("warn"), "An unrecognized value must fall back to warn");
        conn.Close();
    }
}
