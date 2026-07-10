// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using Schema.Delivery;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs;

/// <summary>Outcome of resolving where an extracted table should be written.</summary>
public sealed record TableResolution(string WritePath, bool RefreshStructure, bool IsVariantSet);

/// <summary>
/// Resolves the write target for an extracted table by CONTENT identity (Schema + Name) against the
/// existing files in a Tables/ folder, rather than by computed filename. Phase 1 rules
/// (see the VariantName-aware filename design):
/// <list type="bullet">
/// <item>No existing file for the logical table → write the canonical bare name (new table).</item>
/// <item>Exactly one existing file → refresh it at its existing path (never duplicate).</item>
/// <item>A variant set (more than one file) → refresh none structurally; attributing the single
/// extracted shape to one variant needs target evaluation (Phase 2), so the set is left untouched.</item>
/// </list>
/// The shared <see cref="ExtractionFileIndex"/> stays filename-keyed because it also indexes scripts.
/// </summary>
public sealed class TableFileResolver
{
    private readonly string _tablesDir;
    private readonly bool _isSchemaTemplate;
    private readonly Dictionary<(string Schema, string Name), List<string>> _byIdentity = new(IdentityComparer.Instance);

    public TableFileResolver(string tablesDir, Platform platform, bool isSchemaTemplate)
    {
        _tablesDir = tablesDir;
        _isSchemaTemplate = isSchemaTemplate;

        var directory = DirectoryWrapper.GetFromFactory();
        if (!directory.Exists(tablesDir)) return;

        foreach (var path in directory.GetFiles(tablesDir, "*.json", SearchOption.AllDirectories))
        {
            Table table;
            try { table = JsonHelper.TableLoad(path, platform); }
            catch { continue; } // unreadable/invalid JSON is not our concern here — the loader/validator report it
            if (table == null || string.IsNullOrWhiteSpace(table.Name)) continue;

            var key = IdentityOf(table);
            if (!_byIdentity.TryGetValue(key, out var paths))
                _byIdentity[key] = paths = new List<string>();
            paths.Add(path);
        }
    }

    public TableResolution Resolve(string schema, string name)
    {
        var key = (schema ?? "", (name ?? "").Trim());
        var matches = _byIdentity.TryGetValue(key, out var paths) ? paths : new List<string>();

        if (matches.Count == 0)
        {
            var canonical = TableFileName.Canonical(schema, name, "", _isSchemaTemplate);
            return new TableResolution(Path.Combine(_tablesDir, canonical), RefreshStructure: true, IsVariantSet: false);
        }

        if (matches.Count == 1)
            return new TableResolution(matches[0], RefreshStructure: true, IsVariantSet: false);

        return new TableResolution(WritePath: null, RefreshStructure: false, IsVariantSet: true);
    }

    private static (string Schema, string Name) IdentityOf(Table table) =>
        ((table as IDeliverableTable)?.Schema ?? "", table.Name.Trim());

    private sealed class IdentityComparer : IEqualityComparer<(string Schema, string Name)>
    {
        public static readonly IdentityComparer Instance = new();

        public bool Equals((string Schema, string Name) a, (string Schema, string Name) b) =>
            string.Equals(a.Schema, b.Schema, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Schema, string Name) k) =>
            HashCode.Combine(k.Schema.ToLowerInvariant(), k.Name.ToLowerInvariant());
    }
}
