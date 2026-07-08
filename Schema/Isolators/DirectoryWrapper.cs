// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Utility;

namespace Schema.Isolators;

public class DirectoryWrapper : IDirectory
{
    public bool Exists(string path)
    {
        return Directory.Exists(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public IDirectoryInfo CreateDirectory(string path)
    {
        return new DirectoryInfoWrapper(Directory.CreateDirectory(LongPathSupport.MakeSafeLongFilePath(path)));
    }

    // The directory-listing members long-path-prefix the search root (so listing works on long
    // paths) and then strip the "\\?\" prefix from every returned path, so callers get clean,
    // caller-facing paths that are safe to string-manipulate (e.g. Path.GetRelativePath) or to pass
    // straight back to an isolator call. No-op on Linux, where MakeSafeLongFilePath /
    // StripLongPathPrefix leave the path untouched.
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetFiles(LongPathSupport.MakeSafeLongFilePath(path), searchPattern, searchOption)
            .Select(LongPathSupport.StripLongPathPrefix).ToArray();
    }

    public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetDirectories(LongPathSupport.MakeSafeLongFilePath(path), searchPattern, searchOption)
            .Select(LongPathSupport.StripLongPathPrefix).ToArray();
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.EnumerateFiles(LongPathSupport.MakeSafeLongFilePath(path), searchPattern, searchOption)
            .Select(LongPathSupport.StripLongPathPrefix);
    }

    public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.EnumerateFileSystemEntries(LongPathSupport.MakeSafeLongFilePath(path), searchPattern, searchOption)
            .Select(LongPathSupport.StripLongPathPrefix);
    }

    public void Delete(string path, bool recursive = false)
    {
        Directory.Delete(LongPathSupport.MakeSafeLongFilePath(path), recursive);
    }

    public void Move(string sourceDirName, string destDirName)
    {
        Directory.Move(LongPathSupport.MakeSafeLongFilePath(sourceDirName), LongPathSupport.MakeSafeLongFilePath(destDirName));
    }

    public string GetCurrentDirectory()
    {
        return Directory.GetCurrentDirectory();
    }

    public static IDirectory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDirectory, DirectoryWrapper>();
    }
}
