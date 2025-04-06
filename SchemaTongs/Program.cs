using System;
using Schema.Utility;

namespace SchemaTongs;

public static class Program
{
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        ConfigHelper.GetAppSettingsAndUserSecrets(LogFactory.GetLogger("ProgressLog").Info);

        new SchemaTongs().ExctractTemplate();
        LogBackup.BackupLogsAndExit("SchemaTongs");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaTongs", e);
    }
}