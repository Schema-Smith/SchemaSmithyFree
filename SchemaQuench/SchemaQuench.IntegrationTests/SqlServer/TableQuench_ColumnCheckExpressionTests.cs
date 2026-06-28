// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Text;
using Microsoft.Data.SqlClient;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

// Parity coverage for column-level CheckExpression on SQL Server (#313). A column declaring a
// CheckExpression must materialize as a real, enforced unnamed check constraint (SQL Server does
// not name column-level inline checks), must be idempotent across re-quench (no phantom
// drop/recreate), and must re-apply when the authored expression changes.
//
// SQL Server column-level checks are unnamed by the engine: `ALTER TABLE t ADD CHECK (expr)`
// produces a system-generated name. Presence and column association are confirmed via
// sys.check_constraints joined on parent_column_id / COL_NAME(), not by constraint name.
//
// Idempotency: the authored CheckExpression must be in SQL Server's canonical stored form so
// the modify-detection compare (fn_StripParenWrapping(ck.definition) vs c.CheckExpression)
// is equal on a no-op re-quench. SQL Server stores `[Quantity]>(0)` for `Quantity > 0`
// after paren-stripping, so the canonical authored form is `[Quantity]>(0)`. This mirrors the
// PostgreSQL tests which use pg_get_constraintdef canonical form for the same reason.
[Category("SqlServer")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_ColumnCheckExpressionTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_ColumnCheckConstraintIsCreated()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkCreate_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $"CREATE TABLE dbo.[{table}] ([Quantity] INT NULL)";
            cmd.ExecuteNonQuery();

            // Canonical SQL Server form: fn_StripParenWrapping(([Quantity]>(0))) = [Quantity]>(0)
            RunTableQuenchProc(cmd, Json(table, "[Quantity]>(0)"));

            // SQL Server column checks are unnamed — query by parent_column_id.
            cmd.CommandText = $@"
SELECT COUNT(*) FROM sys.check_constraints ck WITH (NOLOCK)
 WHERE ck.parent_object_id = OBJECT_ID('dbo.[{table}]')
   AND COL_NAME(ck.parent_object_id, ck.parent_column_id) = 'Quantity'";
            var count = Convert.ToInt32(cmd.ExecuteScalar());

            Assert.That(count, Is.EqualTo(1),
                "A column-level CheckExpression must create a check constraint on the Quantity column.");
        }
        finally
        {
            TryDrop(cmd, table);
            conn.Close();
        }
    }

    [Test]
    public void TableQuench_ColumnCheckConstraintIsEnforced()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkEnforce_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $"CREATE TABLE dbo.[{table}] ([Quantity] INT NULL)";
            cmd.ExecuteNonQuery();

            RunTableQuenchProc(cmd, Json(table, "[Quantity]>(0)"));

            cmd.CommandText = $"INSERT INTO dbo.[{table}] ([Quantity]) VALUES (-5)";
            Assert.That(() => cmd.ExecuteNonQuery(), Throws.Exception,
                "A row violating the column CheckExpression (Quantity > 0) must be rejected.");
        }
        finally
        {
            TryDrop(cmd, table);
            conn.Close();
        }
    }

    [Test]
    public void TableQuench_ColumnCheckConstraintIsIdempotent()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkIdem_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $"CREATE TABLE dbo.[{table}] ([Quantity] INT NULL)";
            cmd.ExecuteNonQuery();

            var json = Json(table, "[Quantity]>(0)");
            RunTableQuenchProc(cmd, json);

            // Re-quench in WhatIf mode; a converged column check must not produce
            // any "Adding check constrain to column" or "Dropping check constraint" DDL.
            var captured = new StringBuilder();
            var sqlConn = (SqlConnection)conn;
            SqlInfoMessageEventHandler handler = (_, e) => captured.Append(e.Message);
            sqlConn.InfoMessage += handler;
            try
            {
                RunTableQuenchProc(cmd, json, whatIf: true);
            }
            finally
            {
                sqlConn.InfoMessage -= handler;
            }

            var messages = captured.ToString();
            Assert.That(messages, Does.Not.Contain("Adding check constrain to column"),
                "Re-quench of an unchanged column CheckExpression must NOT add the constraint again. " +
                $"Messages: {messages}");
            Assert.That(messages, Does.Not.Contain("Dropping check constraint"),
                "Re-quench of an unchanged column CheckExpression must NOT drop/recreate the constraint. " +
                $"Messages: {messages}");
        }
        finally
        {
            TryDrop(cmd, table);
            conn.Close();
        }
    }

    [Test]
    public void TableQuench_ColumnCheckConstraintIsReappliedWhenExpressionChanges()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkMod_{uniqueId}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $"CREATE TABLE dbo.[{table}] ([Quantity] INT NULL)";
            cmd.ExecuteNonQuery();

            // Converge to the OLD expression (Quantity > 0) in canonical SQL Server form.
            RunTableQuenchProc(cmd, Json(table, "[Quantity]>(0)"));

            // A value valid under the old bound (50 > 0) but invalid under the new one (50 > 100).
            cmd.CommandText = $"INSERT INTO dbo.[{table}] ([Quantity]) VALUES (50)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"DELETE FROM dbo.[{table}]";
            cmd.ExecuteNonQuery();

            // Re-quench with a semantically distinct expression in canonical form.
            RunTableQuenchProc(cmd, Json(table, "[Quantity]>(100)"));

            cmd.CommandText = $"INSERT INTO dbo.[{table}] ([Quantity]) VALUES (50)";
            Assert.That(() => cmd.ExecuteNonQuery(), Throws.Exception,
                "After changing the column CheckExpression to Quantity > 100, a row with Quantity = 50 must be rejected.");
        }
        finally
        {
            TryDrop(cmd, table);
            conn.Close();
        }
    }

    private static string Json(string table, string checkExpression) => $$"""
{
    "Schema": "[dbo]",
    "Name": "[{{table}}]",
    "Columns": [
        { "Name": "[Quantity]", "DataType": "INT", "Nullable": true, "CheckExpression": "{{checkExpression}}" }
    ]
}
""";

    private static void TryDrop(System.Data.IDbCommand cmd, string table)
    {
        try
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS dbo.[{table}]";
            cmd.ExecuteNonQuery();
        }
        catch { /* best-effort cleanup */ }
    }
}
