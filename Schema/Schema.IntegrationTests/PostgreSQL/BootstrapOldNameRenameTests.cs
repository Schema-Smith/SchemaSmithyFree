// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;

namespace Schema.IntegrationTests.PostgreSQL;

/// <summary>
/// Exercises SchemaSmith.BootstrapTableQuench's declarative OldName rename (TABLE and COLUMN level)
/// directly -- not through ForgeKindler.KindleTheForge -- against a throwaway test table in the
/// "public" schema. Bootstrap has zero SchemaSmith_* dependencies by design, so calling it directly
/// here mirrors how it is actually invoked during kindling: one JSON payload in, no other proc
/// involved. PostgreSQL's ALTER TABLE ... RENAME / RENAME COLUMN has no version floor within the
/// supported range, so (unlike MySQL/MariaDb) there is no below-floor fallback case to exercise here.
/// </summary>
[Category("PostgreSQL")]
[Category("Integration")]
[TestFixture]
public class BootstrapOldNameRenameTests
{
    private const string TableName = "bootstrap_oldname_test";
    private const string OldTableName = "bootstrap_oldname_test_old";

    private IDbConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = DbConnectionFactory.ForPlatform(Platform.PostgreSQL)
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

    private void DropTable(string tableName) => Exec($@"DROP TABLE IF EXISTS ""public"".""{tableName}""");

    private void CallBootstrap(string json) =>
        Exec($"CALL \"SchemaSmith\".\"BootstrapTableQuench\"('{json.Replace("'", "''")}')");

    private long ColumnExists(string tableName, string columnName) =>
        Convert.ToInt64(Scalar($"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{tableName}' AND column_name = '{columnName}'"));

    private long TableExists(string tableName) =>
        Convert.ToInt64(Scalar($"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName}'"));

    // Bootstrap's JSON contract uses bare Schema/Name/OldName (no quoting), matching the real
    // Kindling_CompletedMigrationScripts.json ("SchemaSmith", "CompletedMigrationScripts").
    private static string ColumnRenameJson(string tableName, string newColOldName = null)
    {
        var valueColumnOldName = newColOldName == null ? "" : $", \"OldName\": \"{newColOldName}\"";
        return "{"
            + $"\"Schema\": \"public\", \"Name\": \"{tableName}\","
            + "\"Columns\": ["
            + "{\"Name\": \"id\", \"DataType\": \"INT\", \"Nullable\": false},"
            + "{\"Name\": \"value\", \"DataType\": \"VARCHAR(50)\", \"Nullable\": false, \"Default\": \"'0'\"" + valueColumnOldName + "}"
            + "],"
            + "\"Indexes\": ["
            + "{\"Name\": \"pk_bootstrap_oldname_test\", \"PrimaryKey\": true, \"Unique\": true, \"IndexColumns\": \"id\"}"
            + "]"
            + "}";
    }

    private static string TableRenameJson(string tableName, string oldTableName = null)
    {
        var tableOldName = oldTableName == null ? "" : $", \"OldName\": \"{oldTableName}\"";
        return "{"
            + $"\"Schema\": \"public\", \"Name\": \"{tableName}\"" + tableOldName + ","
            + "\"Columns\": ["
            + "{\"Name\": \"id\", \"DataType\": \"INT\", \"Nullable\": false},"
            + "{\"Name\": \"value\", \"DataType\": \"VARCHAR(50)\", \"Nullable\": false, \"Default\": \"'0'\"}"
            + "],"
            + "\"Indexes\": ["
            + "{\"Name\": \"pk_bootstrap_oldname_test\", \"PrimaryKey\": true, \"Unique\": true, \"IndexColumns\": \"id\"}"
            + "]"
            + "}";
    }

    // ---- COLUMN-level rename ------------------------------------------------

    [Test]
    public void ColumnRename_FreshTable_CreatesWithNewNameOnly_NoOldColumn()
    {
        CallBootstrap(ColumnRenameJson(TableName, newColOldName: "old_value"));

        Assert.That(ColumnExists(TableName, "value"), Is.EqualTo(1), "New column name must exist.");
        Assert.That(ColumnExists(TableName, "old_value"), Is.EqualTo(0), "No legacy column ever existed; nothing to rename.");
    }

    [Test]
    public void ColumnRename_LegacyColumnPresent_RenamesAndDataSurvives()
    {
        Exec($@"CREATE TABLE ""public"".""{TableName}"" (id SERIAL PRIMARY KEY, old_value VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($@"INSERT INTO ""public"".""{TableName}"" (old_value) VALUES ('distinguishing-value')");

        CallBootstrap(ColumnRenameJson(TableName, newColOldName: "old_value"));

        Assert.That(ColumnExists(TableName, "value"), Is.EqualTo(1), "Renamed column must exist under the new name.");
        Assert.That(ColumnExists(TableName, "old_value"), Is.EqualTo(0), "Old column name must be gone after rename.");
        Assert.That(Scalar($@"SELECT value FROM ""public"".""{TableName}"" WHERE id = 1"), Is.EqualTo("distinguishing-value"),
            "The pre-existing row's value must survive the rename, not read back as the column's DEFAULT.");
    }

    [Test]
    public void ColumnRename_SecondRun_IsIdempotent()
    {
        Exec($@"CREATE TABLE ""public"".""{TableName}"" (id SERIAL PRIMARY KEY, old_value VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($@"INSERT INTO ""public"".""{TableName}"" (old_value) VALUES ('distinguishing-value')");

        CallBootstrap(ColumnRenameJson(TableName, newColOldName: "old_value"));
        Assert.DoesNotThrow(() => CallBootstrap(ColumnRenameJson(TableName, newColOldName: "old_value")),
            "A second call against an already-renamed table must be a no-op, not an error.");

        Assert.That(ColumnExists(TableName, "value"), Is.EqualTo(1));
        Assert.That(ColumnExists(TableName, "old_value"), Is.EqualTo(0));
        Assert.That(Scalar($@"SELECT value FROM ""public"".""{TableName}"" WHERE id = 1"), Is.EqualTo("distinguishing-value"),
            "Data must still be intact after the idempotent second run.");
    }

    [Test]
    public void ColumnRename_BothOldAndNewColumnsExist_Throws()
    {
        Exec($@"CREATE TABLE ""public"".""{TableName}"" (id SERIAL PRIMARY KEY, old_value VARCHAR(50) NOT NULL DEFAULT '0', value VARCHAR(50) NOT NULL DEFAULT '0')");

        var ex = Assert.Catch<Exception>(() => CallBootstrap(ColumnRenameJson(TableName, newColOldName: "old_value")));
        Assert.That(ex!.Message + ex.InnerException?.Message, Does.Contain("already exist"));
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
        Exec($@"CREATE TABLE ""public"".""{OldTableName}"" (id SERIAL PRIMARY KEY, value VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($@"INSERT INTO ""public"".""{OldTableName}"" (value) VALUES ('distinguishing-value')");

        CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName));

        Assert.That(TableExists(TableName), Is.EqualTo(1), "Renamed table must exist under the new name.");
        Assert.That(TableExists(OldTableName), Is.EqualTo(0), "Old table name must be gone after rename.");
        Assert.That(Scalar($@"SELECT value FROM ""public"".""{TableName}"" WHERE id = 1"), Is.EqualTo("distinguishing-value"),
            "The old table's row -- and its whole history -- must survive the rename, not be orphaned under an empty new table.");
    }

    [Test]
    public void TableRename_SecondRun_IsIdempotent()
    {
        Exec($@"CREATE TABLE ""public"".""{OldTableName}"" (id SERIAL PRIMARY KEY, value VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($@"INSERT INTO ""public"".""{OldTableName}"" (value) VALUES ('distinguishing-value')");

        CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName));
        Assert.DoesNotThrow(() => CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName)),
            "A second call against an already-renamed table must be a no-op, not an error.");

        Assert.That(TableExists(TableName), Is.EqualTo(1));
        Assert.That(TableExists(OldTableName), Is.EqualTo(0));
        Assert.That(Scalar($@"SELECT value FROM ""public"".""{TableName}"" WHERE id = 1"), Is.EqualTo("distinguishing-value"),
            "Data must still be intact after the idempotent second run.");
    }

    [Test]
    public void TableRename_BothOldAndNewTablesExist_Throws()
    {
        Exec($@"CREATE TABLE ""public"".""{OldTableName}"" (id SERIAL PRIMARY KEY, value VARCHAR(50) NOT NULL DEFAULT '0')");
        Exec($@"CREATE TABLE ""public"".""{TableName}"" (id SERIAL PRIMARY KEY, value VARCHAR(50) NOT NULL DEFAULT '0')");

        var ex = Assert.Catch<Exception>(() => CallBootstrap(TableRenameJson(TableName, oldTableName: OldTableName)));
        Assert.That(ex!.Message + ex.InnerException?.Message, Does.Contain("already exist"));
    }
}
