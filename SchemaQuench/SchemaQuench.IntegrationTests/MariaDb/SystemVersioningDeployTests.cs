// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.DataAccess;
using Schema.Domain;
using Schema.IntegrationTests.MariaDb;
using SchemaQuench.IntegrationTests.Shared;

namespace SchemaQuench.IntegrationTests.MariaDb;

/// <summary>
/// A table declaring <c>IsSystemVersioned: true</c> has to actually deploy WITH SYSTEM VERSIONING, not
/// as an ordinary table.
/// <para>Extraction already reads <c>TABLE_TYPE = 'SYSTEM VERSIONED'</c> back into the package (the
/// round-trip half); this is the write half, closing the gap documented on
/// <see cref="Schema.Domain.MariaDb.MariaDbTable.IsSystemVersioned"/> and exercised by hand in
/// <c>Schema.IntegrationTests.MariaDb.WithoutSystemVersioningTests</c> ("because SchemaSmith cannot").
/// A package extracted from a versioned server and redeployed to a fresh database must produce a
/// versioned table, not silently lose the attribute.</para>
/// <para>MariaDB-only: MySQL has no system versioning at any version.</para>
/// <para>Scope is F1S1 (CREATE a new versioned table). Converging an EXISTING table's versioning
/// (ALTER ADD/DROP SYSTEM VERSIONING) is a separate later task and is deliberately not tested here.</para>
/// </summary>
[Category("MariaDb")]
[TestFixture]
public class SystemVersioningDeployTests : BaseTableQuenchTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDbName => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();

    private const string TableName = "SystemVersionedDeployTarget";

    private static string TableJson() => $$"""
        [{
            "Name": "{{TableName}}",
            "IsSystemVersioned": true,
            "Columns": [
                { "Name": "Id", "DataType": "INT", "Nullable": false },
                { "Name": "Description", "DataType": "VARCHAR(100)", "Nullable": true }
            ],
            "Indexes": [ { "Name": "PRIMARY", "IndexColumns": "Id", "PrimaryKey": true, "Unique": true } ]
        }]
        """;

    private string GetTableType(System.Data.IDbCommand cmd)
    {
        cmd.CommandText = $"SELECT TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES "
                          + $"WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{TableName}'";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    [Test]
    public void ADeclaredSystemVersionedTableIsCreatedAsSystemVersioned()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MariaDb).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith_SupportsSystemVersioning()";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            Assert.Ignore("This server cannot declare system versioning (MariaDB 10.3+), so the clause is "
                          + "deliberately suppressed and there is nothing to verify.");

        cmd.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        cmd.ExecuteNonQuery();

        RunTableQuenchProc(cmd, TableJson());

        // Asserted against the catalog rather than against the generated SQL: the point is that the
        // table IS system-versioned, not that a particular statement was emitted (Rule 32).
        Assert.That(GetTableType(cmd), Is.EqualTo("SYSTEM VERSIONED"),
            "A table declaring IsSystemVersioned: true must deploy WITH SYSTEM VERSIONING, not as an "
            + "ordinary table — losing this silently would mean the package's declared history-tracking "
            + "attribute never reaches the database.");

        cmd.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    /// <summary>
    /// Redeploying the same unchanged package is a clean no-op: no error, and the table stays
    /// SYSTEM VERSIONED.
    /// <para>F1S1 does not build ALTER ADD/DROP SYSTEM VERSIONING convergence (that is F1S2), so the
    /// second pass must not attempt to touch versioning at all — the table is no longer NewTable on
    /// redeploy (extraction's existing-table snapshot already recognises TABLE_TYPE = 'SYSTEM VERSIONED'
    /// as present), so the CREATE path is skipped entirely and nothing regresses or errors.</para>
    /// </summary>
    [Test]
    public void RedeployingAnUnchangedSystemVersionedPackageIsANoOp()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MariaDb).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT SchemaSmith_SupportsSystemVersioning()";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            Assert.Ignore("This server cannot declare system versioning (MariaDB 10.3+), so the clause is "
                          + "deliberately suppressed and there is nothing to verify.");

        cmd.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        cmd.ExecuteNonQuery();

        RunTableQuenchProc(cmd, TableJson());
        Assert.That(GetTableType(cmd), Is.EqualTo("SYSTEM VERSIONED"), "setup: the table must be created versioned");

        Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, TableJson()),
            "Re-deploying an unchanged package must be a no-op. The table is no longer new on this pass, "
            + "so nothing should attempt to re-issue WITH SYSTEM VERSIONING against an already-versioned table.");

        Assert.That(GetTableType(cmd), Is.EqualTo("SYSTEM VERSIONED"),
            "the table must remain system-versioned after the no-op redeploy");

        cmd.CommandText = $"DROP TABLE IF EXISTS `{TableName}`";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
