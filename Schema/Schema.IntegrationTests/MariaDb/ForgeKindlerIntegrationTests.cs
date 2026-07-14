// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.Shared;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb binding of the shared ForgeKindler integration tests.
/// </summary>
[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class ForgeKindlerIntegrationTests : ForgeKindlerSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();

    // MariaDB's INFORMATION_SCHEMA reports an empty-string column default as the literal token `''`.
    protected override string ExpectedEmptyStringColumnDefault => "''";
}
