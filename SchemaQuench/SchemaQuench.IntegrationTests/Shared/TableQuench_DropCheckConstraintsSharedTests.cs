// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

public abstract class TableQuench_DropCheckConstraintsSharedTests : BaseTableQuenchTests
{
    // Non-vacuous + the ### Fixed: MySQL previously dropped a table-level CHECK only as a side
    // effect of a column drop, never by absence (only PostgreSQL did). Now it does by default.
    // ChkControl's check is removed from the JSON with no flag -> dropped (the normalization);
    // ChkSuppressed sets DropCheckConstraintsRemovedFromProduct:false -> its check survives.
    [Test]
    public void TableQuench_ShouldSuppressCheckDropWhenTableFlagIsFalse()
    {
        if (!TargetSupportsCheckConstraints())
            Assert.Ignore("CHECK constraints require MySQL 8.0.16; skipped below the floor.");
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = '{_mainDb}' AND CONSTRAINT_NAME = 'CK_ChkMyChkSuppressed' AND CONSTRAINT_TYPE = 'CHECK'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1), "CK_ChkMyChkSuppressed should still exist (suppressed by table flag).");

        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = '{_mainDb}' AND CONSTRAINT_NAME = 'CK_ChkMyChkControl' AND CONSTRAINT_TYPE = 'CHECK'";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "CK_ChkMyChkControl should be gone (dropped by absence, the normalization).");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        conn.ChangeDatabase(_mainDb);
        using var cmd = conn.CreateCommand();

        // Multi-column checks so the names don't collide with the CK_<table>_<column> column-check
        // convention. Created directly so they exist before the quench removes them by absence.
        cmd.CommandText = $@"
CREATE TABLE `{_mainDb}`.`ChkMyChkSuppressed` (`Id` INT NOT NULL, `Val` INT, CONSTRAINT `CK_ChkMyChkSuppressed` CHECK (`Val` > `Id`));
CREATE TABLE `{_mainDb}`.`ChkMyChkControl` (`Id` INT NOT NULL, `Val` INT, CONSTRAINT `CK_ChkMyChkControl` CHECK (`Val` > `Id`));";
        cmd.ExecuteNonQuery();

        var json = """
            [
            {
                "Name": "ChkMyChkSuppressed",
                "DropCheckConstraintsRemovedFromProduct": false,
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false }, { "Name": "Val", "DataType": "INT", "Nullable": true } ]
            },
            {
                "Name": "ChkMyChkControl",
                "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false }, { "Name": "Val", "DataType": "INT", "Nullable": true } ]
            }
            ]
            """;
        RunTableQuenchProc(cmd, json);

        conn.Close();
    }
}
