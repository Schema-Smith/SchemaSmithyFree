using System;
using System.IO;
using System.Reflection;
using Schema.Isolators;

namespace Schema.Utility;

public static class LogBackup
{
    public static void BackupLogsAndExit(string appName, int exitCode = 0)
    {
        var backupDir = "UNKNOWN";
        try
        {
            var directory = DirectoryWrapper.GetFromFactory();
            var file = FileWrapper.GetFromFactory();
            var ext = 0;
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            var cwd = Path.GetDirectoryName(assembly.Location) ?? @".\";
            backupDir = Path.Combine(cwd, $"{appName}.{$"{++ext}".PadLeft(4, '0')}");
            while (directory.Exists(backupDir))
                backupDir = Path.Combine(cwd, $"{appName}.{$"{++ext}".PadLeft(4, '0')}");

            directory.CreateDirectory(backupDir);

            var logFiles = directory.GetFiles(cwd, $"{appName} - *.log", SearchOption.TopDirectoryOnly);
            foreach (var logFile in logFiles)
                file.Copy(logFile, Path.Combine(backupDir, Path.GetFileName(logFile)));

            EnvironmentWrapper.GetFromFactory().Exit(exitCode);
        }
        catch (Exception e)
        {
            Console.WriteLine("");
            Console.WriteLine($"UNABLE TO BACKUP LOG FILES TO {backupDir}");
            Console.WriteLine(e);
            EnvironmentWrapper.GetFromFactory().Exit(4);
        }
    }

    public static void UnhandledExceptionLogger(string appName, UnhandledExceptionEventArgs e)
    {
        LogFactory.GetLogger("ProgressLog").Error($"EXCEPTION - See the error log:\r\n{e.ExceptionObject}");
        LogFactory.GetLogger("ErrorLog").Error(e.ExceptionObject);

        BackupLogsAndExit(appName, 3);
    }
}