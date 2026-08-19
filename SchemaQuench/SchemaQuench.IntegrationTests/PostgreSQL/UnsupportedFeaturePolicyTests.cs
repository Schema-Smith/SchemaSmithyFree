// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

// Phase 0 unsupported-feature policy — PostgreSQL. NULLS NOT DISTINCT is a PG15 feature; below the floor
// it is degraded (warn, default) or refused (fail). Layer-1 (program design §Test strategy):
// schemasmith.version_override forces the < 15 branch on the modern CI container, so no second container
// is needed for CI; the SAME tests pass unchanged against a real postgres:14 container (the override is
// then a no-op that matches reality) — the milestone proof that the guarded catalog read parses on a
// genuine old server. NULLS NOT DISTINCT is emitted only by IndexOnlyQuench (the --IndexOnly path), so
// the policy tests drive that proc; the read-guard smoke drives the normal TableQuench flow, whose
// ModifiedTableQuench builds the existing-index snapshot that reads pg_index.indnullsnotdistinct.
[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class UnsupportedFeaturePolicyTests : BaseTableQuenchTests
{
    private const string Schema = "UnsupportedFeaturePolicyTests";

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{Schema}"";";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();
        conn.Close();
    }

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

    // The compare-side snapshot reads pg_index.indnullsnotdistinct, a PG15+ column. Forced to report
    // PG14, the guarded dynamic snapshot must omit that column — a normal table+unique-index quench must
    // complete without 42703 (undefined column). This read guard is what unblocks PG < 15 at all.
    [Test]
    public void NormalFlow_BelowPg15_TableWithUniqueIndex_DeploysWithoutUndefinedColumnError()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"Snap_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "SET schemasmith.version_override = '14';";
        cmd.ExecuteNonQuery();

        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Id", "DataType": "INT", "Nullable": false },
        { "Name": "Code", "DataType": "INT", "Nullable": true }
    ],
    "Indexes": [
        { "Name": "UX_{{tableName}}_Code", "Unique": true, "IndexColumns": "Code" }
    ]
}]
""";
        Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, json, productName: $"UFP_{uniqueId}"),
            "A normal quench of a table with a unique index must not 42703 on PG < 15 (guarded indnullsnotdistinct read).");

        Assert.That(IndexExists(cmd, tableName, $"UX_{tableName}_Code"), Is.True, "unique index must exist");

        cmd.CommandText = $@"RESET schemasmith.version_override; DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // warn (default): a unique index declared NULLS NOT DISTINCT on a < 15 target is created WITHOUT the
    // clause and an unsupportedDowngrade manifest row is recorded naming it. Deploy succeeds.
    [Test]
    public void NullsNotDistinct_BelowPg15_WarnPolicy_CreatesIndexWithoutClause_AndRecordsDowngrade()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"WarnNnd_{uniqueId}";
        var indexName = $"UX_{tableName}_Code";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        // Table exists but the unique index does not — so IndexOnlyQuench emits the CREATE UNIQUE INDEX.
        cmd.CommandText = $@"
SET schemasmith.version_override = '14';
CREATE TABLE ""{Schema}"".""{tableName}"" (""Id"" INT NOT NULL, ""Code"" INT NULL);";
        cmd.ExecuteNonQuery();

        IndexOnlyQuenchNnd(cmd, tableName, indexName, productName: $"UFP_{uniqueId}");

        Assert.That(IndexExists(cmd, tableName, indexName), Is.True, "unique index must be created");

        cmd.CommandText = $@"SELECT COUNT(*) FROM ""SchemaSmith"".""ChangeAudit""
                             WHERE ""ActionType"" = 'downgraded'
                               AND ""ObjectName"" = '{Schema}.{tableName}.{indexName}'
                               AND ""ObjectType"" = 'NULLS NOT DISTINCT (PG15)';";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1),
            "a downgrade manifest row must name the index that lost NULLS NOT DISTINCT");

        cmd.CommandText = $@"RESET schemasmith.version_override; DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // No phantom churn: on a < 15 target the declared NULLS NOT DISTINCT is neutralised to false for the
    // compare, so a second quench of the same index must not drop-and-recreate it (stable index oid).
    [Test]
    public void NullsNotDistinct_BelowPg15_Warn_SecondQuench_DoesNotRecreateIndex()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"NoChurn_{uniqueId}";
        var indexName = $"UX_{tableName}_Code";
        var productName = $"UFP_{uniqueId}";

        // Each quench is a distinct work unit on its own connection (fresh session-scoped temp tables),
        // mirroring how the --IndexOnly flow runs. The index is a real object and persists across both.
        uint firstOid;
        using (var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString))
        {
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;
            cmd.CommandText = $@"
SET schemasmith.version_override = '14';
CREATE TABLE ""{Schema}"".""{tableName}"" (""Id"" INT NOT NULL, ""Code"" INT NULL);";
            cmd.ExecuteNonQuery();

            IndexOnlyQuenchNnd(cmd, tableName, indexName, productName);
            firstOid = IndexOid(cmd, indexName);
            Assert.That(firstOid, Is.GreaterThan(0u), "index must exist after first quench");
            conn.Close();
        }

        using (var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString))
        {
            conn.Open();
            conn.ChangeDatabase(_mainDb);
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;
            cmd.CommandText = "SET schemasmith.version_override = '14';";
            cmd.ExecuteNonQuery();

            IndexOnlyQuenchNnd(cmd, tableName, indexName, productName);
            var secondOid = IndexOid(cmd, indexName);

            Assert.That(secondOid, Is.EqualTo(firstOid),
                "the index must not be dropped/recreated on a repeat quench (NULLS NOT DISTINCT neutralised, not churned) on PG < 15");

            cmd.CommandText = $@"RESET schemasmith.version_override; DROP TABLE ""{Schema}"".""{tableName}"";";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
    }

    // fail (opt-in): a below-15 target with a declared NULLS NOT DISTINCT index aborts the quench with a
    // clear "requires PostgreSQL 15" message rather than silently degrading.
    [Test]
    public void NullsNotDistinct_BelowPg15_FailPolicy_AbortsWithRequiresPg15()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"FailNnd_{uniqueId}";
        var indexName = $"UX_{tableName}_Code";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        cmd.CommandText = $@"
SET schemasmith.version_override = '14';
SET schemasmith.unsupported_policy = 'fail';
CREATE TABLE ""{Schema}"".""{tableName}"" (""Id"" INT NOT NULL, ""Code"" INT NULL);";
        cmd.ExecuteNonQuery();

        var ex = Assert.Catch(() => IndexOnlyQuenchNnd(cmd, tableName, indexName, productName: $"UFP_{uniqueId}"));
        Assert.That(ex!.Message, Does.Contain("requires PostgreSQL 15"),
            "the fail policy must abort naming the required version");

        cmd.CommandText = $@"RESET schemasmith.version_override; RESET schemasmith.unsupported_policy; DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // The FULL TableQuench flow (not just --IndexOnly) must honor NULLS NOT DISTINCT on a unique index —
    // it previously only applied through IndexOnlyQuench, so a normal deploy silently dropped the clause.
    [Test]
    public void NormalFlow_NullsNotDistinct_AppliedOnPg15Plus()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"NfNnd_{uniqueId}";
        var indexName = $"UX_{tableName}_Code";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        // Skip on a genuinely-old server (the clause cannot exist there); the sandbox is modern.
        cmd.CommandText = "SELECT current_setting('server_version_num')::int / 10000";
        if (Convert.ToInt32(cmd.ExecuteScalar()) < 15) Assert.Ignore("requires PostgreSQL 15+");

        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Id", "DataType": "INT", "Nullable": false },
        { "Name": "Code", "DataType": "INT", "Nullable": true }
    ],
    "Indexes": [
        { "Name": "{{indexName}}", "Unique": true, "IndexColumns": "Code", "NullsNotDistinct": true }
    ]
}]
""";
        RunTableQuenchProc(cmd, json, productName: $"NF_{uniqueId}");

        cmd.CommandText = $@"SELECT idx.indnullsnotdistinct FROM pg_index idx
                             JOIN pg_class i ON i.oid = idx.indexrelid WHERE i.relname = '{indexName}';";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true),
            "a normal (full) TableQuench deploy must apply NULLS NOT DISTINCT on PG15+, not only --IndexOnly");

        // Idempotent: a second normal quench must not drop/recreate the index over NND.
        var firstOid = IndexOid(cmd, indexName);
        RunTableQuenchProc(cmd, json, productName: $"NF_{uniqueId}");
        Assert.That(IndexOid(cmd, indexName), Is.EqualTo(firstOid), "no phantom churn on re-quench");

        cmd.CommandText = $@"DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // Below 15 the normal flow degrades identically to the --IndexOnly path: clause omitted + a
    // downgrade manifest row recorded.
    [Test]
    public void NormalFlow_NullsNotDistinct_BelowPg15_RecordsDowngrade()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"NfWarn_{uniqueId}";
        var indexName = $"UX_{tableName}_Code";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "SET schemasmith.version_override = '14';";
        cmd.ExecuteNonQuery();

        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Id", "DataType": "INT", "Nullable": false },
        { "Name": "Code", "DataType": "INT", "Nullable": true }
    ],
    "Indexes": [
        { "Name": "{{indexName}}", "Unique": true, "IndexColumns": "Code", "NullsNotDistinct": true }
    ]
}]
""";
        RunTableQuenchProc(cmd, json, productName: $"NFW_{uniqueId}");

        Assert.That(IndexExists(cmd, tableName, indexName), Is.True, "index created (without the clause)");
        cmd.CommandText = $@"SELECT COUNT(*) FROM ""SchemaSmith"".""ChangeAudit""
                             WHERE ""ActionType"" = 'downgraded'
                               AND ""ObjectName"" = '{Schema}.{tableName}.{indexName}'
                               AND ""ObjectType"" = 'NULLS NOT DISTINCT (PG15)';";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1),
            "the normal flow must record a downgrade manifest row below PG15");

        cmd.CommandText = $@"RESET schemasmith.version_override; DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // The --IndexOnly path must also actually APPLY NULLS NOT DISTINCT on PG15+ (its emit was never
    // exercised with the clause present — the policy tests all force override=14, which omits it).
    [Test]
    public void IndexOnly_NullsNotDistinct_AppliedOnPg15Plus()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"IoNnd_{uniqueId}";
        var indexName = $"UX_{tableName}_Code";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        cmd.CommandText = "SELECT current_setting('server_version_num')::int / 10000";
        if (Convert.ToInt32(cmd.ExecuteScalar()) < 15) Assert.Ignore("requires PostgreSQL 15+");

        cmd.CommandText = $@"CREATE TABLE ""{Schema}"".""{tableName}"" (""Id"" INT NOT NULL, ""Code"" INT NULL);";
        cmd.ExecuteNonQuery();

        IndexOnlyQuenchNnd(cmd, tableName, indexName, productName: $"IO_{uniqueId}");

        cmd.CommandText = $@"SELECT idx.indnullsnotdistinct FROM pg_index idx
                             JOIN pg_class i ON i.oid = idx.indexrelid WHERE i.relname = '{indexName}';";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true),
            "the --IndexOnly path must apply NULLS NOT DISTINCT on PG15+");

        cmd.CommandText = $@"DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // Table-level access method (ALTER TABLE ... SET ACCESS METHOD) is a PG15 feature. Below the floor the
    // emit is suppressed (so no hard 42601 on SET ACCESS METHOD, and the fixup pass ignores the difference so
    // it does not churn); warn (default) records a downgrade manifest row naming the table.
    [Test]
    public void AccessMethod_BelowPg15_WarnPolicy_DeploysWithoutError_AndRecordsDowngrade()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"WarnAm_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "SET schemasmith.version_override = '14';";
        cmd.ExecuteNonQuery();

        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "AccessMethod": "columnar",
    "Columns": [
        { "Name": "Id", "DataType": "INT", "Nullable": false }
    ]
}]
""";
        Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, json, productName: $"AMW_{uniqueId}"),
            "a non-default access method must degrade (emit suppressed) below PG15, not error on SET ACCESS METHOD");

        cmd.CommandText = $@"SELECT COUNT(*) FROM ""SchemaSmith"".""ChangeAudit""
                             WHERE ""ActionType"" = 'downgraded'
                               AND ""ObjectName"" = '{Schema}.{tableName}'
                               AND ""ObjectType"" = 'table access method (PG15)';";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1),
            "a downgrade manifest row must record the table that lost its access method");

        cmd.CommandText = $@"RESET schemasmith.version_override; DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // fail (opt-in): a below-15 target declaring a non-default access method aborts the quench with a clear
    // "requires PostgreSQL 15" message rather than silently degrading.
    [Test]
    public void AccessMethod_BelowPg15_FailPolicy_AbortsWithRequiresPg15()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"FailAm_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "SET schemasmith.version_override = '14'; SET schemasmith.unsupported_policy = 'fail';";
        cmd.ExecuteNonQuery();

        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "AccessMethod": "columnar",
    "Columns": [
        { "Name": "Id", "DataType": "INT", "Nullable": false }
    ]
}]
""";
        var ex = Assert.Catch(() => RunTableQuenchProc(cmd, json, productName: $"AMF_{uniqueId}"));
        Assert.That(ex!.Message, Does.Contain("requires PostgreSQL 15"),
            "the fail policy must abort naming the required version");

        cmd.CommandText = $@"RESET schemasmith.version_override; RESET schemasmith.unsupported_policy;
                             DROP TABLE IF EXISTS ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // warn (default): a VIRTUAL generated column on a < 18 target is skipped entirely (STORED siblings
    // are unaffected) and an unsupportedDowngrade manifest row is recorded naming it. Deploy succeeds.
    [Test]
    public void VirtualGeneratedColumn_BelowPg18_WarnPolicy_SkipsColumn_AndRecordsDowngrade()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"WarnVirt_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "SET schemasmith.version_override = '17';";
        cmd.ExecuteNonQuery();

        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Qty", "DataType": "INT", "Nullable": false },
        { "Name": "DoubleQty", "DataType": "INT", "Nullable": true,
          "Generated": "ALWAYS", "GenerationExpression": "(\"Qty\" * 2)" },
        { "Name": "TripleQty", "DataType": "INT", "Nullable": true,
          "Generated": "ALWAYS", "GenerationExpression": "(\"Qty\" * 3)", "Virtual": true }
    ]
}]
""";
        Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, json, productName: $"UFPV_{uniqueId}"),
            "a VIRTUAL generated column must degrade (skipped) below PG18, not raise a raw syntax error");

        cmd.CommandText = $@"SELECT is_generated FROM information_schema.columns
                             WHERE table_schema = '{Schema}' AND table_name = '{tableName}' AND column_name = 'DoubleQty';";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("ALWAYS"),
            "a STORED generated column on the same table must still be created — the rest of the deploy proceeds");

        cmd.CommandText = $@"SELECT COUNT(*) FROM information_schema.columns
                             WHERE table_schema = '{Schema}' AND table_name = '{tableName}' AND column_name = 'TripleQty';";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "the VIRTUAL column must not be created");

        cmd.CommandText = $@"SELECT COUNT(*) FROM ""SchemaSmith"".""ChangeAudit""
                             WHERE ""ActionType"" = 'downgraded'
                               AND ""ObjectName"" = '{Schema}.{tableName}.TripleQty'
                               AND ""ObjectType"" = 'VIRTUAL generated column (PG18)';";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1),
            "a downgrade manifest row must name the column that lost VIRTUAL storage");

        cmd.CommandText = $@"RESET schemasmith.version_override; DROP TABLE ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // fail (opt-in): a below-18 target declaring a VIRTUAL generated column aborts the quench with a clear
    // "require PostgreSQL 18" message rather than surfacing PostgreSQL's own generated-column syntax error.
    [Test]
    public void VirtualGeneratedColumn_BelowPg18_FailPolicy_AbortsWithRequiresPg18()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"FailVirt_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;
        cmd.CommandText = "SET schemasmith.version_override = '17'; SET schemasmith.unsupported_policy = 'fail';";
        cmd.ExecuteNonQuery();

        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Qty", "DataType": "INT", "Nullable": false },
        { "Name": "TripleQty", "DataType": "INT", "Nullable": true,
          "Generated": "ALWAYS", "GenerationExpression": "(\"Qty\" * 3)", "Virtual": true }
    ]
}]
""";
        var ex = Assert.Catch(() => RunTableQuenchProc(cmd, json, productName: $"UFPV_{uniqueId}"));
        Assert.That(ex!.Message, Does.Contain("require PostgreSQL 18"),
            "the fail policy must abort naming the required version, not surface a raw PostgreSQL syntax error");

        cmd.CommandText = $@"RESET schemasmith.version_override; RESET schemasmith.unsupported_policy;
                             DROP TABLE IF EXISTS ""{Schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private void IndexOnlyQuenchNnd(IDbCommand cmd, string tableName, string indexName, string productName)
    {
        var json = $$"""
[{
    "Schema": "{{Schema}}",
    "Name": "{{tableName}}",
    "Indexes": [
        { "Name": "{{indexName}}", "Unique": true, "IndexColumns": "Code", "NullsNotDistinct": true }
    ]
}]
""";
        cmd.CommandText = $@"CALL ""SchemaSmith"".""IndexOnlyQuench""(p_ProductName := '{productName}', p_TableDefinitions := '{json.Replace("'", "''")}', p_DropUnknownIndexes := false);";
        cmd.ExecuteNonQuery();
    }

    private bool IndexExists(IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $@"SELECT COUNT(*) FROM pg_indexes WHERE schemaname = '{Schema}' AND tablename = '{tableName}' AND indexname = '{indexName}';";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private uint IndexOid(IDbCommand cmd, string indexName)
    {
        cmd.CommandText = $@"SELECT COALESCE(to_regclass('""{Schema}"".""{indexName}""')::oid, 0)::oid;";
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0u : Convert.ToUInt32(result);
    }
}
