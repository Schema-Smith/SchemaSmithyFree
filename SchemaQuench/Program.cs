// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Checkpointing;
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

        RegisterCheckpointing();

        new ProductQuench().QuenchProduct(skipKindlingForge);
        LogBackup.BackupLogsAndExit("SchemaQuench");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaQuench", e);
    }

    /// <summary>
    /// Registers an ICheckpointing implementation in FactoryContainer when
    /// --CheckpointDirectory is specified. Without the switch, ProductQuench falls back
    /// to FileCheckpointManager.GetFromFactory() which creates a default instance.
    /// </summary>
    private static void RegisterCheckpointing()
    {
        var dir = CommandLineParser.ValueOfSwitch("CheckpointDirectory");
        if (!string.IsNullOrWhiteSpace(dir))
            FactoryContainer.Register<ICheckpointing>(new FileCheckpointManager(dir));
    }

    private static void ToolSpecificSwitches()
    {
        Console.WriteLine("  --ResumeQuench                   Resume from an existing checkpoint if one is present.");
        Console.WriteLine("  --CheckpointDirectory:<path>     Directory for checkpoint files (default: %TEMP%/schemaquench-checkpoints).");
    }
}
