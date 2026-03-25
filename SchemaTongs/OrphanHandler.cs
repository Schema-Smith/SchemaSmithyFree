// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs;

public static class OrphanHandler
{
    private static readonly ILog Log = LogFactory.GetLogger("ProgressLog");

    public static void ArchiveExistingCleanupScripts(string logDirectory)
    {
        var dir = DirectoryWrapper.GetFromFactory();
        var file = FileWrapper.GetFromFactory();
        if (!dir.Exists(logDirectory)) return;

        var existingScripts = dir.GetFiles(logDirectory, "_OrphanCleanup_*.sql", SearchOption.TopDirectoryOnly)
            .Concat(dir.GetFiles(logDirectory, "_InvalidObjectCleanup.sql", SearchOption.TopDirectoryOnly))
            .ToList();
        if (existingScripts.Count == 0) return;

        var ext = 0;
        var backupDir = Path.Combine(logDirectory, $"SchemaTongs.{$"{++ext}".PadLeft(4, '0')}");
        while (dir.Exists(backupDir))
            backupDir = Path.Combine(logDirectory, $"SchemaTongs.{$"{++ext}".PadLeft(4, '0')}");

        dir.CreateDirectory(backupDir);
        foreach (var script in existingScripts)
        {
            file.Copy(script, Path.Combine(backupDir, Path.GetFileName(script)));
            file.Delete(script);
        }
        Log.Info($"Archived {existingScripts.Count} existing cleanup script(s) to {Path.GetFileName(backupDir)}");
    }

    public static void ProcessOrphans(
        Dictionary<string, ExtractionFileIndex> folderIndexes,
        OrphanHandlingMode mode,
        string logDirectory)
    {
        var totalOrphans = 0;
        foreach (var (folderName, index) in folderIndexes)
        {
            var orphans = index.GetOrphans();
            if (orphans.Count == 0) continue;

            totalOrphans += orphans.Count;
            Log.Info($"  {folderName}: {orphans.Count} orphaned file(s)");
            foreach (var orphan in orphans)
                Log.Info($"    {Path.GetFileName(orphan)}");

            if (mode == OrphanHandlingMode.Detect) continue;

            // Generate cleanup script only for .sql/.sqlerror files, not .json
            var scriptableOrphans = orphans.Where(o =>
            {
                var e = Path.GetExtension(o);
                return e.Equals(".sql", StringComparison.OrdinalIgnoreCase) ||
                       e.Equals(".sqlerror", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (scriptableOrphans.Count > 0)
            {
                var script = CleanupScriptGenerator.GenerateCleanupScript(scriptableOrphans, folderName);
                var scriptPath = Path.Combine(logDirectory, $"_OrphanCleanup_{folderName}.sql");
                FileWrapper.GetFromFactory().WriteAllText(scriptPath, script);
                Log.Info($"  Generated _OrphanCleanup_{folderName}.sql with {scriptableOrphans.Count} DROP statement(s)");
            }

            if (mode == OrphanHandlingMode.DetectDeleteAndCleanup)
            {
                foreach (var orphan in orphans)
                    FileWrapper.GetFromFactory().Delete(orphan);
                Log.Info($"  Deleted {orphans.Count} orphaned file(s) from {folderName}");
            }
        }

        if (totalOrphans == 0)
            Log.Info("No orphaned files detected");
        else
            Log.Info($"Orphan detection complete: {totalOrphans} total orphan(s)");
    }
}
