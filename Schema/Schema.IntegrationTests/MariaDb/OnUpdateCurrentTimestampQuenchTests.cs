// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.Shared;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb binding of the shared column ON UPDATE CURRENT_TIMESTAMP round-trip and drift tests.
/// </summary>
[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class OnUpdateCurrentTimestampQuenchTests : OnUpdateCurrentTimestampQuenchSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();
}
