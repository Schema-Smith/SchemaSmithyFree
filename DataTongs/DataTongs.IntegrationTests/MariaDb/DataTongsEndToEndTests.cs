// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.MariaDb;
using DataTongs.IntegrationTests.Shared;

namespace DataTongs.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb binding of the shared DataTongs end-to-end integration tests.
/// </summary>
[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class DataTongsEndToEndTests : DataTongsEndToEndSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();
}
