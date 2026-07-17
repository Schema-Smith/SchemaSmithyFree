// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using SchemaQuench.IntegrationTests.Shared;

namespace SchemaQuench.IntegrationTests.MySQL;

[Category("MySQL")]
public class FolderGateIntegrationTests : FolderGateIntegrationTestsSharedTests
{
    protected override Platform Platform => Platform.MySQL;
}
