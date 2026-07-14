// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.MariaDb;
using SchemaTongs.IntegrationTests.Shared;

namespace SchemaTongs.IntegrationTests.MariaDb;

/// <summary>
/// MariaDb binding of the shared extraction / <c>SS-FILE-NAME-003</c> reconciliation tests.
/// </summary>
[Category("MariaDb")]
[TestFixture]
[Category("Integration")]
public class TableFileNameReconciliationTests : TableFileNameReconciliationSharedTests
{
    protected override Platform Platform => Platform.MariaDb;
    protected override string ConfigPrefix => "MariaDB";
    protected override string FixtureConnectionString => FixtureSetup.ConnectionString;
}
