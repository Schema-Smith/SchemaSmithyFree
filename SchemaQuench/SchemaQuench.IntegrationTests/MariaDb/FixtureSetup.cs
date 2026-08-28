// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;

namespace SchemaQuench.IntegrationTests.MariaDb;

[Category("MariaDb")]
[SetUpFixture]
public class FixtureSetup
{
    /// <summary>
    /// MariaDB system-versioned tables arrive in 10.3. Below that <c>WITH SYSTEM VERSIONING</c> is a
    /// syntax error, not a degrade, so a test needing that state has nothing to exercise and must skip
    /// rather than fail -- the supported floor is 10.2.
    /// </summary>
    public static bool SupportsSystemVersioning(string serverVersion)
    {
        var parts = (serverVersion ?? "").Split('.', '-');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
            return true;   // Unrecognised: assume modern, so a parsing slip cannot silently skip the test.
        return major > 10 || (major == 10 && minor >= 3);
    }

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
