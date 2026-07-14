// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

#nullable enable
using System;
using Schema.DataAccess;
using Schema.Domain;
using NUnit.Framework;

namespace SchemaQuench.IntegrationTests.Shared;

/// <summary>
/// Integration tests for index visibility changes during table quench.
/// Tests that visibility changes (VISIBLE to INVISIBLE and vice versa) are detected and applied.
/// </summary>
public abstract class TableQuench_IndexVisibilitySharedTests : BaseTableQuenchTests
{
    private const string TestSchema = "IdxVisibilityTests";

    [Test]
    public void ShouldModifyIndexVisibilityViaIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Index should now be invisible. Read via the engine's SchemaSmith_IndexIsVisible helper
        // (returns 1/0) rather than INFORMATION_SCHEMA.STATISTICS.IS_VISIBLE — MariaDB has no
        // IS_VISIBLE column (it exposes the inverted IGNORED); the helper normalizes both engines.
        cmd.CommandText = $"SELECT `{_mainDb}`.SchemaSmith_IndexIsVisible('{TestSchema}', 'ModifyVisibilityIO', 'IDX_VisibilityIO')";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "Index should be invisible after IndexOnly quench");

        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexVisibilityViaTableQuench()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Index should now be invisible (engine-neutral read; see the IndexOnly test above).
        cmd.CommandText = $"SELECT `{_mainDb}`.SchemaSmith_IndexIsVisible('{TestSchema}', 'ModifyVisibilityTQ', 'IDX_VisibilityTQ')";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0), "Index should be invisible after table quench");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Drop and recreate to ensure clean state
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CREATE DATABASE `{TestSchema}`";
        cmd.ExecuteNonQuery();

        cmd.CommandTimeout = 300;
        cmd.CommandText = $@"
-- ShouldModifyIndexVisibilityViaIndexOnly
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyVisibilityIO` (`Column1` INT NOT NULL, `Column2` INT NOT NULL);
CREATE INDEX `IDX_VisibilityIO` ON `{TestSchema}`.`ModifyVisibilityIO` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyVisibilityIO');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyVisibilityIO.IDX_VisibilityIO');
-- ShouldModifyIndexVisibilityViaTableQuench
CREATE TABLE IF NOT EXISTS `{TestSchema}`.`ModifyVisibilityTQ` (`Column1` INT NOT NULL, `Column2` INT NOT NULL);
CREATE INDEX `IDX_VisibilityTQ` ON `{TestSchema}`.`ModifyVisibilityTQ` (`Column1`);
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'TABLE', 'ModifyVisibilityTQ');
INSERT INTO `{_mainDb}`.SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
VALUES ('{_productName}', '', '{TestSchema}', 'INDEX', 'ModifyVisibilityTQ.IDX_VisibilityTQ');
";
        cmd.ExecuteNonQuery();

        // Table quench path: make IDX_VisibilityTQ invisible via MissingIndexesAndConstraintsQuench
        var jsonTQ = """
        [
            {
                "Name": "ModifyVisibilityTQ",
                "Columns": [
                    { "Name": "Column1", "DataType": "INT", "Nullable": false },
                    { "Name": "Column2", "DataType": "INT", "Nullable": false }
                ],
                "Indexes": [
                    { "Name": "IDX_VisibilityTQ", "IndexColumns": "Column1", "Visible": false }
                ]
            }
        ]
        """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{jsonTQ.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingTableAndColumnQuench('{TestSchema}', 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0, 1, 1, 1, 1, 0)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0, 1, 1)";
        cmd.ExecuteNonQuery();

        // Index Only path: make IDX_VisibilityIO invisible via IndexOnlyQuench
        var jsonIO = """
        [
            {
                "Name": "ModifyVisibilityIO",
                "Indexes": [
                    { "Name": "IDX_VisibilityIO", "IndexColumns": "Column1", "Visible": false }
                ]
            }
        ]
        """;

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ParseTableJson('{TestSchema}', '{jsonIO.Replace("'", "''")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_IndexOnlyQuench('{_productName}', '{TestSchema}', 0, 0, 1)";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        try
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestSchema}`";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
        catch { /* Ignore cleanup errors */ }
    }
}
