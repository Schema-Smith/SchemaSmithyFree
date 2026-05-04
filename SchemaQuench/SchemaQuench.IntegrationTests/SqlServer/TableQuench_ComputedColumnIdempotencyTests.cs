// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

// Pinning regression coverage for a computed-column idempotency bug:
// JSON declarations of "Persisted": true, "Nullable": false on a computed
// column (the shape used in the shipped Northwind demo's
// recyclebin.Registry.ExpirationDate) round-trip into is_nullable=1 on the
// live table because the DDL-emission paths build "AS (expr) PERSISTED"
// without appending " NOT NULL" — even though SQL Server fully supports
// "AS (expr) PERSISTED NOT NULL" on deterministic computed columns. The
// JSON-vs-live nullability mismatch then trips drift detection on every
// re-quench, emitting a destructive DROP COLUMN with no successful re-add
// (the re-add hits the same gap so the cycle never converges). When the
// column is referenced by an index, the cascade also drops that index.
//
// Affected DDL-emission sites (all platform Schema scripts):
//   * ParseTableJsonIntoTempTables.sql ColumnScript               (line 60)
//   * ModifiedTableQuench.sql #ColumnChanges.ColumnScript         (line 118)
//   * ModifiedTableQuench.sql Add Missing Computed Columns        (line 194)
[Category("SqlServer")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_ComputedColumnIdempotencyTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_ShouldHonorNotNullOnPersistedComputedColumn()
    {
        // The computed column should be created with is_nullable=0 when JSON
        // declares Persisted:true + Nullable:false. Without the fix, the
        // emitted DDL is "AS (expr) PERSISTED" — no NOT NULL — and SQL Server
        // defaults computed columns to is_nullable=1.
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"NotNullPersistedComputed_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // No pre-existing table — let TableQuench create it from JSON. Mirrors
        // the demo Northwind recyclebin.Registry shape: NOT NULL anchor, NOT
        // NULL retention days, NOT NULL persisted computed column.
        var json = $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        { "Name": "[Id]",            "DataType": "INT IDENTITY(1, 1)", "Nullable": false },
        { "Name": "[RecycledDate]",  "DataType": "DATETIME2",          "Nullable": false },
        { "Name": "[RetentionDays]", "DataType": "INT",                "Nullable": false, "Default": "90" },
        {
            "Name": "[ExpirationDate]",
            "DataType": "DATETIME2",
            "Nullable": false,
            "ComputedExpression": "DATEADD(DAY, [RetentionDays], [RecycledDate])",
            "Persisted": true
        }
    ],
    "Indexes": [
        { "Name": "[PK_{{tableName}}]", "PrimaryKey": true, "Unique": true, "Clustered": true, "IndexColumns": "[Id]" }
    ]
}
""";
        cmd.CommandText = $"EXEC SchemaSmith.TableQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Sanity: the table and computed column exist.
        cmd.CommandText = $"SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('[dbo].[{tableName}]'), 'ExpirationDate', 'IsComputed') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True, "ExpirationDate should be a computed column");

        cmd.CommandText = $"SELECT [is_persisted] FROM sys.computed_columns WHERE [object_id] = OBJECT_ID('[dbo].[{tableName}]') AND [name] = 'ExpirationDate'";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True, "ExpirationDate should be persisted");

        // The bug: live is_nullable should be 0 because JSON declared Nullable:false,
        // but without the fix it's 1 because the emitted DDL omitted NOT NULL.
        cmd.CommandText = $"SELECT [is_nullable] FROM sys.columns WHERE [object_id] = OBJECT_ID('[dbo].[{tableName}]') AND [name] = 'ExpirationDate'";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.False, "ExpirationDate should be NOT NULL — JSON declared Nullable:false on a Persisted:true computed column, which SQL Server supports as 'AS (expr) PERSISTED NOT NULL'");

        // Cleanup
        cmd.CommandText = $"DROP TABLE [dbo].[{tableName}]";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void TableQuench_ShouldNotDriftOnNotNullPersistedComputedColumn()
    {
        // Idempotency check: after the table is created, a no-op re-quench
        // must not emit a DROP COLUMN. Without the fix, the column comes back
        // as is_nullable=1 (Nullable mismatch with JSON Nullable:false) on
        // every quench, triggering an endless drop-and-readd cycle that also
        // cascades to dependent indexes.
        var productName = Guid.NewGuid().ToString();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"NoDriftPersistedComputed_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        var json = $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        { "Name": "[Id]",            "DataType": "INT IDENTITY(1, 1)", "Nullable": false },
        { "Name": "[RecycledDate]",  "DataType": "DATETIME2",          "Nullable": false },
        { "Name": "[RetentionDays]", "DataType": "INT",                "Nullable": false, "Default": "90" },
        {
            "Name": "[ExpirationDate]",
            "DataType": "DATETIME2",
            "Nullable": false,
            "ComputedExpression": "DATEADD(DAY, [RetentionDays], [RecycledDate])",
            "Persisted": true
        }
    ],
    "Indexes": [
        { "Name": "[PK_{{tableName}}]", "PrimaryKey": true, "Unique": true, "Clustered": true, "IndexColumns": "[Id]" },
        { "Name": "[IX_{{tableName}}_Exp]", "PrimaryKey": false, "Unique": false, "Clustered": false, "IndexColumns": "[ExpirationDate]" }
    ]
}
""";
        cmd.CommandText = $"EXEC SchemaSmith.TableQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Capture state after first quench. column_id is a stable identity for
        // a column — it bumps higher every time SQL Server creates a new column,
        // even if the name is reused. A drop+re-add cycle moves the column to a
        // larger column_id; a true no-op leaves it unchanged.
        cmd.CommandText = $"SELECT [definition] FROM sys.computed_columns WHERE [object_id] = OBJECT_ID('[dbo].[{tableName}]') AND [name] = 'ExpirationDate'";
        var liveDefinitionBefore = cmd.ExecuteScalar()?.ToString();

        cmd.CommandText = $"SELECT [column_id] FROM sys.columns WHERE [object_id] = OBJECT_ID('[dbo].[{tableName}]') AND [name] = 'ExpirationDate'";
        var columnIdBefore = cmd.ExecuteScalar()?.ToString();

        cmd.CommandText = $"SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('[dbo].[{tableName}]'), 'IX_{tableName}_Exp', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True, "Index on the computed column should exist after first quench");

        // Re-quench the same JSON — should be a complete no-op.
        cmd.CommandText = $"EXEC SchemaSmith.TableQuench @ProductName = '{productName}', @TableDefinitions = '{json.Replace("'", "''")}', @DropTablesRemovedFromProduct = 0, @DropUnknownIndexes = 0";
        cmd.CommandTimeout = 300;
        cmd.ExecuteNonQuery();

        // Computed column unchanged.
        cmd.CommandText = $"SELECT CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID('[dbo].[{tableName}]'), 'ExpirationDate', 'IsComputed') = 1 THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True, "ExpirationDate should still be a computed column after re-quench — no drop+readd cycle");

        cmd.CommandText = $"SELECT [definition] FROM sys.computed_columns WHERE [object_id] = OBJECT_ID('[dbo].[{tableName}]') AND [name] = 'ExpirationDate'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo(liveDefinitionBefore), "ExpirationDate definition should be unchanged after a no-op re-quench");

        // The load-bearing assertion: column_id must be unchanged. If TableQuench
        // silently dropped and re-added ExpirationDate (the bug), the column_id
        // would advance — even though the column name and definition are reused.
        cmd.CommandText = $"SELECT [column_id] FROM sys.columns WHERE [object_id] = OBJECT_ID('[dbo].[{tableName}]') AND [name] = 'ExpirationDate'";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo(columnIdBefore), "ExpirationDate column_id should be unchanged — a bumped column_id proves a destructive drop+re-add cycle ran");

        // Dependent index survived (was NOT cascaded out by a drop+re-add).
        cmd.CommandText = $"SELECT CAST(CASE WHEN INDEXPROPERTY(OBJECT_ID('[dbo].[{tableName}]'), 'IX_{tableName}_Exp', 'IndexId') IS NOT NULL THEN 1 ELSE 0 END AS BIT)";
        Assert.That(cmd.ExecuteScalar() as bool?, Is.True, "Dependent index on ExpirationDate should still exist — must not be cascaded out by a false-drift drop");

        // Cleanup
        cmd.CommandText = $"DROP TABLE [dbo].[{tableName}]";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
