# SchemaSmithyFree — Complete Feature List

SchemaSmithyFree is a **state-based database migration toolset for SQL Server**, analogous to Terraform but for databases. Rather than tracking schema evolution through sequential migrations, you define the desired state as metadata (JSON + SQL), and the tools transform any target SQL Server to match.

---

## Four Tools

### 1. SchemaTongs — Schema Extraction
Extracts a live SQL Server database's schema into a versioned schema package using pure SQL queries — no external dependencies (SMO removed in v2).

**Extractable Object Types (12):**
| Object | Output Format | Folder |
|--------|--------------|--------|
| Tables | JSON definitions | `Tables/` |
| Indexed views | JSON definitions | `Indexed Views/` |
| Schemas | SQL scripts | `Schemas/` |
| User-defined data types | SQL | `DataTypes/` |
| User-defined table types | SQL | `DataTypes/` |
| Scalar & table-valued functions | SQL | `Functions/` |
| Views | SQL | `Views/` |
| Stored procedures | SQL | `Procedures/` |
| DML triggers | SQL | `Triggers/` |
| DDL triggers | SQL | `DDLTriggers/` |
| Full-text catalogs | SQL | `FullTextCatalogs/` |
| Full-text stop lists | SQL | `FullTextStopLists/` |
| XML schema collections | SQL | `XMLSchemaCollections/` |

**Extraction Controls:**
- Per-object-type toggle flags (`ShouldCast`) to selectively include/exclude
- Object list filter — extract only specific named objects (case-insensitive)
- `CREATE OR ALTER` formatting for procedures and functions
- Extended properties preserved on programmable objects
- Auto-exclusion of system objects, SchemaSmith infrastructure, and encrypted objects
- Dynamic dependency removal scripting for functions (drops/recreates computed columns, constraints, indexes that reference a function)

**Orphan Detection:**
- Detects script files that no longer correspond to database objects
- Three modes: `Detect` (log only), `DetectWithCleanupScripts` (generate DROP scripts), `DetectDeleteAndCleanup` (delete and generate DROP scripts)

**Script Validation:**
- Post-extraction syntax validation of extracted SQL
- Invalid scripts saved as `.sqlerror` files (configurable via `SaveInvalidScripts`)
- SchemaQuench skips `.sqlerror` files; SchemaHammer displays them with error indicators

**Subfolder Preservation:**
- User-created subfolders within script directories are preserved on re-extraction
- `ExtractionFileIndex` per-folder tracking ensures scripts return to their original subfolder

**CheckConstraintStyle:**
- Product-level setting: `ColumnLevel` (default) or `TableLevel`
- Controls whether check constraints are written as column properties or named table-level constraints

**Package Initialization:**
- First run auto-creates full directory structure with all folders
- Generates `Product.json`, `Template.json`, and JSON schema validation files (runtime-generated via SchemaGenerator)
- Subsequent runs update scripts and table definitions without overwriting config

---

### 2. SchemaQuench — Schema Deployment Engine
Applies schema packages to target SQL Server databases using state-based comparison.

**Execution Slots (9 total, in order):**

*Product-level (outside template loop):*
1. **Product Before** — Product-level scripts (`ProductScripts/Before/`)

*Template-level (per database):*
2. **Before** — Sequential, tracked migration scripts (`MigrationScripts/Before/`)
3. **Objects** — Schemas, types, functions, views, procedures with automatic dependency retry loop
4. **Table Quench** — State-based table creation/alteration/dropping via modular procedures
5. **BetweenTablesAndKeys** — Migration scripts after table structure but before foreign keys (`MigrationScripts/BetweenTablesAndKeys/`)
6. **AfterTablesScripts** — Migration scripts after tables are fully updated (`MigrationScripts/AfterTablesScripts/`)
7. **AfterTablesObjects** — Triggers and DDL triggers with dependency retry
8. **Table Data** — Data synchronization scripts with dependency retry (`Table Data/` folder)
9. **After** — Sequential, tracked migration scripts (`MigrationScripts/After/`)

*Product-level (after all templates):*
10. **Product After** — Product-level scripts (`ProductScripts/After/`)

**Table Quench (State-Based Table Management):**
- Modular architecture: 4 focused procedures (MissingTableAndColumnQuench, ModifiedTableQuench, MissingIndexesAndConstraintsQuench, ForeignKeyQuench) replacing monolithic TableQuench
- Creates, alters, and drops columns, indexes, constraints automatically
- Compares desired JSON state vs. current database state, makes only necessary changes
- Per-table and per-index `UpdateFillFactor` for granular fill factor control (OR'd with template setting)
- Automatic index rebuilds when table structure changes
- Debug SQL output file for audit trail
- Idempotent — safe to run repeatedly

**Indexed View Deployment:**
- Diff-based change detection — views only rebuild when definitions change
- Index-only changes (add/modify/drop indexes on a view) don't trigger view rebuilds
- Ownership tracking via extended properties to prevent cross-product conflicts
- Clustered index required — validated at quench time

**Migration Script Features:**
- One-time execution tracking via `CompletedMigrationScripts` table
- `[ALWAYS]` suffix forces re-execution every run
- Batch splitting on `GO` statements
- Alphabetical ordering within folders
- Obsolete tracking entries auto-cleaned

**Multi-Database Targeting:**
- Single run can update multiple databases on the same server
- `DatabaseIdentificationScript` dynamically discovers target databases
- Ordered template processing for multi-template products

**WhatIf Dry-Run Mode:**
- Set `WhatIfONLY: true` to generate SQL preview without executing
- Validation scripts still execute
- Output saved to reviewable SQL file

**Drift Cleanup:**
- `DropUnknownIndexes` removes indexes not defined in the schema package
- Brings database back into fully managed desired state

**Index-Only Mode:**
- `IndexOnlyTableQuenches` template setting restricts quench to index management only
- Skips table creation, column changes, and foreign key management
- Only adds, modifies, and drops indexes, statistics, XML indexes, and full-text indexes
- Tables that don't exist are silently skipped

**Validation & Verification:**
- Product-level validation script (runs once before any template)
- Template-level baseline validation (per database)
- Version stamp scripts at product and template level
- Aborts on validation failure

**Additional SchemaQuench Settings:**
- `RunScriptsTwice` — Re-executes object scripts to resolve cross-dependencies (e.g., views referencing other views)
- `MinimumVersion` — Product-level SqlServerVersion enum (Sql2016–Sql2025) for version-gated features

**Auto-Deployed Infrastructure ("Kindling the Forge"):**
- `SchemaSmith` schema created automatically
- `SchemaSmith.MissingTableAndColumnQuench` — Creates missing tables, adds missing columns
- `SchemaSmith.ModifiedTableQuench` — Alters existing columns (type, nullability, defaults, etc.)
- `SchemaSmith.MissingIndexesAndConstraintsQuench` — Creates missing indexes, constraints, statistics
- `SchemaSmith.ForeignKeyQuench` — Creates and drops foreign keys
- `SchemaSmith.IndexOnlyQuench` — Index-only management mode (skips table/column changes)
- `SchemaSmith.IndexedViewQuench` — Indexed view deployment with diff-based change detection
- `SchemaSmith.GenerateTableJson` — Reverse-engineers table state to JSON
- `SchemaSmith.GenerateIndexedViewJson` — Reverse-engineers indexed view state to JSON
- `SchemaSmith.PrintWithNoWait` — Real-time progress logging via RAISERROR WITH NOWAIT
- `ParseTableJsonIntoTempTables` — Shared JSON parsing for modular procedures (embedded, executed inline)
- Helper functions (`fn_SafeBracketWrap`, `fn_StripBracketWrapping`, `fn_StripParenWrapping`, `fn_FormatJson`)
- `CompletedMigrationScripts` tracking table
- Updated to match tool version on every run

---

### 3. DataTongs — Data Synchronization
Extracts table data and generates self-contained MERGE scripts.

**Output:** One `Populate schema.tablename.sql` file per table containing:
- OPENJSON-based data loading from embedded JSON
- Optional `DISABLE TRIGGER ALL` / `ENABLE TRIGGER ALL` wrapping
- Automatic `SET IDENTITY_INSERT ON/OFF` handling
- NULL-safe change detection in UPDATE conditions

**Configurable MERGE Behavior:**
- `MergeUpdate` — include WHEN MATCHED THEN UPDATE clause (default: true)
- `MergeDelete` — include WHEN NOT MATCHED BY SOURCE THEN DELETE (default: false)
- `DisableTriggers` — wrap with trigger disable/enable (default: true)

**Table Selection:**
- Name (schema.table format)
- Key columns for MERGE ON clause (optional — auto-detected from primary key or best unique index when blank)
- Nullable key columns supported with `*` prefix
- Optional WHERE filter for row subsetting
- Empty tables automatically skipped

**Special Data Type Handling:**
- Geography → WKT (Well-Known Text) with `STGeomFromText()` restoration
- Geometry → WKT with `geometry::STGeomFromText()` restoration
- HierarchyID → canonical string with `hierarchyid::Parse()` restoration
- XML → CAST to NVARCHAR(MAX)
- NTEXT/TEXT/IMAGE → CAST to comparable types
- sql_variant, rowversion, and timestamp columns automatically excluded
- Computed columns and ROWGUIDCOL columns automatically excluded
- Identity columns detected and managed

---

### 4. SchemaHammer — Desktop Schema Viewer
A read-only desktop application for browsing SchemaSmith schema packages visually.

**Navigation:**
- Product tree with lazy-loaded nodes (Product → Templates → Tables/Scripts/Indexed Views → child items)
- Back button with history dropdown
- Last selection restoration on reopen
- Recent products list (up to 10)

**Schema Viewing (All Read-Only):**
- Product, Template, Table, Column, Index, ForeignKey, CheckConstraint, Statistic, XmlIndex, FullTextIndex, and IndexedView property editors
- SQL script viewing with T-SQL syntax highlighting
- Script token preview — toggle between raw `{{Token}}` placeholders and expanded values
- Container/folder editors with child summary

**Search:**
- Tree search with Contains, Begins With, Ends With matching
- Code search across SQL script files, table metadata, and script tokens
- In-editor find bar with next/previous, match count, and case toggle
- Token navigation — double-click `{{Token}}` in SQL to navigate to its definition

**Tools:**
- Update Schema Files — regenerates `.json-schemas/` validation files
- Light and dark themes

---

## SQL Server Features Supported

**Table-Level:**
- Data compression (NONE, ROW, PAGE)
- Temporal tables (system-versioned)
- Table rename tracking via `OldName`
- `UpdateFillFactor` — per-table fill factor override (OR'd with template and index settings)

**Column-Level:**
- All SQL Server data types with precision/scale/length
- Identity columns (seed and increment)
- Computed columns (persisted and non-persisted)
- Sparse columns
- Column-level collation overrides
- Dynamic data masking
- Default constraints
- Column-level check constraints
- Nullable control
- Column rename tracking via `OldName`

**Index-Level:**
- Clustered and non-clustered indexes
- Primary key constraints
- Unique indexes and unique constraints
- Columnstore indexes (clustered and non-clustered)
- Filtered indexes with WHERE expressions
- Covering indexes with INCLUDE columns
- Fill factor control
- `UpdateFillFactor` — per-index fill factor override (OR'd with template and table settings)
- Index-level compression (NONE, ROW, PAGE)
- DESC column ordering
- XML indexes (PRIMARY, and secondary PATH/VALUE/PROPERTY)

**Relationships & Constraints:**
- Foreign keys with cascade actions (NO ACTION, CASCADE, SET NULL, SET DEFAULT)
- Cross-schema foreign keys
- Table-level and column-level check constraints

**Full-Text Search:**
- Full-text catalogs and stop lists
- Full-text indexes with per-column language and type weight
- Change tracking modes (OFF, MANUAL, AUTO)

**Other:**
- User-defined statistics (single/multi-column, filtered, with sample size)
- Extended properties preserved on programmable objects
- XML schema collections

---

## Schema Package Structure

```
Product/
  Product.json
  .json-schemas/          (runtime-generated validation schemas)
  .community              (edition marker)
  ProductScripts/
    Before/               (SQL — runs once before all templates)
    After/                (SQL — runs once after all templates)
  Templates/
    TemplateName/
      Template.json
      Tables/             (JSON per table)
      Indexed Views/      (JSON per indexed view)
      Schemas/            (SQL)
      DataTypes/          (SQL)
      FullTextCatalogs/   (SQL)
      FullTextStopLists/  (SQL)
      XMLSchemaCollections/ (SQL)
      Functions/          (SQL)
      Views/              (SQL)
      Procedures/         (SQL)
      Triggers/           (SQL)
      DDLTriggers/        (SQL)
      MigrationScripts/
        Before/                 (SQL)
        BetweenTablesAndKeys/   (SQL)
        AfterTablesScripts/     (SQL)
        After/                  (SQL)
      Table Data/         (SQL)
```

Packages can be deployed from **folders** or **ZIP archives**.

---

## Script Token System

- Placeholder format: `{{TokenName}}`
- Case-insensitive replacement
- Automatic tokens: `{{ProductName}}`, `{{TemplateName}}`
- Applied to all SQL scripts and JSON config files
- Override hierarchy: Product.json → `{ToolName}.settings.json` → environment variables → CLI switches

---

## Configuration System

**Loading Hierarchy (lowest to highest priority):**
1. `{ToolName}.settings.json` (in tool directory or via `--ConfigFile`)
2. User secrets (debug builds only)
3. Environment variables (`SmithySettings_` prefix, `__` as hierarchy separator)
4. Command-line switches

**Authentication:**
- Windows integrated authentication (default when user/password blank)
- SQL Server authentication (user + password)

**Connection Properties:**
- `ConnectionProperties` config section for arbitrary connection string properties (e.g., `TrustServerCertificate`, `ApplicationIntent`)
- `Port` field for non-default SQL Server ports
- `--ConnectionString` CLI override for full connection string control

**CLI Switches (all tools):**
- `--version` / `-v` / `--ver`
- `--help` / `-h` / `-?`
- `--ConfigFile:<path>`
- `--LogPath:<path>`
- `--ConnectionString:<connstr>`
- Switch prefixes: `--` or `/`, separators: `:` or `=`

---

## Logging & Diagnostics

- Dual log streams per tool: Progress log + Error log
- Logs overwritten at start, backed up to numbered subdirectories on completion
- Passwords redacted in configuration logging
- Startup banner with version, platform, and edition
- Debug SQL file for table quench operations
- Configurable log path

**Exit Codes:**
- 0 = success
- 2 = one or more database quenches failed
- 3 = unhandled exception
- 4 = log backup failure

---

## Platform Support

Self-contained single-file executables — no .NET runtime install required.

| Platform | x64 | ARM64 |
|----------|-----|-------|
| Windows | win-x64 | win-arm64 |
| Linux | linux-x64 | linux-arm64 |
| macOS | osx-x64 | osx-arm64 |

**Distribution:** Self-contained executables (GitHub Release ZIPs), Chocolatey packages, Docker images, Docker Compose.

**Tested against:** SQL Server 2022. Compatible with compatibility level 130+.

---

## Key Differentiators

1. **State-based, not migration-based** — define desired state, tool computes the diff
2. **Idempotent** — same package can be deployed repeatedly with identical results
3. **Multi-database targeting** — one run deploys to many databases
4. **Automatic dependency resolution** — retry loops handle script interdependencies
5. **Full extraction pipeline** — SchemaTongs reverse-engineers any existing database into a package
6. **Data synchronization** — DataTongs generates MERGE scripts for reference/config data
7. **WhatIf validation** — preview all changes before executing
8. **Database-as-code** — schema versioned alongside application code in source control
