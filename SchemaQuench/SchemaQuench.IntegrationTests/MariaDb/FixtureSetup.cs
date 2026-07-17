// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench.IntegrationTests.MariaDb;

[Category("MariaDb")]
[SetUpFixture]
public class FixtureSetup
{
    // Store a reference to the fully configured IConfigurationRoot so we can
    // re-register it after individual tests clear FactoryContainer.
    internal static IConfigurationRoot Config { get; private set; } = null!;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        // Delegate database creation to Schema.IntegrationTests.MariaDb.FixtureSetup,
        // which already creates unique test databases and maps MariaDB:* config to Target:* keys.
        Schema.IntegrationTests.MariaDb.FixtureSetup.EnsureInitialized();

        // Capture the config that was built and mutated by the Schema FixtureSetup.
        // ConfigHelper.GetAppSettingsAndUserSecrets registered it in FactoryContainer,
        // and Schema FixtureSetup added Target:* and ScriptTokens:* keys to it.
        Config = FactoryContainer.Resolve<IConfigurationRoot>();
    }

    // Delegate static properties to Schema.IntegrationTests.MariaDb.FixtureSetup
    // so existing test files that reference FixtureSetup.MainDb etc. continue working.
    public static string MainDb => Schema.IntegrationTests.MariaDb.FixtureSetup.MainDb;
    public static string SecondaryDb => Schema.IntegrationTests.MariaDb.FixtureSetup.SecondaryDb;
    public static string ConnectionString => Schema.IntegrationTests.MariaDb.FixtureSetup.ConnectionString;

    public static string GetMainDbConnectionString() => Schema.IntegrationTests.MariaDb.FixtureSetup.GetMainDbConnectionString();
    public static string GetSecondaryDbConnectionString() => Schema.IntegrationTests.MariaDb.FixtureSetup.GetSecondaryDbConnectionString();

    public static void EnsureInitialized() => Schema.IntegrationTests.MariaDb.FixtureSetup.EnsureInitialized();
}
