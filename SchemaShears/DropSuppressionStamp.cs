// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SchemaShears;

public static class DropSuppressionStamp
{
    private static readonly Dictionary<string, string> CategoryToFlag =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tables"]             = "DropTablesRemovedFromProduct",
            ["Columns"]            = "DropColumnsRemovedFromProduct",
            ["Indexes"]            = "DropUnknownIndexes",
            ["ForeignKeys"]        = "DropForeignKeysRemovedFromProduct",
            ["CheckConstraints"]   = "DropCheckConstraintsRemovedFromProduct",
            ["ExcludeConstraints"] = "DropExcludeConstraintsRemovedFromProduct",
            ["Statistics"]         = "DropStatisticsRemovedFromProduct",
        };

    public static void Apply(string productJsonPath, IReadOnlyCollection<string> allowDrops)
    {
        if (!File.Exists(productJsonPath))
            throw new PatchBuildException($"Product.json not found in patch output: '{productJsonPath}'.");

        var unknown = allowDrops
            .Where(c => !CategoryToFlag.ContainsKey(c))
            .ToList();

        if (unknown.Count > 0)
        {
            var valid = string.Join(", ", CategoryToFlag.Keys);
            throw new PatchBuildException(
                $"Unknown drop category '{unknown[0]}'. Valid categories: {valid}.");
        }

        var json = JObject.Parse(File.ReadAllText(productJsonPath));

        foreach (var (category, flag) in CategoryToFlag)
        {
            if (!allowDrops.Contains(category, StringComparer.OrdinalIgnoreCase))
                json[flag] = false;
        }

        File.WriteAllText(productJsonPath, json.ToString(Formatting.Indented));
    }
}
