// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
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

            var cwd = ConfigHelper.ResolveLogPath();

            // Two runs sharing one install race here: Directory.CreateDirectory is idempotent, so both
            // settle on the same App.0001 and the loser's Copy hits a destination that already exists.
            // That threw into the catch below and exited 4 -- a run that had just printed PASS reported as
            // a failure, which is entirely realistic in CI. Claim a directory by finding one whose target
            // files do not exist yet, and advance to the next index if another run got there first.
            const int maxAttempts = 50;
            string backupTarget = null;
            string[] logFiles = null;
            string[] summaryFiles = null;
            for (var attempt = 0; attempt < maxAttempts && backupTarget == null; attempt++)
            {
                var candidate = Path.Combine(cwd, $"{appName}.{$"{++ext}".PadLeft(4, '0')}");
                if (directory.Exists(candidate)) continue;

                directory.CreateDirectory(candidate);
                logFiles = directory.GetFiles(cwd, $"{appName} - *.log", SearchOption.TopDirectoryOnly);
                // Deployment summary report (#243, E4e): archive the always-on Summary.json/.md alongside
                // the run's logs. Harmless on tools that never write them (SchemaTongs, DataTongs).
                summaryFiles = directory.GetFiles(cwd, $"{appName} - Summary.*", SearchOption.TopDirectoryOnly);

                // Another run claimed this directory between the Exists check and CreateDirectory if any
                // destination is already occupied. Leave its files alone and take the next index.
                var taken = false;
                foreach (var source in logFiles)
                    taken |= file.Exists(Path.Combine(candidate, Path.GetFileName(source)));
                foreach (var source in summaryFiles)
                    taken |= file.Exists(Path.Join(candidate, Path.GetFileName(source)));
                if (!taken) backupTarget = candidate;
            }

            backupDir = backupTarget ?? throw new IOException(
                $"Could not claim a log backup directory under '{cwd}' after {maxAttempts} attempts.");

            foreach (var logFile in logFiles)
                file.Copy(logFile, Path.Combine(backupDir, Path.GetFileName(logFile)));
            foreach (var summaryFile in summaryFiles)
                file.Copy(summaryFile, Path.Join(backupDir, Path.GetFileName(summaryFile)));

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
