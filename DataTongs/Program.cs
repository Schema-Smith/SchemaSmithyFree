// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;
using Schema.Isolators;
using SchemaSmith.Pro;
using Schema.Utility;
using Microsoft.Extensions.Configuration;

namespace DataTongs;

public static class Program
{
    public static void Main(string[] args)
    {
        CommandLineParser.HandleCommonSwitches("DataTongs", ToolSpecificSwitches);

        // Register Community defaults — Pro packages register replacements via module initializers.
        FactoryContainer.Register<ISchemaLicense>(new NullSchemaLicense());

        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        LogFactory.LogInitializer = ConfigHelper.ConfigureLog4Net;
        ConfigHelper.GetAppSettingsAndUserSecrets("DataTongs", LogFactory.GetLogger("ProgressLog").Info);

        Console.WriteLine(ToolHelpFormatter.GetLicenseDisplayText());

        var platform = ResolvePlatform();

        new DataTongs(platform).CastData();
        LogBackup.BackupLogsAndExit("DataTongs");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("DataTongs", e);
    }

    internal static Platform ResolvePlatform()
    {
        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var platformValue = config["Source:Platform"] ?? config["Target:Platform"];
        if (string.IsNullOrWhiteSpace(platformValue))
            throw new Exception("Platform is required. Set 'Source:Platform' or 'Target:Platform' in DataTongs.settings.json.");

        return PlatformExtensions.ParsePlatform(platformValue);
    }

    private static void ToolSpecificSwitches()
    {
        var proOptions = ToolHelpFormatter.FormatProOptions("DataTongs");
        if (!string.IsNullOrEmpty(proOptions))
        {
            Console.WriteLine();
            Console.WriteLine("Pro options:");
            Console.Write(proOptions);
        }
    }
}
