// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;

namespace Schema.Isolators;

public interface IDirectory
{
    bool Exists(string path);
    IDirectoryInfo CreateDirectory(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    string[] GetDirectories(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
    void Delete(string path, bool recursive = false);
    void Move(string sourceDirName, string destDirName);
    string GetCurrentDirectory();
}
