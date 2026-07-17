// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.MySQL;
using DataTongs.IntegrationTests.Shared;

namespace DataTongs.IntegrationTests.MySQL;

/// <summary>
/// MySQL binding of the shared DataTongs output integration tests.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class DataTongsOutputTests : DataTongsOutputSharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();
}
