// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.SqlServer;

[Category("SqlServer")]
[NonParallelizable]
public class TableQuench_CDCTests : BaseTableQuenchTests
{
    [Test]
    public void ShouldEnableCDCOnTable()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create a table without CDC
        cmd.CommandText = "CREATE TABLE dbo.EnableCDCTest (Id INT NOT NULL, Val NVARCHAR(100) NULL)";
        cmd.ExecuteNonQuery();

        // Quench with EnableCDC = true
        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[EnableCDCTest]",
                "EnableCDC": true,
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Val]", "DataType": "NVARCHAR(100)"}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        // Assert CDC is enabled
        cmd.CommandText = "SELECT is_tracked_by_cdc FROM sys.tables WHERE object_id = OBJECT_ID('dbo.EnableCDCTest')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true));

        // Cleanup: disable CDC before dropping
        cmd.CommandText = "EXEC sys.sp_cdc_disable_table @source_schema = N'dbo', @source_name = N'EnableCDCTest', @capture_instance = N'dbo_EnableCDCTest'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DROP TABLE dbo.EnableCDCTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void ShouldDisableCDCOnTable()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create a table and enable CDC on it
        cmd.CommandText = @"
CREATE TABLE dbo.DisableCDCTest (Id INT NOT NULL, Val NVARCHAR(100) NULL)
EXEC sys.sp_cdc_enable_table @source_schema = N'dbo', @source_name = N'DisableCDCTest', @role_name = NULL
";
        cmd.ExecuteNonQuery();

        // Verify CDC is enabled before quench
        cmd.CommandText = "SELECT is_tracked_by_cdc FROM sys.tables WHERE object_id = OBJECT_ID('dbo.DisableCDCTest')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true), "CDC should be enabled before quench");

        // Quench with EnableCDC = false (default)
        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[DisableCDCTest]",
                "EnableCDC": false,
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Val]", "DataType": "NVARCHAR(100)"}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        // Assert CDC is disabled
        cmd.CommandText = "SELECT is_tracked_by_cdc FROM sys.tables WHERE object_id = OBJECT_ID('dbo.DisableCDCTest')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(false));

        cmd.CommandText = "DROP TABLE dbo.DisableCDCTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void ShouldRefreshCDCAfterColumnSwap()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table with IDENTITY column and enable CDC
        cmd.CommandText = @"
CREATE TABLE dbo.CDCSwapTest (Id INT IDENTITY(1,1) NOT NULL, Val NVARCHAR(100) NULL)
INSERT dbo.CDCSwapTest (Val) VALUES ('before swap')
EXEC sys.sp_cdc_enable_table @source_schema = N'dbo', @source_name = N'CDCSwapTest', @role_name = NULL";
        cmd.ExecuteNonQuery();

        // Verify CDC is tracking and capture the original column count
        cmd.CommandText = "SELECT COUNT(*) FROM cdc.captured_columns cc JOIN cdc.change_tables ct ON cc.object_id = ct.object_id WHERE ct.source_object_id = OBJECT_ID('dbo.CDCSwapTest')";
        var columnsBefore = (int)cmd.ExecuteScalar();
        Assert.That(columnsBefore, Is.EqualTo(2), "Should capture both columns initially");

        // Quench: remove IDENTITY (triggers MustSwapColumn swap pattern)
        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[CDCSwapTest]",
                "EnableCDC": true,
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": true}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        // Assert CDC is still enabled after the swap
        cmd.CommandText = "SELECT is_tracked_by_cdc FROM sys.tables WHERE object_id = OBJECT_ID('dbo.CDCSwapTest')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true), "CDC should be re-enabled after column swap");

        // Assert the capture instance has correct column count (fresh instance, not stale)
        // Rotation leaves BOTH instances in place, so scope this to the one this deploy created --
        // an unscoped count sums the old and new column sets and means nothing.
        cmd.CommandText = @"SELECT COUNT(*) FROM cdc.captured_columns cc
                            WHERE cc.object_id = (SELECT TOP 1 ct.object_id FROM cdc.change_tables ct
                                                   WHERE ct.source_object_id = OBJECT_ID('dbo.CDCSwapTest')
                                                   ORDER BY ct.create_date DESC, ct.object_id DESC)";
        var columnsAfter = (int)cmd.ExecuteScalar();
        Assert.That(columnsAfter, Is.EqualTo(2), "Refreshed capture instance should track both columns");

        // Verify data was preserved through the swap
        cmd.CommandText = "SELECT Val FROM dbo.CDCSwapTest";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("before swap"));

        // Cleanup
        cmd.CommandText = "EXEC sys.sp_cdc_disable_table @source_schema = N'dbo', @source_name = N'CDCSwapTest', @capture_instance = N'dbo_CDCSwapTest'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DROP TABLE dbo.CDCSwapTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void ShouldRefreshCDCAfterAddColumn()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table and enable CDC
        cmd.CommandText = @"
CREATE TABLE dbo.CDCAddColTest (Id INT NOT NULL, Val NVARCHAR(100) NULL)
EXEC sys.sp_cdc_enable_table @source_schema = N'dbo', @source_name = N'CDCAddColTest', @role_name = NULL";
        cmd.ExecuteNonQuery();

        // Verify initial captured column count
        cmd.CommandText = "SELECT COUNT(*) FROM cdc.captured_columns cc JOIN cdc.change_tables ct ON cc.object_id = ct.object_id WHERE ct.source_object_id = OBJECT_ID('dbo.CDCAddColTest')";
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(2), "Should capture 2 columns initially");

        // Quench: add a new column
        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[CDCAddColTest]",
                "EnableCDC": true,
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": true},
                    {"Name": "[NewCol]", "DataType": "INT", "Nullable": true}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        // Assert CDC is still enabled
        cmd.CommandText = "SELECT is_tracked_by_cdc FROM sys.tables WHERE object_id = OBJECT_ID('dbo.CDCAddColTest')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true), "CDC should remain enabled");

        // Assert the capture instance now tracks 3 columns (including the new one)
        // Rotation leaves BOTH instances in place, so scope this to the one this deploy created --
        // an unscoped count sums the old and new column sets and means nothing.
        cmd.CommandText = @"SELECT COUNT(*) FROM cdc.captured_columns cc
                            WHERE cc.object_id = (SELECT TOP 1 ct.object_id FROM cdc.change_tables ct
                                                   WHERE ct.source_object_id = OBJECT_ID('dbo.CDCAddColTest')
                                                   ORDER BY ct.create_date DESC, ct.object_id DESC)";
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(3), "Refreshed capture instance should track all 3 columns");

        // Cleanup
        cmd.CommandText = "EXEC sys.sp_cdc_disable_table @source_schema = N'dbo', @source_name = N'CDCAddColTest', @capture_instance = N'dbo_CDCAddColTest'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DROP TABLE dbo.CDCAddColTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void ShouldRefreshCDCAfterDropColumn()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table with 3 columns and enable CDC
        cmd.CommandText = @"
CREATE TABLE dbo.CDCDropColTest (Id INT NOT NULL, Val NVARCHAR(100) NULL, Extra INT NULL)
EXEC sys.sp_cdc_enable_table @source_schema = N'dbo', @source_name = N'CDCDropColTest', @role_name = NULL";
        cmd.ExecuteNonQuery();

        // Verify initial captured column count
        cmd.CommandText = "SELECT COUNT(*) FROM cdc.captured_columns cc JOIN cdc.change_tables ct ON cc.object_id = ct.object_id WHERE ct.source_object_id = OBJECT_ID('dbo.CDCDropColTest')";
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(3), "Should capture 3 columns initially");

        // Quench: remove the Extra column
        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[CDCDropColTest]",
                "EnableCDC": true,
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": true}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        // Assert CDC is still enabled
        cmd.CommandText = "SELECT is_tracked_by_cdc FROM sys.tables WHERE object_id = OBJECT_ID('dbo.CDCDropColTest')";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(true), "CDC should remain enabled");

        // Assert the capture instance now tracks 2 columns (Extra is gone)
        // Rotation leaves BOTH instances in place, so scope this to the one this deploy created --
        // an unscoped count sums the old and new column sets and means nothing.
        cmd.CommandText = @"SELECT COUNT(*) FROM cdc.captured_columns cc
                            WHERE cc.object_id = (SELECT TOP 1 ct.object_id FROM cdc.change_tables ct
                                                   WHERE ct.source_object_id = OBJECT_ID('dbo.CDCDropColTest')
                                                   ORDER BY ct.create_date DESC, ct.object_id DESC)";
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(2), "Refreshed capture instance should track only 2 columns");

        // Cleanup
        cmd.CommandText = "EXEC sys.sp_cdc_disable_table @source_schema = N'dbo', @source_name = N'CDCDropColTest', @capture_instance = N'dbo_CDCDropColTest'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DROP TABLE dbo.CDCDropColTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    [Test]
    public void ShouldNotRefreshCDCWhenNoColumnChanges()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Create table, enable CDC, and note the capture instance create time
        cmd.CommandText = @"
CREATE TABLE dbo.CDCNoChangeTest (Id INT NOT NULL, Val NVARCHAR(100) NULL)
EXEC sys.sp_cdc_enable_table @source_schema = N'dbo', @source_name = N'CDCNoChangeTest', @role_name = NULL";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT create_date FROM cdc.change_tables WHERE source_object_id = OBJECT_ID('dbo.CDCNoChangeTest')";
        var createDateBefore = (System.DateTime)cmd.ExecuteScalar();

        // Small delay to ensure time difference would be detectable
        System.Threading.Thread.Sleep(1100);

        // Quench with identical columns (no changes) but add an index
        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[CDCNoChangeTest]",
                "EnableCDC": true,
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false},
                    {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": true}
                ],
                "Indexes": [
                    {"Name": "[IX_CDCNoChangeTest_Val]", "IndexColumns": "[Val]"}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        // Assert capture instance was NOT recreated (same create_date)
        cmd.CommandText = "SELECT create_date FROM cdc.change_tables WHERE source_object_id = OBJECT_ID('dbo.CDCNoChangeTest')";
        var createDateAfter = (System.DateTime)cmd.ExecuteScalar();
        Assert.That(createDateAfter, Is.EqualTo(createDateBefore), "Capture instance should not be recreated when no column changes");

        // Cleanup
        cmd.CommandText = "EXEC sys.sp_cdc_disable_table @source_schema = N'dbo', @source_name = N'CDCNoChangeTest', @capture_instance = N'dbo_CDCNoChangeTest'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DROP TABLE dbo.CDCNoChangeTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
    [Test]
    public void ShouldPreserveCapturedChangeHistoryAcrossColumnAdd()
    {
        // A column change on a CDC-tracked table disables CDC before the column work and
        // re-enables it afterwards with no @capture_instance, so the capture instance --
        // and the change table holding every not-yet-consumed row -- is dropped and a fresh
        // empty one created. Asserting on captured ROWS would need the SQL Agent capture
        // job, which the test container does not run, so this asserts on the identity of
        // the change table instead: a different object_id means whatever it held is gone.
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE dbo.CDCHistoryTest (Id INT IDENTITY(1,1) NOT NULL, Val NVARCHAR(100) NULL)
INSERT dbo.CDCHistoryTest (Val) VALUES ('tracked')
EXEC sys.sp_cdc_enable_table @source_schema = N'dbo', @source_name = N'CDCHistoryTest', @role_name = NULL";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"SELECT ct.object_id FROM cdc.change_tables ct
                            WHERE ct.source_object_id = OBJECT_ID('dbo.CDCHistoryTest')";
        var changeTableBefore = cmd.ExecuteScalar();
        Assert.That(changeTableBefore, Is.Not.Null, "CDC should be tracking before the deploy");

        var json = """
            {
                "Schema": "[dbo]",
                "Name": "[CDCHistoryTest]",
                "EnableCDC": true,
                "Columns": [
                    {"Name": "[Id]", "DataType": "INT", "Nullable": false, "Identity": true},
                    {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": true},
                    {"Name": "[Note]", "DataType": "NVARCHAR(50)", "Nullable": true}
                ]
            }
            """;
        RunTableQuenchProc(cmd, json);

        // Two instances now exist, so ask directly whether the ORIGINAL one survived rather than
        // reading "the" object_id -- an unqualified scalar read would take whichever row came back
        // first and could pass by accident.
        cmd.CommandText = @"SELECT COUNT(*) FROM cdc.change_tables ct
                            WHERE ct.source_object_id = OBJECT_ID('dbo.CDCHistoryTest')
                              AND ct.object_id = " + changeTableBefore;
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(1),
            "The original CDC capture instance was dropped, discarding every captured change a "
            + "downstream reader had not yet consumed.");

        cmd.CommandText = @"SELECT COUNT(*) FROM cdc.change_tables ct
                            WHERE ct.source_object_id = OBJECT_ID('dbo.CDCHistoryTest')";
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(2),
            "Rotation should add a second capture instance covering the new column set.");

        cmd.CommandText = "EXEC sys.sp_cdc_disable_table @source_schema = N'dbo', @source_name = N'CDCHistoryTest', @capture_instance = N'all'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DROP TABLE dbo.CDCHistoryTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }
    [Test]
    public void ShouldRefuseAColumnChangeWhenBothCaptureInstancesAreInUse()
    {
        // Rotation deliberately leaves the old instance in place -- only the operator knows when
        // downstream readers have drained it. SQL Server allows two per table, so the second
        // column change has nowhere to rotate to and must refuse up front rather than fail
        // partway through the column work or silently discard history.
        using var conn = DbConnectionFactory.ForPlatform(Platform.SqlServer).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE dbo.CDCCeilingTest (Id INT IDENTITY(1,1) NOT NULL, Val NVARCHAR(100) NULL)
EXEC sys.sp_cdc_enable_table @source_schema = N'dbo', @source_name = N'CDCCeilingTest', @role_name = NULL";
        cmd.ExecuteNonQuery();

        RunTableQuenchProc(cmd, CeilingPackage(withExtra: false));

        cmd.CommandText = "SELECT COUNT(*) FROM cdc.change_tables WHERE source_object_id = OBJECT_ID('dbo.CDCCeilingTest')";
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(2), "First change should have rotated to a second instance");

        var ex = Assert.Catch<System.Exception>(() => RunTableQuenchProc(cmd, CeilingPackage(withExtra: true)));
        Assert.That(ex!.Message, Does.Contain("CDC capture-instance limit reached"));
        Assert.That(ex.Message, Does.Contain("[dbo].[CDCCeilingTest]"), "the message must name the offending table");

        cmd.CommandText = "SELECT COUNT(*) FROM cdc.change_tables WHERE source_object_id = OBJECT_ID('dbo.CDCCeilingTest')";
        Assert.That((int)cmd.ExecuteScalar(), Is.EqualTo(2), "The refusal must not have disturbed either instance");

        cmd.CommandText = "EXEC sys.sp_cdc_disable_table @source_schema = N'dbo', @source_name = N'CDCCeilingTest', @capture_instance = N'all'";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DROP TABLE dbo.CDCCeilingTest";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private static string CeilingPackage(bool withExtra) => withExtra
        ? """
          {
              "Schema": "[dbo]",
              "Name": "[CDCCeilingTest]",
              "EnableCDC": true,
              "Columns": [
                  {"Name": "[Id]", "DataType": "INT", "Nullable": false, "Identity": true},
                  {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": true},
                  {"Name": "[Note]", "DataType": "NVARCHAR(50)", "Nullable": true},
                  {"Name": "[Extra]", "DataType": "INT", "Nullable": true}
              ]
          }
          """
        : """
          {
              "Schema": "[dbo]",
              "Name": "[CDCCeilingTest]",
              "EnableCDC": true,
              "Columns": [
                  {"Name": "[Id]", "DataType": "INT", "Nullable": false, "Identity": true},
                  {"Name": "[Val]", "DataType": "NVARCHAR(100)", "Nullable": true},
                  {"Name": "[Note]", "DataType": "NVARCHAR(50)", "Nullable": true}
              ]
          }
          """;
}

