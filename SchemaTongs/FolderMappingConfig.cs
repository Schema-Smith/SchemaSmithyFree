// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Schema.Domain;
using Schema.Configuration;

namespace SchemaTongs;

public class FolderMappingConfig
{
    private static readonly HashSet<ScriptObjectType> FixedFolderTypes =
    [
        ScriptObjectType.None,
        ScriptObjectType.IndexedViews,
        ScriptObjectType.MaterializedViews,
    ];

    private readonly Dictionary<ScriptObjectType, string> _mappings = new();

    public FolderMappingConfig(IConfigurationRoot config, Platform platform)
    {
        var defaults = Template.GetDefaultTemplateFolders(platform);
        foreach (var folder in defaults.Where(f => f.ObjectType != ScriptObjectType.None))
            _mappings[folder.ObjectType] = folder.FolderPath;

        var section = config.GetSection(SettingsKeys.FolderMapping);
        if (!section.Exists()) return;

        foreach (var child in section.GetChildren())
        {
            if (!Enum.TryParse<ScriptObjectType>(child.Key, true, out var objectType))
                throw new ArgumentException($"Unknown ScriptObjectType '{child.Key}' in FolderMapping configuration.");

            if (FixedFolderTypes.Contains(objectType))
                throw new ArgumentException($"ScriptObjectType '{objectType}' has a fixed folder and cannot be remapped.");

            if (string.IsNullOrWhiteSpace(child.Value))
                continue;

            _mappings[objectType] = child.Value;
        }
    }

    public string GetFolderName(ScriptObjectType objectType)
        => _mappings.TryGetValue(objectType, out var name) ? name : null;

    public void Validate()
    {
        var duplicates = _mappings
            .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicates.Count == 0) return;

        var detail = string.Join("; ", duplicates.Select(d =>
            $"'{d.Key}' mapped by {string.Join(", ", d.Select(kv => kv.Key))}"));
        throw new ArgumentException($"Duplicate folder names in FolderMapping configuration: {detail}");
    }
}
