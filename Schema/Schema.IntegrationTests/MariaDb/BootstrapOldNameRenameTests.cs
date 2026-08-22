// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.Shared;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb binding of the shared Bootstrap OldName-rename integration tests.
/// </summary>
[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class BootstrapOldNameRenameTests : BootstrapOldNameRenameSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();

    // Below MariaDB 10.6's RENAME COLUMN floor (mirrors InvisibleColumnGatingTests / DefaultExpressionGatingTests).
    protected override int BelowRenameColumnFloorVersionOverride => 1005;
}
