// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using DataTongs.IntegrationTests.Shared;
using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.MySQL;

namespace DataTongs.IntegrationTests.MySQL;

/// <summary>
/// MySQL binding of the shared --ConfigureDataDelivery integration tests.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class ConfigureDataDeliveryTests : ConfigureDataDeliverySharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();
    protected override string ConfigPrefix => "MySQL";
}
