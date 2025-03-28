using Schema.Utility;
using System;

namespace SchemaQuench;

public static class Program
{
    public static void Main(string[] args)
    {
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
}