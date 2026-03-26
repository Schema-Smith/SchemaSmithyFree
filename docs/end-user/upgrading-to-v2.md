# Upgrading to SchemaSmith v2

Most of your existing setup carries forward to v2 without changes. Schema packages, table definitions, scripts, and deployment logic all work the same way. This guide covers the breaking changes you need to address and the new capabilities available to you.

## Breaking Changes

### Configuration Files Renamed

**Old:** `appsettings.json` (shared name for all tools)
**New:** Tool-specific names:
- `SchemaQuench.settings.json`
- `SchemaTongs.settings.json`
- `DataTongs.settings.json`

**What to do:** Rename your config files. The format and content are unchanged — only the filename is different.

### Connection Properties No Longer Hardcoded

**Old:** `TrustServerCertificate=True` and `ApplicationIntent=ReadWrite` were silently added to every connection string.

**New:** Only `Data Source`, `Initial Catalog`, and authentication fields are built in. All other properties must be explicitly configured.

**What to do:** If your SQL Server requires `TrustServerCertificate=True`, add it to the `ConnectionProperties` section of your settings file:

```json
{
    "Target": {
        "Server": "myserver",
        "ConnectionProperties": {
            "TrustServerCertificate": "True"
        }
    }
}
```

The default settings files shipped with v2 include `TrustServerCertificate: True` as a recommended starting point.

### TableData Folder Renamed

**Old:** `TableData/`
**New:** `Table Data/` (with a space)

**What to do:** SchemaTongs auto-renames the folder on re-extraction. If you have custom scripts or tooling that references the old name, update the path. The legacy name is tolerated on read.

### MSI Installer Removed

**Old:** WiX-based MSI installer for Windows.
**New:** No MSI. Distribution via self-contained executables (GitHub Release ZIPs), Chocolatey, and Docker.

**What to do:** If you installed via MSI, uninstall it, then install using one of the new methods. Self-contained executables need no installer — download, extract, run.

### Chocolatey Package Names

**Old:** `schematongs-dotnetcore9`, `schemaquench-dotnetcore9`, `datatongs-dotnetcore9` (and `*-netfx481` variants)
**New:** `schematongs-dotnetcore10`, `schemaquench-dotnetcore10`, `datatongs-dotnetcore10` (netfx481 variants removed)

**What to do:** Uninstall old packages, install new ones:
```
choco uninstall schematongs-dotnetcore9
choco install schematongs-dotnetcore10
```

### .NET Framework 4.8.1 Builds Removed

**Old:** Dual-targeting .NET 9 and .NET Framework 4.8.1.
**New:** .NET 10 single target. Self-contained builds bundle the runtime.

**What to do:** Use self-contained executables (recommended — no runtime needed) or install .NET 10 runtime for framework-dependent builds.

## License Changes

SSCL v2.0 removes **all organization size and revenue restrictions**. No employee count limits, no revenue caps. Use SchemaSmith Community for any purpose — personal or commercial. Tiers are feature-based only.

This is a strictly more permissive change — no action needed on your part.

## New Capabilities

v2 adds significant new functionality. Here are the highlights with links to full documentation:

- **[SchemaHammer](schemahammer/README.md)** — Brand new desktop schema viewer. Browse schema packages visually with 13 property editors, T-SQL highlighting, and search.
- **[9 execution slots](schemaquench/script-folders.md)** — Product Before/After, BetweenTablesAndKeys, AfterTablesScripts, plus the original template slots.
- **Indexed view support** — Extract and deploy SQL Server indexed views with diff-based change detection.
- **[Token system in all script folders](script-tokens.md)** — `{{TokenName}}` placeholders now work everywhere.
- **[Orphan detection and script validation](schematongs/configuration.md)** — Detect stale scripts, validate SQL syntax on extraction.
- **Per-table and per-index UpdateFillFactor** — Granular fill factor control beyond the template level.
- **[DataTongs auto PK detection](datatongs/configuration.md)** — KeyColumns is now optional; auto-detected from primary key or unique index.
- **Self-contained executables** — No .NET runtime install needed. Available for 6 platforms.

For the complete list, see [FEATURE_LIST.md](../FEATURE_LIST.md).
