// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
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

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetFiles(LongPathSupport.MakeSafeLongFilePath(path), searchPattern, searchOption);
    }

    public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetDirectories(LongPathSupport.MakeSafeLongFilePath(path), searchPattern, searchOption);
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
