// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.Shared;

namespace Schema.IntegrationTests.MySQL;

/// <summary>
/// MySQL binding of the shared end-to-end schema-extraction integration tests.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class EndToEndExtractionTests : EndToEndExtractionSharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();
}
