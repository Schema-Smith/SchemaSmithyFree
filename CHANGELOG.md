# Changelog

All notable changes to SchemaSmith Community Edition are documented here.

For full release details and download links, see [GitHub Releases](https://github.com/Schema-Smith/SchemaSmithyFree/releases).

## [Unreleased]

### Added

- **SchemaTongs: Subfolder preservation** — Script folders (`Procedures/`, `Views/`, etc.) now support user-created subfolders for organizing scripts. Extracted scripts are written back to the same subfolder on re-extraction; new objects are placed in the folder root.
- **SchemaTongs: Orphan detection** — New `OrphanHandling:Mode` setting detects script files that no longer correspond to any database object. Modes: `Detect` (log warnings only), `DetectWithCleanupScripts` (log and generate DROP scripts in `MigrationScripts/After/`), `DetectDeleteAndCleanup` (delete orphans and generate DROP scripts).
- **SchemaTongs: Script validation** — New `ShouldCast:ValidateScripts` setting (default: `false`) parses each extracted SQL script for syntax errors. Scripts that fail validation are saved as `.sqlerror` files (or discarded if `ShouldCast:SaveInvalidScripts` is `false`). SchemaQuench skips `.sqlerror` files; SchemaHammer displays them with an error indicator.
- **SchemaTongs: CheckConstraintStyle** — New `Product:CheckConstraintStyle` setting controls whether check constraints in a newly initialized `Product.json` are written as column-level `CheckExpression` properties (`ColumnLevel`, default) or promoted to named table-level constraints (`TableLevel`). Has no effect on existing products.
- **SchemaHammer: .sqlerror display** — `.sqlerror` files are displayed in the script tree with an error indicator. Content is shown read-only with syntax highlighting.

### Changed

- **`TableData` folder renamed to `Table Data`** — The script folder for data synchronization scripts is now named `Table Data` (with a space) for improved readability in file explorers. Legacy `TableData` folders are automatically renamed on re-extraction.

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
