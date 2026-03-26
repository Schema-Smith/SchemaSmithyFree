// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System.IO;

namespace Schema.Isolators;

public interface IDirectory
{
    bool Exists(string path);
    IDirectoryInfo CreateDirectory(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    string[] GetDirectories(string path, string searchPattern, SearchOption searchOption);
    void Delete(string path, bool recursive = false);
}