// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

/// <summary>
/// Always Encrypted (AE) certification — what TableQuench actually does for encrypted columns.
///
/// CERTIFIED FINDINGS (2026-06-27, live SQL Server container, standard NON-enclave deployment).
/// Every claim below was reproduced against the container; the swap path lives in
/// Schema/Scripts/SqlServer/SchemaSmith.ModifiedTableQuench.sql (the @v_SwapColumnScript
/// LIKE '%ENCRYPTED WITH%' block, ~lines 702-711).
///
/// HARNESS CONTROL (passes): a static DETERMINISTIC column, populated through an
/// AE-enabled (Column Encryption Setting=Enabled), PARAMETERIZED insert and read back through an
/// AE-enabled connection, decrypts to the original. The harness is sound, so the failures below
/// are the product, not the test.
///
/// WHAT WORKS:
///   - New encrypted column on an EMPTY table via quench: column is created with the declared
///     CEK / ENCRYPTION_TYPE / algorithm (verified in sys.columns). Certified by
///     ShouldCreateNewEncryptedColumnOnEmptyTable below.
///   - DDL containing ENCRYPTED WITH executes fine over an AE-enabled connection on this engine
///     version — it does NOT parse-error. (Refutes the prior POC note; certified by
///     ShouldExecuteEncryptedWithDdlOverAeEnabledConnection.)
///
/// WHAT DOES NOT WORK — encryption CHANGES on a POPULATED table (re-type, re-key, or initial
/// encryption of plaintext). The quench ERRORS; it does NOT silently corrupt. Observed on the
/// container (these are documented here, NOT asserted as passing tests — a fail-closed guard and
/// its green tests are the follow-up task):
///
///   1. DETERMINISTIC -> RANDOMIZED on a populated encrypted column:
///        TableQuench fails with "Incorrect syntax near 'ENCRYPTED'." The column is left UNTOUCHED
///        (still DETERMINISTIC, original value still decrypts). Root cause: the swap path emits
///        ALTER TABLE ... ADD [col_swap_temp] NVARCHAR(50) NOT NULL ENCRYPTED WITH (...) — the
///        nullability clause precedes ENCRYPTED WITH, which is invalid T-SQL ordering.
///   2. Initial encryption of a populated plaintext column: identical "Incorrect syntax near
///        'ENCRYPTED'." failure; column left plaintext, value intact.
///
/// WHY IT CANNOT WORK EVEN IF THE DDL ORDERING WERE FIXED (the deeper, fundamental limit):
///   - A NOT NULL encrypted column cannot be ADDed to a non-empty table (general SQL rule; no
///     DEFAULT is allowed on encrypted columns to escape it).
///   - A NULLABLE encrypted column CAN be added to a non-empty table on this engine version
///     (so the prior POC claim "encrypted columns cannot be nullable" is outdated). But the
///     swap's data copy is a SERVER-SIDE "UPDATE [temp] = [original]", and when the two columns
///     have different encryption settings the server rejects it:
///        "Operand type clash: nvarchar(50) encrypted with (encryption_type = 'DETERMINISTIC'...)
///         is incompatible with nvarchar(50) encrypted with (encryption_type = 'RANDOMIZED'...)."
///     The server holds no Column Master Key and cannot decrypt/re-encrypt; re-encryption requires
///     secure enclaves, which a standard deployment does not have. The server-side copy is only a
///     valid byte move when CEK AND encryption type are unchanged.
///
/// NET: on a standard (non-enclave) server, an AE encryption CHANGE on populated data is
/// impossible to perform inside the quench. The current behavior is a loud (if misleading) syntax
/// error rather than silent corruption — which is the motivation for an explicit fail-closed guard.
///
/// WORKAROUND for users — full table rebuild via Before/After migration scripts:
///   Before: create the new table with the target (encrypted) schema; INSERT INTO new SELECT * FROM
///     old over a "Column Encryption Setting=Enabled" connection (the ADO.NET driver does the
///     decrypt/re-encrypt client-side); drop FKs/indexes/constraints on old; drop old; sp_rename new.
///   After:  recreate FKs/indexes/constraints/extended properties.
///   This is the same full-rebuild approach SSMS uses for Always Encrypted changes; run it in a
///   maintenance window (referential integrity is briefly broken).
/// </summary>
[TestFixture]
[Category("SqlServer")]
[NonParallelizable]
public class TableQuench_AlwaysEncryptedTests : BaseTableQuenchTests
{
    // AE-enabled connection targeting the integration main DB: the ADO.NET driver encrypts
    // parameters on insert and decrypts on read. Required for any data round-trip verification.
    private string AeConnectionString() =>
        new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = _mainDb,
            ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
        }.ToString();

    private const string Cek = "[TestCEK]";
    private const string Algo = "AEAD_AES_256_CBC_HMAC_SHA_256";

    /// <summary>
    /// HARNESS CONTROL — proves the AE round-trip works before any swap scenario is judged.
    /// Static DETERMINISTIC column, parameterized encrypted insert, AE-enabled read-back.
    /// </summary>
    [Test]
    public void HarnessControl_StaticEncryptedColumn_RoundTripsThroughAeConnection()
    {
        using var conn = new SqlConnection(AeConnectionString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS dbo.AeControlRoundTrip";
        cmd.ExecuteNonQuery();
        try
        {
            cmd.CommandText = $@"
                CREATE TABLE dbo.AeControlRoundTrip (
                  Id INT NOT NULL,
                  Secret NVARCHAR(50) COLLATE Latin1_General_BIN2
                    ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = {Cek}, ENCRYPTION_TYPE = DETERMINISTIC,
                                    ALGORITHM = '{Algo}') NOT NULL)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT dbo.AeControlRoundTrip (Id, Secret) VALUES (@id, @secret)";
            cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(new SqlParameter("@secret", SqlDbType.NVarChar, 50) { Value = "TopSecret" });
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();

            cmd.CommandText = "SELECT Secret FROM dbo.AeControlRoundTrip WHERE Id = 1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("TopSecret"),
                "AE harness control: encrypted value must round-trip through the driver");
        }
        finally
        {
            cmd.CommandText = "DROP TABLE IF EXISTS dbo.AeControlRoundTrip";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Certifies the prior POC claim is false on this engine: ENCRYPTED WITH DDL does NOT
    /// parse-error when run over an AE-enabled connection.
    /// </summary>
    [Test]
    public void ShouldExecuteEncryptedWithDdlOverAeEnabledConnection()
    {
        using var conn = new SqlConnection(AeConnectionString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS dbo.AeDdlOverAe";
        cmd.ExecuteNonQuery();
        try
        {
            cmd.CommandText = $@"
                CREATE TABLE dbo.AeDdlOverAe (
                  Id INT NOT NULL,
                  Secret NVARCHAR(50) COLLATE Latin1_General_BIN2
                    ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = {Cek}, ENCRYPTION_TYPE = RANDOMIZED,
                                    ALGORITHM = '{Algo}') NULL)";
            Assert.DoesNotThrow(() => cmd.ExecuteNonQuery(),
                "ENCRYPTED WITH DDL must not parse-error over an AE-enabled connection");
        }
        finally
        {
            cmd.CommandText = "DROP TABLE IF EXISTS dbo.AeDdlOverAe";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// SCENARIO A (works): quench that adds a new ENCRYPTED column to an EMPTY table creates it
    /// with the declared CEK / type / algorithm, and the column round-trips encrypted data.
    /// </summary>
    [Test]
    public void ShouldCreateNewEncryptedColumnOnEmptyTable()
    {
        using var conn = new SqlConnection(AeConnectionString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS dbo.AeNewColEmpty";
        cmd.ExecuteNonQuery();
        try
        {
            cmd.CommandText = "CREATE TABLE dbo.AeNewColEmpty (Id INT NOT NULL)";
            cmd.ExecuteNonQuery();

            var json = """
                {
                    "Schema": "[dbo]",
                    "Name": "[AeNewColEmpty]",
                    "Columns": [
                        {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                        {"Name": "[Secret]", "DataType": "NVARCHAR(50)", "Nullable": false, "Collation": "Latin1_General_BIN2",
                         "EncryptionType": "DETERMINISTIC", "EncryptionKey": "[TestCEK]", "EncryptionAlgorithm": "AEAD_AES_256_CBC_HMAC_SHA_256"}
                    ]
                }
                """;
            RunTableQuenchProc(cmd, json);

            cmd.CommandText = @"SELECT encryption_type_desc, encryption_algorithm_name
                                FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AeNewColEmpty') AND name = 'Secret'";
            using (var r = cmd.ExecuteReader())
            {
                Assert.That(r.Read(), Is.True, "encrypted column should exist");
                Assert.That(r.GetString(0), Is.EqualTo("DETERMINISTIC"));
                Assert.That(r.GetString(1), Is.EqualTo(Algo));
            }

            // And it must actually round-trip encrypted data.
            cmd.CommandText = "INSERT dbo.AeNewColEmpty (Id, Secret) VALUES (@id, @secret)";
            cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(new SqlParameter("@secret", SqlDbType.NVarChar, 50) { Value = "NewColSecret" });
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();

            cmd.CommandText = "SELECT Secret FROM dbo.AeNewColEmpty WHERE Id = 1";
            Assert.That(cmd.ExecuteScalar(), Is.EqualTo("NewColSecret"));
        }
        finally
        {
            cmd.CommandText = "DROP TABLE IF EXISTS dbo.AeNewColEmpty";
            cmd.ExecuteNonQuery();
        }
    }

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

    [Test]
    public void GenerateTableJson_AeColumn_MapsKeyAndAlgorithmCorrectly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE dbo.AeRoundTrip (
              Id INT NOT NULL,
              Secret NVARCHAR(50) COLLATE Latin1_General_BIN2
                ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [TestCEK], ENCRYPTION_TYPE = DETERMINISTIC,
                                ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256') NOT NULL)";
        cmd.ExecuteNonQuery();

        try
        {
            cmd.CommandText = "EXEC SchemaSmith.GenerateTableJSON @p_Schema = 'dbo', @p_Table = 'AeRoundTrip'";
            var json = ReadAllLines(cmd);

            Assert.That(json, Does.Contain("\"EncryptionType\": \"DETERMINISTIC\""));
            Assert.That(json, Does.Contain("\"EncryptionKey\": \"[TestCEK]\""));
            Assert.That(json, Does.Contain("\"EncryptionAlgorithm\": \"AEAD_AES_256_CBC_HMAC_SHA_256\""));
        }
        finally
        {
            cmd.CommandText = "DROP TABLE dbo.AeRoundTrip";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
    }

    private static string ReadAllLines(IDbCommand cmd)
    {
        var sb = new StringBuilder();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            sb.Append(reader.GetString(0));
        return sb.ToString();
    }
}
