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

    /// <summary>
    /// Walks the directory yielding each entry WITH the last-write time the platform already surfaced,
    /// so a caller ordering by write time does not pay a stat per candidate.
    /// <para><b>Must be lazy.</b> The only bound a caller has on a directory of unknown size is its
    /// ability to stop walking; an implementation that materialises first puts back the stall this
    /// exists to remove.</para>
    /// </summary>
    IEnumerable<TimestampedFile> EnumerateFilesWithTimestamps(string path, string searchPattern, SearchOption searchOption);
    void Delete(string path, bool recursive = false);
    void Move(string sourceDirName, string destDirName);
    string GetCurrentDirectory();
}
