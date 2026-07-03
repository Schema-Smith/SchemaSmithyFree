// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using log4net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.UnitTests;

/// <summary>
/// Unit tests for the `--Validate` gate switch in <see cref="Program.Main"/>. Target-less and
/// DB-less by design (mirrors <c>--TestConnection</c>/<c>--PreviewTargets</c> as an early-exit
/// gate) — exercises only the package-load path via mocked <see cref="IFile"/>/<see cref="IDirectory"/>,
/// no real disk I/O and no database connection. Setup mirrors <see cref="PreviewProvisioningTests"/>.
/// </summary>
[TestFixture]
public class ProgramValidateTests
{
    [Test]
    public void Validate_CleanPackage_ExitsZero()
    {
        var environment = RunValidate("CleanProduct", """
            {
              "Name": "CleanProduct",
              "Platform": "SqlServer",
              "ScriptFolders": []
            }
            """);

        environment.Received(1).Exit(0);
    }

    [Test]
    public void Validate_UnloadablePackage_ExitsTwo()
    {
        var environment = RunValidate("BadProduct", "{ this is not valid json");

        environment.Received(1).Exit(2);
    }

    /// <summary>
    /// Registers a mocked SchemaPackagePath directory containing the given Product.json content,
    /// then runs <see cref="Program.Main"/> with <c>--Validate</c> on the (mocked) command line.
    /// </summary>
    private static IEnvironment RunValidate(string schemaPackagePath, string productJson)
    {
        IEnvironment environment;
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Clear();
            LogFactory.Clear();

            var productPath = Path.Combine(schemaPackagePath, "Product.json");
            var file = Substitute.For<IFile>();
            var directory = Substitute.For<IDirectory>();

            file.Exists(schemaPackagePath).Returns(false); // not a zip
            directory.Exists(schemaPackagePath).Returns(true);
            file.Exists(productPath).Returns(true);
            file.ReadAllText(productPath).Returns(productJson);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["SchemaPackagePath"] = schemaPackagePath
                })
                .Build();

            environment = Substitute.For<IEnvironment>();
            environment.CommandLine.Returns("SchemaQuench.exe --Validate");

            FactoryContainer.Register<IConfigurationRoot>(config);
            FactoryContainer.Register(file);
            FactoryContainer.Register(directory);
            FactoryContainer.Register(environment);

            LogFactory.Register("ProgressLog", Substitute.For<ILog>());
            LogFactory.Register("ErrorLog", Substitute.For<ILog>());

            try
            {
                Program.Main(["--Validate"]);
            }
            finally
            {
                FactoryContainer.Clear();
                LogFactory.Clear();
            }
        }

        return environment;
    }
}
