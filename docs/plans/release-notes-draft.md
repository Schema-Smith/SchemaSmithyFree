# Release Notes Draft — SchemaSmith Community v2.0

## v2.0 (Upcoming)

### Breaking Changes

- **Config file renamed:** `appsettings.json` is now `{ToolName}.settings.json` (e.g., `SchemaQuench.settings.json`). Rename your existing config files to match.
- **Environment variable prefix:** `QuenchSettings_` prefix is no longer supported. Use `SmithySettings_` instead.

### .NET 10

All tools now target .NET 10 (previously .NET 9). Requires .NET 10 runtime for framework-dependent deployments. Self-contained builds (ZIP downloads) bundle the runtime — no separate install needed.

### New Features

- **IndexOnlyTableQuenches:** New template-level setting that restricts quench to index management only — skips table/column/FK changes. Supports use cases like indexing replicated databases or third-party schemas.
- **SchemaQuench config toggles:** New settings to control quench behavior without modifying Product.json:
  - `KindleTheForge` (default: true) — skip SchemaSmith helper installation when set to false
  - `UpdateTables` (default: true) — skip table structure changes when set to false
  - `DropTablesRemovedFromProduct` (default: true) — prevent dropping unknown tables when set to false
  - `RunScriptsTwice` (default: false) — resolve cross-dependencies in object scripts
  - `ScriptTokens` — override product-level script tokens from config file
- **Config file is now optional:** Tools can run with environment variables alone (no config file required)
- **AppContext.BaseDirectory fallback:** Config file is found even when current directory differs from tool directory

### SchemaHammer — New Desktop Schema Viewer

SchemaHammer is a new read-only desktop application for browsing SchemaSmith schema packages visually. It provides a graphical alternative to exploring Product.json, Template.json, table definitions, and SQL scripts.

**Navigation:**
- Product tree with lazy-loaded nodes — browse products, templates, tables, indexed views, and all child objects
- Back button with history dropdown
- Recent products list (up to 10)
- Last selection restoration on reopen
- Keyboard shortcuts: F5 (reload), Ctrl+F (search), Ctrl+Shift+F (code search)

**Schema Viewing:**
- Dedicated read-only editors for every node type: Product, Template, Table, Column, Index, Foreign Key, Check Constraint, Statistic, XML Index, Full-Text Index, Indexed View
- SQL script viewing with T-SQL syntax highlighting
- Script token preview — toggle to expand `{{{Token}}}` placeholders to their actual values

**Search:**
- Tree search with Contains, Begins With, Ends With matching (auto-search as you type)
- Code search across SQL script files, table metadata, and script tokens
- In-editor find bar with next/previous, match count, and case toggle
- Token navigation — double-click `{{{Token}}}` to jump to its definition

**Tools and Polish:**
- Update Schema Files — regenerates `.json-schemas/` validation files
- Light and dark themes
- About dialog with version info and GitHub link
- Status bar tooltip showing product path, node count, and load time

### MinimumVersion

- **SqlServerVersion enum:** New `SqlServerVersion` enum (Sql2016-Sql2025) with year-based names for human readability. Serializes as strings in Product.json via `StringEnumConverter`.
- **Product.MinimumVersion:** Optional property declaring minimum SQL Server version for a schema package. Null means no version ceiling. Used by SchemaHammer and available for future SchemaQuench feature gating.
- **VersionHelper:** Utility for version threshold comparison.

### Demo Products Consolidated

- **Demo products moved into repository:** Northwind, AdventureWorks, and tutorial products now live in `demo/` instead of a separate repository.
- **Build from source:** `docker compose -f demo/docker-compose.yml up` builds SchemaQuench from source and deploys demo products — no published Docker image dependency.
- **`.community` sentinel:** SchemaTongs now writes a `.community` file to new product roots, marking them as Community edition packages.
- **CI validation:** Demo product JSON files are validated against schemas on every PR.

### SMO-Free SchemaTongs

- **SMO dependency removed:** SchemaTongs no longer depends on `Microsoft.SqlServer.SqlManagementObjects`. All schema extraction uses direct SQL queries against system views, matching Enterprise's proven approach.
- **Extended properties stripped:** Extracted scripts and table JSON no longer include extended properties. Custom properties are an Enterprise feature.
- **Performance improvement:** Direct SQL queries are faster than SMO's reflection-based extraction.

### Schema Validation

- **Runtime-generated JSON Schema files:** `.json-schemas/` files are now generated from the domain model at runtime instead of static embedded files. SchemaTongs always overwrites with the latest schema on each extraction.
- **SchemaPropertyAttribute:** Domain model properties now carry validation metadata (Required, Pattern, Min/Max) used by the schema generator.
- **ExtendedProperties removed from schemas:** Table JSON schemas no longer include ExtendedProperties fields. Custom properties are an Enterprise feature.
- **Product.Platform added to schema:** The products.schema now correctly includes the Platform property.
