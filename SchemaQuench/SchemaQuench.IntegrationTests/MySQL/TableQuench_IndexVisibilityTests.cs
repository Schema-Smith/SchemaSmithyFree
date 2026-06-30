// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

#nullable enable
using System;
using Schema.DataAccess;
using Schema.Domain;
using NUnit.Framework;
using Schema.IntegrationTests.MySQL;

namespace SchemaQuench.IntegrationTests.MySQL;

/// <summary>
/// Integration tests for index visibility changes during table quench.
/// Tests that visibility changes (VISIBLE to INVISIBLE and vice versa) are detected and applied.
/// </summary>
[TestFixture]
[Category("MySQL")]
[Parallelizable(scope: ParallelScope.All)]
public class TableQuench_IndexVisibilityTests : BaseTableQuenchTests
{
    private const string TestSchema = "IdxVisibilityTests";

    [Test]
    public void ShouldModifyIndexVisibilityViaIndexOnly()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Index should now be invisible
        cmd.CommandText = $@"
            SELECT IS_VISIBLE FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyVisibilityIO'
              AND INDEX_NAME = 'IDX_VisibilityIO'
              AND SEQ_IN_INDEX = 1";
        var isVisible = cmd.ExecuteScalar()?.ToString();
        Assert.That(isVisible, Is.EqualTo("NO"), "Index should be invisible (IS_VISIBLE=NO) after IndexOnly quench");

        conn.Close();
    }

    [Test]
    public void ShouldModifyIndexVisibilityViaTableQuench()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Index should now be invisible
        cmd.CommandText = $@"
            SELECT IS_VISIBLE FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = '{TestSchema}'
              AND TABLE_NAME = 'ModifyVisibilityTQ'
              AND INDEX_NAME = 'IDX_VisibilityTQ'
              AND SEQ_IN_INDEX = 1";
        var isVisible = cmd.ExecuteScalar()?.ToString();
        Assert.That(isVisible, Is.EqualTo("NO"), "Index should be invisible (IS_VISIBLE=NO) after table quench");

        conn.Close();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
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

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_ModifiedTableQuench('{_productName}', '{TestSchema}', 0, 0, 1, 1)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_MissingIndexesAndConstraintsQuench('{_productName}', '{TestSchema}', 0, 0)";
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

        cmd.CommandText = $"CALL `{_mainDb}`.SchemaSmith_IndexOnlyQuench('{_productName}', '{TestSchema}', 0, 0)";
        cmd.ExecuteNonQuery();

        conn.Close();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        try
        {
            using var conn = DbConnectionFactory.ForPlatform(Platform.MySQL).GetDbConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{TestSchema}`";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
        catch { /* Ignore cleanup errors */ }
    }
}
