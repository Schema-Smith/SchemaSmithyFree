// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_IdentityConvergenceTests : BaseTableQuenchTests
{
    // Regression guard for the "Alter Modified Columns" 42601 bug: a GENERATED ALWAYS
    // identity column used to be read back with its sequence's (START WITH .. INCREMENT BY ..)
    // suffix appended, so it was perpetually flagged "modified", built an empty ALTER clause,
    // and stranded a sibling clause's trailing comma -> "syntax error at end of input" (42601)
    // on any quench where a sibling column also needed altering.
    [Test]
    public void TableQuench_IdentityAlwaysColumn_WithSiblingAlter_Converges()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var schema = $"IdentityConvergence_{uniqueId}";
        var tableName = $"price_history_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Table already exists with an ALWAYS identity PK and a sibling column whose declared
        // type differs from deployed, forcing a real ALTER clause alongside the identity column.
        cmd.CommandText = $@"
CREATE SCHEMA IF NOT EXISTS ""{schema}"";
CREATE TABLE ""{schema}"".""{tableName}"" (
  ""history_id"" INT GENERATED ALWAYS AS IDENTITY,
  ""amount"" INT NOT NULL,
  CONSTRAINT ""pk_{tableName}"" PRIMARY KEY (""history_id"")
);";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $$"""
{
    "Schema": "{{schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "history_id", "DataType": "INT",    "Nullable": false, "Generated": "GENERATED ALWAYS AS IDENTITY" },
        { "Name": "amount",     "DataType": "BIGINT", "Nullable": false }
    ],
    "Indexes": [
        { "Name": "pk_{{tableName}}", "PrimaryKey": true, "Unique": true, "IndexColumns": "history_id" }
    ]
}
""";

        // Pre-fix this throws 42601 on the first quench (sibling alter + phantom identity clause).
        Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, json, productName: productName),
            "First quench must not fail on the identity-column Alter Modified Columns phase");

        // Sibling alter actually applied.
        Assert.That(GetColumnDataType(cmd, schema, tableName, "amount"), Is.EqualTo("INT8"),
            "amount should have been altered to BIGINT");

        // Identity survived the quench.
        cmd.CommandText = $@"SELECT a.attidentity FROM pg_attribute a
WHERE a.attrelid = '""{schema}"".""{tableName}""'::regclass AND a.attname = 'history_id';";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("a"),
            "history_id must remain a GENERATED ALWAYS identity column");

        // Re-quench: nothing changed, so it must converge cleanly (identity no longer phantom-flagged).
        Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, json, productName: productName),
            "Re-quench must converge without error");

        cmd.CommandText = $@"DROP TABLE ""{schema}"".""{tableName}""; DROP SCHEMA ""{schema}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
