// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Microsoft.Extensions.Configuration;
using Schema.Checkpointing;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using Schema.Validation;

namespace SchemaQuench;

public static class Program
{
    public static void Main(string[] args)
    {
        CommandLineParser.HandleCommonSwitches("SchemaQuench", ToolSpecificSwitches);

        var skipKindlingForge = args.Length > 0 && args[0] == "SkipKindlingForge";
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        LogFactory.LogInitializer = ConfigHelper.ConfigureLog4Net;
        var config = ConfigHelper.GetAppSettingsAndUserSecrets("SchemaQuench", LogFactory.GetLogger("ProgressLog").Info);

        RegisterCheckpointing();

        // Checked before ProductQuench is constructed: ProductQuench's constructor eagerly calls
        // Product.Load(), which throws on a malformed package — that would crash Main before this
        // gate ever ran. --Validate does its own loading via PackageLoader so a load failure is
        // reported as a finding (SS-LOAD-001) instead of an unhandled exception.
        if (CommandLineParser.ContainsSwitch("Validate"))
        {
            var validator = new SchemaPackageValidator(PackageLoader.LoadPackage, ValidationCheckRegistry.Default());
            var result = validator.Validate(config["SchemaPackagePath"] ?? ".");
            foreach (var line in ValidationReporter.Render(result.Findings))
                LogFactory.GetLogger("ProgressLog").Info(line);
            LogBackup.BackupLogsAndExit("SchemaQuench", result.HasErrors ? 2 : 0);
            return;
        }

        var productQuench = new ProductQuench();

        var testConnection = CommandLineParser.ContainsSwitch("TestConnection");
        var previewTargets = CommandLineParser.ContainsSwitch("PreviewTargets");
        if (testConnection || previewTargets)
        {
            var ok = productQuench.RunPreFlight(previewTargets);
            LogBackup.BackupLogsAndExit("SchemaQuench", ok ? 0 : 2);
            return;
        }

        // --ResumeQuench is opt-in (#332): without it, discard any leftover checkpoint so a
        // re-run starts fresh and is never influenced by a stale checkpoint. Checkpointing
        // stays active for this run — a fresh failure still writes a checkpoint for a later
        // --ResumeQuench. Uses the same FileCheckpointManager (and directory) the run will use.
        if (!CommandLineParser.ContainsSwitch("ResumeQuench"))
            CleanupCheckpoints();

        try
        {
            productQuench.QuenchProduct(skipKindlingForge);
        }
        catch (Exception e) when (ConnectionLostClassifier.IsConnectionLost(e))
        {
            // A product-level script drop (Before/After scripts, version stamp) propagates out of
            // QuenchProduct; classify it here so the user sees the mid-deploy disconnect message
            // instead of a raw AppDomain stack. Non-connection exceptions still reach the
            // AppDomain UnhandledException handler unchanged.
            var server = config["Target:Server"] ?? "the target server";
            LogFactory.GetLogger("ProgressLog").Error(ConnectionLostMessage.Build(server, "deployment"));
            LogFactory.GetLogger("ErrorLog").Error("Lost connection during deployment", e);
            LogBackup.BackupLogsAndExit("SchemaQuench", 2);
            return;
        }

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
        Console.WriteLine("  --Validate                       Statically validate the schema package (no database connection), then exit.");
        Console.WriteLine("  --TestConnection                 Validate server connection(s) + minimum version, then exit. No deployment.");
        Console.WriteLine("  --PreviewTargets                 Validate, then list the databases/schemas each template would target (read-only). No deployment.");
        Console.WriteLine("  --ResumeQuench                   Resume from an existing checkpoint if one is present.");
        Console.WriteLine("  --CheckpointDirectory:<path>     Directory for checkpoint files (default: %TEMP%/schemaquench-checkpoints).");
        Console.WriteLine("  --ForceReKindle                  Re-deploy the SchemaSmith helper procedures this run even if the in-database kindle stamp is current.");
    }
}
