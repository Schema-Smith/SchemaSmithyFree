// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench;

public static class Program
{
    public static void Main(string[] args)
    {
        CommandLineParser.HandleCommonSwitches("SchemaQuench", ToolSpecificSwitches);

        var skipKindlingForge = args.Length > 0 && args[0] == "SkipKindlingForge";
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        LogFactory.LogInitializer = ConfigHelper.ConfigureLog4Net;
        ConfigHelper.GetAppSettingsAndUserSecrets("SchemaQuench", LogFactory.GetLogger("ProgressLog").Info);

        Console.WriteLine(ProLicenseWrapper.GetFromFactory().GetLicenseDisplayText());

        new ProductQuench().QuenchProduct(skipKindlingForge);
        LogBackup.BackupLogsAndExit("SchemaQuench");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaQuench", e);
    }

    private static void ToolSpecificSwitches()
    {
        var proOptions = ProLicenseWrapper.GetFromFactory().FormatProOptions("SchemaQuench");
        if (!string.IsNullOrEmpty(proOptions))
        {
            Console.WriteLine();
            Console.WriteLine("Pro options:");
            Console.Write(proOptions);
        }
    }
}
