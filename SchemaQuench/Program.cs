using System;
using System.Diagnostics;
using System.Reflection;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench;

public static class Program
{
    public static void Main(string[] args)
    {
        if (CommandLineParser.ContainsSwitch("v") || CommandLineParser.ContainsSwitch("ver") || CommandLineParser.ContainsSwitch("version")) ShowVersionAndExit();
        if (CommandLineParser.ContainsSwitch("?") || CommandLineParser.ContainsSwitch("h") || CommandLineParser.ContainsSwitch("help")) ShowHelpAndExit();

        var skipKindlingForge = args.Length > 0 && args[0] == "SkipKindlingForge";
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        ConfigHelper.GetAppSettingsAndUserSecrets(LogFactory.GetLogger("ProgressLog").Info);
        new ProductQuencher().Quench(skipKindlingForge);
        LogBackup.BackupLogsAndExit("SchemaQuench");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaQuench", e);
    }

    public static void ShowVersionAndExit()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        Console.WriteLine($"SchemaQuench - Version: {FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion}");
        EnvironmentWrapper.GetFromFactory().Exit(0);
    }

    public static void ShowHelpAndExit()
    {
        Console.WriteLine("SchemaQuench.exe [<command>]");
        Console.WriteLine("  --version                Show the program version");
        Console.WriteLine("  --ConfigFile:<filepath>  Path and file name of the config file. The default is appsettings.json in the current path.");
        Console.WriteLine("  --help                   Show the command line options");
        EnvironmentWrapper.GetFromFactory().Exit(0);
    }
}