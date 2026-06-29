// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SchemaShears;

public static class PatchPackageWriter
{
    public static void Write(IReadOnlyDictionary<string, IncludeReason> includeSet, string sourcePath, string outputPath)
    {
        if (Directory.Exists(outputPath) && Directory.EnumerateFileSystemEntries(outputPath).Any())
            throw new PatchBuildException($"Output path already exists and is not empty: '{outputPath}'.");

        Directory.CreateDirectory(outputPath);

        foreach (var (relPath, _) in includeSet)
        {
            var dest = Path.Combine(outputPath, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(Path.Combine(sourcePath, relPath), dest, overwrite: true);
        }

        var report = new StringBuilder();
        report.AppendLine("SchemaShears patch build report");
        report.AppendLine("================================");
        foreach (var (relPath, reason) in includeSet.OrderBy(e => e.Key, System.StringComparer.Ordinal))
            report.AppendLine($"{reason,-13} {relPath}");

        File.WriteAllText(Path.Combine(outputPath, "patch-build-report.txt"), report.ToString());
    }
}
