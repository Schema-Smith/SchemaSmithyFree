// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Always Encrypted infrastructure validation and known limitation documentation.
///
/// POC FINDINGS (2026-03-23):
///
/// The AE test infrastructure works:
/// - Custom SqlColumnEncryptionKeyStoreProvider (InMemoryKeyStoreProvider) works in Docker Linux
/// - CMK/CEK creation via DDL with custom provider works
/// - Column Encryption Setting=Enabled on connections does not break non-AE tests
/// - Provider registration is process-wide and stable across parallel test execution
///
/// KNOWN LIMITATION — Column-level modifications cannot handle Always Encrypted:
///
/// The MustSwapColumn pattern (add temp column → copy data → drop original → rename) and
/// ALTER COLUMN both fail for Always Encrypted changes. SQL Server imposes these restrictions
/// on encrypted columns:
///   - Cannot be nullable (blocks the "add as NULL, copy, constrain" swap pattern)
///   - Cannot have DEFAULT constraints (blocks the "add with DEFAULT" workaround)
///   - Cannot be added to non-empty tables without one of the above
///   - ALTER COLUMN with ENCRYPTED WITH requires secure enclaves (not standard deployments)
///   - DDL containing ENCRYPTED WITH is intercepted by the ADO.NET driver when
///     Column Encryption Setting=Enabled, causing parse errors for direct execution
///
/// Additional bugs found during POC:
///   - #ColumnChanges.ColumnScript was missing the ENCRYPTED WITH clause entirely
///   - ColumnScript had wrong ordering: ENCRYPTED WITH must come before NULL/NOT NULL
///     (fixed in ParseTableJsonIntoTempTables.sql — correct regardless of swap support)
///   - ALTER COLUMN section processed MustSwapColumn rows redundantly (fixed below)
///   - #ColumnChanges WHERE clause has parenthesization issue: encryption checks at line 181
///     are OR'd outside the NewTable=0 AND group, causing spurious detection on new tables
///
/// WORKAROUND — Full table rebuild via Before/After migration scripts:
///
///   Before Script:
///     1. Create new table with target schema (encrypted columns as needed)
///     2. INSERT INTO new_table SELECT * FROM old_table
///        (Connection must have "Column Encryption Setting=Enabled" — the ADO.NET driver
///         handles encrypt/decrypt transparently during the INSERT...SELECT)
///     3. Drop foreign keys, indexes, constraints referencing the old table
///     4. Drop old table
///     5. sp_rename new table to original name
///
///   After Script:
///     6. Recreate foreign keys, indexes, constraints, extended properties
///
///   Note: This temporarily breaks referential integrity. Run during a maintenance window.
///   The full table rebuild is the same approach SSMS uses for Always Encrypted changes.
/// </summary>
[TestFixture]
[Category("SqlServer")]
[NonParallelizable]
public class TableQuench_AlwaysEncryptedTests : BaseTableQuenchTests
{
    [Test]
    public void ShouldNotAlterSwappedColumnsRedundantly()
    {
        // The "Alter Modified Columns" section must skip rows where MustSwapColumn = 1.
        // Without this fix, columns handled by the swap cursor would ALSO get an ALTER COLUMN
        // which is a no-op for identity removal but fails for encrypted columns.
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "CREATE TABLE dbo.AESwapSkipTest (Id INT IDENTITY(1,1) NOT NULL, Val NVARCHAR(50) NULL)";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "INSERT dbo.AESwapSkipTest (Val) VALUES ('test')";
        cmd.ExecuteNonQuery();

        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[AESwapSkipTest]",
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Val]", "DataType": "NVARCHAR(50)"}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        Assert.That(GetColumnDataType(cmd, "AESwapSkipTest", "Id"), Is.EqualTo("INT"));
        cmd.CommandText = "SELECT Val FROM dbo.AESwapSkipTest";
        Assert.That(cmd.ExecuteScalar()?.ToString(), Is.EqualTo("test"));

        cmd.CommandText = "DROP TABLE dbo.AESwapSkipTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}
