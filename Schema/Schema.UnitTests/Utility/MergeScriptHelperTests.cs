// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NSubstitute;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class MergeScriptHelperTests
{
    #region GetKeyColumns Tests

    [Test]
    public void GetKeyColumns_SqlServer_ExecutesPlatformSpecificQuery()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("[Id],[Name]");

        var result = MergeScriptHelper.GetKeyColumns(Platform.SqlServer, cmd, "dbo", "TestTable");

        Assert.That(result, Is.EqualTo("[Id],[Name]"));
        Assert.That(cmd.CommandText, Does.Contain("sys.indexes"));
        Assert.That(cmd.CommandText, Does.Contain("COL_NAME"));
    }

    [Test]
    public void GetKeyColumns_PostgreSQL_ExecutesPlatformSpecificQuery()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("\"id\",\"name\"");

        var result = MergeScriptHelper.GetKeyColumns(Platform.PostgreSQL, cmd, "public", "test_table");

        Assert.That(result, Is.EqualTo("\"id\",\"name\""));
        Assert.That(cmd.CommandText, Does.Contain("pg_index"));
        Assert.That(cmd.CommandText, Does.Contain("pg_attribute"));
    }

    [Test]
    public void GetKeyColumns_MySQL_ExecutesPlatformSpecificQuery()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("`id`,`name`");

        var result = MergeScriptHelper.GetKeyColumns(Platform.MySQL, cmd, "testdb", "test_table");

        Assert.That(result, Is.EqualTo("`id`,`name`"));
        Assert.That(cmd.CommandText, Does.Contain("TABLE_CONSTRAINTS"));
        Assert.That(cmd.CommandText, Does.Contain("PRIMARY KEY"));
    }

    [Test]
    public void GetKeyColumns_SqlServer_TrimsBrackets()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("[Id]");

        MergeScriptHelper.GetKeyColumns(Platform.SqlServer, cmd, "[dbo]", "[TestTable]");

        Assert.That(cmd.CommandText, Does.Not.Contain("[["));
    }

    [Test]
    public void GetKeyColumns_PostgreSQL_TrimsQuotes()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("\"id\"");

        MergeScriptHelper.GetKeyColumns(Platform.PostgreSQL, cmd, "\"public\"", "\"test\"");

        Assert.That(cmd.CommandText, Does.Contain("'public'"));
        Assert.That(cmd.CommandText, Does.Contain("'test'"));
    }

    [Test]
    public void GetKeyColumns_MySQL_TrimsBackticks()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("`id`");

        MergeScriptHelper.GetKeyColumns(Platform.MySQL, cmd, "`testdb`", "`test`");

        Assert.That(cmd.CommandText, Does.Contain("'testdb'"));
        Assert.That(cmd.CommandText, Does.Contain("'test'"));
    }

    [Test]
    public void GetKeyColumns_NullResult_ReturnsEmptyString()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns(null);

        var result = MergeScriptHelper.GetKeyColumns(Platform.SqlServer, cmd, "dbo", "TestTable");

        Assert.That(result, Is.EqualTo(""));
    }

    #endregion

    #region BuildMergeScript - SQL Server Tests

    [Test]
    public void BuildMergeScript_SqlServer_InsertOnly_ContainsMergeAndInsert()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id],[Name]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [Name] NVARCHAR(100)",
            insertCols: "        [Id],\r\n        [Name]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("MERGE INTO [dbo].[TestTable] AS Target"));
        Assert.That(result, Does.Contain("OPENJSON(@v_json)"));
        Assert.That(result, Does.Contain("WHEN NOT MATCHED BY TARGET THEN"));
        Assert.That(result, Does.Contain("INSERT"));
        Assert.That(result, Does.Not.Contain("WHEN MATCHED"));
        Assert.That(result, Does.Not.Contain("DELETE"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_InsertUpdate_ContainsMatchedClause()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id],[Name]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [Name] NVARCHAR(100)",
            insertCols: "        [Id],\r\n        [Name]",
            updateCols: "[Name]");

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN MATCHED AND"));
        Assert.That(result, Does.Contain("UPDATE SET"));
        Assert.That(result, Does.Contain("WHEN NOT MATCHED BY TARGET THEN"));
        Assert.That(result, Does.Not.Contain("WHEN NOT MATCHED BY SOURCE"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_InsertUpdateDelete_ContainsDeleteClause()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id],[Name]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [Name] NVARCHAR(100)",
            insertCols: "        [Id],\r\n        [Name]",
            updateCols: "[Name]");

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: true, mergeDelete: true, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN NOT MATCHED BY SOURCE"));
        Assert.That(result, Does.Contain("DELETE"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_WithMergeFilter_IncludesFilterInDelete()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id],[Name]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [Name] NVARCHAR(100)",
            insertCols: "        [Id],\r\n        [Name]",
            updateCols: "[Name]");

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: true, mergeDelete: true, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: "Target.[Type] = 'Active'");

        Assert.That(result, Does.Contain("AND (Target.[Type] = 'Active')"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_WithDisableTriggers_IncludesTriggerStatements()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT",
            insertCols: "        [Id]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: true,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("DISABLE TRIGGER ALL"));
        Assert.That(result, Does.Contain("ENABLE TRIGGER ALL"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_WithIdentityInsert_IncludesIdentityStatements()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id]",
            needsIdentity: true,
            jsonColDefs: "           [Id] INT",
            insertCols: "        [Id]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("SET IDENTITY_INSERT [dbo].[TestTable] ON"));
        Assert.That(result, Does.Contain("SET IDENTITY_INSERT [dbo].[TestTable] OFF"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_WithTokenize_UsesTokenPlaceholder()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT",
            insertCols: "        [Id]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: true, mergeFilter: null);

        Assert.That(result, Does.Contain("{{dbo.TestTable.tabledata}}"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_EscapesSingleQuotes()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id],[Name]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [Name] NVARCHAR(100)",
            insertCols: "        [Id],\r\n        [Name]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Name\":\"O'Brien\"}]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("O''Brien"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_NullableKeyColumn_GeneratesIsNullComparison()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT",
            insertCols: "        [Id]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[]", "*[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("IS NULL AND Target.[Id] IS NULL"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_TrimsBracketsFromSchemaAndTable()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT",
            insertCols: "        [Id]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "[dbo]", "[TestTable]", "[]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("MERGE INTO [dbo].[TestTable] AS Target"));
        Assert.That(result, Does.Not.Contain("[[dbo]]"));
    }

    #endregion

    #region BuildMergeScript - PostgreSQL Tests

    [Test]
    public void BuildMergeScript_PostgreSQL_InsertOnly_ContainsMergeAndInsert()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",(elem ->> 'name')::varchar(100) AS \"name\"",
            insertCols: "        \"id\",\r\n        \"name\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[{\"id\":1}]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("MERGE INTO"));
        Assert.That(result, Does.Contain("\"public\".\"test_table\""));
        Assert.That(result, Does.Contain("JSON_ARRAY_ELEMENTS"));
        Assert.That(result, Does.Contain("WHEN NOT MATCHED"));
        Assert.That(result, Does.Contain("INSERT"));
        Assert.That(result, Does.Contain("DO $$"));
        Assert.That(result, Does.Contain("END $$ LANGUAGE plpgsql"));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_InsertUpdate_ContainsMatchedClause()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",(elem ->> 'name')::varchar(100) AS \"name\"",
            insertCols: "        \"id\",\r\n        \"name\"",
            updateCols: "\"name\"");

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[{\"id\":1}]", "\"id\"",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN MATCHED AND"));
        Assert.That(result, Does.Contain("UPDATE SET"));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_InsertUpdateDelete_ContainsDeleteClause()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\"",
            insertCols: "        \"id\"",
            updateCols: "\"id\"");

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: true, mergeDelete: true, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN NOT MATCHED BY SOURCE"));
        Assert.That(result, Does.Contain("DELETE"));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_WithDisableTriggers()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\"",
            insertCols: "        \"id\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: true,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("DISABLE TRIGGER ALL"));
        Assert.That(result, Does.Contain("ENABLE TRIGGER ALL"));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_WithIdentitySequence_IncludesSetval()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: "id=public.test_table_id_seq=SYSTEM",
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\"",
            insertCols: "        \"id\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("OVERRIDING SYSTEM VALUE"));
        Assert.That(result, Does.Contain("SETVAL"));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_WithTokenize_UsesTokenPlaceholder()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\"",
            insertCols: "        \"id\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: true, mergeFilter: null);

        Assert.That(result, Does.Contain("{{public.test_table.tabledata}}"));
    }

    #endregion

    #region BuildMergeScript - MySQL Tests

    [Test]
    public void BuildMergeScript_MySQL_Insert_GeneratesInsertIgnore()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "", null),
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[{\"id\":1}]", "`id`",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("INSERT IGNORE INTO `testdb`.`testtable`"));
        Assert.That(result, Does.Contain("JSON_TABLE("));
        Assert.That(result, Does.Not.Contain("ON DUPLICATE KEY"));
    }

    [Test]
    public void BuildMergeScript_MySQL_InsertUpdate_GeneratesOnDuplicateKey()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "", null),
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[{\"id\":1,\"name\":\"test\"}]", "`id`",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("INSERT INTO `testdb`.`testtable`"));
        Assert.That(result, Does.Contain("ON DUPLICATE KEY UPDATE"));
        Assert.That(result, Does.Contain("`name` = VALUES(`name`)"));
        Assert.That(result, Does.Not.Contain("`id` = VALUES(`id`)"));
    }

    [Test]
    public void BuildMergeScript_MySQL_InsertUpdateDelete_GeneratesReplaceInto()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "", null),
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[{\"id\":1}]", "`id`",
            mergeUpdate: true, mergeDelete: true, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("REPLACE INTO `testdb`.`testtable`"));
    }

    [Test]
    public void BuildMergeScript_MySQL_IncludesAutoIncrementColumns()
    {
        // MySQL allows explicit inserts into AUTO_INCREMENT columns without any special command.
        // Auto-increment columns must be included to preserve original ID values from data files,
        // matching how SQL Server (IDENTITY_INSERT ON) and PostgreSQL (OVERRIDING VALUE) work.
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "auto_increment", null),
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[]", "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("`id`"));
        Assert.That(result, Does.Contain("`name`"));
    }

    [Test]
    public void BuildMergeScript_MySQL_ExcludesGeneratedColumns()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("first_name", "varchar", 50L, null, null, null, "varchar(50)", "", null),
            new("full_name", "varchar", 101L, null, null, null, "varchar(101)", "", "CONCAT(`first_name`)")
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[]", "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("`first_name`"));
        Assert.That(result, Does.Not.Contain("`full_name`"));
    }

    [Test]
    public void BuildMergeScript_MySQL_EscapesSingleQuotes()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[{\"name\":\"O'Connor\"}]", "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("O''Connor"));
    }

    [Test]
    public void BuildMergeScript_MySQL_NullTableData_TreatsAsEmptyArray()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", null, "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("@json_data = '[]'"));
    }

    [Test]
    public void BuildMergeScript_MySQL_NoColumnsAfterFiltering_ThrowsInvalidOperationException()
    {
        // Only generated columns remain after filtering — no insertable columns
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("full_name", "varchar", 101L, null, null, null, "varchar(101)", "", "CONCAT(`first`,`last`)")
        });

        Assert.Throws<InvalidOperationException>(() =>
            MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
                "testdb", "testtable", "[]", "",
                mergeUpdate: false, mergeDelete: false, disableTriggers: false,
                tokenizeScripts: false, mergeFilter: null));
    }

    [Test]
    public void BuildMergeScript_MySQL_VariousDataTypes()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("int_col", "int", null, 10L, 0L, null, "int", "", null),
            new("dec_col", "decimal", null, 10L, 2L, null, "decimal(10,2)", "", null),
            new("dt_col", "datetime", null, null, null, 6, "datetime(6)", "", null),
            new("vc_col", "varchar", 100L, null, null, null, "varchar(100)", "", null),
            new("json_col", "json", null, null, null, null, "json", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[]", "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("`int_col` INT PATH '$.int_col'"));
        Assert.That(result, Does.Contain("`dec_col` DECIMAL(10,2) PATH '$.dec_col'"));
        Assert.That(result, Does.Contain("`dt_col` DATETIME(6) PATH '$.dt_col'"));
        Assert.That(result, Does.Contain("`vc_col` VARCHAR(100) PATH '$.vc_col'"));
        Assert.That(result, Does.Contain("`json_col` JSON PATH '$.json_col'"));
    }

    [Test]
    public void BuildMergeScript_MySQL_BinaryType_UsesFromBase64()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("data", "blob", null, null, null, null, "blob", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[]", "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("FROM_BASE64(`data`)"));
        Assert.That(result, Does.Contain("`data` TEXT PATH '$.data'"));
    }

    [Test]
    public void BuildMergeScript_MySQL_GeometryType_UsesStGeomFromText()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("location", "point", null, null, null, null, "point", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[]", "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("ST_GeomFromText(`location`)"));
    }

    [Test]
    public void BuildMergeScript_MySQL_TrimsBackticks()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "`testdb`", "`testtable`", "[]", "",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("`testdb`.`testtable`"));
        Assert.That(result, Does.Not.Contain("``testdb``"));
    }

    [Test]
    public void BuildMergeScript_MySQL_AllColumnsAreKeys_UsesNoOpUpdate()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "", null),
            new("code", "varchar", 10L, null, null, null, "varchar(10)", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[]", "`id`,`code`",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("ON DUPLICATE KEY UPDATE"));
        Assert.That(result, Does.Contain("`id` = VALUES(`id`)"));
    }

    [Test]
    public void BuildMergeScript_MySQL_JsonColumn_UsesConditionalComparisonInUpsert()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "", null),
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null),
            new("metadata", "json", null, null, null, null, "json", "", null)
        });

        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", @"[{""id"":1,""name"":""test"",""metadata"":{""key"":""value""}}]", "`id`",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        // JSON column should use conditional comparison to prevent false updates from key reordering
        Assert.That(result, Does.Contain("IF(CAST(VALUES(`metadata`) AS JSON) = CAST(`testdb`.`testtable`.`metadata` AS JSON), `testdb`.`testtable`.`metadata`, VALUES(`metadata`))"));

        // Non-JSON column should use simple assignment
        Assert.That(result, Does.Contain("`name` = VALUES(`name`)"));
        Assert.That(result, Does.Not.Contain("IF(CAST(VALUES(`name`)"));
    }

    #endregion

    #region Cross-Platform Consistency Tests

    [Test]
    public void BuildMergeScript_AllPlatforms_InsertEscapesSingleQuotes()
    {
        // SQL Server
        var sqlCmd = CreateSqlServerMockCommand("[Id]", false, "           [Id] INT", "        [Id]", null);
        var sqlResult = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, sqlCmd,
            "dbo", "T", "[{\"v\":\"it's\"}]", "[Id]", false, false, false, false, null);
        Assert.That(sqlResult, Does.Contain("it''s"));

        // PostgreSQL
        var pgCmd = CreatePostgreSqlMockCommand(null, "(elem ->> 'id')::int4 AS \"id\"", "        \"id\"", null);
        var pgResult = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, pgCmd,
            "public", "t", "[{\"v\":\"it's\"}]", "\"id\"", false, false, false, false, null);
        Assert.That(pgResult, Does.Contain("it''s"));

        // MySQL
        var myCmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "", null)
        });
        var myResult = MergeScriptHelper.BuildMergeScript(Platform.MySQL, myCmd,
            "db", "t", "[{\"id\":1,\"v\":\"it's\"}]", "`id`", false, false, false, false, null);
        Assert.That(myResult, Does.Contain("it''s"));
    }

    #endregion

    #region PostgreSQL-specific: DisableRules and UpdateDescendents

    [Test]
    public void BuildMergeScript_PostgreSQL_WithDisableRules()
    {
        var cmd = Substitute.For<IDbCommand>();

        // Set up ExecuteReader to return rule names on first call (rules query),
        // then empty on subsequent calls
        var readerCallCount = 0;
        var ruleNames = new[] { "rule_insert_p1", "rule_insert_p2" };
        var ruleIndex = -1;

        cmd.ExecuteReader().Returns(ci =>
        {
            readerCallCount++;
            var reader = Substitute.For<IDataReader>();
            if (readerCallCount == 1)
            {
                // Rules query — return rule names
                reader.Read().Returns(_ => { ruleIndex++; return ruleIndex < ruleNames.Length; });
                reader.GetString(0).Returns(_ => ruleNames[ruleIndex]);
            }
            else
            {
                // Subsequent readers — return no rows
                reader.Read().Returns(false);
            }
            return reader;
        });

        // Set up ExecuteScalar sequence for PostgreSQL merge helper
        var scalarSequence = new object[]
        {
            null,  // GetUnsupportedColumnComments
            null,  // GetIdentityColumnAndSequence
            "(elem ->> 'id')::int4 AS \"id\"", // GetJsonColumnDefinitions
            "        \"id\""  // GetInsertColumns
        };
        var scalarIndex = 0;
        cmd.ExecuteScalar().Returns(_ => scalarIndex < scalarSequence.Length ? scalarSequence[scalarIndex++] : null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null,
            disableRules: true);

        // Rules are disabled/enabled individually by name (DISABLE RULE ALL is not valid PostgreSQL syntax)
        Assert.That(result, Does.Contain("DISABLE RULE \"rule_insert_p1\""));
        Assert.That(result, Does.Contain("DISABLE RULE \"rule_insert_p2\""));
        Assert.That(result, Does.Contain("ENABLE RULE \"rule_insert_p1\""));
        Assert.That(result, Does.Contain("ENABLE RULE \"rule_insert_p2\""));
        // Disable statements should be outside the DO $$ block (before it)
        Assert.That(result.IndexOf("DISABLE RULE"), Is.LessThan(result.IndexOf("DO $$")));
        // Enable statements should be outside the DO $$ block (after it)
        Assert.That(result.IndexOf("ENABLE RULE"), Is.GreaterThan(result.IndexOf("END $$ LANGUAGE plpgsql")));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_WithUpdateDescendents_OmitsOnlyKeyword()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\"",
            insertCols: "        \"id\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null,
            updateDescendents: true);

        Assert.That(result, Does.Not.Contain("ONLY "));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_WithoutUpdateDescendents_IncludesOnlyKeyword()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\"",
            insertCols: "        \"id\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null,
            updateDescendents: false);

        Assert.That(result, Does.Contain("ONLY "));
    }

    [TestCase("geometry", true)]
    [TestCase("geography", true)]
    [TestCase("point", true)]
    [TestCase("linestring", true)]
    [TestCase("polygon", true)]
    [TestCase("multipoint", true)]
    [TestCase("multilinestring", true)]
    [TestCase("multipolygon", true)]
    [TestCase("geometrycollection", true)]
    [TestCase("int4", false)]
    [TestCase("varchar", false)]
    [TestCase("text", false)]
    [TestCase("bytea", false)]
    public void IsGeometryTypePostgreSql_ReturnsExpected(string udtName, bool expected)
    {
        Assert.That(MergeScriptHelper.IsGeometryTypePostgreSql(udtName), Is.EqualTo(expected));
    }

    [TestCase("bytea", true)]
    [TestCase("int4", false)]
    [TestCase("varchar", false)]
    [TestCase("geometry", false)]
    public void IsByteaTypePostgreSql_ReturnsExpected(string udtName, bool expected)
    {
        Assert.That(MergeScriptHelper.IsByteaTypePostgreSql(udtName), Is.EqualTo(expected));
    }

    [TestCase("xml", true)]
    [TestCase("XML", true)]
    [TestCase("int4", false)]
    [TestCase("text", false)]
    [TestCase("varchar", false)]
    public void IsXmlTypePostgreSql_ReturnsExpected(string udtName, bool expected)
    {
        Assert.That(MergeScriptHelper.IsXmlTypePostgreSql(udtName), Is.EqualTo(expected));
    }

    [Test]
    public void GetJsonDataKeys_ReturnsUnionOfAllRowKeys()
    {
        var keys = MergeScriptHelper.GetJsonDataKeys("[{\"a\":1},{\"b\":2},{\"a\":3,\"c\":null}]");
        Assert.That(keys, Is.Not.Null);
        Assert.That(keys, Does.Contain("a"));
        Assert.That(keys, Does.Contain("b"));
        Assert.That(keys, Does.Contain("c"));
        Assert.That(keys.Count, Is.EqualTo(3));
    }

    [Test]
    public void GetJsonDataKeys_ReturnsNull_ForEmptyArray()
    {
        Assert.That(MergeScriptHelper.GetJsonDataKeys("[]"), Is.Null);
    }

    [Test]
    public void GetJsonDataKeys_ReturnsNull_ForNullOrEmpty()
    {
        Assert.That(MergeScriptHelper.GetJsonDataKeys(null), Is.Null);
        Assert.That(MergeScriptHelper.GetJsonDataKeys(""), Is.Null);
    }

    [Test]
    public void BuildMergeScript_MySQL_ExcludesColumnsNotInJsonData()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10L, 0L, null, "int", "", null),
            new("name", "varchar", 100L, null, null, null, "varchar(100)", "", null),
            new("rowguid", "char", 36L, null, null, null, "char(36)", "", null)
        });

        // Data only has "id" and "name" — "rowguid" should be excluded
        var result = MergeScriptHelper.BuildMergeScript(Platform.MySQL, cmd,
            "testdb", "testtable", "[{\"id\":1,\"name\":\"test\"}]", "`id`",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("`id`"));
        Assert.That(result, Does.Contain("`name`"));
        Assert.That(result, Does.Not.Contain("`rowguid`"));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_GeometryType_UsesStGeomFromText()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",ST_GeomFromText(elem ->> 'location') AS \"location\"",
            insertCols: "        \"id\",\r\n        \"location\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("ST_GeomFromText(elem ->> 'location') AS \"location\""));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_ByteaType_UsesDecodeBase64()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",decode(elem ->> 'photo', 'base64') AS \"photo\"",
            insertCols: "        \"id\",\r\n        \"photo\"",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("decode(elem ->> 'photo', 'base64') AS \"photo\""));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_WithGeometryUpdate_UsesStandardEquality()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",ST_GeomFromText(elem ->> 'location') AS \"location\"",
            insertCols: "        \"id\",\r\n        \"location\"",
            updateCols: "\"location\"");

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN MATCHED AND"));
        Assert.That(result, Does.Contain("UPDATE SET"));
        Assert.That(result, Does.Contain("\"location\" = \"Source\".\"location\""));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_XmlType_UsesTextCastForComparison()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",(elem ->> 'demographics')::xml AS \"demographics\"",
            insertCols: "        \"id\",\r\n        \"demographics\"",
            updateCols: "\"demographics\"",
            xmlCols: "demographics");

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN MATCHED AND"));
        Assert.That(result, Does.Contain("\"Target\".\"demographics\"::text = \"Source\".\"demographics\"::text"));
        Assert.That(result, Does.Contain("\"demographics\" = \"Source\".\"demographics\""));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_JsonType_UsesTextCastForComparison()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",(elem ->> 'metadata')::json AS \"metadata\"",
            insertCols: "        \"id\",\r\n        \"metadata\"",
            updateCols: "\"metadata\"",
            jsonTypeCols: new Dictionary<string, string> { { "metadata", "json" } });

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN MATCHED AND"));
        Assert.That(result, Does.Contain("\"Target\".\"metadata\"::text = \"Source\".\"metadata\"::text"));
        Assert.That(result, Does.Contain("\"metadata\" = \"Source\".\"metadata\""));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_JsonbType_UsesJsonbCastForComparison()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",(elem ->> 'settings')::jsonb AS \"settings\"",
            insertCols: "        \"id\",\r\n        \"settings\"",
            updateCols: "\"settings\"",
            jsonTypeCols: new Dictionary<string, string> { { "settings", "jsonb" } });

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN MATCHED AND"));
        Assert.That(result, Does.Contain("\"Target\".\"settings\"::jsonb = \"Source\".\"settings\"::jsonb"));
        Assert.That(result, Does.Contain("\"settings\" = \"Source\".\"settings\""));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_NonJsonColumn_UsesStandardComparison()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",(elem ->> 'name')::varchar(100) AS \"name\"",
            insertCols: "        \"id\",\r\n        \"name\"",
            updateCols: "\"name\"",
            jsonTypeCols: new Dictionary<string, string>());

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("WHEN MATCHED AND"));
        Assert.That(result, Does.Contain("\"Target\".\"name\" = \"Source\".\"name\""));
        Assert.That(result, Does.Not.Contain("::jsonb"));
        Assert.That(result, Does.Not.Contain("::text"));
    }

    #endregion

    #region SQL Server GEOMETRY Support Tests

    [Test]
    public void BuildMergeScript_SqlServer_GeometryColumn_UsesGeometryPrefix()
    {
        // GEOMETRY columns get G[ prefix just like GEOGRAPHY
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "geometry::STGeomFromText([Shape], [Shape.STSrid]) AS [Shape],[Id]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [Shape] NVARCHAR(4000), [Shape.STSrid] INT",
            insertCols: "        [Id],\r\n        [Shape]",
            updateCols: "G[Shape]");

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        // G[ prefix triggers .ToString() comparison
        Assert.That(result, Does.Contain("Target.[Shape].ToString() = Source.[Shape].ToString()"));
        // SET clause strips prefix
        Assert.That(result, Does.Contain("[Shape] = Source.[Shape]"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_GeometrySelectColumns_UsesGeometryType()
    {
        // Verify the SQL query for JSON select uses geometry::STGeomFromText
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "geometry::STGeomFromText([Shape], [Shape.STSrid]) AS [Shape]",
            needsIdentity: false,
            jsonColDefs: "           [Shape] NVARCHAR(4000), [Shape.STSrid] INT",
            insertCols: "        [Shape]",
            updateCols: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Contain("geometry::STGeomFromText([Shape], [Shape.STSrid]) AS [Shape]"));
    }

    [Test]
    public void GetJsonSelectColumnsSqlServer_QueryContainsGeometryAndGeography()
    {
        // Verify the SQL query text checks for both GEOGRAPHY and GEOMETRY
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("[Id]");

        // Use BuildMergeScript to trigger the SQL generation internally
        var cmd2 = CreateSqlServerMockCommand("[Id]", false, "           [Id] INT", "        [Id]", null);
        MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd2,
            "dbo", "TestTable", "[]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        // Inspect the generated SQL queries. The first call sets up GetJsonSelectColumns
        // which should contain IN ('GEOGRAPHY', 'GEOMETRY')
        var commandTexts = cmd2.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "set_CommandText")
            .Select(c => c.GetArguments()[0]?.ToString())
            .ToList();

        // Second query is GetJsonSelectColumns (first is GetUnsupportedColumnComments) - should contain both types
        Assert.That(commandTexts[1], Does.Contain("'GEOGRAPHY'"));
        Assert.That(commandTexts[1], Does.Contain("'GEOMETRY'"));
    }

    [Test]
    public void GetUpdateColumnsSqlServer_QueryContainsGeometryAndGeography()
    {
        var cmd = CreateSqlServerMockCommand("[Id]", false, "           [Id] INT", "        [Id]", "[Name]");
        MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[]", "[Id]",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        var commandTexts = cmd.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "set_CommandText")
            .Select(c => c.GetArguments()[0]?.ToString())
            .ToList();

        // Fifth query is GetUpdateColumns - should check for both GEOGRAPHY and GEOMETRY
        var updateQuery = commandTexts[4];
        Assert.That(updateQuery, Does.Contain("'GEOGRAPHY'"));
        Assert.That(updateQuery, Does.Contain("'GEOMETRY'"));
    }

    [Test]
    public void GetJsonColumnDefinitionsSqlServer_QueryContainsGeometryReplacement()
    {
        var cmd = CreateSqlServerMockCommand("[Id]", false, "           [Id] INT", "        [Id]", null);
        MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        var commandTexts = cmd.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "set_CommandText")
            .Select(c => c.GetArguments()[0]?.ToString())
            .ToList();

        // Fourth query is GetJsonColumnDefinitions - should contain GEOMETRY replacement
        var jsonColDefsQuery = commandTexts[3];
        Assert.That(jsonColDefsQuery, Does.Contain("'GEOMETRY'"));
        Assert.That(jsonColDefsQuery, Does.Contain("'GEOGRAPHY'"));
        // STSrid companion for both types
        Assert.That(jsonColDefsQuery, Does.Contain("IN ('GEOGRAPHY', 'GEOMETRY')"));
    }

    #endregion

    #region SQL Server DATETIMEOFFSET Support Tests

    [Test]
    public void BuildMergeScript_SqlServer_DateTimeOffsetColumn_UsesDPrefix()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id],[EventTime]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [EventTime] NVARCHAR(50)",
            insertCols: "        [Id],\r\n        [EventTime]",
            updateCols: "D[EventTime]");

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        // D[ prefix triggers CAST to NVARCHAR(50) comparison
        Assert.That(result, Does.Contain("CAST(Target.[EventTime] AS NVARCHAR(50)) = CAST(Source.[EventTime] AS NVARCHAR(50))"));
        // SET clause strips prefix
        Assert.That(result, Does.Contain("[EventTime] = Source.[EventTime]"));
    }

    [Test]
    public void GetUpdateColumnsSqlServer_QueryContainsDateTimeOffset()
    {
        var cmd = CreateSqlServerMockCommand("[Id]", false, "           [Id] INT", "        [Id]", "[Name]");
        MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[]", "[Id]",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        var commandTexts = cmd.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "set_CommandText")
            .Select(c => c.GetArguments()[0]?.ToString())
            .ToList();

        var updateQuery = commandTexts[4];
        Assert.That(updateQuery, Does.Contain("'DATETIMEOFFSET'"));
        Assert.That(updateQuery, Does.Contain("'D'"));
    }

    [Test]
    public void GetJsonColumnDefinitionsSqlServer_QueryContainsDateTimeOffsetReplacement()
    {
        var cmd = CreateSqlServerMockCommand("[Id]", false, "           [Id] INT", "        [Id]", null);
        MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        var commandTexts = cmd.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "set_CommandText")
            .Select(c => c.GetArguments()[0]?.ToString())
            .ToList();

        var jsonColDefsQuery = commandTexts[3];
        Assert.That(jsonColDefsQuery, Does.Contain("'DATETIMEOFFSET'"));
        Assert.That(jsonColDefsQuery, Does.Contain("'NVARCHAR(50)'"));
    }

    #endregion

    #region Helper Methods - SQL Server Mock

    /// <summary>
    /// Creates a SQL Server mock command. Call order for BuildMergeScript:
    /// 1. GetUnsupportedColumnComments, 2. GetJsonSelectColumns, 3. NeedsIdentityInsert,
    /// 4. GetJsonColumnDefinitions, 5. GetUpdateColumns (only if mergeUpdate=true), 6. GetInsertColumns
    /// </summary>
    private static IDbCommand CreateSqlServerMockCommand(
        string jsonSelectCols, bool needsIdentity, string jsonColDefs,
        string insertCols, string updateCols, string unsupportedComments = null)
    {
        var cmd = Substitute.For<IDbCommand>();
        // Build the sequence based on whether updateCols is provided
        var sequence = new List<object>
        {
            unsupportedComments,  // 1. GetUnsupportedColumnComments
            jsonSelectCols,       // 2. GetJsonSelectColumns
            needsIdentity,        // 3. NeedsIdentityInsert
            jsonColDefs           // 4. GetJsonColumnDefinitions
        };
        if (updateCols != null)
            sequence.Add(updateCols); // 5. GetUpdateColumns (only if mergeUpdate)
        sequence.Add(insertCols);     // GetInsertColumns

        var callCount = 0;
        cmd.ExecuteScalar().Returns(ci =>
        {
            var idx = callCount++;
            return idx < sequence.Count ? sequence[idx] : null;
        });
        return cmd;
    }

    #endregion

    #region Helper Methods - PostgreSQL Mock

    /// <summary>
    /// Creates a PostgreSQL mock command. Call order for BuildMergeScript:
    /// 1. GetUnsupportedColumnComments, 2. GetIdentityColumnAndSequence, 3. GetJsonColumnDefinitions,
    /// 4. GetUpdateColumns (only if mergeUpdate=true), 5. GetXmlColumns (if mergeUpdate),
    /// 6. GetJsonColumnsPostgreSql (ExecuteReader, if mergeUpdate), 7. GetInsertColumns
    /// </summary>
    private static IDbCommand CreatePostgreSqlMockCommand(
        string identAndSeq, string jsonColDefs, string insertCols, string updateCols,
        string xmlCols = null, Dictionary<string, string> jsonTypeCols = null,
        string unsupportedComments = null)
    {
        var cmd = Substitute.For<IDbCommand>();
        var sequence = new List<object>
        {
            unsupportedComments, // 1. GetUnsupportedColumnComments
            identAndSeq,        // 2. GetIdentityColumnAndSequence
            jsonColDefs          // 3. GetJsonColumnDefinitions
        };
        if (updateCols != null)
        {
            sequence.Add(updateCols); // 4. GetUpdateColumns (only if mergeUpdate)
            sequence.Add(xmlCols);    // 5. GetXmlColumns (only if mergeUpdate)
            // 6. GetJsonColumnsPostgreSql uses ExecuteReader (handled below)
        }
        sequence.Add(insertCols);     // GetInsertColumns

        var callCount = 0;
        cmd.ExecuteScalar().Returns(ci =>
        {
            var idx = callCount++;
            return idx < sequence.Count ? sequence[idx] : null;
        });

        // Set up ExecuteReader for GetJsonColumnsPostgreSql (only called when mergeUpdate)
        var jsonTypeEntries = jsonTypeCols ?? new Dictionary<string, string>();
        var jsonTypeList = jsonTypeEntries.ToList();
        var readerIndex = -1;
        var reader = Substitute.For<IDataReader>();
        reader.Read().Returns(ci =>
        {
            readerIndex++;
            return readerIndex < jsonTypeList.Count;
        });
        reader.GetString(0).Returns(ci => jsonTypeList[readerIndex].Key);
        reader.GetString(1).Returns(ci => jsonTypeList[readerIndex].Value);
        cmd.ExecuteReader().Returns(reader);

        return cmd;
    }

    #endregion

    #region Helper Methods - MySQL Mock

    private record MySqlColumnDef(
        string Name, string DataType, long? CharMaxLen,
        long? NumPrec, long? NumScale, int? DtPrec,
        string ColType, string Extra, string GenExpr);

    private static IDbCommand CreateMySqlMockCommand(MySqlColumnDef[] columns)
    {
        var cmd = Substitute.For<IDbCommand>();
        var reader = Substitute.For<IDataReader>();

        var currentIndex = -1;
        reader.Read().Returns(ci =>
        {
            currentIndex++;
            return currentIndex < columns.Length;
        });

        reader.GetOrdinal("COLUMN_NAME").Returns(0);
        reader.GetOrdinal("DATA_TYPE").Returns(1);
        reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH").Returns(2);
        reader.GetOrdinal("NUMERIC_PRECISION").Returns(3);
        reader.GetOrdinal("NUMERIC_SCALE").Returns(4);
        reader.GetOrdinal("DATETIME_PRECISION").Returns(5);
        reader.GetOrdinal("COLUMN_TYPE").Returns(6);
        reader.GetOrdinal("EXTRA").Returns(7);
        reader.GetOrdinal("GENERATION_EXPRESSION").Returns(8);

        reader.GetString(0).Returns(ci => columns[currentIndex].Name);
        reader.GetString(1).Returns(ci => columns[currentIndex].DataType);
        reader.IsDBNull(2).Returns(ci => !columns[currentIndex].CharMaxLen.HasValue);
        reader.GetInt64(2).Returns(ci => columns[currentIndex].CharMaxLen ?? 0);
        reader.IsDBNull(3).Returns(ci => !columns[currentIndex].NumPrec.HasValue);
        reader.GetInt64(3).Returns(ci => columns[currentIndex].NumPrec ?? 0);
        reader.IsDBNull(4).Returns(ci => !columns[currentIndex].NumScale.HasValue);
        reader.GetInt64(4).Returns(ci => columns[currentIndex].NumScale ?? 0);
        reader.IsDBNull(5).Returns(ci => !columns[currentIndex].DtPrec.HasValue);
        reader.GetInt32(5).Returns(ci => columns[currentIndex].DtPrec ?? 0);
        reader.GetString(6).Returns(ci => columns[currentIndex].ColType);
        reader.GetString(7).Returns(ci => columns[currentIndex].Extra);
        reader.IsDBNull(8).Returns(ci => columns[currentIndex].GenExpr == null);
        reader.GetString(8).Returns(ci => columns[currentIndex].GenExpr ?? "");

        cmd.ExecuteReader().Returns(reader);
        return cmd;
    }

    #endregion

    #region Unsupported Type Exclusion Tests

    [Test]
    public void BuildMergeScript_SqlServer_QueriesContainUnsupportedTypeFilter()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id],[Name]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT,\r\n           [Name] NVARCHAR(100)",
            insertCols: "        [Id],\r\n        [Name]",
            updateCols: "[Name]");

        MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1,\"Name\":\"test\"}]", "[Id]",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        // Verify that the unsupported type filter was included in queries
        var allQueries = cmd.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "set_CommandText")
            .Select(c => c.GetArguments()[0]?.ToString())
            .ToList();

        // GetJsonSelectColumns, GetJsonColumnDefinitions, GetInsertColumns, GetUpdateColumns should all have the filter
        var filteredQueries = allQueries.Where(q => q != null && q.Contains("sql_variant")).ToList();
        Assert.That(filteredQueries.Count, Is.GreaterThanOrEqualTo(4),
            "Expected at least 4 queries (select, json defs, insert, update) to include sql_variant filter");

        // Also check that timestamp and rowversion are excluded
        var timestampQueries = allQueries.Where(q => q != null && q.Contains("'timestamp'")).ToList();
        Assert.That(timestampQueries.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_QueriesContainUnsupportedTypeFilter()
    {
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\",(elem ->> 'name')::varchar(100) AS \"name\"",
            insertCols: "        \"id\",\r\n        \"name\"",
            updateCols: "\"name\"");

        MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[{\"id\":1,\"name\":\"test\"}]", "\"id\"",
            mergeUpdate: true, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        var allQueries = cmd.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "set_CommandText")
            .Select(c => c.GetArguments()[0]?.ToString())
            .ToList();

        // GetJsonColumnDefinitions, GetInsertColumns, GetUpdateColumns should all have the filter
        var filteredQueries = allQueries.Where(q => q != null && q.Contains("tsvector")).ToList();
        Assert.That(filteredQueries.Count, Is.GreaterThanOrEqualTo(3),
            "Expected at least 3 queries (json defs, insert, update) to include tsvector filter");

        // Check that money, box, circle, line, lseg, path are also excluded
        var moneyQueries = allQueries.Where(q => q != null && q.Contains("'money'")).ToList();
        Assert.That(moneyQueries.Count, Is.GreaterThanOrEqualTo(3));

        // Check composite type exclusion
        var compositeQueries = allQueries.Where(q => q != null && q.Contains("t.typtype = 'c'")).ToList();
        Assert.That(compositeQueries.Count, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_DoesNotExcludePointOrPolygon()
    {
        // The unsupported type filter constant should not contain point or polygon
        // (they overlap with PostGIS geometry types and must remain supported)
        var filter = MergeScriptHelper.PostgreSqlUnsupportedTypeFilter;

        // Extract just the NOT IN list from the filter
        var notInMatch = System.Text.RegularExpressions.Regex.Match(filter, @"NOT IN \([^)]+\)");
        Assert.That(notInMatch.Success, Is.True, "Expected a NOT IN clause in the filter");

        var notInList = notInMatch.Value;
        Assert.That(notInList, Does.Not.Contain("'point'"),
            "point should not be excluded — it overlaps with PostGIS");
        Assert.That(notInList, Does.Not.Contain("'polygon'"),
            "polygon should not be excluded — it overlaps with PostGIS");
    }

    [Test]
    public void SqlServerUnsupportedTypeFilter_ContainsAllExpectedTypes()
    {
        var filter = MergeScriptHelper.SqlServerUnsupportedTypeFilter;
        Assert.That(filter, Does.Contain("'sql_variant'"));
        Assert.That(filter, Does.Contain("'rowversion'"));
        Assert.That(filter, Does.Contain("'timestamp'"));
    }

    [Test]
    public void PostgreSqlUnsupportedTypeFilter_ContainsAllExpectedTypes()
    {
        var filter = MergeScriptHelper.PostgreSqlUnsupportedTypeFilter;
        Assert.That(filter, Does.Contain("'tsvector'"));
        Assert.That(filter, Does.Contain("'tsquery'"));
        Assert.That(filter, Does.Contain("'money'"));
        Assert.That(filter, Does.Contain("'box'"));
        Assert.That(filter, Does.Contain("'circle'"));
        Assert.That(filter, Does.Contain("'line'"));
        Assert.That(filter, Does.Contain("'lseg'"));
        Assert.That(filter, Does.Contain("'path'"));
        Assert.That(filter, Does.Contain("t.typtype = 'c'")); // composite type check
    }

    [Test]
    public void PostgreSqlUnsupportedTypeFilter_DoesNotContainPointOrPolygon()
    {
        var filter = MergeScriptHelper.PostgreSqlUnsupportedTypeFilter;
        Assert.That(filter, Does.Not.Contain("'point'"));
        Assert.That(filter, Does.Not.Contain("'polygon'"));
    }

    #endregion

    #region Unsupported Column Comments Tests

    [Test]
    public void GetUnsupportedColumnComments_SqlServer_ReturnsCommentLines()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("-- Column [VariantCol] skipped: sql_variant is not supported for data delivery\r\n-- Column [VersionCol] skipped: rowversion is not supported for data delivery");

        var result = MergeScriptHelper.GetUnsupportedColumnComments(Platform.SqlServer, cmd, "dbo", "TestTable");

        Assert.That(result, Does.Contain("-- Column [VariantCol] skipped: sql_variant is not supported for data delivery"));
        Assert.That(result, Does.Contain("-- Column [VersionCol] skipped: rowversion is not supported for data delivery"));
    }

    [Test]
    public void GetUnsupportedColumnComments_SqlServer_NoUnsupportedColumns_ReturnsEmpty()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns(null);

        var result = MergeScriptHelper.GetUnsupportedColumnComments(Platform.SqlServer, cmd, "dbo", "TestTable");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetUnsupportedColumnComments_PostgreSQL_ReturnsCommentLines()
    {
        var cmd = Substitute.For<IDbCommand>();
        cmd.ExecuteScalar().Returns("-- Column \"search_vector\" skipped: tsvector is not supported for data delivery\n-- Column \"price\" skipped: money is not supported for data delivery");

        var result = MergeScriptHelper.GetUnsupportedColumnComments(Platform.PostgreSQL, cmd, "public", "test_table");

        Assert.That(result, Does.Contain("tsvector is not supported for data delivery"));
        Assert.That(result, Does.Contain("money is not supported for data delivery"));
    }

    [Test]
    public void GetUnsupportedColumnComments_MySQL_ReturnsEmpty()
    {
        var cmd = Substitute.For<IDbCommand>();

        var result = MergeScriptHelper.GetUnsupportedColumnComments(Platform.MySQL, cmd, "testdb", "test_table");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void BuildMergeScript_SqlServer_WithUnsupportedColumns_PrependsComments()
    {
        var comments = "-- Column [VariantCol] skipped: sql_variant is not supported for data delivery";
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT",
            insertCols: "        [Id]",
            updateCols: null,
            unsupportedComments: comments);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.StartWith("-- Column [VariantCol] skipped: sql_variant is not supported for data delivery"));
        Assert.That(result, Does.Contain("MERGE INTO"));
    }

    [Test]
    public void BuildMergeScript_SqlServer_NoUnsupportedColumns_DoesNotPrependComments()
    {
        var cmd = CreateSqlServerMockCommand(
            jsonSelectCols: "[Id]",
            needsIdentity: false,
            jsonColDefs: "           [Id] INT",
            insertCols: "        [Id]",
            updateCols: null,
            unsupportedComments: null);

        var result = MergeScriptHelper.BuildMergeScript(Platform.SqlServer, cmd,
            "dbo", "TestTable", "[{\"Id\":1}]", "[Id]",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.Not.StartWith("--"));
        Assert.That(result, Does.Contain("MERGE INTO"));
    }

    [Test]
    public void BuildMergeScript_PostgreSQL_WithUnsupportedColumns_PrependsComments()
    {
        var comments = "-- Column \"search_vector\" skipped: tsvector is not supported for data delivery";
        var cmd = CreatePostgreSqlMockCommand(
            identAndSeq: null,
            jsonColDefs: "(elem ->> 'id')::int4 AS \"id\"",
            insertCols: "        \"id\"",
            updateCols: null,
            unsupportedComments: comments);

        var result = MergeScriptHelper.BuildMergeScript(Platform.PostgreSQL, cmd,
            "public", "test_table", "[]", "\"id\"",
            mergeUpdate: false, mergeDelete: false, disableTriggers: false,
            tokenizeScripts: false, mergeFilter: null);

        Assert.That(result, Does.StartWith("-- Column \"search_vector\" skipped: tsvector is not supported for data delivery"));
        Assert.That(result, Does.Contain("MERGE INTO"));
    }

    #endregion

    #region MySQL Fragment Method Tests

    [Test]
    public void GetJsonColumnDefinitions_MySQL_ReturnsJsonTableColumns()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10, 0, null, "int", "", null),
            new("name", "varchar", 100, null, null, null, "varchar(100)", "", null),
            new("location", "geometry", null, null, null, null, "geometry", "", null),
        });

        var result = MergeScriptHelper.GetJsonColumnDefinitions(Platform.MySQL, cmd, "testdb", "test_table");

        Assert.That(result, Does.Contain("`id` INT PATH '$.id'"));
        Assert.That(result, Does.Contain("`name` VARCHAR(100) PATH '$.name'"));
        Assert.That(result, Does.Contain("`location` TEXT PATH '$.location'"));
    }

    [Test]
    public void GetJsonSelectColumns_MySQL_ReturnsSelectExpressions()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10, 0, null, "int", "", null),
            new("location", "geometry", null, null, null, null, "geometry", "", null),
            new("data", "blob", 65535, null, null, null, "blob", "", null),
        });

        var result = MergeScriptHelper.GetJsonSelectColumns(Platform.MySQL, cmd, "testdb", "test_table");

        Assert.That(result, Does.Contain("`id`"));
        Assert.That(result, Does.Contain("ST_GeomFromText(`location`)"));
        Assert.That(result, Does.Contain("FROM_BASE64(`data`)"));
    }

    [Test]
    public void GetInsertColumns_MySQL_ReturnsBacktickQuotedList()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10, 0, null, "int", "", null),
            new("name", "varchar", 100, null, null, null, "varchar(100)", "", null),
        });

        var result = MergeScriptHelper.GetInsertColumns(Platform.MySQL, cmd, "testdb", "test_table");

        Assert.That(result, Is.EqualTo("`id`, `name`"));
    }

    [Test]
    public void GetUpdateColumns_MySQL_ReturnsColumnNames()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10, 0, null, "int", "", null),
            new("name", "varchar", 100, null, null, null, "varchar(100)", "", null),
        });

        var result = MergeScriptHelper.GetUpdateColumns(Platform.MySQL, cmd, "testdb", "test_table");

        Assert.That(result, Does.Contain("`id`"));
        Assert.That(result, Does.Contain("`name`"));
    }

    [Test]
    public void GetUpdateColumns_MySQL_MarksJsonColumnsWithPrefix()
    {
        var cmd = CreateMySqlMockCommand(new MySqlColumnDef[]
        {
            new("id", "int", null, 10, 0, null, "int", "", null),
            new("metadata", "json", null, null, null, null, "json", "", null),
        });

        var result = MergeScriptHelper.GetUpdateColumns(Platform.MySQL, cmd, "testdb", "test_table");

        Assert.That(result, Does.Contain("`id`"));
        Assert.That(result, Does.Contain("J[`metadata`]"));
    }

    #endregion

    #region MySQL GetMatchColumns Tests

    [Test]
    public void GetMatchColumns_MySQL_SingleKey()
    {
        var result = MergeScriptHelper.GetMatchColumns(Platform.MySQL, "id");
        Assert.That(result, Is.EqualTo("`Source`.`id` = `Target`.`id`"));
    }

    [Test]
    public void GetMatchColumns_MySQL_CompositeKey()
    {
        var result = MergeScriptHelper.GetMatchColumns(Platform.MySQL, "id,code");
        Assert.That(result, Does.Contain("`Source`.`id` = `Target`.`id`"));
        Assert.That(result, Does.Contain("`Source`.`code` = `Target`.`code`"));
        Assert.That(result, Does.Contain(" AND "));
    }

    [Test]
    public void GetMatchColumns_MySQL_NullableKey()
    {
        var result = MergeScriptHelper.GetMatchColumns(Platform.MySQL, "*nullable_col");
        Assert.That(result, Does.Contain("`Source`.`nullable_col` = `Target`.`nullable_col`"));
        Assert.That(result, Does.Contain("`Source`.`nullable_col` IS NULL AND `Target`.`nullable_col` IS NULL"));
    }

    [Test]
    public void GetMatchColumns_MySQL_TrimsBackticks()
    {
        var result = MergeScriptHelper.GetMatchColumns(Platform.MySQL, "`id`");
        Assert.That(result, Is.EqualTo("`Source`.`id` = `Target`.`id`"));
    }

    [Test]
    public void GetMatchColumns_MySQL_MixedKeys()
    {
        var result = MergeScriptHelper.GetMatchColumns(Platform.MySQL, "id,*nullable_col");
        Assert.That(result, Does.Contain("`Source`.`id` = `Target`.`id`"));
        Assert.That(result, Does.Contain("(`Source`.`nullable_col` = `Target`.`nullable_col` OR (`Source`.`nullable_col` IS NULL AND `Target`.`nullable_col` IS NULL))"));
    }

    #endregion

}
