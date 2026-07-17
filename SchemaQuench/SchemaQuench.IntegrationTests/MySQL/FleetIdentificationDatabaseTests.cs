// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using SchemaQuench.IntegrationTests.Shared;

namespace SchemaQuench.IntegrationTests.MySQL;

[Category("MySQL")]
[TestFixture]
[NonParallelizable]
public class FleetIdentificationDatabaseTests : FleetIdentificationDatabaseTestsSharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string ProductPlatformFolder => "MySQL";
}
