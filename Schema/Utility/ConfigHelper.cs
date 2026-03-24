// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System;

﻿using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;
using Schema.Isolators;

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

    public const string Platform = "SqlServer";
    private static readonly string[] ValidPlatforms = ["SqlServer", "MSSQL"];

    public static bool IsValidPlatform(string platform)
        => Array.Exists(ValidPlatforms, p => p.Equals(platform, StringComparison.OrdinalIgnoreCase));

    public static IConfigurationRoot GetAppSettingsAndUserSecrets(string app, Action<string> logLine)
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            if (config != null) return config;

            var settingsFile = CommandLineParser.ValueOfSwitch("ConfigFile", null) ?? $"{app}.settings.json";
            var basePath = Directory.GetCurrentDirectory();

            if (!File.Exists(Path.Combine(basePath, settingsFile)))
            {
                var appBasePath = AppContext.BaseDirectory;
                if (File.Exists(Path.Combine(appBasePath, settingsFile)))
                    basePath = appBasePath;
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(settingsFile, optional: true)
#if DEBUG
                .AddUserSecrets(Assembly.GetCallingAssembly(), optional: true)
#endif
                .AddEnvironmentVariables("SmithySettings_");

            config = builder.Build();
            FactoryContainer.Register(config);
            logLine?.Invoke($"{app} {Platform} Community");
            ConfigurationLogger.LogConfiguration(config, logLine);

            return config;
        }
    }
}