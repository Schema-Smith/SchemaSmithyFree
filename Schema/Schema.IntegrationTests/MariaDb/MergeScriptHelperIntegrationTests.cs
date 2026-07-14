// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.Shared;

namespace Schema.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb binding of the shared MergeScriptHelper integration tests.
/// </summary>
[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class MergeScriptHelperIntegrationTests : MergeScriptHelperSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();

    // MariaDB's JSON_TABLE coerces JSON numeric 0 to YEAR 2000 (MySQL yields 0).
    protected override int ExpectedYearZeroValue => 2000;
}
