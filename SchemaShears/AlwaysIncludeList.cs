// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;

namespace SchemaShears;

public static class AlwaysIncludeList
{
    public static IReadOnlyList<string> Expand(string alwaysIncludePath, string sourcePath)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(alwaysIncludePath)) return result;

        if (!File.Exists(alwaysIncludePath))
            throw new PatchBuildException($"Always-include file not found: '{alwaysIncludePath}'.");

        foreach (var raw in File.ReadAllLines(alwaysIncludePath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

            var normalized = line.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var absolute = Path.Combine(sourcePath, normalized);

            if (File.Exists(absolute))
            {
                result.Add(normalized);
            }
            else if (Directory.Exists(absolute))
            {
                foreach (var file in Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories))
                    result.Add(Path.GetRelativePath(sourcePath, file));
            }
            else
            {
                throw new PatchBuildException($"Always-include entry does not exist under source: '{line}'.");
            }
        }

        return result;
    }
}
