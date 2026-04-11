// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;
using Schema.Isolators;
using SchemaSmith.Pro;

namespace Schema.Utility;

public static class ConfigHelper
{
    public static void ConfigureLog4Net()
    {
        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly());
        var toolDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        GlobalContext.Properties["LogPath"] = (CommandLineParser.ValueOfSwitch("LogPath", null) ?? toolDir).TrimEnd('\\', '/');
        try
        {
            using var configStream = ResourceLoader.Load("Log4Net.config").ToStream();
            XmlConfigurator.Configure(logRepository, configStream);
        }
        catch
        {
            XmlConfigurator.Configure(logRepository); // use default config if not embedded
        }
    }

    // NOTE: No Platform constant — unified tools read platform from Product.Platform

    public static IConfigurationRoot GetAppSettingsAndUserSecrets(string app, Action<string> logLine)
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            if (config != null) return config;

            var basePath = Directory.GetCurrentDirectory();
            var settingsFile = CommandLineParser.ValueOfSwitch("ConfigFile", null) ?? $"{app}.settings.json";
            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath);

            // Check AppContext.BaseDirectory as fallback (test runners may not set CWD to the output directory)
            var appBasePath = AppContext.BaseDirectory;
            if (!File.Exists(Path.Combine(basePath, settingsFile)) && File.Exists(Path.Combine(appBasePath, settingsFile)))
                builder.SetBasePath(appBasePath);

            builder.AddJsonFile(settingsFile, optional: true)
#if DEBUG
                .AddUserSecrets(Assembly.GetCallingAssembly(), optional: true)
#endif
                .AddEnvironmentVariables("SmithySettings_");

            config = builder.Build();
            FactoryContainer.Register(config);
            logLine?.Invoke(app);
            // License display text may be multi-line; log each line as its own
            // progress entry so timestamps line up and Pro license blocks render cleanly.
            var licenseText = ProServices.GetLicenseDisplayText();
            if (!string.IsNullOrEmpty(licenseText))
            {
                foreach (var line in licenseText.Split('\n'))
                    logLine?.Invoke(line.TrimEnd('\r'));
            }

            ConfigurationLogger.LogConfiguration(config, logLine);

            return config;
        }
    }
}
