// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

// Backlog E3: COLUMN_SET FOR ALL_SPARSE_COLUMNS. SQL Server only allows adding a column set to a
// table when either (a) it is created fresh, or (b) the sparse columns it aggregates are added in
// the SAME statement -- a column set cannot be added by ALTER TABLE to a table that already has
// standalone sparse columns. SchemaSmith already batches a table's new columns into one CREATE
// TABLE / ALTER TABLE ADD (SchemaSmith.MissingTableAndColumnQuench.sql), so both cases are covered
// by construction; no pre-validation of the sparse/column-set relationship was added -- an illegal
// combination is left to the engine's own (specific) rejection.
//
// The reliable no-rebuild signal for idempotency is the procedure's own log: when a column is
// classified as modified, ModifiedTableQuench emits a "  Altering Column <schema>.<table>.<column>"
// RAISERROR (captured here via SqlConnection.InfoMessage). A converged table must emit none.
[Category("SqlServer")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_ColumnSetTests : BaseTableQuenchTests
{
    [Test]
    public void ShouldCreateTableWithSparseColumnsAndColumnSet()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"ColSetNew_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        var json = $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[SparseA]", "DataType": "VARCHAR(20)", "Nullable": true, "Sparse": true},
        {"Name": "[SparseB]", "DataType": "INT", "Nullable": true, "Sparse": true},
        {"Name": "[Aggregated]", "DataType": "XML", "Nullable": true, "IsColumnSet": true}
    ]
}
""";

        try
        {
            RunTableQuenchProc(cmd, json);

            cmd.CommandText = $"SELECT [name], is_sparse, is_column_set FROM sys.columns WHERE [object_id] = OBJECT_ID('dbo.{tableName}') ORDER BY column_id";
            using (var reader = cmd.ExecuteReader())
            {
                var rows = new Dictionary<string, (bool IsSparse, bool IsColumnSet)>();
                while (reader.Read())
                    rows[reader.GetString(0)] = (reader.GetBoolean(1), reader.GetBoolean(2));

                Assert.Multiple(() =>
                {
                    Assert.That(rows["SparseA"].IsSparse, Is.True, "SparseA must be created SPARSE");
                    Assert.That(rows["SparseA"].IsColumnSet, Is.False);
                    Assert.That(rows["SparseB"].IsSparse, Is.True, "SparseB must be created SPARSE");
                    Assert.That(rows["SparseB"].IsColumnSet, Is.False);
                    Assert.That(rows["Aggregated"].IsColumnSet, Is.True, "Aggregated must be created as the column set");
                    Assert.That(rows["Aggregated"].IsSparse, Is.False, "the column set column is not itself sparse");
                });
            }
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS [dbo].[{tableName}];";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }

    [Test]
    public void ShouldAddSparseColumnsAndColumnSetToExistingTableInOneAlter()
    {
        // Exercises MissingTableAndColumnQuench's ADD-columns batching: a column set cannot be
        // added by ALTER TABLE to a table that already has standalone sparse columns, but IS legal
        // when the column set and the sparse columns it aggregates arrive in the same ALTER TABLE
        // ADD statement -- which is exactly what this proc emits (one comma-separated ADD per
        // table, not one ALTER per column).
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"ColSetAdd_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"CREATE TABLE dbo.{tableName} (Id INT NOT NULL);";
        cmd.ExecuteNonQuery();

        var json = $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[SparseA]", "DataType": "VARCHAR(20)", "Nullable": true, "Sparse": true},
        {"Name": "[Aggregated]", "DataType": "XML", "Nullable": true, "IsColumnSet": true}
    ]
}
""";

        try
        {
            RunTableQuenchProc(cmd, json);

            cmd.CommandText = $"SELECT is_column_set FROM sys.columns WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = 'Aggregated'";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true), "the column set must be added alongside its sparse column in the same ALTER TABLE ADD");
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS [dbo].[{tableName}];";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }

    [Test]
    public void ShouldNotAlterColumnSetOrSparseColumnsOnReQuenchWithNoChanges()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"ColSetIdem_{uniqueId}";
        var pkName = $"PK_{tableName}";

        var messages = new List<string>();
        using var conn = (SqlConnection)DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.InfoMessage += (_, e) => { foreach (SqlError err in e.Errors) messages.Add(err.Message); };
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        var json = $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[SparseA]", "DataType": "VARCHAR(20)", "Nullable": true, "Sparse": true},
        {"Name": "[Aggregated]", "DataType": "XML", "Nullable": true, "IsColumnSet": true}
    ],
    "Indexes": [
        { "Name": "[{{pkName}}]", "PrimaryKey": true, "Unique": true, "Clustered": true, "IndexColumns": "[Id]" }
    ]
}
""";

        try
        {
            // Converge once.
            RunTableQuenchProc(cmd, json);

            // No-op re-quench: capture only the messages from THIS pass.
            messages.Clear();
            RunTableQuenchProc(cmd, json);

            var alterMessages = messages.FindAll(m => m.Contains("Altering Column") && m.Contains(tableName));
            Assert.That(alterMessages, Is.Empty,
                "Re-quench of an unchanged sparse/column-set table must NOT re-alter any column. " +
                "An 'Altering Column' message means the Sparse/IsColumnSet drift comparison produced a phantom modification. " +
                $"Messages: {string.Join(" | ", messages)}");
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS [dbo].[{tableName}];";
            cmd.ExecuteNonQuery();
        }
        conn.Close();
    }

    [Test]
    public void ShouldConvertExistingPlainColumnIntoColumnSetOnReQuench()
    {
        // Toggling an EXISTING plain column into a column set requires drop+recreate: ALTER COLUMN does
        // not accept the COLUMN_SET clause at all (confirmed against Microsoft's docs -- it is CREATE-TABLE-
        // or ADD-only), so ModifiedTableQuench routes an IsColumnSet mismatch through MustDropAndRecreate
        // rather than attempting an ALTER COLUMN that would always be a syntax error. A brand-new sparse
        // column declared in the SAME quench lands in the SAME ALTER TABLE ADD as the recreated column-set
        // column (MissingTableAndColumnQuench batches every NewColumn=1 row per table), satisfying SQL
        // Server's "same statement" requirement without any special-casing.
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"ColSetConvert_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"CREATE TABLE dbo.{tableName} (Id INT NOT NULL, PlainXml XML NULL);";
        cmd.ExecuteNonQuery();

        var json = $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[PlainXml]", "DataType": "XML", "Nullable": true, "IsColumnSet": true},
        {"Name": "[SparseA]", "DataType": "VARCHAR(20)", "Nullable": true, "Sparse": true}
    ]
}
""";

        try
        {
            RunTableQuenchProc(cmd, json);

            cmd.CommandText = $"SELECT is_column_set FROM sys.columns WHERE [object_id] = OBJECT_ID('dbo.{tableName}') AND [name] = 'PlainXml'";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true), "PlainXml must be converted into the column set via drop+recreate");
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS [dbo].[{tableName}];";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }

    [Test]
    public void ShouldSurfaceEngineRejectionWhenConvertingToColumnSetOnTableWithExistingSparseColumns()
    {
        // The illegal counterpart: the table already has a standalone sparse column from a prior deploy,
        // so converting another column into the column set cannot land in the same ALTER TABLE ADD as
        // that pre-existing sparse column. SQL Server rejects this by design (Microsoft docs: "A column
        // set cannot be added to a table if that table already contains sparse columns"). No pre-
        // validation catches this ahead of time -- the engine's own rejection is the deliberate design
        // choice (Task C1-6 Step 4: prefer the engine's own error over pre-validating the relationship).
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"ColSetIllegal_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"CREATE TABLE dbo.{tableName} (Id INT NOT NULL, ExistingSparse VARCHAR(20) SPARSE NULL, PlainXml XML NULL);";
        cmd.ExecuteNonQuery();

        var json = $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{tableName}}]",
    "Columns": [
        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
        {"Name": "[ExistingSparse]", "DataType": "VARCHAR(20)", "Nullable": true, "Sparse": true},
        {"Name": "[PlainXml]", "DataType": "XML", "Nullable": true, "IsColumnSet": true}
    ]
}
""";

        try
        {
            var ex = Assert.Throws<SqlException>(() => RunTableQuenchProc(cmd, json));
            var lowerMessage = ex!.Message.ToLowerInvariant();
            Assert.That(lowerMessage.Contains("sparse") || lowerMessage.Contains("column set"), Is.True,
                $"expected SQL Server's own column-set/sparse rejection, got: {ex.Message}");
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS [dbo].[{tableName}];";
            cmd.ExecuteNonQuery();
        }

        conn.Close();
    }
}
