// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Schema.Isolators;

namespace SchemaShears;

public static class PatchPackageWriter
{
    public static void Write(IReadOnlyDictionary<string, IncludeReason> includeSet, string sourcePath, string outputPath)
    {
        if (DirectoryWrapper.GetFromFactory().Exists(outputPath) &&
            DirectoryWrapper.GetFromFactory().EnumerateFileSystemEntries(outputPath, "*", SearchOption.TopDirectoryOnly).Any())
            throw new PatchBuildException($"Output path already exists and is not empty: '{outputPath}'.");

        DirectoryWrapper.GetFromFactory().CreateDirectory(outputPath);

        foreach (var (relPath, _) in includeSet)
        {
            var dest = Path.Join(outputPath, relPath);
            DirectoryWrapper.GetFromFactory().CreateDirectory(Path.GetDirectoryName(dest));
            FileWrapper.GetFromFactory().Copy(Path.Join(sourcePath, relPath), dest, overwrite: true);
        }

        var report = new StringBuilder();
        report.AppendLine("SchemaShears patch build report");
        report.AppendLine("================================");
        foreach (var (relPath, reason) in includeSet.OrderBy(e => e.Key, System.StringComparer.Ordinal))
            report.AppendLine($"{reason,-13} {relPath}");

        FileWrapper.GetFromFactory().WriteAllText(Path.Join(outputPath, "patch-build-report.txt"), report.ToString());
    }
}
