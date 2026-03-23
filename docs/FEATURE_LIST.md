# SchemaSmithyFree — Complete Feature List

SchemaSmithyFree is a **state-based database migration toolset for SQL Server**, analogous to Terraform but for databases. Rather than tracking schema evolution through sequential migrations, you define the desired state as metadata (JSON + SQL), and the tools transform any target SQL Server to match.

---

## Three CLI Tools

### 1. SchemaTongs — Schema Extraction
Extracts a live SQL Server database's schema into a versioned schema package.

**Extractable Object Types (11):**
| Object | Output Format | Folder |
|--------|--------------|--------|
| Tables | JSON definitions | `Tables/` |
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

**Package Initialization:**
- First run auto-creates full directory structure with all 13 folders
- Generates `Product.json`, `Template.json`, and JSON schema validation files
- Subsequent runs update scripts and table definitions without overwriting config

---

### 2. SchemaQuench — Schema Deployment Engine
Applies schema packages to target SQL Server databases using state-based comparison.

**Execution Slots (in order):**
1. **Before** — Sequential, tracked migration scripts (`MigrationScripts/Before/`)
2. **Objects** — Schemas, types, functions, views, procedures with automatic dependency retry loop
3. **Table Quench** — State-based table creation/alteration/dropping via JSON definitions
4. **AfterTablesObjects** — Triggers and DDL triggers with dependency retry
5. **TableData** — Data synchronization scripts with dependency retry
6. **After** — Sequential, tracked migration scripts (`MigrationScripts/After/`)

**Table Quench (State-Based Table Management):**
- Creates, alters, and drops columns, indexes, constraints automatically
- Compares desired JSON state vs. current database state, makes only necessary changes
- Automatic index rebuilds when table structure changes
- Debug SQL output file for audit trail
- Idempotent — safe to run repeatedly

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

**Validation & Verification:**
- Product-level validation script (runs once before any template)
- Template-level baseline validation (per database)
- Version stamp scripts at product and template level
- Aborts on validation failure

**Auto-Deployed Infrastructure ("Kindling the Forge"):**
- `SchemaSmith` schema created automatically
- `SchemaSmith.TableQuench` stored procedure for table modifications
- `SchemaSmith.GenerateTableJson` for reverse-engineering table JSON
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
- Key columns for MERGE ON clause
- Optional WHERE filter for row subsetting

**Special Data Type Handling:**
- Geography → WKT (Well-Known Text) with `STGeomFromText()` restoration
- XML → CAST to NVARCHAR(MAX)
- NTEXT/TEXT/IMAGE → CAST to comparable types
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
- Script token preview — toggle between raw `{{{Token}}}` placeholders and expanded values
- Container/folder editors with child summary

**Search:**
- Tree search with Contains, Begins With, Ends With matching
- Code search across SQL script files, table metadata, and script tokens
- In-editor find bar with next/previous, match count, and case toggle
- Token navigation — double-click `{{{Token}}}` in SQL to navigate to its definition

**Tools:**
- Update Schema Files — regenerates `.json-schemas/` validation files
- Light and dark themes

---

## SQL Server Features Supported

**Table-Level:**
- Data compression (NONE, ROW, PAGE)
- Temporal tables (system-versioned)
- Table rename tracking via `OldName`

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
  Templates/
    TemplateName/
      Template.json
      Tables/           (JSON per table)
      Schemas/          (SQL)
      DataTypes/        (SQL)
      FullTextCatalogs/ (SQL)
      FullTextStopLists/(SQL)
      XMLSchemaCollections/ (SQL)
      Functions/        (SQL)
      Views/            (SQL)
      Procedures/       (SQL)
      Triggers/         (SQL)
      DDLTriggers/      (SQL)
      MigrationScripts/
        Before/         (SQL)
        After/          (SQL)
      TableData/        (SQL)
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
- TrustServerCertificate enabled

**CLI Switches (all tools):**
- `--version` / `-v` / `--ver`
- `--help` / `-h` / `-?`
- `--ConfigFile:<path>`
- `--LogPath:<path>`
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

| Platform | .NET 9.0 | .NET Framework 4.8.1 |
|----------|----------|---------------------|
| Windows | Yes | Yes |
| Linux | Yes | No |
| macOS | Yes | No |

**Distribution:** Standalone executables, ZIP packages, Docker images, Docker Compose (includes SQL Server 2019 with Full-Text Search for testing).

**Tested against:** SQL Server 2019. Compatible with compatibility level 130+.

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
