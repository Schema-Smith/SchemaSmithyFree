// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using System.IO.Compression;
using Schema.Isolators;

namespace SchemaShears;

public static class PatchZipper
{
    public static string Zip(string outputPath)
    {
        var zipPath = outputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
        if (FileWrapper.GetFromFactory().Exists(zipPath))
            throw new PatchBuildException($"Zip already exists: '{zipPath}'.");

        ZipFile.CreateFromDirectory(outputPath, zipPath);
        return zipPath;
    }
}
