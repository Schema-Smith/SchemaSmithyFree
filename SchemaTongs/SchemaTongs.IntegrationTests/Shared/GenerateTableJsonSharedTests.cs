// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Domain.MySQL;
using Schema.Utility;
using Index = Schema.Domain.Index;

namespace SchemaTongs.IntegrationTests.Shared;

public abstract class GenerateTableJsonSharedTests
{
    protected abstract Platform Platform { get; }
    protected abstract string ConfigPrefix { get; }

    // Extraction faithfully reports each engine's native metadata form (correct for a single-platform
    // product; the quench comparison normalizes both to stay idempotent). Two forms diverge:
    // MySQL 8.0 drops integer display widths (`int`), MariaDB keeps them (`int(11)`); and the default
    // FK referential action reads `NO ACTION` on MySQL, `RESTRICT` on MariaDB (semantically identical).
    protected virtual string ExpectedIntegerType(string canonical) => canonical;
    protected virtual string ExpectedDefaultFkAction => "NO ACTION";

    private string _integrationDb = "";
    private string _connectionString;
    private string _testConnectionString;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("test", null);
        var connProps = ConnectionString.ReadProperties(config, $"{ConfigPrefix}:ConnectionProperties");
        _connectionString = ConnectionString.Build(Platform, config[$"{ConfigPrefix}:Server"], "mysql", config[$"{ConfigPrefix}:User"], config[$"{ConfigPrefix}:Password"], config[$"{ConfigPrefix}:Port"], connProps);
        _integrationDb = GenerateUniqueDBName("GenerateTableJson");

        CreateTestDatabases();

        _testConnectionString = ConnectionString.Build(Platform, config[$"{ConfigPrefix}:Server"], _integrationDb, config[$"{ConfigPrefix}:User"], config[$"{ConfigPrefix}:Password"], config[$"{ConfigPrefix}:Port"], connProps);
    }

    [Test]
    public void ShouldGenerateCorrectJsonForColumns()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`TestColumns` (
    `MyBit` BIT(1) NOT NULL,
    `MyInt` INT NULL,
    `MyDecimal` DECIMAL(10, 2) NOT NULL,
    `MyNumeric` NUMERIC(5, 3) NULL,
    `MyString` VARCHAR(200) NULL,
    `MyDateTime` DATETIME NULL,
    `MyTimestamp` TIMESTAMP NULL,
    `MyText` TEXT NULL,
    `MyBlob` BLOB NULL,
    `MyEnum` ENUM('a','b','c') NULL,
    `MySet` SET('x','y','z') NULL,
    `MyFloat` FLOAT NULL,
    `MySmallint` SMALLINT NULL,
    `MyTinyint` TINYINT NULL,
    `MyBigint` BIGINT NULL,
    `MyDate` DATE NULL,
    `MyTime` TIME NULL,
    `MyBitWithDefault` BIT(1) NOT NULL DEFAULT b'1',
    `MyIntWithDefault` INT NOT NULL DEFAULT 42,
    `MyDecimalWithDefault` DECIMAL(12, 4) NOT NULL DEFAULT 3.14,
    `MyIdentity` INT NOT NULL AUTO_INCREMENT,
    `MyMediumText` MEDIUMTEXT NULL,
    PRIMARY KEY (`MyIdentity`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
";
        cmd.ExecuteNonQuery();

        var result = GenerateTable(cmd, _integrationDb, "TestColumns");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("`TestColumns`"));
        Assert.That(result.Engine, Is.EqualTo("InnoDB"));
        Assert.That(result.CharacterSet, Is.EqualTo("utf8mb4"));
        Assert.That(result.Collation, Is.EqualTo("utf8mb4_unicode_ci"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(22));

        // Columns are ordered by ORDINAL_POSITION (creation order)
        AssertColumnProperties(result.Columns[0], "MyBit", "bit(1)", false, null);
        AssertColumnProperties(result.Columns[1], "MyInt", ExpectedIntegerType("int"), true, null);
        AssertColumnProperties(result.Columns[2], "MyDecimal", "decimal(10,2)", false, null);
        AssertColumnProperties(result.Columns[3], "MyNumeric", "decimal(5,3)", true, null);
        AssertColumnProperties(result.Columns[4], "MyString", "varchar(200)", true, null);
        AssertColumnProperties(result.Columns[5], "MyDateTime", "datetime", true, null);
        AssertColumnProperties(result.Columns[6], "MyTimestamp", "timestamp", true, null);
        AssertColumnProperties(result.Columns[7], "MyText", "text", true, null);
        AssertColumnProperties(result.Columns[8], "MyBlob", "blob", true, null);
        AssertColumnProperties(result.Columns[9], "MyEnum", "enum('a','b','c')", true, null);
        AssertColumnProperties(result.Columns[10], "MySet", "set('x','y','z')", true, null);
        AssertColumnProperties(result.Columns[11], "MyFloat", "float", true, null);
        AssertColumnProperties(result.Columns[12], "MySmallint", ExpectedIntegerType("smallint"), true, null);
        AssertColumnProperties(result.Columns[13], "MyTinyint", ExpectedIntegerType("tinyint"), true, null);
        AssertColumnProperties(result.Columns[14], "MyBigint", ExpectedIntegerType("bigint"), true, null);
        AssertColumnProperties(result.Columns[15], "MyDate", "date", true, null);
        AssertColumnProperties(result.Columns[16], "MyTime", "time", true, null);
        AssertColumnProperties(result.Columns[17], "MyBitWithDefault", "bit(1)", false, "b'1'");
        AssertColumnProperties(result.Columns[18], "MyIntWithDefault", ExpectedIntegerType("int"), false, "42");
        AssertColumnProperties(result.Columns[19], "MyDecimalWithDefault", "decimal(12,4)", false, "3.1400");

        var identityCol = (MySqlColumn)result.Columns[20];
        Assert.That(identityCol.Name, Is.EqualTo("`MyIdentity`"), "Name of MyIdentity");
        Assert.That(identityCol.AutoIncrement, Is.True, "AutoIncrement of MyIdentity");

        AssertColumnProperties(result.Columns[21], "MyMediumText", "mediumtext", true, null);

        Assert.That(result.ForeignKeys, Is.Not.Null);
        Assert.That(result.ForeignKeys, Has.Count.EqualTo(0));
        Assert.That(result.CheckConstraints, Is.Not.Null);
        Assert.That(result.CheckConstraints, Has.Count.EqualTo(0));

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForIndexes()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`TestIndexes` (
    `MyInt` INT NOT NULL,
    `MyBigInt` BIGINT NOT NULL,
    `MyString` VARCHAR(100) NULL,
    PRIMARY KEY (`MyInt`),
    UNIQUE KEY `UQ_TestIndexes_MyString` (`MyString`),
    INDEX `IX_TestIndexes_MyBigInt` (`MyBigInt`),
    INDEX `IX_TestIndexes_Composite` (`MyString`, `MyBigInt`)
) ENGINE=InnoDB;
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, _integrationDb, "TestIndexes");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("`TestIndexes`"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.Indexes, Is.Not.Null);
        Assert.That(result.Indexes, Has.Count.EqualTo(4));

        // Indexes are returned by GROUP BY on INDEX_NAME
        AssertIndexProperties(result.Indexes, "PRIMARY", true, true, true, "BTREE", "`MyInt`", true);
        AssertIndexProperties(result.Indexes, "IX_TestIndexes_MyBigInt", false, false, false, "BTREE", "`MyBigInt`", true);
        AssertIndexProperties(result.Indexes, "IX_TestIndexes_Composite", false, false, false, "BTREE", "`MyString`,`MyBigInt`", true);
        AssertIndexProperties(result.Indexes, "UQ_TestIndexes_MyString", false, true, true, "BTREE", "`MyString`", true);

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForForeignKeys()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`MyFKReferencedTable` (
    `Id` INT NOT NULL PRIMARY KEY,
    `RefCol` INT NOT NULL,
    UNIQUE KEY `IDX_RefKey` (`RefCol`)
) ENGINE=InnoDB;

CREATE TABLE `{_integrationDb}`.`MyFKTable` (
    `Id` INT NOT NULL PRIMARY KEY,
    `Col2` INT NULL,
    `Col3` INT NULL,
    CONSTRAINT `FK_MyFKTable_Col3_Ref_Id` FOREIGN KEY (`Col3`) REFERENCES `{_integrationDb}`.`MyFKReferencedTable` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_MyFKTable_Col2_Ref_RefCol` FOREIGN KEY (`Col2`) REFERENCES `{_integrationDb}`.`MyFKReferencedTable` (`RefCol`) ON UPDATE CASCADE
) ENGINE=InnoDB;
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, _integrationDb, "MyFKTable");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("`MyFKTable`"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.ForeignKeys, Is.Not.Null);
        Assert.That(result.ForeignKeys, Has.Count.EqualTo(2));

        var fk0 = FindForeignKey(result, "FK_MyFKTable_Col2_Ref_RefCol");
        Assert.That(fk0, Is.Not.Null, "FK_MyFKTable_Col2_Ref_RefCol should exist");
        Assert.That(fk0.Columns, Is.EqualTo("`Col2`"));
        Assert.That(fk0.RelatedTable, Is.EqualTo("`MyFKReferencedTable`"));
        Assert.That(fk0.RelatedColumns, Is.EqualTo("`RefCol`"));
        Assert.That(fk0.DeleteAction, Is.EqualTo(ExpectedDefaultFkAction));
        Assert.That(fk0.UpdateAction, Is.EqualTo("CASCADE"));

        var fk1 = FindForeignKey(result, "FK_MyFKTable_Col3_Ref_Id");
        Assert.That(fk1, Is.Not.Null, "FK_MyFKTable_Col3_Ref_Id should exist");
        Assert.That(fk1.Columns, Is.EqualTo("`Col3`"));
        Assert.That(fk1.RelatedTable, Is.EqualTo("`MyFKReferencedTable`"));
        Assert.That(fk1.RelatedColumns, Is.EqualTo("`Id`"));
        Assert.That(fk1.DeleteAction, Is.EqualTo("CASCADE"));
        Assert.That(fk1.UpdateAction, Is.EqualTo(ExpectedDefaultFkAction));

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForCheckConstraint()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`TestChecks` (
    `MyInt` INT NOT NULL,
    `MyBigInt` BIGINT NOT NULL,
    `MyString` VARCHAR(100) NULL,
    CONSTRAINT `CK_TestChecks_MyInt` CHECK (`MyInt` < `MyBigInt`),
    CONSTRAINT `CK_TestChecks_MyBigInt` CHECK (`MyBigInt` > `MyInt`)
) ENGINE=InnoDB;
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, _integrationDb, "TestChecks");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("`TestChecks`"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(3));
        Assert.That(result.CheckConstraints, Is.Not.Null);
        Assert.That(result.CheckConstraints, Has.Count.EqualTo(2));

        var ck0 = FindCheckConstraint(result, "CK_TestChecks_MyBigInt");
        Assert.That(ck0, Is.Not.Null, "CK_TestChecks_MyBigInt should exist");
        Assert.That(ck0.Expression, Does.Contain("`MyBigInt`"));
        Assert.That(ck0.Expression, Does.Contain("`MyInt`"));

        var ck1 = FindCheckConstraint(result, "CK_TestChecks_MyInt");
        Assert.That(ck1, Is.Not.Null, "CK_TestChecks_MyInt should exist");
        Assert.That(ck1.Expression, Does.Contain("`MyInt`"));
        Assert.That(ck1.Expression, Does.Contain("`MyBigInt`"));

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForFullTextIndexes()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`TestFullText` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Title` VARCHAR(200) NULL,
    `Body` TEXT NULL,
    PRIMARY KEY (`Id`),
    FULLTEXT KEY `FT_Title` (`Title`),
    FULLTEXT KEY `FT_TitleBody` (`Title`, `Body`)
) ENGINE=InnoDB;
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, _integrationDb, "TestFullText");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("`TestFullText`"));
        Assert.That(result.FullTextIndexes, Is.Not.Null);
        Assert.That(result.FullTextIndexes, Has.Count.EqualTo(2));

        var ft0 = FindFullTextIndex(result, "FT_Title");
        Assert.That(ft0, Is.Not.Null, "FT_Title should exist");
        Assert.That(ft0.Columns, Is.EqualTo("`Title`"));

        var ft1 = FindFullTextIndex(result, "FT_TitleBody");
        Assert.That(ft1, Is.Not.Null, "FT_TitleBody should exist");
        Assert.That(ft1.Columns, Is.EqualTo("`Title`,`Body`"));

        // Fulltext indexes should NOT appear in regular Indexes list
        foreach (var idx in result.Indexes)
        {
            var mysqlIdx = (MySqlIndex)idx;
            Assert.That(mysqlIdx.IndexType, Is.Not.EqualTo("FULLTEXT"), $"Index {mysqlIdx.Name} should not be FULLTEXT in regular indexes");
        }

        conn.Close();
    }

    [Test]
    public void ShouldGenerateCorrectJsonForGeneratedColumns()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`TestGenerated` (
    `Price` DECIMAL(10,2) NOT NULL,
    `Quantity` INT NOT NULL,
    `Total` DECIMAL(10,2) GENERATED ALWAYS AS (`Price` * `Quantity`) STORED,
    `DoubleTotal` DECIMAL(10,2) GENERATED ALWAYS AS (`Total` * 2) VIRTUAL
) ENGINE=InnoDB;
";
        cmd.ExecuteNonQuery();
        var result = GenerateTable(cmd, _integrationDb, "TestGenerated");
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("`TestGenerated`"));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.EqualTo(4));

        var totalCol = (MySqlColumn)result.Columns[2];
        Assert.That(totalCol.Name, Is.EqualTo("`Total`"));
        Assert.That(totalCol.Generated, Is.EqualTo("STORED"));
        Assert.That(totalCol.GenerationExpression, Is.Not.Null.And.Not.Empty);

        var doubleTotalCol = (MySqlColumn)result.Columns[3];
        Assert.That(doubleTotalCol.Name, Is.EqualTo("`DoubleTotal`"));
        Assert.That(doubleTotalCol.Generated, Is.EqualTo("VIRTUAL"));
        Assert.That(doubleTotalCol.GenerationExpression, Is.Not.Null.And.Not.Empty);

        conn.Close();
    }

    private void AssertColumnProperties(Column column, string name, string dataType, bool nullable, string defaultValue)
    {
        var mysqlColumn = (MySqlColumn)column;
        Assert.That(mysqlColumn.Name, Is.EqualTo($"`{name}`"), $"Name of {name}");
        Assert.That(mysqlColumn.DataType, Is.EqualTo(dataType), $"Type of {name}");
        Assert.That(mysqlColumn.Nullable, Is.EqualTo(nullable), $"Nullability of {name}");
        Assert.That(mysqlColumn.Default, Is.EqualTo(defaultValue), $"Default of {name}");
    }

    private void AssertIndexProperties(List<Index> indexes, string name, bool isPrimaryKey, bool isUnique, bool isUniqueConstraint, string indexType, string columns, bool visible)
    {
        var index = indexes.Find(i => i.Name == name || i.Name == $"`{name}`");
        Assert.That(index, Is.Not.Null, $"Index {name} should exist");
        var mysqlIndex = (MySqlIndex)index;
        Assert.That(mysqlIndex.PrimaryKey, Is.EqualTo(isPrimaryKey), $"PrimaryKey of {name}");
        Assert.That(mysqlIndex.Unique, Is.EqualTo(isUnique), $"Unique of {name}");
        Assert.That(mysqlIndex.UniqueConstraint, Is.EqualTo(isUniqueConstraint), $"UniqueConstraint of {name}");
        Assert.That(mysqlIndex.IndexType, Is.EqualTo(indexType), $"IndexType of {name}");
        Assert.That(mysqlIndex.IndexColumns, Is.EqualTo(columns), $"IndexColumns of {name}");
        Assert.That(mysqlIndex.Visible, Is.EqualTo(visible), $"Visible of {name}");
    }

    private static MySqlForeignKey FindForeignKey(MySqlTable table, string name)
    {
        return (MySqlForeignKey)table.ForeignKeys.Find(fk => fk.Name == name || fk.Name == $"`{name}`");
    }

    private static CheckConstraint FindCheckConstraint(MySqlTable table, string name)
    {
        return table.CheckConstraints.Find(ck => ck.Name == name || ck.Name == $"`{name}`");
    }

    private static FullTextIndex FindFullTextIndex(MySqlTable table, string name)
    {
        return table.FullTextIndexes.Find(ft => ft.Name == name || ft.Name == $"`{name}`");
    }

    [Test]
    public void ShouldEmitPreventDropOnlyForProtectedTables()
    {
        // #270 round-trip: a table protected in the source DB (sticky PreventDrop marker set) must extract with
        // "PreventDrop": true so an extract -> re-deploy preserves protection; an unprotected sibling omits the key.
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`ProtectedExtractTable` (`Id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
CREATE TABLE `{_integrationDb}`.`UnprotectedExtractTable` (`Id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;

INSERT INTO SchemaSmith_ProductOwnership (ObjectType, ObjectSchema, ObjectName, ProductName, TemplateName, PreventDrop)
VALUES ('TABLE', '{_integrationDb}', 'ProtectedExtractTable', 'TestProduct', '', 1);
";
        cmd.ExecuteNonQuery();

        var protectedJson = GenerateTableJson(cmd, _integrationDb, "ProtectedExtractTable");
        var protectedTable = (MySqlTable)PlatformDeserializer.DeserializeTable(protectedJson, Platform);
        var unprotectedJson = GenerateTableJson(cmd, _integrationDb, "UnprotectedExtractTable");
        var unprotectedTable = (MySqlTable)PlatformDeserializer.DeserializeTable(unprotectedJson, Platform);

        Assert.Multiple(() =>
        {
            Assert.That(protectedTable.PreventDrop, Is.True, "Protected table must round-trip PreventDrop:true.");
            Assert.That(protectedJson, Does.Contain("PreventDrop"), "Extracted JSON for a protected table must carry the PreventDrop marker.");
            Assert.That(unprotectedTable.PreventDrop, Is.False, "Unprotected table must deserialize to PreventDrop:false.");
            Assert.That(unprotectedJson, Does.Not.Contain("PreventDrop"), "Extracted JSON for an unprotected table must omit the PreventDrop key.");
        });

        conn.Close();
    }

    private string GenerateTableJson(IDbCommand cmd, string schema, string table)
    {
        cmd.CommandText = $"CALL SchemaSmith_GenerateTableJSON('{schema}', '{table}')";
        using var reader = cmd.ExecuteReader();

        var tableJson = string.Empty;
        while (reader.Read())
        {
            tableJson += reader.GetString(0);
        }

        return tableJson;
    }

    private MySqlTable GenerateTable(IDbCommand cmd, string schema, string table)
    {
        return (MySqlTable)PlatformDeserializer.DeserializeTable(GenerateTableJson(cmd, schema, table), Platform);
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        DropTestDatabases();
    }

    private void CreateTestDatabases()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE `{_integrationDb}`";
        cmd.ExecuteNonQuery();

        conn.ChangeDatabase(_integrationDb);
        ForgeKindler.KindleTheForge(cmd, Platform);

        conn.Close();
    }

    private static string GenerateUniqueDBName(string dbName)
    {
        dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
        var uniqueSegment = Guid.NewGuid().ToString().Replace(" - ", "_").Substring(0, 8);
        return $"{dbName}_Test_{DateTime.Now:yyyyMMdd_HHmmss}_{uniqueSegment}";
    }

    private void DropTestDatabases()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        DropOneDatabase(cmd, _integrationDb);

        conn.Close();
    }

    private static void DropOneDatabase(IDbCommand cmd, string dbName)
    {
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{dbName}`";
        cmd.ExecuteNonQuery();
    }
}
