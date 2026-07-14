// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using SchemaQuench.IntegrationTests.Shared;

namespace SchemaQuench.IntegrationTests.MariaDb;

[TestFixture]
[Category("MariaDb")]
[NonParallelizable]
public class PreFlight_PreviewTargetsTests : PreFlight_PreviewTargetsTestsSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string ProductPlatformFolder => "MariaDb";
}
