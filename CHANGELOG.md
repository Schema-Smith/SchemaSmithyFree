# Changelog

All notable changes to SchemaSmith Community Edition are documented here.

For full release details and download links, see [GitHub Releases](https://github.com/Schema-Smith/SchemaSmith/releases).

## [Unreleased]

### Added

- **Schema Templates — Multi-Schema Fan-Out.** Templates can now fan out across multiple schemas inside a single database via a new `SchemaIdentificationScript` field, with the active schema available to scripts and JSON as the `{{SchemaName}}` token. Common use: each tenant owns their own schema. New `Template.json` fields: `SchemaIdentificationScript`, `CreateSchemaIfMissing` (default `false`), `AllowParallel` (default `true`), `ContinueOnSchemaFailure` (default `true`). Supported on SQL Server and PostgreSQL. See the [Multi-Tenant Deployments](docs/end-user/guide/10-multi-tenant-deployments.md) chapter and the new `TenantCRM` demo for the end-to-end walkthrough. Originally proposed by Christopher Baker.
- **`ForceReKindle` — force re-install of helper objects.** New `SchemaQuench.settings.json` setting (default `false`) and `--ForceReKindle` CLI switch that bypasses the new version-stamp skip and re-installs the SchemaSmith helper procedures/tables unconditionally. Useful after a manual edit to the helper objects or when diagnosing a kindle problem; normal deployments leave it off and pay the kindle cost only when the tooling actually changes.
- **`ContinueOnDatabaseFailure` setting.** Failure-isolation parity at the database level on regular templates. Default `true` matches existing behavior.
- **`Target` — Selective Execution Scope.** New `Target:Templates`, `Target:Databases`, and `Target:Schemas` array filters in `SchemaQuench.settings.json`. Common use: deploy to a single newly-onboarded tenant without re-running the full product. `PruneObsoleteMigrationTracking` is restricted to the targeted scope when `Target` filters are active, so excluded schemas keep their tracking rows untouched.
- **Schema-Template Extraction in SchemaTongs and DataTongs.** Both tools gain schema-template extraction modes via `Source:Schema` (both SchemaTongs and DataTongs). Source-schema-qualified references in extracted SQL bodies are rewritten to `{{SchemaName}}`; cross-schema references are preserved literally. Lets you cast one canonical hand-replicated schema into a schema template that fans out to the rest.

### Breaking Changes

- **`Template.Required` renamed to `RequireAtLeastOneTarget`.** The old name read as "this template must load" but actually meant "discovery must return ≥1 database (or ≥1 `(database, schema)` pair for schema templates), else fail." The new name is self-describing. Unknown JSON properties are ignored at deserialization, so an unmigrated `Template.json` silently picks up the new property's default (`true`) — which surfaces as an explicit "no targets discovered for template" error rather than a silent behavior change. **Migration:** find-and-replace `"Required":` with `"RequireAtLeastOneTarget":` in every `Template.json` in your schema packages. The change applies to every platform.

### Changed

- **Migration tracking table schema.** `SchemaSmith.CompletedMigrationScripts` gains `template_name` and `schema_name` columns. Existing rows are preserved with empty values; reads use a permissive `template_name` match against legacy rows so no previously-completed migrations re-run. Schema migration is idempotent and runs as part of `KindleTheForge`.
- **Failure scoping consolidated per template type.** `ContinueOnSchemaFailure` now governs every failure inside a schema template (discovery, reserved-name rejection, per-iteration script failure, `CREATE SCHEMA` failure, dispatcher exceptions). `ContinueOnDatabaseFailure` now governs every failure inside a regular template. Setting `ContinueOnDatabaseFailure` on a schema template has no effect; setting `ContinueOnSchemaFailure` on a regular template has no effect. **Prior behavior:** the two flags layered ambiguously — a schema template's discovery failure (e.g., a reserved name like `dbo` returned by `SchemaIdentificationScript`) was incorrectly classified as a database-level failure and aborted under `ContinueOnDatabaseFailure: false`, even when `ContinueOnSchemaFailure: true` should have let it continue. The new contract is "the template's type determines which flag governs its failures" — no more cross-flag mental gymnastics.

### Fixed

- **`--ConnectionString` override database retargeting** — Per-database operations (schema discovery, per-iteration execution) now retarget the override connection string to the actual target database instead of reusing the override's embedded database (e.g., `master` / `postgres`) for every operation. Thanks to @noctelvirei. — #248
- **Completed migration script tracking SQL literals** — Product names, quench slots, and script paths are now escaped before being embedded in completed-script tracking SQL. Thanks to @noctelvirei and @zacnaloen.
- **DataDelivery content file failures** — Declared data delivery files now abort deployment when missing or unreadable instead of logging `SKIPPING` and continuing without delivering the table data. Thanks to @noctelvirei and @zacnaloen.
- **ZIP package file reads for data delivery and binary tokens** — DataDelivery content files now use the package-aware file wrapper, and ZIP-backed packages can resolve binary file tokens through `ReadAllBytes`. Thanks to @noctelvirei and @zacnaloen.
- **Product-level script routing for SQL Server secondary servers** — Product-level script folders configured for secondary servers now open the command against the routed server instead of always using the primary server connection. Thanks to @noctelvirei (first PR — welcome to the Forge!) and @zacnaloen.
- **TaskQueueManager wedge on uncaught work-procedure exceptions** — When a work procedure threw, the failed worker was never removed from the queue's working set, hanging `WaitForAll` and reducing effective capacity by one per failure. Parallel work in `ProductQuench` (server/database quench), `Template` (per-table token resolution), `ScriptFolder` (parallel file load), and `TokenHelper` (file-token resolution) could silently hang on any uncaught exception inside a work item. The worker now wraps the work procedure in `try`/`finally` so the completion handshake always runs.
- **Deadlock resilience for parallel deployments** — When many schemas (or databases) deploy concurrently, the database engine can choose one iteration as a deadlock victim while it mutates the shared system catalog. SchemaSmith now recognizes the deadlock (SQL Server 1205, PostgreSQL `40P01`, MySQL 1213) and automatically retries the affected table/index/constraint/view quench — which is idempotent — with backoff until it converges, instead of failing that iteration. This makes `AllowParallel` deployments robust at high schema/database fan-out across SQL Server, PostgreSQL, and MySQL.
- **Parallel kindle collisions on a shared database** — When multiple deployments installed the SchemaSmith helper objects into the same database at the same time (parallel product loads targeting a shared admin database, or high-fan-out schema-template iterations), concurrent `CREATE OR REPLACE` / `CREATE OR ALTER` could collide and abort one of the runs. SchemaSmith now records a content-hash stamp of the kindled object set per database and installs them at most once per content-version, serialized by a session lock. Re-installs only fire when the kindle content actually changes, when the stamp is missing, or when `ForceReKindle` is set. — #251

## [v2.0.0](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v2.0.0) — 2026-05-06

### Added

- **Multi-platform support** — SchemaSmith now supports PostgreSQL and MySQL alongside SQL Server across all three CLI tools (SchemaQuench, SchemaTongs, DataTongs)
- **Platform-specific domain models** — Dedicated domain types for PostgreSQL (materialized views, exclude constraints, range types) and MySQL (multi-column full-text indexes, generated columns, tablespace support)
- **ShouldApplyExpression** — Template-level conditional deployment using SQL expressions evaluated at runtime
- **Secondary servers** — Deploy the same product to additional server instances in a single run
- **Custom script folders** — User-defined script execution slots beyond the built-in folder structure
- **Extensions carrier** — User-extensible `Extensions` (JToken) on every domain object (Table, Column, Index, ForeignKey, IndexedView, MaterializedView, etc.). Because Extensions serialize alongside core properties, any custom metadata you attach is queryable from your scripts through the `{{TableSchema}}`, `{{IndexedViewSchema}}`, and `{{MaterializedViewSchema}}` auto-tokens — or through per-object tokens like `<*SpecificTable*>`. Opens the door to replication metadata, data dictionaries, environment-driven behavior, custom validation rules, anything your deployment needs. Preserved during SchemaTongs re-extraction.
- **Modular table quench** — Monolithic TableQuench replaced with focused procedures: MissingTableAndColumnQuench, ModifiedTableQuench, MissingIndexesAndConstraintsQuench, ForeignKeyQuench, plus ParseTableJsonIntoTempTables for shared JSON parsing
- **IndexOnlyQuench** — Template-level `IndexOnlyTableQuenches` mode for managing indexes without modifying table structure
- **Expanded execution slots** — 9 total (7 template + 2 product): added BetweenTablesAndKeys, AfterTablesScripts, Product Before, Product After
- **Indexed view support (SQL Server)** — SchemaTongs extraction, SchemaQuench diff-based deployment; index-only changes skip view rebuild
- **GenerateIndexedViewJson / IndexedViewQuench** — Stored procedures for indexed view extraction and deployment with ownership tracking
- **Materialized view support (PostgreSQL)** — SchemaTongs extraction and SchemaQuench diff-based deployment of PostgreSQL materialized views with full index management; index-only changes skip the materialized view rebuild
- **GenerateMaterializedViewJson / MaterializedViewQuench / MissingMaterializedViewIndexesQuench** — Stored procedures for materialized view extraction and deployment, with ownership tracking and validation/fixup helpers
- **Per-table and per-index UpdateFillFactor** — Granular fill factor control at table and index level (OR'd with template setting)
- **ConnectionProperties** — Config section for arbitrary connection string properties, plus `Port` field and `--ConnectionString` CLI override
- **DataTongs: Auto PK detection** — KeyColumns is now optional; auto-detected from primary key or best unique index when blank
- **DataTongs: Geometry and HierarchyID support** — Added handling for GEOMETRY, HIERARCHYID data types; sql_variant/rowversion/timestamp excluded
- **DataTongs: MySQL tokenization** — Full token resolution support for MySQL merge scripts
- **WhatIf improvements** — Detailed per-script logging across all phases ("Would APPLY"/"Would SKIP (previously quenched)")
- **RunScriptsTwice** — SchemaQuench setting that runs object scripts twice to verify idempotency; a CI/testing tool for catching `[ALWAYS]` script bugs before production
- **SchemaTongs: Subfolder preservation** — ExtractionFileIndex per-folder tracking; scripts written back to same subfolder on re-extraction
- **SchemaTongs: Orphan detection** — 3 modes: Detect, DetectWithCleanupScripts, DetectDeleteAndCleanup
- **SchemaTongs: Script validation** — Post-extraction syntax validation with `.sqlerror` files for invalid SQL
- **SchemaTongs: CheckConstraintStyle** — Product-level switch for ColumnLevel or TableLevel constraint extraction
- **SchemaTongs: --WriteSchemasOnly** — Regenerate JSON schema files from C# types without a database connection
- **Simple tokens in every script** — `{{TokenName}}` resolution extended to every script folder — Before/After, object scripts, migrations, table data — not just the select few it used to work in. Tokens are defined in Product.json and Template.json with environment-variable overrides, so one package parameterizes cleanly across dev, test, and prod.
- **Advanced token tags** — Token values can now carry `<*Query*>` (result of an inline SQL query), `<*QueryFile*>` (query loaded from a file), `<*File*>` / `<*BinaryFile*>` (file contents as text or hex), and `<*SpecificTable*>` / `<*SpecificIndexedView*>` / `<*SpecificMaterializedView*>` (single-object JSON). Resolvable anywhere simple tokens are — object scripts, migrations, validation, everywhere.
- **`{{TableSchema}}` / `{{IndexedViewSchema}}` / `{{MaterializedViewSchema}}` auto-tokens** — The template's full table, indexed view, and materialized view definitions are exposed as JSON tokens at deployment time. Combined with the Extensions carrier, scripts can query any core OR custom property through standard JSON operations — no hand-authored metadata pipeline required.
- **Parallel execution** — Parallel template processing across all tools
- **Parallel file token resolution** — Token replacement and script loading parallelized within folders
- **VerboseLogging setting** — Controls whether SQL informational messages appear in deployment logs; when disabled (default), noisy SQL info messages are suppressed
- **Template Required property** — Marks templates as required so misconfigured deploys fail fast instead of silently skipping
- **Template SkipIfReadOnly property** — Skips templates targeting Availability Group read-only replicas
- **TrackRunOnceMigrations setting** — Tracks run-once migration scripts for datafix pipeline scenarios
- **PruneObsoleteMigrationTracking setting** — Cleans up tracking records for migration scripts that no longer exist in the package
- **KindleTheForge / UpdateTables / DropTablesRemovedFromProduct toggles** — SchemaQuench config switches for datafix pipeline scenarios
- **Filesystem-illegal character handling** — FileNameEncoder percent-encodes `\ / : * ? " < > |` in output filenames; original names preserved in content
- **Demo products** — AdventureWorks, Chinook, Northwind, and Sakila across all three platforms with MERGE data scripts and docker-compose validation
- **Self-contained executables** — Single-file builds for all tools across 6 RIDs (win/linux/osx × x64/arm64)
- **Runtime JSON schema generator** — SchemaGenerator replaces static schema files; schemas regenerated on every product init
- **Release workflow** — Automated build, package, and GitHub Release creation via workflow_dispatch
- **Authenticode signing** — Windows binaries (`SchemaQuench.exe`, `SchemaTongs.exe`, `DataTongs.exe`) are signed via Azure Trusted Signing on every release. Eliminates SmartScreen "Windows protected your PC" warnings and lets users verify provenance with `signtool verify /pa /v`.
- **Chocolatey package** — `choco install schemasmith` installs all three CLI tools as a single combined package on Windows. Embedded signed binaries — no checksum maintenance, no .NET runtime install needed. Triggered automatically on GitHub Release publish.
- **Linux `.deb` and `.rpm` packages** — single combined `schemasmith` package per (`amd64`/`arm64`) × (`.deb`/`.rpm`) covers Debian/Ubuntu and RHEL/Fedora/Amazon Linux. `dpkg -i` / `rpm -i` installs all three CLI commands (`schemaquench`, `schematongs`, `datatongs`) onto PATH from one download — binaries land under `/usr/lib/schemasmith/` with `/usr/bin/` symlinks. Zero declared dependencies; the bundled binaries are fully self-contained. Built via nfpm and attached to every GitHub Release alongside the bundle ZIPs.
- **Cross-platform `install.sh`** — single POSIX-sh script that detects OS and architecture (Linux/macOS, x64/arm64), resolves the latest release without a GitHub API token, downloads the matching `.tar.gz` bundle, verifies SHA-256 against the release manifest, and installs the three CLIs onto PATH. `curl -fsSL https://raw.githubusercontent.com/Schema-Smith/SchemaSmith/main/packaging/install/install.sh | sh` is the canonical invocation. Supports `INSTALL_VERSION` and `INSTALL_DIR` env-var overrides.
- **Release-level `SHA256SUMS` manifest** — every GitHub Release publishes a single `SHA256SUMS` file covering every artifact (bundle archives, `.deb`, `.rpm`). Enables one-shot verification with `sha256sum -c SHA256SUMS` (Linux) or `shasum -a 256 -c SHA256SUMS` (macOS) after downloading the artifacts you want; `install.sh` performs the same check automatically.
- **Linux and macOS bundles in `.tar.gz`** — Linux and macOS RIDs ship as `.tar.gz` instead of `.zip` for native compatibility with `tar -xzf`, `install.sh`, and standard Unix tooling. Windows bundles continue as `.zip`.
- **Libicu-independent runtime** — Self-contained Linux publishes of all three CLI tools bundle a private ICU runtime (`Microsoft.ICU.ICU4C.Runtime` 72.1.0.3) so the binaries run on minimal Linux containers (slim Docker images, hardened distros) that ship without `libicu`. Three ICU shared libraries (`libicudata`, `libicui18n`, `libicuuc`) install alongside the binaries in a single dir — `/usr/lib/schemasmith/` for `.deb`/`.rpm` packages — and one shared set serves all three CLIs. Zero declared system dependencies for ICU on the Linux package side.
- **Copyright header CI** — Validates headers on all .cs and .sql files on every push
- **TreatWarningsAsErrors** — Enabled globally in Directory.Build.props
- **Multi-platform CI** — Parallel SQL Server, PostgreSQL, and MySQL integration test jobs with service containers
- **Checkpoint/resume for SchemaQuench** — `--ResumeQuench` and `--CheckpointDirectory` skip already-completed steps and migration scripts after a failed run; checkpoints cleaned up automatically on success
- **FK-aware data delivery** — Declarative `DataDelivery` block on table JSON drives automatic foreign-key dependency ordering; two-pass delivery handles nullable FK columns without hand-authored merge scripts
- **DataTongs `--ConfigureDataDelivery`** — Writes `DataDelivery` settings (ContentFile, MergeType, MatchColumns, MergeFilter, and trigger/rule flags) into table JSON files after extraction so the declarative pipeline can take over

### Changed

- **.NET 10** — Upgraded from .NET 9 / .NET 4.8.1 dual-targeting to .NET 10 single target
- **Config files renamed** — `appsettings.json` → `SchemaQuench.settings.json`, `SchemaTongs.settings.json`, `DataTongs.settings.json`
- **SSCL v2.0 license** — Removed organization size and revenue restrictions; feature-based tiers only
- **SchemaTongs: Pure SQL extraction** — Complete rewrite from SMO-based to direct SQL queries; Microsoft.SqlServer.SqlManagementObjects dependency removed
- **`TableData` folder renamed to `Table Data`** — Legacy folders auto-renamed on re-extraction
- **SQL Server integration CI** — Runs SQL Server 2019 on port 1440, matching the checked-in integration test settings
- **Central NuGet package management** — Version centralization via Directory.Packages.props
- **Demo products reorganized** — Per-platform directories under `Demos/` with dedicated docker-compose per platform
- **Platform naming** — MSSQL → SqlServer in code and Product.json (accepts both on read)
- **Solution renamed** — SchemaSmithyFree.sln → SchemaSmith.sln
- **Test layout restructured** — Test projects nested inside their component directories
- **Token format** — DataTongs uses `{{TokenName}}` double-brace format (was triple-brace)
- **Batch splitter optimization** — Splits before token resolution to avoid processing expanded multi-MB content
- **SqlScript.TokenReplace** — O(1) dictionary lookup replaces O(n) regex scan

### Removed

- **WiX MSI installer** — Setup/ and SetupAll/ projects removed; distribution via self-contained executables, ZIPs, Chocolatey, and Docker
- **.NET Framework 4.8.1 builds** — All net481 targets and Chocolatey netfx481 packages removed
- **SMO dependency** — Microsoft.SqlServer.SqlManagementObjects NuGet package removed
- **ZipAllTools project** — Replaced by packaging scripts
- **Static JSON schema files** — Replaced by runtime SchemaGenerator

### Fixed

- **Column rename via OldName** — Bracket-wrapped names passed to `COLUMNPROPERTY()` silently returned NULL, skipping sp_rename
- **Table/column rename NewColumn flag** — Incorrect marking during renames caused duplicate column creation instead of rename
- **GenerateTableJSON partition reference** — All indexes reported same compression as clustered index due to wrong object reference
- **GenerateTableJSON check constraint lookup** — Replaced `COLUMNPROPERTY()` with direct `sys.columns` join
- **fn_StripParenWrapping** — Trailing whitespace edge case in parenthesis stripping
- **ZipDirectoryWrapper.Exists** — Boundary check prevented correct path matching for zip archive directory entries
- **DataTongs empty tables** — Skip empty tables instead of generating invalid MERGE scripts
- **Identity removal** — Data-preserving column swap now supports removing identity from a column
- **MustSwapColumn** — Aligned column swap detection across all platforms
- **CommandLineParser null safety** — Added null-conditional in `ValueOfSwitch` to prevent NullReferenceException
- **Product script folder names** — Aligned folder naming convention
- **Single quote escaping** — Proper escaping for `<*File*>` token values inside SQL string literals
- **Backslash escaping for MySQL** — Platform-specific escaping (SQL Server/PostgreSQL don't need it)
- **TrustServerCertificate default** — Removed from platform-agnostic defaults (broke MySQL connections)
- **Docker release image UID** — Fixed UID 1000 conflict with Ubuntu 24.04 base image
- **`<*BinaryFile*>` PostgreSQL output** — Resolver now emits PostgreSQL `BYTEA` literal syntax (`E'\\x...'::bytea`) when the product platform is PostgreSQL; SQL Server and MySQL continue to receive `0x<hex>` literals. Previously emitted `0x<hex>` unconditionally, which parses as an invalid integer on PostgreSQL and silently broke binary token insertion into BYTEA columns.

## [v1.1.8](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.8) — 2026-02-08

### Fixed
- MSI installer: missing files for .NET 4.8.1 installs, installation path now shown on Finish dialog, corrected default appsettings files
- Batch parser: single quote inside bracketed identifier caused parse failure
- Foreign key and full-text index comparison issues during quench
- SchemaTongs incorrectly filtering all tables with names starting with `sys`

### Changed
- Converted MSI generation to WiX
- Output folders cleaned more thoroughly before packaging

## [v1.1.7](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.7) — 2025-12-19

### Changed
- Simplified binary distribution — fewer download variants
- Simplified DataTongs configuration
- Updated NuGet packages

## [v1.1.6](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.6) — 2025-11-30

### Added
- Platform and edition displayed in version information
- Platform field in Product.json — tool validates platform match at startup

## [v1.1.5](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.5) — 2025-10-06

### Fixed
- DataTongs: incorrect handling of TEXT, NTEXT, and IMAGE columns

## [v1.1.4](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.4) — 2025-09-22

### Added
- ZIP package deployment — SchemaQuench can now deploy from zipped schema packages
- Unified MSI and ZIP installers combining all 3 CLI tools per framework
- Automatic function dependency management — SchemaTongs optionally scripts drop/recreate for computed columns, constraints, and indexes that reference functions
- AfterTablesObjects execution slot for triggers and DDL triggers (moved from Objects slot to avoid dependency errors)

### Changed
- New environment variable prefix for configuration
- Added Code of Conduct

## [v1.1.3](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.3) — 2025-09-05

### Added
- Table and column rename support via `OldName` property
- `--version` / `-v` CLI switch for all tools
- `--ConfigFile:<path>` CLI switch for alternate configuration
- `--LogPath:<path>` CLI switch for relocating logs and log backups
- Object list filter for SchemaTongs — extract only specific named objects

### Fixed
- SchemaTongs ObjectList config bug

## [v1.1.2](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.2) — 2025-08-12

### Changed
- Disabled AOT compilation to work around erroneous Windows Defender virus detection
- Updated NuGet packages

## [v1.1.1](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.1) — 2025-08-11

### Added
- DataTongs: option to disable triggers during data load
- DataTongs integration tests
- CI test summary reporting

### Fixed
- Docker build csproj configuration

## [v1.1.0](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.1.0) — 2025-08-04

### Added
- **DataTongs** — new tool for extracting table data and generating MERGE deployment scripts
  - Configurable MERGE behavior (update, delete, trigger disable)
  - Per-table WHERE filters for row subsetting
  - Special handling for geography, XML, and legacy data types
- TableData execution slot in SchemaQuench for deploying DataTongs scripts
- Product and template version validation

### Fixed
- Logging issues caused by incorrect connection usage in SchemaQuench

## [v1.0.9](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.0.9) — 2025-07-18

### Changed
- Multi-platform Docker support with non-root user (improved Docker Scout score)
- Centralized version setting across all projects

## [v1.0.8](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.0.8) — 2025-07-14

### Changed
- MSI filenames now include framework version

### Fixed
- Database identification script no longer requires a specific column name

## [v1.0.7](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.0.7) — 2025-07-06

### Added
- Double-byte (Unicode) schema element support
- MSI installers for .NET Framework 4.8.1 builds

### Fixed
- Large table quench overflowing length limits
- Tables without columns no longer cause quench errors
- Chocolatey package names corrected to standard

## [v1.0.6](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.0.6) — 2025-06-09

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

## [v1.0.5](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.0.5) — 2025-06-02

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

## [v1.0.4](https://github.com/Schema-Smith/SchemaSmith/releases/tag/v1.0.4) — 2025-05-01

Initial release of SchemaSmith Community Edition with SchemaQuench (deploy) and SchemaTongs (extract).
