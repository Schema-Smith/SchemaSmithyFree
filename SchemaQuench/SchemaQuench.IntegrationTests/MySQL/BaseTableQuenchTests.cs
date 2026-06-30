// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

#nullable enable
using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using System.Threading;

using Schema.IntegrationTests.MySQL;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Base class for specialized table quench tests providing common setup and helper methods.
/// Follows the pattern established in SQL Server and PostgreSQL reference implementations.
/// </summary>
[Category("MySQL")]
public class BaseTableQuenchTests
{
    protected readonly string _connectionString;
    protected readonly string _mainDb;
    protected readonly string _productName = "Quench Table Tests";

    public BaseTableQuenchTests()
    {
        FixtureSetup.EnsureInitialized();
        _connectionString = FixtureSetup.GetMainDbConnectionString();
        _mainDb = FixtureSetup.MainDb;
    }

    /// <summary>
    /// Runs the TableQuench procedure with the given JSON table definitions.
    /// Handles deadlock retry logic for parallel test execution.
    /// </summary>
    /// <param name="cmd">The database command to use</param>
    /// <param name="json">JSON array of table definitions</param>
    /// <param name="indexOnly">If true, runs IndexOnlyQuench instead of full TableQuench</param>
    protected void RunTableQuenchProc(IDbCommand cmd, string json, bool indexOnly = false, bool dropTablesRemovedFromProduct = false, bool whatIf = false, string productName = "")
    {
        var prod = string.IsNullOrEmpty(productName) ? _productName : productName;
        cmd.CommandTimeout = 300;
        var escapedJson = json.Replace("'", "''");

        cmd.CommandText = indexOnly
            ? $"CALL SchemaSmith_IndexOnlyQuench('{prod}', '{_mainDb}', 0, 1); CALL SchemaSmith_FixupIndexOwnership('{prod}');"
            : $"CALL SchemaSmith_TableQuench('{prod}', '{_mainDb}', '{escapedJson}', {(whatIf ? 1 : 0)}, 0, {(dropTablesRemovedFromProduct ? 1 : 0)});";

        // For index only mode, we need to first parse the table JSON
        if (indexOnly)
        {
            cmd.CommandText = $"CALL SchemaSmith_ParseTableJson('{_mainDb}', '{escapedJson}'); " +
                             $"CALL SchemaSmith_IndexOnlyQuench('{_productName}', '{_mainDb}', 0, 1);";
        }

        var retry = true;
        var tries = 0;
        while (retry && tries++ < 10)
        {
            try
            {
                cmd.ExecuteNonQuery();
                retry = false;
            }
            catch (Exception e)
            {
                // MySQL deadlock error message
                if (!e.Message.ContainsIgnoringCase("Deadlock found when trying to get lock") &&
                    !e.Message.ContainsIgnoringCase("Lock wait timeout exceeded"))
                    throw;
                Thread.Sleep(1000);
            }
        }
    }

    /// <summary>
    /// Runs the TableQuench procedure using the simplified three-call pattern.
    /// </summary>
    protected void RunTableQuenchSteps(IDbCommand cmd, string json)
    {
        cmd.CommandTimeout = 300;
        var escapedJson = json.Replace("'", "''");

        // Step 1: Parse JSON into temp tables
        cmd.CommandText = $"CALL SchemaSmith_ParseTableJson('{_mainDb}', '{escapedJson}')";
        ExecuteWithDeadlockRetry(cmd);

        // Step 2: Create missing tables and columns
        cmd.CommandText = $"CALL SchemaSmith_MissingTableAndColumnQuench('{_mainDb}', 0)";
        ExecuteWithDeadlockRetry(cmd);

        // Step 3: Modify existing tables
        cmd.CommandText = $"CALL SchemaSmith_ModifiedTableQuench('{_productName}', '{_mainDb}', 0, 0, 1, 1)";
        ExecuteWithDeadlockRetry(cmd);

        // Step 4: Create missing indexes and constraints
        cmd.CommandText = $"CALL SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{_mainDb}', 0, 1)";
        ExecuteWithDeadlockRetry(cmd);
    }

    private void ExecuteWithDeadlockRetry(IDbCommand cmd)
    {
        var retry = true;
        var tries = 0;
        while (retry && tries++ < 10)
        {
            try
            {
                cmd.ExecuteNonQuery();
                retry = false;
            }
            catch (Exception e)
            {
                if (!e.Message.ContainsIgnoringCase("Deadlock found when trying to get lock") &&
                    !e.Message.ContainsIgnoringCase("Lock wait timeout exceeded"))
                    throw;
                Thread.Sleep(1000);
            }
        }
    }

    /// <summary>
    /// Gets the data type string for a column in a table.
    /// </summary>
    protected static string GetColumnDataType(IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"
SELECT UPPER(COLUMN_TYPE)
  FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND COLUMN_NAME = '{columnName}'";
        return cmd.ExecuteScalar()?.ToString()?.ToUpper() ?? "UNKNOWN";
    }

    /// <summary>
    /// Gets the data type string for a column including schema (database) context.
    /// </summary>
    protected static string GetColumnDataType(IDbCommand cmd, string schemaName, string tableName, string columnName)
    {
        cmd.CommandText = $@"
SELECT UPPER(COLUMN_TYPE)
  FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_SCHEMA = '{schemaName}'
   AND TABLE_NAME = '{tableName}'
   AND COLUMN_NAME = '{columnName}'";
        return cmd.ExecuteScalar()?.ToString()?.ToUpper() ?? "UNKNOWN";
    }

    /// <summary>
    /// Checks if a column exists in a table.
    /// </summary>
    protected static bool ColumnExists(IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND COLUMN_NAME = '{columnName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Checks if an index exists on a table.
    /// </summary>
    protected static bool IndexExists(IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND INDEX_NAME = '{indexName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Checks if a foreign key constraint exists.
    /// </summary>
    protected static bool ForeignKeyExists(IDbCommand cmd, string tableName, string constraintName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND CONSTRAINT_NAME = '{constraintName}'
   AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Checks if a check constraint exists.
    /// </summary>
    protected static bool CheckConstraintExists(IDbCommand cmd, string tableName, string constraintName)
    {
        cmd.CommandText = $@"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND CONSTRAINT_NAME = '{constraintName}'
   AND CONSTRAINT_TYPE = 'CHECK'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Gets the column default value.
    /// </summary>
    protected static string? GetColumnDefault(IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"
SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND COLUMN_NAME = '{columnName}'";
        return cmd.ExecuteScalar()?.ToString();
    }

    /// <summary>
    /// Checks if a column is nullable.
    /// </summary>
    protected static bool IsColumnNullable(IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"
SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND COLUMN_NAME = '{columnName}'";
        return cmd.ExecuteScalar()?.ToString() == "YES";
    }

    /// <summary>
    /// Checks if a column is a generated column.
    /// </summary>
    protected static bool IsGeneratedColumn(IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"
SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND COLUMN_NAME = '{columnName}'";
        var extra = cmd.ExecuteScalar()?.ToString() ?? "";
        return extra.Contains("GENERATED");
    }

    /// <summary>
    /// Gets the generation expression for a generated column.
    /// </summary>
    protected static string? GetGenerationExpression(IDbCommand cmd, string tableName, string columnName)
    {
        cmd.CommandText = $@"
SELECT GENERATION_EXPRESSION FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND COLUMN_NAME = '{columnName}'";
        return cmd.ExecuteScalar()?.ToString();
    }

    /// <summary>
    /// Gets the index columns for an index as a comma-separated string.
    /// </summary>
    protected static string GetIndexColumns(IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $@"
SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',')
  FROM INFORMATION_SCHEMA.STATISTICS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND INDEX_NAME = '{indexName}'";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    /// <summary>
    /// Checks if an index is unique.
    /// </summary>
    protected static bool IsIndexUnique(IDbCommand cmd, string tableName, string indexName)
    {
        cmd.CommandText = $@"
SELECT NON_UNIQUE FROM INFORMATION_SCHEMA.STATISTICS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = '{tableName}'
   AND INDEX_NAME = '{indexName}'
   AND SEQ_IN_INDEX = 1";
        var nonUnique = cmd.ExecuteScalar();
        return nonUnique != null && Convert.ToInt32(nonUnique) == 0;
    }
}
