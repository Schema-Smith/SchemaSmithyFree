// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.PostgreSQL;

[Category("PostgreSQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_GeneratedColumnVersionTests : BaseTableQuenchTests
{
    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"CREATE SCHEMA IF NOT EXISTS ""GeneratedColumnVersionTests"";";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // Forces the in-DB version helper to report PG 16 (the override GUC), so the
    // pre-17 drop-and-re-add path runs even on a PG17 CI container. Asserts the
    // generated-column expression change is applied AND data is correct, with no
    // 42601 SET EXPRESSION error. Layer-1 — no second container (design §Testing Strategy).
    [Test]
    public void TableQuench_ChangesGeneratedExpression_ViaDropAndReAdd_WhenForcedPg16()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var schema = "GeneratedColumnVersionTests";
        var tableName = $"GenColV16_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Seed: a stored generated column "DoubleQty" = Qty * 2, with a row.
        cmd.CommandText = $@"
SET schemasmith.version_override = '16';
CREATE TABLE ""{schema}"".""{tableName}"" (
  ""Qty"" INT NOT NULL,
  ""DoubleQty"" INT GENERATED ALWAYS AS (""Qty"" * 2) STORED
);
INSERT INTO ""{schema}"".""{tableName}"" (""Qty"") VALUES (5);";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Quench with a CHANGED expression: Qty * 3. Override is still set on this session.
        var json = $$"""
{
    "Schema": "{{schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Qty", "DataType": "INT", "Nullable": false },
        { "Name": "DoubleQty", "DataType": "INT", "Nullable": true,
          "Generated": "ALWAYS", "GenerationExpression": "(\"Qty\" * 3)" }
    ]
}
""";
        cmd.CommandText = $@"
SET schemasmith.version_override = '16';
CALL ""SchemaSmith"".""TableQuench""(p_ProductName := '{productName}', p_TableDefinitions := '{json.Replace("'", "''")}', p_WhatIf := false, p_DropTablesRemovedFromProduct := false, p_DropUnknownIndexes := false);";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // New expression must be live and the existing row recomputed to 15 (5 * 3).
        cmd.CommandText = $@"RESET schemasmith.version_override;
SELECT ""DoubleQty"" FROM ""{schema}"".""{tableName}"" WHERE ""Qty"" = 5;";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(15),
            "Generated-column expression change must apply via drop-and-re-add on PG < 17 (no SET EXPRESSION)");

        cmd.CommandText = $@"DROP TABLE ""{schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    // Native-version path: no override, so the helper reports the real container version.
    // End state must be identical regardless of which branch fired.
    [Test]
    public void TableQuench_ChangesGeneratedExpression_NativeVersion()
    {
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var schema = "GeneratedColumnVersionTests";
        var tableName = $"GenColNative_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.PostgreSQL).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $@"
CREATE TABLE ""{schema}"".""{tableName}"" (
  ""Qty"" INT NOT NULL,
  ""DoubleQty"" INT GENERATED ALWAYS AS (""Qty"" * 2) STORED
);
INSERT INTO ""{schema}"".""{tableName}"" (""Qty"") VALUES (5);";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        var json = $$"""
{
    "Schema": "{{schema}}",
    "Name": "{{tableName}}",
    "Columns": [
        { "Name": "Qty", "DataType": "INT", "Nullable": false },
        { "Name": "DoubleQty", "DataType": "INT", "Nullable": true,
          "Generated": "ALWAYS", "GenerationExpression": "(\"Qty\" * 3)" }
    ]
}
""";
        cmd.CommandText = $@"CALL ""SchemaSmith"".""TableQuench""(p_ProductName := '{productName}', p_TableDefinitions := '{json.Replace("'", "''")}', p_WhatIf := false, p_DropTablesRemovedFromProduct := false, p_DropUnknownIndexes := false);";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $@"SELECT ""DoubleQty"" FROM ""{schema}"".""{tableName}"" WHERE ""Qty"" = 5;";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(15));

        cmd.CommandText = $@"DROP TABLE ""{schema}"".""{tableName}"";";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
