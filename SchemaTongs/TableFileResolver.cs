// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.IO;
using Schema.Delivery;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaTongs;

/// <summary>Where an extracted table should be written, and whether it is an ungated emit.</summary>
public sealed record TableResolution(string WritePath, bool UngatedEmit);

/// <summary>
/// Resolves the write target for an extracted table by CONTENT identity (Schema + Name) against the
/// existing files in a Tables/ folder, rather than by computed filename. When a logical table has a
/// variant SET on disk, the extracted shape is attributed to its active variant (gate evaluated
/// against the source) — refreshing that variant's file — or written to the bare canonical name as an
/// ungated emit when no single variant is active, so validation (SS-DUP-001) surfaces the drift.
/// The shared <see cref="ExtractionFileIndex"/> stays filename-keyed because it also indexes scripts.
/// </summary>
public sealed class TableFileResolver
{
    private readonly string _tablesDir;
    private readonly bool _isSchemaTemplate;
    private readonly Func<string, bool> _isVariantActive;
    private readonly Dictionary<(string Schema, string Name), List<TableFileEntry>> _byIdentity = new(IdentityComparer.Instance);

    private sealed record TableFileEntry(string Path, string Gate, string VariantName);

    public TableFileResolver(string tablesDir, Platform platform, bool isSchemaTemplate, Func<string, bool> isVariantActive)
    {
        _tablesDir = tablesDir;
        _isSchemaTemplate = isSchemaTemplate;
        _isVariantActive = isVariantActive;

        var directory = DirectoryWrapper.GetFromFactory();
        if (!directory.Exists(tablesDir)) return;

        foreach (var path in directory.GetFiles(tablesDir, "*.json", SearchOption.AllDirectories))
        {
            Table table;
            try { table = JsonHelper.TableLoad(path, platform); }
            catch { continue; } // unreadable/invalid JSON is the loader/validator's concern, not ours
            if (table == null || string.IsNullOrWhiteSpace(table.Name)) continue;

            // Schema-template files are schema-scrubbed on extraction, so identity is name-only there.
            // Identifiers load quoted ([dbo], `Widget`); normalize so they match the unquoted query.
            var schema = _isSchemaTemplate ? "" : TableFileName.NormalizeIdentifier((table as IDeliverableTable)?.Schema ?? "");
            var key = (schema, TableFileName.NormalizeIdentifier(table.Name));
            if (!_byIdentity.TryGetValue(key, out var entries))
                _byIdentity[key] = entries = new List<TableFileEntry>();
            entries.Add(new TableFileEntry(path, table.ShouldApplyExpression ?? "", table.VariantName ?? ""));
        }
    }

    public TableResolution Resolve(string schema, string name)
    {
        var key = (_isSchemaTemplate ? "" : TableFileName.NormalizeIdentifier(schema), TableFileName.NormalizeIdentifier(name));
        var matches = _byIdentity.TryGetValue(key, out var entries) ? entries : new List<TableFileEntry>();

        if (matches.Count == 0)
            return new TableResolution(Path.Join(_tablesDir, TableFileName.Canonical(schema, name, "", _isSchemaTemplate)), UngatedEmit: false);

        if (matches.Count == 1)
            return new TableResolution(matches[0].Path, UngatedEmit: false);

        // Variant set: attribute the extracted shape to the active variant, or emit ungated.
        var decision = VariantAttribution.Decide(matches, e => e.Gate, _isVariantActive);
        return decision.Action == VariantAction.RefreshActive
            ? new TableResolution(matches[decision.ActiveIndex].Path, UngatedEmit: false)
            : new TableResolution(Path.Join(_tablesDir, TableFileName.Canonical(schema, name, "", _isSchemaTemplate)), UngatedEmit: true);
    }

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
