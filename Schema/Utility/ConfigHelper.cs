using System;
using System.Reflection;
using Schema.Isolators;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;

namespace Schema.Utility;

public static class ConfigHelper
{
    public static void ConfigureLog4Net()
    {
        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly());
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

    public static IConfigurationRoot GetAppSettingsAndUserSecrets(Action<string> logLine)
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            if (config != null) return config;

            var builder = new ConfigurationBuilder();
            var settingsFile = CommandLineParser.ValueOfSwitch("ConfigFile", null) ?? "appsettings.json";
            builder.AddJsonFile(settingsFile)
#if DEBUG
                .AddUserSecrets(Assembly.GetCallingAssembly())
#endif
                .AddEnvironmentVariables("QuenchSettings_");

            config = builder.Build();
            FactoryContainer.Register(config);
            ConfigurationLogger.LogConfiguration(config, logLine);

            return config;
        }
    }
}