# Complete Feature List

Applies to: SchemaSmithyFree (SQL Server, Community)

---

## Schema Management

### Table Definitions

- JSON-based table definitions with full structural metadata
- Product and template hierarchy with ordered template processing
- 13 default script folders per template with fixed quench slot assignments
- JSON schema validation files for Product, Template, and Table definitions
- Table rename detection via `OldName` property
- Table-level data compression (NONE, ROW, PAGE)
- Temporal table support (`IsTemporal`)

### Column Management

- All SQL Server data types with precision, scale, and length
- Identity columns (seed and increment)
- Computed columns (persisted and non-persisted)
- Sparse columns
- Column-level collation override
- Dynamic data masking
- Default constraints
- Column-level check constraints via `CheckExpression`
- Column rename detection via `OldName`

### Index Management

- Clustered and non-clustered indexes
- Primary key constraints
- Unique indexes and unique constraints
- Columnstore indexes (clustered and non-clustered)
- Filtered indexes with WHERE expressions
- INCLUDE columns for covering indexes
- Index fill factor
- Index-level data compression
- Automatic index rebuild when structure changes

### Foreign Key Constraints

- Single and multi-column foreign keys
- Cascading actions: NO ACTION, CASCADE, SET NULL, SET DEFAULT
- Separate delete and update action configuration

### Check Constraints

- Table-level check constraints
- Column-level check constraints

### XML Indexes

- Primary XML indexes
- Secondary XML indexes (PATH, VALUE, PROPERTY types)

### Full-Text Indexes

- Full-text catalog creation and management
- Full-text stop list creation and management
- Full-text index configuration per table with catalog, key index, change tracking, and column selection

### Statistics

- User-defined statistics on single or multiple columns
- Sample size configuration
- Filtered statistics with WHERE expressions

---

## Schema Extraction (SchemaTongs)

### Extracted Object Types

SchemaTongs extracts 11 types of database objects:

| Object Type | Output Format |
|-------------|---------------|
| Tables | JSON definition files |
| Schemas | SQL scripts |
| Data Types | SQL scripts |
| Table Types | SQL scripts |
| Functions | SQL scripts |
| Views | SQL scripts |
| Stored Procedures | SQL scripts |
| Triggers | SQL scripts |
| Full-Text Catalogs | SQL scripts |
| Full-Text Stop Lists | SQL scripts |
| DDL Triggers | SQL scripts |
| XML Schema Collections | SQL scripts |

### Extraction Controls

- Per-object-type ShouldCast flags for selective extraction
- ObjectList filtering for named object extraction
- CREATE OR ALTER scripting for procedures and functions
- SET ANSI_NULLS/QUOTED_IDENTIFIER preservation
- Extended property extraction for programmable objects
- Encrypted object detection and skip with warning
- ScriptDynamicDependencyRemovalForFunctions for computed column/constraint dependency handling
- JSON schema validation file generation

### Package Initialization

- Automatic schema package initialization on first extraction
- Creates the full folder structure and configuration files

---

## Schema Deployment (SchemaQuench)

### Multi-Database Targeting

- Templates define a **DatabaseIdentificationScript** that returns the list of databases to quench
- A single quench run can update multiple databases on the same server
- Multi-template processing in defined order

### Quench Execution Slots

Scripts and objects are applied in a defined order through dedicated slots:

| Slot | Content | Retry Loop |
|------|---------|------------|
| Before | Migration scripts (Before Scripts folder) | No |
| Objects | Programmable objects (functions, procedures, etc.) | Yes |
| AfterTablesObjects | Views, triggers (plus retry of Objects) | Yes |
| TableData | Table data scripts (Table Data folder) | Yes |
| After | Migration scripts (After Scripts folder) | No |

Slots marked with "Retry Loop" use a dependency resolution loop -- scripts that fail due to missing dependencies are retried until no more progress can be made.

### Table Quench Steps

- State-based deployment: desired state defined in metadata, applied to target
- Table quench via SchemaSmith.TableQuench stored procedure
- Automatic SchemaSmith infrastructure deployment (Kindling)
- Debug SQL output for table quench operations

### Migration Script Tracking

- Migration scripts (Before and After slots) are tracked in a CompletedMigrationScripts table in the target database
- Previously executed scripts are skipped on subsequent runs
- Scripts with the `[ALWAYS]` suffix in their filename are executed on every run regardless of tracking

### WhatIf Dry-Run Mode

- WhatIf dry-run mode with SQL output
- Logs which scripts would be applied, skipped, or delivered
- Useful for validating a schema package before applying it to a production server

### Additional Deployment Options

- **DropUnknownIndexes** -- When enabled, indexes that exist in the database but not in the package are dropped (drift cleanup)
- **UpdateFillFactor** -- Control per template
- **IndexOnlyTableQuenches** -- Template-level setting that restricts quench to index management only; skips table creation, column changes, and FK management. Tables that don't exist are silently skipped.
- **ZIP package deployment** -- SchemaQuench can load schema packages directly from a `.zip` file
- **Product and template validation scripts**
- **Version stamp scripts** at product and template level

### Validation

- **Product validation script** -- Evaluated before quenching to verify the target server is correct
- **Baseline validation script** -- Evaluated at both product and template levels to verify the database is at the expected starting state

---

## Data Management

### Data Extraction (DataTongs)

DataTongs generates standalone MERGE scripts from live table data:

- MERGE script generation from live table data
- OPENJSON-based data loading
- Configurable INSERT, UPDATE, DELETE behavior
- Identity column detection and IDENTITY_INSERT handling
- Computed column and ROWGUIDCOL exclusion
- Geography column WKT extraction and restoration
- Special type handling for XML, NTEXT, TEXT, IMAGE columns
- NULL-safe change detection in UPDATE clauses
- Optional trigger disable during MERGE execution
- Row filtering via WHERE clause
- NOLOCK hints on source queries

---

## Schema Viewing (SchemaHammer)

### Desktop Schema Viewer

SchemaHammer is a read-only desktop application for browsing SchemaSmith schema packages.

- Product tree navigation with lazy-loaded table and indexed view nodes
- Back button with history dropdown for quick navigation
- Last selection restoration on application reopen
- Recent products list (up to 10 entries)

### Schema Editors (Read-Only)

Dedicated editors for every node type in the tree:

| Editor | Properties Shown |
|--------|-----------------|
| Product | Name, Platform, ValidationScript, TemplateOrder, ScriptTokens, MinimumVersion, DropUnknownIndexes |
| Template | Name, DatabaseIdentificationScript, VersionStampScript, UpdateFillFactor, ScriptTokens |
| Table | Schema, Name, CompressionType, IsTemporal, UpdateFillFactor, OldName |
| Column | Name, DataType, Nullable, Default, CheckExpression, ComputedExpression, Persisted, Sparse, Collation, DataMaskFunction, OldName |
| Index | Name, PrimaryKey, Unique, Clustered, ColumnStore, FillFactor, IndexColumns, IncludeColumns, FilterExpression, CompressionType |
| Foreign Key | Name, Columns, RelatedTable, RelatedColumns, DeleteAction, UpdateAction |
| Check Constraint | Name, Expression |
| Statistic | Name, Columns, SampleSize, FilterExpression |
| XML Index | Name, IsPrimary, SecondaryType, ParentIndex |
| Full-Text Index | CatalogName, KeyIndex, ChangeTracking, Columns |
| Indexed View | Name, Schema, Indexes |

### SQL Script Viewing

- T-SQL syntax highlighting via AvaloniaEdit
- Token preview toggle — expand `{{{Token}}}` placeholders to their actual values
- Product and template tokens merged with template overriding product

### Search

- **Tree search** — Contains, Begins With, Ends With matching with auto-search
- **Code search** — Searches SQL script files, table metadata (columns, indexes, FKs, constraints, statistics), and script tokens
- **Find bar** — In-editor text find with next/previous navigation, match count, and case toggle
- **Token navigation** — Double-click `{{{Token}}}` in SQL to navigate to the token definition

### Tools

- **Update Schema Files** — Regenerates `.json-schemas/` validation files for the current product
- **Light/Dark themes** — Toggle via Tools menu

---

## Script Tokens

Tokens use the `{{TokenName}}` syntax and are replaced at runtime in all SQL files and configuration scripts. Token replacement is case-insensitive.

### Token Scoping and Precedence

- **Product-level tokens** are defined in `Product.json` and available to all templates
- **Configuration overrides** -- Token values can be overridden in `{ToolName}.settings.json` and environment variables
- **Built-in tokens** -- `{{ProductName}}` and `{{TemplateName}}` are automatically available

---

## Configuration

### Settings Files

Each tool has its own `{ToolName}.settings.json` with hierarchical settings (e.g., `SchemaQuench.settings.json`, `SchemaTongs.settings.json`, `DataTongs.settings.json`).

### Configuration Hierarchy

Settings are loaded from multiple sources with this precedence (highest wins):

1. CLI switches
2. Environment variables (`SmithySettings_` prefix)
3. User secrets
4. Configuration file (`{ToolName}.settings.json`)

### ZIP Support

SchemaQuench can load schema packages directly from a `.zip` file. Set `SchemaPackagePath` to the path of the zip file and the tool extracts and processes it in memory.

### Operational Details

- Dual-stream logging (Progress + Error) with Apache Log4Net
- Log backup with numbered directories
- Password masking in configuration logs
- Windows authentication and SQL Server authentication
- Standardized exit codes across all tools
- Multi-targeting: .NET 9.0 and .NET Framework 4.8.1
- Docker Compose support for local development and testing
- Cross-platform .NET 9.0 execution (Windows, Linux, macOS)
