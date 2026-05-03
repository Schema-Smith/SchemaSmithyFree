// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench.IntegrationTests.MySQL;

[Category("MySQL")]
[SetUpFixture]
public class FixtureSetup
{
    // Store a reference to the fully configured IConfigurationRoot so we can
    // re-register it after individual tests clear FactoryContainer.
    internal static IConfigurationRoot Config { get; private set; } = null!;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        // Delegate database creation to Schema.IntegrationTests.MySQL.FixtureSetup,
        // which already creates unique test databases and maps MySQL:* config to Target:* keys.
        Schema.IntegrationTests.MySQL.FixtureSetup.EnsureInitialized();

        // Capture the config that was built and mutated by the Schema FixtureSetup.
        // ConfigHelper.GetAppSettingsAndUserSecrets registered it in FactoryContainer,
        // and Schema FixtureSetup added Target:* and ScriptTokens:* keys to it.
        Config = FactoryContainer.Resolve<IConfigurationRoot>();
    }

    // Delegate static properties to Schema.IntegrationTests.MySQL.FixtureSetup
    // so existing test files that reference FixtureSetup.MainDb etc. continue working.
    public static string MainDb => Schema.IntegrationTests.MySQL.FixtureSetup.MainDb;
    public static string SecondaryDb => Schema.IntegrationTests.MySQL.FixtureSetup.SecondaryDb;
    public static string ConnectionString => Schema.IntegrationTests.MySQL.FixtureSetup.ConnectionString;

    public static string GetMainDbConnectionString() => Schema.IntegrationTests.MySQL.FixtureSetup.GetMainDbConnectionString();
    public static string GetSecondaryDbConnectionString() => Schema.IntegrationTests.MySQL.FixtureSetup.GetSecondaryDbConnectionString();

    public static void EnsureInitialized() => Schema.IntegrationTests.MySQL.FixtureSetup.EnsureInitialized();
}
