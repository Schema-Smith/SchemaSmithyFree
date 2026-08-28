// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.IntegrationTests.SqlServer;

/// <summary>
/// Exercises SchemaSmith.BootstrapTableQuench's declarative OldName rename (TABLE and COLUMN level)
/// directly -- not through ForgeKindler.KindleTheForge -- against a throwaway test table. Bootstrap has
/// zero SchemaSmith_* dependencies by design, so calling it directly here mirrors how it is actually
/// invoked during kindling: one JSON payload in, no other proc involved. SQL Server's sp_rename has no
/// version floor within the supported range, so (unlike MySQL/MariaDb) there is no below-floor fallback
/// case to exercise here.
/// </summary>
[Category("SqlServer")]
[Category("Integration")]
[TestFixture]
public class BootstrapOldNameRenameTests
{
    private const string TableName = "BootstrapOldNameTest";
    private const string OldTableName = "BootstrapOldNameTestOld";

    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.SqlServer)
            .GetDbConnection(FixtureSetup.GetMainDbConnectionString());
        _connection.Open();
        DropTable(TableName);
        DropTable(OldTableName);
    }

    [TearDown]
    public void TearDown()
    {
        DropTable(TableName);
        DropTable(OldTableName);
        _connection?.Close();
        _connection?.Dispose();
    }

    // ---- helpers -----------------------------------------------------------

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private object Scalar(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private void DropTable(string tableName) =>
        Exec($"IF OBJECT_ID('dbo.{tableName}', 'U') IS NOT NULL DROP TABLE dbo.{tableName}");

    private void CallBootstrap(string json) =>
        Exec($"EXEC SchemaSmith.BootstrapTableQuench @TableDefinitions = N'{json.Replace("'", "''")}'");

    private long ColumnExists(string tableName, string columnName) =>
        Convert.ToInt64(Scalar($"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.{tableName}') AND name = '{columnName}'"));

    private long TableExists(string tableName) =>
        Convert.ToInt64(Scalar($"SELECT CASE WHEN OBJECT_ID('dbo.{tableName}', 'U') IS NULL THEN 0 ELSE 1 END"));

    // Bootstrap's JSON contract uses bracket-wrapped Schema/Name/OldName, matching the real
    // Kindling_CompletedMigrationScripts.json ("[SchemaSmith]", "[CompletedMigrationScripts]").
    private static string ColumnRenameJson(string tableName, string newColOldName = null)
    {
        var valueColumnOldName = newColOldName == null ? "" : $", \"OldName\": \"[{newColOldName}]\"";
        return "{"
            + $"\"Schema\": \"[dbo]\", \"Name\": \"[{tableName}]\","
            + "\"Columns\": ["
            + "{\"Name\": \"[Id]\", \"DataType\": \"INT\", \"Nullable\": false},"
            + "{\"Name\": \"[Value]\", \"DataType\": \"VARCHAR(50)\", \"Nullable\": false, \"Default\": \"'0'\"" + valueColumnOldName + "}"
            + "],"
            + "\"Indexes\": ["
            + "{\"Name\": \"[PK_BootstrapOldNameTest]\", \"PrimaryKey\": true, \"Unique\": true, \"Clustered\": true, \"IndexColumns\": \"[Id]\"}"
            + "]"
            + "}";
    }

    private static string TableRenameJson(string tableName, string oldTableName = null)
    {
        var tableOldName = oldTableName == null ? "" : $", \"OldName\": \"[{oldTableName}]\"";
        return "{"
            + $"\"Schema\": \"[dbo]\", \"Name\": \"[{tableName}]\"" + tableOldName + ","
            + "\"Columns\": ["
            + "{\"Name\": \"[Id]\", \"DataType\": \"INT\", \"Nullable\": false},"
            + "{\"Name\": \"[Value]\", \"DataType\": \"VARCHAR(50)\", \"Nullable\": false, \"Default\": \"'0'\"}"
            + "],"
            + "\"Indexes\": ["
            + "{\"Name\": \"[PK_BootstrapOldNameTest]\", \"PrimaryKey\": true, \"Unique\": true, \"Clustered\": true, \"IndexColumns\": \"[Id]\"}"
            + "]"
            + "}";
    }

    // ---- COLUMN-level rename ------------------------------------------------

    [Test]
    public void ColumnRename_FreshTable_CreatesWithNewNameOnly_NoOldColumn()
    {
        CallBootstrap(ColumnRenameJson(TableName, newColOldName: "OldCol"));

        Assert.That(ColumnExists(TableName, "Value"), Is.EqualTo(1), "New column name must exist.");
        Assert.That(ColumnExists(TableName, "OldCol"), Is.EqualTo(0), "No legacy column ever existed; nothing to rename.");
    }

    [Test]
    public void ColumnRename_LegacyColumnPresent_RenamesAndDataSurvives()
    {
        Exec($"CREATE TABLE dbo.{TableName} ([Id] INT IDENTITY(1,1) PRIMARY KEY, [OldCol] VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($"INSERT INTO dbo.{TableName} ([OldCol]) VALUES ('distinguishing-value')");

        CallBootstrap(ColumnRenameJson(TableName, newColOldName: "OldCol"));

        Assert.That(ColumnExists(TableName, "Value"), Is.EqualTo(1), "Renamed column must exist under the new name.");
        Assert.That(ColumnExists(TableName, "OldCol"), Is.EqualTo(0), "Old column name must be gone after rename.");
        Assert.That(Scalar($"SELECT [Value] FROM dbo.{TableName} WHERE [Id] = 1"), Is.EqualTo("distinguishing-value"),
            "The pre-existing row's value must survive the rename, not read back as the column's DEFAULT.");
    }

    [Test]
    public void ColumnRename_SecondRun_IsIdempotent()
    {
        Exec($"CREATE TABLE dbo.{TableName} ([Id] INT IDENTITY(1,1) PRIMARY KEY, [OldCol] VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($"INSERT INTO dbo.{TableName} ([OldCol]) VALUES ('distinguishing-value')");

        CallBootstrap(ColumnRenameJson(TableName, newColOldName: "OldCol"));
        Assert.DoesNotThrow(() => CallBootstrap(ColumnRenameJson(TableName, newColOldName: "OldCol")),
            "A second call against an already-renamed table must be a no-op, not an error.");

        Assert.That(ColumnExists(TableName, "Value"), Is.EqualTo(1));
        Assert.That(ColumnExists(TableName, "OldCol"), Is.EqualTo(0));
        Assert.That(Scalar($"SELECT [Value] FROM dbo.{TableName} WHERE [Id] = 1"), Is.EqualTo("distinguishing-value"),
            "Data must still be intact after the idempotent second run.");
    }

    [Test]
    public void ColumnRename_BothOldAndNewColumnsExist_Throws()
    {
        Exec($"CREATE TABLE dbo.{TableName} ([Id] INT IDENTITY(1,1) PRIMARY KEY, [OldCol] VARCHAR(50) NOT NULL DEFAULT '0', [Value] VARCHAR(50) NOT NULL DEFAULT '0')");

        var ex = Assert.Catch<Exception>(() => CallBootstrap(ColumnRenameJson(TableName, newColOldName: "OldCol")));
        var message = ex!.Message + ex.InnerException?.Message;
        Assert.That(message, Does.Contain("already exist"));
        // Refusing is correct; refusing without saying WHICH objects leaves the operator to guess.
        // PostgreSQL has always named them -- this is the parity the other engines owe.
        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain(TableName), "the message must name the table");
            Assert.That(message, Does.Contain("OldCol"), "the message must name the OldName column");
            Assert.That(message, Does.Contain("Value"), "the message must name the current column");
        });
    }

    // ---- TABLE-level rename --------------------------------------------------

    [Test]
    public void TableRename_NoLegacyTable_CreatesWithNewNameOnly()
    {
        CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName));

        Assert.That(TableExists(TableName), Is.EqualTo(1), "New table name must exist.");
        Assert.That(TableExists(OldTableName), Is.EqualTo(0), "No legacy table ever existed; nothing to rename.");
    }

    [Test]
    public void TableRename_LegacyTablePresent_RenamesAndDataSurvives()
    {
        Exec($"CREATE TABLE dbo.{OldTableName} ([Id] INT IDENTITY(1,1) PRIMARY KEY, [Value] VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($"INSERT INTO dbo.{OldTableName} ([Value]) VALUES ('distinguishing-value')");

        CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName));

        Assert.That(TableExists(TableName), Is.EqualTo(1), "Renamed table must exist under the new name.");
        Assert.That(TableExists(OldTableName), Is.EqualTo(0), "Old table name must be gone after rename.");
        Assert.That(Scalar($"SELECT [Value] FROM dbo.{TableName} WHERE [Id] = 1"), Is.EqualTo("distinguishing-value"),
            "The old table's row -- and its whole history -- must survive the rename, not be orphaned under an empty new table.");
    }

    [Test]
    public void TableRename_SecondRun_IsIdempotent()
    {
        Exec($"CREATE TABLE dbo.{OldTableName} ([Id] INT IDENTITY(1,1) PRIMARY KEY, [Value] VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($"INSERT INTO dbo.{OldTableName} ([Value]) VALUES ('distinguishing-value')");

        CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName));
        Assert.DoesNotThrow(() => CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName)),
            "A second call against an already-renamed table must be a no-op, not an error.");

        Assert.That(TableExists(TableName), Is.EqualTo(1));
        Assert.That(TableExists(OldTableName), Is.EqualTo(0));
        Assert.That(Scalar($"SELECT [Value] FROM dbo.{TableName} WHERE [Id] = 1"), Is.EqualTo("distinguishing-value"),
            "Data must still be intact after the idempotent second run.");
    }

    [Test]
    public void TableRename_BothOldAndNewTablesExist_Throws()
    {
        Exec($"CREATE TABLE dbo.{OldTableName} ([Id] INT IDENTITY(1,1) PRIMARY KEY, [Value] VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($"CREATE TABLE dbo.{TableName} ([Id] INT IDENTITY(1,1) PRIMARY KEY, [Value] VARCHAR(50) NOT NULL DEFAULT '0')");

        var ex = Assert.Catch<Exception>(() => CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName)));
        var message = ex!.Message + ex.InnerException?.Message;
        Assert.That(message, Does.Contain("already exist"));
        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain(OldTableName), "the message must name the OldName table");
            Assert.That(message, Does.Contain(TableName), "the message must name the current table");
        });
    }
}
