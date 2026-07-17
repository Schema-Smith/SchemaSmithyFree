// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.MySQL;
using SchemaQuench.IntegrationTests.Shared;

namespace SchemaQuench.IntegrationTests.MySQL;

[Category("MySQL")]
[TestFixture]
public class DatabaseQuenchTests : DatabaseQuenchTestsSharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string SecondaryDb => FixtureSetup.SecondaryDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();
}
