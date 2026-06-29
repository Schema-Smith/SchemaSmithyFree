// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Microsoft.Extensions.Configuration;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaShears;

public static class Program
{
    public static void Main(string[] args)
    {
        CommandLineParser.HandleCommonSwitches("SchemaShears", ToolSpecificSwitches);

        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        LogFactory.LogInitializer = ConfigHelper.ConfigureLog4Net;
        ConfigHelper.GetAppSettingsAndUserSecrets("SchemaShears", LogFactory.GetLogger("ProgressLog").Info);

        var config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
        var request = new PatchBuildRequest
        {
            SourcePath = CommandLineParser.ValueOfSwitch("Source", config["SourcePath"]),
            ManifestPath = CommandLineParser.ValueOfSwitch("Manifest", config["ManifestPath"]),
            AlwaysIncludePath = CommandLineParser.ValueOfSwitch("AlwaysInclude", config["AlwaysIncludePath"]),
            OutputPath = CommandLineParser.ValueOfSwitch("Output", config["OutputPath"]),
            Zip = CommandLineParser.ContainsSwitch("Zip") || string.Equals(config["Zip"], "true", StringComparison.OrdinalIgnoreCase)
        };

        new PatchBuilder().Build(request);
        LogBackup.BackupLogsAndExit("SchemaShears");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaShears", e);
    }

    private static void ToolSpecificSwitches()
    {
        Console.WriteLine("  --Source:<path>          Full product folder to carve the patch from.");
        Console.WriteLine("  --Manifest:<path>        File listing product-relative paths to include (one per line).");
        Console.WriteLine("  --AlwaysInclude:<path>   Optional file listing paths/folders always included.");
        Console.WriteLine("  --Output:<path>          Destination folder for the patch package.");
        Console.WriteLine("  --Zip                    Also produce <Output>.zip.");
    }
}
