// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using System.Linq;
using Schema.DataAccess;
using Schema.Domain.MariaDb;
using Schema.Domain;
using SchemaTongs.IntegrationTests.Shared;

namespace SchemaTongs.IntegrationTests.MariaDb;

[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class GenerateTableJsonTests : GenerateTableJsonSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string ConfigPrefix => "MariaDB";

    // MariaDB keeps integer display widths and reports the default FK action as RESTRICT.
    protected override string ExpectedIntegerType(string canonical) => canonical switch
    {
        "tinyint" => "tinyint(4)",
        "smallint" => "smallint(6)",
        "mediumint" => "mediumint(9)",
        "int" => "int(11)",
        "bigint" => "bigint(20)",
        _ => canonical
    };

    protected override string ExpectedDefaultFkAction => "RESTRICT";

    /// <summary>
    /// A system-versioned table must EXTRACT. Before this, it was silently omitted from the package --
    /// no error, no warning -- because the extraction filters accepted only TABLE_TYPE = 'BASE TABLE'
    /// and MariaDB reports such a table as 'SYSTEM VERSIONED'.
    /// <para>Both authoring forms are covered deliberately. The implicit form
    /// (<c>WITH SYSTEM VERSIONING</c>, no declared period columns) exposes nothing but its TABLE_TYPE --
    /// no PERIODS row, no period columns, empty CREATE_OPTIONS -- so any detection keyed on periods or
    /// columns sees it as an ordinary table. Testing only the explicit form would pass while the
    /// commoner form stayed broken.</para>
    /// <para>MariaDB-only: MySQL has no system versioning at any version, which is why this lives here
    /// rather than in the shared fixture.</para>
    /// </summary>
    [Test]
    public void ShouldExtractSystemVersionedTables_AndExcludeTheirEngineOwnedPeriodColumns()
    {
        using var conn = DbConnectionFactory.ForPlatform(Platform).GetDbConnection(_testConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        // System versioning arrives in 10.3; the supported floor is 10.2, where the CREATE below is a
        // syntax error rather than a degrade, so there is no state to exercise.
        if (ServerVersionNum < 1003)
            Assert.Ignore($"MariaDB {ServerVersionNum} predates system-versioned tables (10.3).");

        cmd.CommandText = $@"
CREATE TABLE `{_integrationDb}`.`ImplicitVersioned` (
    `Id` INT NOT NULL PRIMARY KEY, `Val` VARCHAR(20) NULL
) ENGINE=InnoDB WITH SYSTEM VERSIONING;

CREATE TABLE `{_integrationDb}`.`ExplicitVersioned` (
    `Id` INT NOT NULL PRIMARY KEY, `Val` VARCHAR(20) NULL,
    `RowStart` TIMESTAMP(6) GENERATED ALWAYS AS ROW START,
    `RowEnd` TIMESTAMP(6) GENERATED ALWAYS AS ROW END,
    PERIOD FOR SYSTEM_TIME(`RowStart`, `RowEnd`)
) ENGINE=InnoDB WITH SYSTEM VERSIONING;

CREATE TABLE `{_integrationDb}`.`NotVersioned` (`Id` INT NOT NULL PRIMARY KEY) ENGINE=InnoDB;
";
        cmd.ExecuteNonQuery();

        var implicitJson = GenerateTableJson(cmd, _integrationDb, "ImplicitVersioned");
        var explicitJson = GenerateTableJson(cmd, _integrationDb, "ExplicitVersioned");
        var plainJson = GenerateTableJson(cmd, _integrationDb, "NotVersioned");

        // Asserted before deserializing: an omitted table yields empty/no JSON, and a deserialize
        // failure there would report as a parse error rather than as "the table was dropped".
        Assert.Multiple(() =>
        {
            Assert.That(implicitJson, Is.Not.Null.And.Not.Empty,
                "The implicit form extracted to nothing -- this is the silent omission the fix exists to stop.");
            Assert.That(explicitJson, Is.Not.Null.And.Not.Empty,
                "The explicit form extracted to nothing -- this is the silent omission the fix exists to stop.");
        });

        var implicitTable = (MariaDbTable)PlatformDeserializer.DeserializeTable(implicitJson, Platform);
        var explicitTable = (MariaDbTable)PlatformDeserializer.DeserializeTable(explicitJson, Platform);
        var plainTable = (MariaDbTable)PlatformDeserializer.DeserializeTable(plainJson, Platform);

        Assert.Multiple(() =>
        {
            Assert.That(implicitTable.IsSystemVersioned, Is.True,
                "TABLE_TYPE is the only signal the implicit form gives, and it must be enough.");
            Assert.That(explicitTable.IsSystemVersioned, Is.True);
            Assert.That(plainTable.IsSystemVersioned, Is.False,
                "An ordinary table must not be reported as versioned.");
            Assert.That(plainJson, Does.Not.Contain("IsSystemVersioned"),
                "An ordinary table's package must omit the key entirely rather than carry it as false.");

            // The trap: these are generated and maintained by the engine. Extracted as ordinary columns,
            // the apply path would try to manage them and re-deploy would attempt DDL the engine owns.
            var explicitColumns = explicitTable.Columns.Select(c => c.Name.Replace("`", "")).ToList();
            Assert.That(explicitColumns, Does.Not.Contain("RowStart"),
                "The row-start period column is engine-owned and must not be extracted as a user column.");
            Assert.That(explicitColumns, Does.Not.Contain("RowEnd"),
                "The row-end period column is engine-owned and must not be extracted as a user column.");
            Assert.That(explicitColumns, Does.Contain("Id"),
                "Excluding the period columns must not take the real columns with them.");
            Assert.That(explicitColumns, Does.Contain("Val"));
        });

        conn.Close();
    }

}
