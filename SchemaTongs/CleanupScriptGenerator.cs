// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SchemaTongs;

public static class CleanupScriptGenerator
{
    private static readonly Dictionary<string, string> FolderToDropKeyword = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Views"] = "VIEW",
        ["Functions"] = "FUNCTION",
        ["Procedures"] = "PROCEDURE",
        ["Triggers"] = "TRIGGER",
        ["DDLTriggers"] = "TRIGGER",
        ["Schemas"] = "SCHEMA",
        ["DataTypes"] = "TYPE",
        ["FullTextCatalogs"] = "FULLTEXT CATALOG",
        ["FullTextStopLists"] = "FULLTEXT STOPLIST",
        ["XMLSchemaCollections"] = "XML SCHEMA COLLECTION",
    };

    public static string GenerateDropStatement(string fileName, string folderName)
    {
        var ext = Path.GetExtension(fileName);
        if (!ext.Equals(".sql", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".sqlerror", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!FolderToDropKeyword.TryGetValue(folderName, out var keyword))
            return null;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var dotIndex = nameWithoutExt.IndexOf('.');
        if (dotIndex < 0) return null;

        var schema = nameWithoutExt.Substring(0, dotIndex);
        var objectName = nameWithoutExt.Substring(dotIndex + 1);

        return $"DROP {keyword} IF EXISTS [{schema}].[{objectName}];";
    }

    public static string GenerateCleanupScript(List<string> orphanFileNames, string folderName)
    {
        var sb = new StringBuilder();
        var statements = orphanFileNames
            .Select(f => GenerateDropStatement(Path.GetFileName(f), folderName))
            .Where(s => s != null)
            .ToList();

        sb.AppendLine($"-- {statements.Count} orphaned objects detected during extraction on {DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine();
        foreach (var stmt in statements)
            sb.AppendLine(stmt);

        return sb.ToString();
    }
}
