// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.DataAccess;
using Schema.Domain;

namespace SchemaQuench.IntegrationTests.Shared;

/// <summary>
/// #375: an explicit <c>"OldName": ""</c> in the table (or column) JSON must be treated as "no rename"
/// (NULL), not manufactured into a backtick pair. SchemaTongs-extracted packages emit empty OldName fields,
/// so this is the common case. Before the fix, ParseTableJson wrapped the blank into <c>``</c> (non-NULL),
/// the <c>OldName IS NOT NULL</c> rename guards fired, and the SECOND deploy of two such tables collided on
/// <c>_SchemaSmith_TableRenames.PRIMARY</c> ("Duplicate entry '' for key ..."). Runs under both the MySQL
/// and MariaDB fixtures — the collision reproduces on both engine families.
/// </summary>
public abstract class TableRenameEmptyOldNameSharedTests : BaseTableQuenchTests
{
    [Test]
    public void EmptyOldNameOnMultipleTables_IsIdempotentAcrossDeploys()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var product = $"EmptyOld_{id}";
        var a = $"EmptyOldA_{id}";
        var b = $"EmptyOldB_{id}";
        var json = $$"""
[
  { "Name": "{{a}}", "OldName": "", "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false, "PrimaryKey": true } ] },
  { "Name": "{{b}}", "OldName": "", "Columns": [ { "Name": "Id", "DataType": "INT", "Nullable": false, "PrimaryKey": true } ] }
]
""";

        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        try
        {
            // Pass 1 creates both tables (NewTable = 1, so the rename-tracking insert is skipped).
            RunTableQuenchProc(cmd, json, productName: product);

            // Pass 2: the tables now exist. With the empty OldName wrapped to `` (non-NULL), both tables insert
            // OldTableName = '' into _SchemaSmith_TableRenames and collide on its PRIMARY KEY. Must not throw.
            Assert.DoesNotThrow(() => RunTableQuenchProc(cmd, json, productName: product),
                "two tables declaring an empty OldName must stay idempotent on re-deploy (#375) — no _SchemaSmith_TableRenames PK collision");
        }
        finally
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{_mainDb}`.`{a}`, `{_mainDb}`.`{b}`";
            cmd.ExecuteNonQuery();
        }
    }
}
