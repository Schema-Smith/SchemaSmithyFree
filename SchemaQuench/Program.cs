// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Microsoft.Extensions.Configuration;
using Schema.Checkpointing;
using Schema.Domain;
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

        var productQuench = new ProductQuench();

        var testConnection = CommandLineParser.ContainsSwitch("TestConnection");
        var previewTargets = CommandLineParser.ContainsSwitch("PreviewTargets");
        if (testConnection || previewTargets)
        {
            var ok = productQuench.RunPreFlight(previewTargets);
            LogBackup.BackupLogsAndExit("SchemaQuench", ok ? 0 : 2);
            return;
        }

        productQuench.QuenchProduct(skipKindlingForge);

        // Clean up checkpoint files only on a clean success — a failed run must preserve
        // checkpoints so the next invocation can resume.
        if (!productQuench.Failed)
        {
            CleanupCheckpoints();
            LogBackup.BackupLogsAndExit("SchemaQuench");
        }
        else
        {
            // Continue mode: some work units failed but the run was not aborted mid-template.
            // Exit with code 2 to signal partial failure to the caller.
            LogBackup.BackupLogsAndExit("SchemaQuench", 2);
        }
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaQuench", e);
    }

    /// <summary>
    /// Registers an ICheckpointing implementation in FactoryContainer using
    /// --CheckpointDirectory (or the CheckpointDirectory config key) when provided.
    /// Without either, ProductQuench falls back to FileCheckpointManager.GetFromFactory()
    /// which uses a default temp directory.
    /// </summary>
    private static void RegisterCheckpointing()
    {
        var dir = CommandLineParser.ValueOfSwitch("CheckpointDirectory");
        if (string.IsNullOrWhiteSpace(dir))
            dir = FactoryContainer.Resolve<IConfigurationRoot>()?["CheckpointDirectory"];
        if (!string.IsNullOrWhiteSpace(dir))
            FactoryContainer.Register<ICheckpointing>(new FileCheckpointManager(dir));
    }

    private static void CleanupCheckpoints()
    {
        try
        {
            FileCheckpointManager.GetFromFactory().DeleteCheckpoints(Product.Load().Name);
        }
        catch
        {
            // Product failed to load or no checkpointing registered — nothing to clean.
        }
    }

    private static void ToolSpecificSwitches()
    {
        Console.WriteLine("  --TestConnection                 Validate server connection(s) + minimum version, then exit. No deployment.");
        Console.WriteLine("  --PreviewTargets                 Validate, then list the databases/schemas each template would target (read-only). No deployment.");
        Console.WriteLine("  --ResumeQuench                   Resume from an existing checkpoint if one is present.");
        Console.WriteLine("  --CheckpointDirectory:<path>     Directory for checkpoint files (default: %TEMP%/schemaquench-checkpoints).");
        Console.WriteLine("  --ForceReKindle                  Re-deploy the SchemaSmith helper procedures this run even if the in-database kindle stamp is current.");
    }
}
