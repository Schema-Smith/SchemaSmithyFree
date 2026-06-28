// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

using NUnit.Framework;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Coverage for column-level CheckExpression (a column property that emits a deterministically
/// named CK_&lt;table&gt;_&lt;column&gt; check) and for the Bug 2 gap where a MODIFIED table-level
/// check was never re-applied on MySQL. Parity with the SQL Server + PostgreSQL behavior (#313).
///
/// Idempotency is the load-bearing assertion: MySQL reformats CHECK_CLAUSE on storage (an authored
/// "`Id` &gt; 100" comes back as "(`Id` &gt; 100)"), so a naive desired-vs-stored text compare would
/// phantom-drop/recreate every run. The proc normalizes both sides before comparing; the no-op
/// re-quench tests below are the arbiter and assert NO drop/create check DDL is emitted on a
/// converged table (observed via the SchemaSmith_StatusMessages status log, MySQL's no-rebuild signal).
/// </summary>
[Category("MySQL")]
[TestFixture]
[Parallelizable(scope: ParallelScope.All)]
[Category("Integration")]
public class TableQuench_ColumnCheckExpressionTests : BaseTableQuenchTests
{
    [Test]
    public void TableQuench_ColumnCheck_IsCreatedWithDeterministicName()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkCreate_{id}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`{table}`;
CREATE TABLE `{_mainDb}`.`{table}` (`Id` INT NOT NULL, `Quantity` INT NULL, PRIMARY KEY (`Id`));";
            cmd.ExecuteNonQuery();

            RunTableQuenchProc(cmd, ColumnCheckJson(table, "`Quantity` > 0"));

            // The deterministic CK_<table>_<column> constraint exists.
            cmd.CommandText = $@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
 WHERE TABLE_SCHEMA = '{_mainDb}' AND TABLE_NAME = '{table}'
   AND CONSTRAINT_NAME = 'CK_{table}_Quantity' AND CONSTRAINT_TYPE = 'CHECK'";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1),
                "Column-level CheckExpression should create a CK_<table>_<column> check constraint.");
        }
        finally
        {
            Cleanup(cmd, table);
        }
        conn.Close();
    }

    [Test]
    public void TableQuench_ColumnCheck_IsEnforced()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkEnforce_{id}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`{table}`;
CREATE TABLE `{_mainDb}`.`{table}` (`Id` INT NOT NULL, `Quantity` INT NULL, PRIMARY KEY (`Id`));";
            cmd.ExecuteNonQuery();

            RunTableQuenchProc(cmd, ColumnCheckJson(table, "`Quantity` > 0"));

            cmd.CommandText = $"INSERT INTO `{_mainDb}`.`{table}` (`Id`, `Quantity`) VALUES (1, -5)";
            Assert.That(() => cmd.ExecuteNonQuery(), Throws.Exception,
                "A row violating the column check (Quantity > 0) must be rejected.");
        }
        finally
        {
            Cleanup(cmd, table);
        }
        conn.Close();
    }

    [Test]
    public void TableQuench_ColumnCheck_IsIdempotent()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkIdem_{id}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`{table}`;
CREATE TABLE `{_mainDb}`.`{table}` (`Id` INT NOT NULL, `Quantity` INT NULL, PRIMARY KEY (`Id`));";
            cmd.ExecuteNonQuery();

            var json = ColumnCheckJson(table, "`Quantity` > 0");
            RunTableQuenchProc(cmd, json);

            // Re-quench on a clean status log: a converged column check must emit NO drop and NO create.
            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();

            ReQuenchChecks(cmd, json);

            Assert.Multiple(() =>
            {
                Assert.That(CountMessages(cmd, "Drop modified column check constraint:", table), Is.EqualTo(0),
                    "Converged column check must NOT be phantom-dropped (normalization failed).");
                Assert.That(CountMessages(cmd, "Create column check constraint:", table), Is.EqualTo(0),
                    "Converged column check must NOT be re-created.");
            });
        }
        finally
        {
            Cleanup(cmd, table);
        }
        conn.Close();
    }

    [Test]
    public void TableQuench_ColumnCheck_ModifiedIsReApplied()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var table = $"ColChkMod_{id}";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`{table}`;
CREATE TABLE `{_mainDb}`.`{table}` (`Id` INT NOT NULL, `Quantity` INT NULL, PRIMARY KEY (`Id`));";
            cmd.ExecuteNonQuery();

            // Converge to the OLD expression (Quantity > 0).
            RunTableQuenchProc(cmd, ColumnCheckJson(table, "`Quantity` > 0"));

            // A row valid under the old expression but invalid under the new one.
            cmd.CommandText = $"INSERT INTO `{_mainDb}`.`{table}` (`Id`, `Quantity`) VALUES (1, 50)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"DELETE FROM `{_mainDb}`.`{table}`";
            cmd.ExecuteNonQuery();

            // Re-quench with the NEW expression (Quantity > 100).
            RunTableQuenchProc(cmd, ColumnCheckJson(table, "`Quantity` > 100"));

            // The new expression must now be enforced: 50 was valid before, must be rejected now.
            cmd.CommandText = $"INSERT INTO `{_mainDb}`.`{table}` (`Id`, `Quantity`) VALUES (2, 50)";
            Assert.That(() => cmd.ExecuteNonQuery(), Throws.Exception,
                "After modifying the column check to Quantity > 100, a Quantity of 50 must be rejected.");
        }
        finally
        {
            Cleanup(cmd, table);
        }
        conn.Close();
    }

    [Test]
    public void TableQuench_TableLevelCheck_ModifiedIsReApplied()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var table = $"TblChkMod_{id}";
        var ck = $"CK_{table}_Id";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            // Existing table-level check enforces `Id` > 0.
            cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`{table}`;
CREATE TABLE `{_mainDb}`.`{table}` (`Id` INT NOT NULL, PRIMARY KEY (`Id`));
ALTER TABLE `{_mainDb}`.`{table}` ADD CONSTRAINT `{ck}` CHECK (`Id` > 0);
INSERT INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{_mainDb}', 'TABLE', '{table}');
INSERT INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{_mainDb}', 'CHECK CONSTRAINT', '{table}.{ck}');";
            cmd.ExecuteNonQuery();

            // Desired table-level check is semantically different: `Id` > 100.
            RunTableQuenchProc(cmd, TableCheckJson(table, ck, "`Id` > 100"));

            // The NEW clause must be enforced (semantically distinct from the old `Id` > 0):
            // Id = 50 satisfied the old check but must be rejected by the new one.
            cmd.CommandText = $"INSERT INTO `{_mainDb}`.`{table}` (`Id`) VALUES (50)";
            Assert.That(() => cmd.ExecuteNonQuery(), Throws.Exception,
                "After modifying the table-level check to Id > 100, an Id of 50 must be rejected.");
        }
        finally
        {
            Cleanup(cmd, table);
        }
        conn.Close();
    }

    [Test]
    public void TableQuench_TableLevelCheck_IsIdempotent()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var table = $"TblChkIdem_{id}";
        var ck = $"CK_{table}_Id";

        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 300;

        try
        {
            cmd.CommandText = $@"
DROP TABLE IF EXISTS `{_mainDb}`.`{table}`;
CREATE TABLE `{_mainDb}`.`{table}` (`Id` INT NOT NULL, PRIMARY KEY (`Id`));
ALTER TABLE `{_mainDb}`.`{table}` ADD CONSTRAINT `{ck}` CHECK (`Id` > 0);
INSERT INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{_mainDb}', 'TABLE', '{table}');
INSERT INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{_mainDb}', 'CHECK CONSTRAINT', '{table}.{ck}');";
            cmd.ExecuteNonQuery();

            // Author the SAME expression that is already live (`Id` > 0).
            var json = TableCheckJson(table, ck, "`Id` > 0");
            RunTableQuenchProc(cmd, json);

            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();

            ReQuenchChecks(cmd, json);

            Assert.Multiple(() =>
            {
                Assert.That(CountMessages(cmd, "Drop modified check constraint:", table), Is.EqualTo(0),
                    "Converged table-level check must NOT be phantom-dropped (normalization failed).");
                Assert.That(CountMessages(cmd, "Create check constraint:", table), Is.EqualTo(0),
                    "Converged table-level check must NOT be re-created.");
            });
        }
        finally
        {
            Cleanup(cmd, table);
        }
        conn.Close();
    }

    private static string ColumnCheckJson(string table, string checkExpression) => $$"""
[
{
    "Name": "{{table}}",
    "Columns": [
        { "Name": "Id",       "DataType": "INT", "Nullable": false },
        { "Name": "Quantity", "DataType": "INT", "Nullable": true, "CheckExpression": "{{checkExpression}}" }
    ],
    "Indexes": [
        { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" }
    ]
}
]
""";

    private static string TableCheckJson(string table, string constraintName, string expression) => $$"""
[
{
    "Name": "{{table}}",
    "Columns": [
        { "Name": "Id", "DataType": "INT", "Nullable": false }
    ],
    "Indexes": [
        { "Name": "PRIMARY", "PrimaryKey": true, "Unique": true, "IndexColumns": "Id" }
    ],
    "CheckConstraints": [
        { "Name": "{{constraintName}}", "Expression": "{{expression}}" }
    ]
}
]
""";

    // Re-runs only the two procs that handle check constraints (parse + modify + create) so the
    // status log isolates check DDL. Mirrors the converge-once-then-drive-directly idempotency pattern.
    private void ReQuenchChecks(System.Data.IDbCommand cmd, string json)
    {
        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{_mainDb}', '{json.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();
        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{_mainDb}', 0, 0)";
        cmd.ExecuteNonQuery();
        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{_mainDb}', 0, 0)";
        cmd.ExecuteNonQuery();
    }

    private static int CountMessages(System.Data.IDbCommand cmd, string messagePrefix, string table)
    {
        cmd.CommandText = $@"SELECT COUNT(*) FROM SchemaSmith_StatusMessages
 WHERE SessionId = CONNECTION_ID()
   AND Message LIKE '%{messagePrefix}%'
   AND Message LIKE '%{table}%'";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void Cleanup(System.Data.IDbCommand cmd, string table)
    {
        try
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`{table}`";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"DELETE FROM SchemaSmith_ProductOwnership WHERE ObjectSchema = '{_mainDb}' AND ObjectName LIKE '{table}%'";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM SchemaSmith_StatusMessages WHERE SessionId = CONNECTION_ID()";
            cmd.ExecuteNonQuery();
        }
        catch { /* best-effort cleanup */ }
    }
}
