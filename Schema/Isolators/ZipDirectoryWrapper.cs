using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace Schema.Isolators;

public class ZipDirectoryWrapper : IDirectory
{
    private List<ZipArchiveEntry> _zipEntries;

    public bool Exists(string path)
    {
        if (_zipEntries == null || string.IsNullOrEmpty(path)) return false;

        var normalizedPath = NormalizePath(path);
        return _zipEntries.Any(e =>
            e.FullName.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) &&
            (NormalizePath(e.FullName).Length == normalizedPath.Length || e.FullName[normalizedPath.Length] == '/'));
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        if (_zipEntries == null || string.IsNullOrEmpty(path)) return [];

        var normalizedPath = NormalizePath(path);
        return _zipEntries
            .Where(e =>
                e.FullName.Replace('\\', '/').StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.Replace('\\', '/').EndsWith("/") &&
                !(searchOption == SearchOption.TopDirectoryOnly && e.FullName.Replace('\\', '/').Substring(normalizedPath.Length).TrimStart('/').Contains("/")) &&
                ((searchPattern ?? "*") == "*" || Regex.IsMatch(Path.GetFileName(e.FullName), $"^{Regex.Escape(searchPattern!).Replace(@"\*", ".*").Replace(@"\?", ".")}$", RegexOptions.IgnoreCase))
                )
            .Select(e => e.FullName)
            .ToArray();
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        return string.IsNullOrEmpty(normalized) ? "" : normalized + "/";
    }

    public static IDirectory GetFromFactory(List<ZipArchiveEntry> zipEntries)
    {
        var zipDir = FactoryContainer.ResolveOrCreate<ZipDirectoryWrapper>(true);
        zipDir._zipEntries = zipEntries;
        return zipDir;
    }

    // Other IDirectory methods not used for zip access
    IDirectoryInfo IDirectory.CreateDirectory(string path) => throw new NotImplementedException();
    public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => throw new NotImplementedException();
    public void Delete(string path, bool recursive = false) => throw new NotImplementedException();
    public void Move(string sourceDirName, string destDirName) => throw new NotImplementedException();
}