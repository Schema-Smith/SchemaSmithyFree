# Release Notes Draft — SchemaSmith Community v2.0

## v2.0 (Upcoming)

### Breaking Changes

- **Config file renamed:** `appsettings.json` is now `{ToolName}.settings.json` (e.g., `SchemaQuench.settings.json`). Rename your existing config files to match.
- **Environment variable prefix:** `QuenchSettings_` prefix is no longer supported. Use `SmithySettings_` instead.

### New Features

- **SchemaQuench config toggles:** New settings to control quench behavior without modifying Product.json:
  - `KindleTheForge` (default: true) — skip SchemaSmith helper installation when set to false
  - `UpdateTables` (default: true) — skip table structure changes when set to false
  - `DropTablesRemovedFromProduct` (default: true) — prevent dropping unknown tables when set to false
  - `RunScriptsTwice` (default: false) — resolve cross-dependencies in object scripts
  - `ScriptTokens` — override product-level script tokens from config file
- **Config file is now optional:** Tools can run with environment variables alone (no config file required)
- **AppContext.BaseDirectory fallback:** Config file is found even when current directory differs from tool directory

### Schema Validation

- **Runtime-generated JSON Schema files:** `.json-schemas/` files are now generated from the domain model at runtime instead of static embedded files. SchemaTongs always overwrites with the latest schema on each extraction.
- **SchemaPropertyAttribute:** Domain model properties now carry validation metadata (Required, Pattern, Min/Max) used by the schema generator.
- **ExtendedProperties removed from schemas:** Table JSON schemas no longer include ExtendedProperties fields. Custom properties are an Enterprise feature.
- **Product.Platform added to schema:** The products.schema now correctly includes the Platform property.
