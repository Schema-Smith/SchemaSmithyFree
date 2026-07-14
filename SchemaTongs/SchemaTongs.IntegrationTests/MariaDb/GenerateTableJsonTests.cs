// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
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
}
