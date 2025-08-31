using System;
using System.Diagnostics;
using System.Reflection;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs;

public static class Program
{
    public static void Main(string[] args)
    {
        CommandLineParser.HandleCommonSwitches("SchemaTongs");

        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        ConfigHelper.GetAppSettingsAndUserSecrets(LogFactory.GetLogger("ProgressLog").Info);

        new SchemaTongs().CastTemplate();
        LogBackup.BackupLogsAndExit("SchemaTongs");
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogBackup.UnhandledExceptionLogger("SchemaTongs", e);
    }
}