// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using SchemaQuench.IntegrationTests.Shared;

namespace SchemaQuench.IntegrationTests.MariaDb;

[Category("MariaDb")]
public class FolderGateIntegrationTests : FolderGateIntegrationTestsSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
}
