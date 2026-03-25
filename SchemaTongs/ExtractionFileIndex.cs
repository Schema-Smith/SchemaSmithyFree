// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs;

public class ExtractionFileIndex
{
    private static readonly ILog Log = LogFactory.GetLogger("ProgressLog");
    private static readonly HashSet<string> IndexedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".sql", ".sqlerror", ".json" };

    private readonly Dictionary<string, List<string>> _fileIndex;
    private readonly HashSet<string> _writtenFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedFromOrphans = new(StringComparer.OrdinalIgnoreCase);

    private ExtractionFileIndex(Dictionary<string, List<string>> fileIndex)
    {
        _fileIndex = fileIndex;
    }

    public static ExtractionFileIndex Build(string baseFolderPath)
    {
        var directory = DirectoryWrapper.GetFromFactory();
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!directory.Exists(baseFolderPath))
            return new ExtractionFileIndex(index);

        var files = directory.GetFiles(baseFolderPath, "*.*", SearchOption.AllDirectories);
        foreach (var filePath in files)
        {
            var ext = Path.GetExtension(filePath);
            if (!IndexedExtensions.Contains(ext)) continue;
            var fileName = Path.GetFileName(filePath);
            if (!index.ContainsKey(fileName))
                index[fileName] = new List<string>();
            index[fileName].Add(filePath);
        }
        return new ExtractionFileIndex(index);
    }

    public string ResolvePath(string fileName, string baseFolderPath)
    {
        if (_fileIndex.TryGetValue(fileName, out var paths))
        {
            if (paths.Count == 1) return paths[0];
            if (paths.Count > 1)
            {
                var folders = string.Join(", ", paths.Select(p => Path.GetDirectoryName(p)));
                Log.Warn($"Found {fileName} in multiple subfolders: {folders} — writing to base folder");
            }
        }
        return Path.Combine(baseFolderPath, fileName);
    }

    public void MarkWritten(string filePath) => _writtenFiles.Add(filePath);
    public void ExcludeFromOrphans(string fileName) => _excludedFromOrphans.Add(fileName);

    public List<string> GetOrphans()
    {
        var orphans = new List<string>();
        foreach (var (fileName, paths) in _fileIndex)
        {
            if (_excludedFromOrphans.Contains(fileName)) continue;
            foreach (var path in paths)
                if (!_writtenFiles.Contains(path)) orphans.Add(path);
        }
        return orphans;
    }
}
