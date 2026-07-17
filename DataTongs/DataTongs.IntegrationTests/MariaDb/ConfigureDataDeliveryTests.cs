// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using DataTongs.IntegrationTests.Shared;
using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.MariaDb;

namespace DataTongs.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb binding of the shared --ConfigureDataDelivery integration tests.
/// </summary>
[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class ConfigureDataDeliveryTests : ConfigureDataDeliverySharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string MainDb => FixtureSetup.MainDb;
    protected override string MainConnectionString => FixtureSetup.GetMainDbConnectionString();
    protected override string ConfigPrefix => "MariaDB";
}
