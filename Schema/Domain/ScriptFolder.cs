// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Isolators;

namespace Schema.Domain;

public class ScriptFolder
{
    public string FolderPath { get; set; }
    public List<SqlScript> Scripts { get; set; } = [];

    public void LoadSqlFiles(string basePath, List<KeyValuePair<string, string>> scriptTokens = null)
    {
        var sqlFilePath = Path.Combine(basePath, FolderPath);
        if (!ProductDirectoryWrapper.GetFromFactory().Exists(sqlFilePath))
        {
            // Read tolerance: fall back to legacy "TableData" (no space) when "Table Data" (with space) is absent.
            // Allows old packages extracted before the folder rename to still load correctly.
            var legacyPath = Path.Combine(basePath, FolderPath.Replace("Table Data", "TableData"));
            if (legacyPath == sqlFilePath || !ProductDirectoryWrapper.GetFromFactory().Exists(legacyPath)) return;
            sqlFilePath = legacyPath;
        }

        var files = ProductDirectoryWrapper.GetFromFactory().GetFiles(sqlFilePath, "*.sql", SearchOption.AllDirectories).OrderBy(x => x);
        Scripts.AddRange(files.Select(f => SqlScript.Load(f, scriptTokens)));
    }
}
