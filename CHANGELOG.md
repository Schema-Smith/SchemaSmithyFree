# Changelog

All notable changes to SchemaSmith Community Edition are documented here.

For full release details and download links, see [GitHub Releases](https://github.com/Schema-Smith/SchemaSmithyFree/releases).

## [Unreleased]

### Added

- **SchemaHammer** — Read-only desktop schema viewer with 13 property editors, T-SQL syntax highlighting, tree and code search, lazy loading, back button with history, recent products
- **Modular table quench** — Monolithic TableQuench replaced with focused procedures: MissingTableAndColumnQuench, ModifiedTableQuench, MissingIndexesAndConstraintsQuench, ForeignKeyQuench, plus ParseTableJsonIntoTempTables for shared JSON parsing
- **IndexOnlyQuench** — Template-level `IndexOnlyTableQuenches` mode for managing indexes without modifying table structure
- **Expanded execution slots** — 9 total (7 template + 2 product): added BetweenTablesAndKeys, AfterTablesScripts, Product Before, Product After
- **Indexed view support** — SchemaTongs extraction, SchemaQuench diff-based deployment; index-only changes skip view rebuild
- **GenerateIndexedViewJson** — Stored procedure for indexed view extraction
- **IndexedViewQuench** — Stored procedure for indexed view deployment with ownership tracking
- **MinimumVersion** — Product-level property with SqlServerVersion enum (Sql2016–Sql2025)
- **Per-table and per-index UpdateFillFactor** — Granular fill factor control at table and index level (OR'd with template setting)
- **ConnectionProperties** — Config section for arbitrary connection string properties, plus `Port` field and `--ConnectionString` CLI override
- **DataTongs: Auto PK detection** — KeyColumns is now optional; auto-detected from primary key or best unique index when blank
- **DataTongs: Geometry and HierarchyID support** — Added handling for GEOMETRY, HIERARCHYID data types; sql_variant/rowversion/timestamp excluded
- **PrintWithNoWait** — Stored procedure for real-time progress logging using RAISERROR WITH NOWAIT
- **WhatIf improvements** — Detailed per-script logging across all phases ("Would APPLY"/"Would SKIP (previously quenched)")
- **RunScriptsTwice** — SchemaQuench setting for resolving cross-dependencies between objects (e.g., views referencing other views)
- **SchemaTongs: Subfolder preservation** — ExtractionFileIndex per-folder tracking; scripts written back to same subfolder on re-extraction
- **SchemaTongs: Orphan detection** — 3 modes: Detect, DetectWithCleanupScripts, DetectDeleteAndCleanup
- **SchemaTongs: Script validation** — Post-extraction syntax validation with `.sqlerror` files for invalid SQL
- **SchemaTongs: CheckConstraintStyle** — Product-level switch for ColumnLevel or TableLevel constraint extraction
- **SchemaHammer: .sqlerror display** — Error indicator and warning banner for invalid scripts
- **Demo products** — AdventureWorks (71 tables) and Northwind (13 tables) with MERGE data scripts and PROVENANCE documentation
- **Self-contained executables** — Single-file builds for all tools across 6 RIDs (win/linux/osx × x64/arm64)
- **Runtime JSON schema generator** — SchemaGenerator replaces static schema files; schemas regenerated on every product init
- **Release workflow** — Automated build, package, and GitHub Release creation via workflow_dispatch
- **Copyright header CI** — Validates headers on all .cs and .sql files on every push
- **TreatWarningsAsErrors** — Enabled globally in Directory.Build.props

### Changed

- **.NET 10** — Upgraded from .NET 9 / .NET 4.8.1 dual-targeting to .NET 10 single target
- **Config files renamed** — `appsettings.json` → `SchemaQuench.settings.json`, `SchemaTongs.settings.json`, `DataTongs.settings.json`
- **SSCL v2.0 license** — Removed organization size and revenue restrictions; feature-based tiers only
- **SchemaTongs: Pure SQL extraction** — Complete rewrite from SMO-based to direct SQL queries; Microsoft.SqlServer.SqlManagementObjects dependency removed
- **`TableData` folder renamed to `Table Data`** — Legacy folders auto-renamed on re-extraction
- **SQL Server 2022 in CI** — Upgraded from 2019; port changed from 1440 to 1450
- **Central NuGet package management** — Version centralization via Directory.Packages.props
- **Demo products reorganized** — Moved to `demo/` directory with dedicated docker-compose
- **Platform naming** — MSSQL → SqlServer in code and Product.json (accepts both on read)

### Removed

- **WiX MSI installer** — Setup/ and SetupAll/ projects removed; distribution via self-contained executables, ZIPs, Chocolatey, and Docker
- **.NET Framework 4.8.1 builds** — All net481 targets and Chocolatey netfx481 packages removed
- **SMO dependency** — Microsoft.SqlServer.SqlManagementObjects NuGet package removed
- **ZipAllTools project** — Replaced by package-tools.ps1/sh scripts
- **Static JSON schema files** — Replaced by runtime SchemaGenerator

### Fixed

- **Column rename via OldName** — Bracket-wrapped names passed to `COLUMNPROPERTY()` silently returned NULL, skipping sp_rename
- **Table/column rename NewColumn flag** — Incorrect marking during renames caused duplicate column creation instead of rename
- **GenerateTableJSON partition reference** — All indexes reported same compression as clustered index due to wrong object reference
- **GenerateTableJSON check constraint lookup** — Replaced `COLUMNPROPERTY()` with direct `sys.columns` join
- **fn_StripParenWrapping** — Trailing whitespace edge case in parenthesis stripping
- **ZipDirectoryWrapper.Exists** — Boundary check prevented correct path matching for zip archive directory entries

## [v1.1.8](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.8) — 2026-02-08

### Fixed
- MSI installer: missing files for .NET 4.8.1 installs, installation path now shown on Finish dialog, corrected default appsettings files
- Batch parser: single quote inside bracketed identifier caused parse failure
- Foreign key and full-text index comparison issues during quench
- SchemaTongs incorrectly filtering all tables with names starting with `sys`

### Changed
- Converted MSI generation to WiX
- Output folders cleaned more thoroughly before packaging

## [v1.1.7](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.7) — 2025-12-19

### Changed
- Simplified binary distribution — fewer download variants
- Simplified DataTongs configuration
- Updated NuGet packages

## [v1.1.6](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.6) — 2025-11-30

### Added
- Platform and edition displayed in version information
- Platform field in Product.json — tool validates platform match at startup

## [v1.1.5](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.5) — 2025-10-06

### Fixed
- DataTongs: incorrect handling of TEXT, NTEXT, and IMAGE columns

## [v1.1.4](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.4) — 2025-09-22

### Added
- ZIP package deployment — SchemaQuench can now deploy from zipped schema packages
- Unified MSI and ZIP installers combining all 3 CLI tools per framework
- Automatic function dependency management — SchemaTongs optionally scripts drop/recreate for computed columns, constraints, and indexes that reference functions
- AfterTablesObjects execution slot for triggers and DDL triggers (moved from Objects slot to avoid dependency errors)

### Changed
- New environment variable prefix for configuration
- Added Code of Conduct

## [v1.1.3](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.3) — 2025-09-05

### Added
- Table and column rename support via `OldName` property
- `--version` / `-v` CLI switch for all tools
- `--ConfigFile:<path>` CLI switch for alternate configuration
- `--LogPath:<path>` CLI switch for relocating logs and log backups
- Object list filter for SchemaTongs — extract only specific named objects

### Fixed
- SchemaTongs ObjectList config bug

## [v1.1.2](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.2) — 2025-08-12

### Changed
- Disabled AOT compilation to work around erroneous Windows Defender virus detection
- Updated NuGet packages

## [v1.1.1](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.1) — 2025-08-11

### Added
- DataTongs: option to disable triggers during data load
- DataTongs integration tests
- CI test summary reporting

### Fixed
- Docker build csproj configuration

## [v1.1.0](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.1.0) — 2025-08-04

### Added
- **DataTongs** — new tool for extracting table data and generating MERGE deployment scripts
  - Configurable MERGE behavior (update, delete, trigger disable)
  - Per-table WHERE filters for row subsetting
  - Special handling for geography, XML, and legacy data types
- TableData execution slot in SchemaQuench for deploying DataTongs scripts
- Product and template version validation

### Fixed
- Logging issues caused by incorrect connection usage in SchemaQuench

## [v1.0.9](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.0.9) — 2025-07-18

### Changed
- Multi-platform Docker support with non-root user (improved Docker Scout score)
- Centralized version setting across all projects

## [v1.0.8](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.0.8) — 2025-07-14

### Changed
- MSI filenames now include framework version

### Fixed
- Database identification script no longer requires a specific column name

## [v1.0.7](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.0.7) — 2025-07-06

### Added
- Double-byte (Unicode) schema element support
- MSI installers for .NET Framework 4.8.1 builds

### Fixed
- Large table quench overflowing length limits
- Tables without columns no longer cause quench errors
- Chocolatey package names corrected to standard

## [v1.0.6](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.0.6) — 2025-06-09

### Added
- SchemaTongs: script token for additional databases

### Fixed
- Password masking in SchemaTongs log output
- Batch parser issues
- SchemaQuench connection drifting to wrong database when scripts contain `USE`
- Blank compression type handling in table quench
- STRING_AGG length overflow with many foreign key drops
- Table quench ignoring new tables with no columns
- ROWVERSION/TIMESTAMP synonym handling
- Column comparison issues

## [v1.0.5](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.0.5) — 2025-06-02

### Added
- Sparse column support
- Dynamic data masking support
- Column-level collation overrides
- Full foreign key cascade action support (NO ACTION, CASCADE, SET NULL, SET DEFAULT)
- Columnstore index support in quench
- Chocolatey packages for SchemaTongs and SchemaQuench (both frameworks)

### Fixed
- Error handling for bad or missing configuration
- Minor product generation fix

## [v1.0.4](https://github.com/Schema-Smith/SchemaSmithyFree/releases/tag/v1.0.4) — 2025-05-01

Initial release of SchemaSmith Community Edition with SchemaQuench (deploy) and SchemaTongs (extract).
