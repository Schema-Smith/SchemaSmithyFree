using System;
using System.IO;
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
        XmlConfigurator.Configure(logRepository, new FileInfo("Log4Net.config"));
    }

    public static IConfigurationRoot GetAppSettingsAndUserSecrets(Action<string> logLine)
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            if (config != null) return config;

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json")
                .AddUserSecrets(Assembly.GetCallingAssembly())
                .AddEnvironmentVariables();

            config = builder.Build();
            FactoryContainer.Register(config);
            ConfigurationLogger.LogConfiguration(config, logLine);

            return config;
        }
    }
}