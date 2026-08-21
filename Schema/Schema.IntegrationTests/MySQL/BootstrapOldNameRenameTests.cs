// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.Shared;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// MySQL binding of the shared Bootstrap OldName-rename integration tests.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class BootstrapOldNameRenameTests : BootstrapOldNameRenameSharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();

    // Below MySQL 8.0's RENAME COLUMN floor (mirrors InvisibleColumnGatingTests / DefaultExpressionGatingTests).
    protected override int BelowRenameColumnFloorVersionOverride => 507;
}
