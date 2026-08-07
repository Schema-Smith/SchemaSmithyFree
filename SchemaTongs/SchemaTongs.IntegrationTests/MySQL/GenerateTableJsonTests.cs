// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using SchemaTongs.IntegrationTests.Shared;

namespace SchemaTongs.IntegrationTests.MySQL;

[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class GenerateTableJsonTests : GenerateTableJsonSharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string ConfigPrefix => "MySQL";

    // MySQL 8.0.19+ drops integer display widths (`int`) and reports the default FK action as `NO ACTION`;
    // MySQL 5.7 keeps display widths (`int(11)`) and reports `RESTRICT` — same as MariaDB. Expect the legacy
    // form below the 8.0 floor.
    protected override string ExpectedIntegerType(string canonical) => ServerVersionNum >= 800
        ? canonical
        : canonical switch
        {
            "tinyint" => "tinyint(4)",
            "smallint" => "smallint(6)",
            "mediumint" => "mediumint(9)",
            "int" => "int(11)",
            "bigint" => "bigint(20)",
            _ => canonical
        };

    protected override string ExpectedDefaultFkAction => ServerVersionNum >= 800 ? "NO ACTION" : "RESTRICT";
}
