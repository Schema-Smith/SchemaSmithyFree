// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Npgsql;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

// Regression coverage for the PK-implies-unique false-change bug. A primary key declared the
// natural way -- an index entry { "Name": ..., "PrimaryKey": true, "IndexColumns": ... } with NO
// explicit "Unique": true -- has declared "Unique" = false, while the existing PK's backing index
// reports indisunique = TRUE. The "Modified Index" comparison did COALESCE(i."Unique", FALSE) !=
// ei."Unique" (false != true) and so wrongly classed the unchanged PK as modified on EVERY quench,
// dropping and recreating it (and cascading dependent FKs) every run.
//
// The strong, rewrite-proof signal is pg_constraint.oid: a drop/recreate of the PK produces a NEW
// oid. A converged, idempotent quench must leave the oid UNCHANGED across a no-op re-quench.
[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_IndexIdempotencyTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_PrimaryKeyDeclaredWithoutUniqueIsIdempotent()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var schema = "public";
        var tableName = $"PkIdem_{uniqueId}";
        var pkName = $"PK_{tableName}";

        using var conn = (NpgsqlConnection)DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        // Primary key declared WITHOUT an explicit "Unique" flag -- the natural hand-authored form.
        var json = $$"""
{
    "Schema": "{{schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Id",   "DataType": "integer",     "Nullable": false },
        { "Name": "Note", "DataType": "varchar(50)", "Nullable": true }
    ],
    "Indexes": [
        { "Name": "{{pkName}}", "PrimaryKey": true, "IndexColumns": "Id" }
    ]
}
""";

        try
        {
            // Converge: create the table + PK from scratch.
            RunTableQuenchProc(cmd, json);

            var oidBefore = GetPrimaryKeyOid(cmd, schema, tableName, pkName);
            Assert.That(oidBefore, Is.Not.Null.And.Not.EqualTo(0L),
                "Setup failed: the primary key constraint was not created by the first quench.");

            // No-op re-quench of the identical JSON.
            RunTableQuenchProc(cmd, json);

            var oidAfter = GetPrimaryKeyOid(cmd, schema, tableName, pkName);

            Assert.That(oidAfter, Is.EqualTo(oidBefore),
                "Re-quench of an unchanged primary key (declared PrimaryKey:true with no explicit Unique) " +
                "must NOT drop and recreate it. A changed pg_constraint.oid proves the PK was dropped and " +
                $"recreated -- the false 'modified index' verdict. Before: {oidBefore}, After: {oidAfter}.");
        }
        finally
        {
            cmd.CommandText = $@"DROP TABLE IF EXISTS ""{schema}"".""{tableName}"";";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    private static long? GetPrimaryKeyOid(IDbCommand cmd, string tableSchema, string tableName, string constraintName)
    {
        cmd.CommandText = $@"
SELECT con.oid
  FROM pg_constraint con
  JOIN pg_class rel ON rel.oid = con.conrelid
  JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
  WHERE nsp.nspname = '{tableSchema}'
    AND rel.relname = '{tableName}'
    AND con.conname = '{constraintName}'
    AND con.contype = 'p';";
        var result = cmd.ExecuteScalar();
        return result is null || result is DBNull ? null : Convert.ToInt64(result);
    }
}
