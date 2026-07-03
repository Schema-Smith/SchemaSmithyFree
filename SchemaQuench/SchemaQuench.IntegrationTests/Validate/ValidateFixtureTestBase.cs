// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Schema.Isolators;
using SchemaQuench.Validation;

namespace SchemaQuench.IntegrationTests.Validate;

/// <summary>
/// Shared plumbing for `--Validate` fixture tests: real on-disk packages under
/// <c>Fixtures/Validate/&lt;Case&gt;/&lt;Engine&gt;</c> (copied next to the test assembly — see
/// the csproj's <c>Fixtures\**\*</c> content glob), driven the same way <see cref="Program.Main"/>
/// drives it (<see cref="SchemaPackageValidator"/> over <see cref="PackageLoader.LoadPackage"/>).
/// No database connection — <c>--Validate</c> is static analysis only.
/// <para>
/// Mirrors the save/restore-under-lock pattern <c>ProductQuenchTests</c> (MySQL) already uses for
/// tests that temporarily register their own <see cref="IConfigurationRoot"/>: the whole test body
/// holds <see cref="FactoryContainer.SharedLockObject"/> so a platform fixture's config (set up by
/// its own <c>[SetUpFixture]</c> for the DB-backed suites) is never observed half-swapped by a
/// concurrently-scheduled test, and is restored afterward so that fixture keeps working.
/// </para>
/// </summary>
public abstract class ValidateFixtureTestBase
{
    private IConfigurationRoot _savedConfig;
    private bool _lockTaken;

    [SetUp]
    public void ValidateFixtureSetUp()
    {
        _lockTaken = false;
        Monitor.Enter(FactoryContainer.SharedLockObject, ref _lockTaken);
        _savedConfig = FactoryContainer.Resolve<IConfigurationRoot>();
    }

    [TearDown]
    public void ValidateFixtureTearDown()
    {
        try
        {
            if (_savedConfig != null)
                FactoryContainer.Register(_savedConfig);
            else
                FactoryContainer.Unregister<IConfigurationRoot>();
            _savedConfig = null;
        }
        finally
        {
            if (_lockTaken)
            {
                Monitor.Exit(FactoryContainer.SharedLockObject);
                _lockTaken = false;
            }
        }
    }

    /// <summary>
    /// Absolute path to <c>Fixtures/Validate/&lt;caseName&gt;/&lt;engine&gt;</c> next to this test
    /// assembly (copied there at build time by the csproj's content glob).
    /// </summary>
    protected static string FixturePath(string caseName, string engine)
    {
        var assemblyLocation = Path.GetDirectoryName(typeof(ValidateFixtureTestBase).Assembly.Location);
        return Path.Combine(assemblyLocation!, "Fixtures", "Validate", caseName, engine);
    }

    /// <summary>
    /// Registers a fresh in-memory config pointing SchemaPackagePath at the fixture, then runs
    /// `--Validate` exactly the way <see cref="Program.Main"/> does.
    /// </summary>
    protected static ValidationResult RunValidate(string packagePath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("SchemaPackagePath", packagePath)
            })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(config);

        var validator = new SchemaPackageValidator(PackageLoader.LoadPackage, ValidationCheckRegistry.Default());
        return validator.Validate(packagePath);
    }
}
