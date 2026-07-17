// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Domain;
using Schema.IntegrationTests.MySQL;
using SchemaTongs.IntegrationTests.Shared;

namespace SchemaTongs.IntegrationTests.MySQL;

/// <summary>
/// MySQL binding of the shared extraction / <c>SS-FILE-NAME-003</c> reconciliation tests.
/// </summary>
[Category("MySQL")]
[TestFixture]
[Category("Integration")]
public class TableFileNameReconciliationTests : TableFileNameReconciliationSharedTests
{
    protected override Platform Platform => Platform.MySQL;
    protected override string ConfigPrefix => "MySQL";
    protected override string FixtureConnectionString => FixtureSetup.ConnectionString;
}
