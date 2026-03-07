# Products and Templates

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

## Product.json

The `Product.json` file is the root configuration for a schema package. It lives at the top level of the package directory.

### Properties

| Property | Type | Required | Description |
|---|---|---|---|
| `Name` | string | Yes | Product name. Used for tracking migration script completion and version stamping. Automatically added as a `{{ProductName}}` token. |
| `TemplateOrder` | string[] | Yes | Ordered list of template names. Templates are quenched in this order. |
| `ScriptTokens` | object | No | Key-value pairs for `{{TokenName}}` replacement in scripts and configuration. See [Script Tokens](script-tokens.md). |
| `ValidationScript` | string | No | T-SQL expression evaluated before quench begins. Must return a truthy value or the quench aborts. Tokens are resolved before execution. |
| `BaselineValidationScript` | string | No | T-SQL expression evaluated after server validation but before template processing. Used to verify the server is in an expected baseline state. |
| `VersionStampScript` | string | No | T-SQL executed after all templates complete successfully. Typically used to record the release version on the server. |
| `DropUnknownIndexes` | bool | No | Default: `false`. When `true`, the table quench process drops indexes on managed tables that are not defined in the table JSON. |
| `Platform` | string | No | Default: `"MSSQL"`. Must match the tool's platform. |

### Example

```json
{
    "Name": "MyProduct",
    "TemplateOrder": ["Main", "Reporting"],
    "ScriptTokens": {
        "MainDB": "Production",
        "ReportDB": "Reporting",
        "ReleaseVersion": "2.1.0"
    },
    "ValidationScript": "SELECT CASE WHEN EXISTS (SELECT 1 FROM master.sys.databases WHERE [name] = '{{MainDB}}') THEN 1 ELSE 0 END",
    "VersionStampScript": "PRINT '{{ReleaseVersion}}'",
    "Platform": "MSSQL"
}
```

---

## Template.json

Each template directory under `Templates/` contains a `Template.json` file that configures how that template targets databases.

### Properties

| Property | Type | Required | Description |
|---|---|---|---|
| `Name` | string | Yes | Template name. Must match the directory name. |
| `DatabaseIdentificationScript` | string | Yes | T-SQL query that returns a result set with a `name` column. Each row identifies a database to quench with this template. Tokens are resolved before execution. |
| `VersionStampScript` | string | No | T-SQL executed per database after quench completes for that database. |
| `BaselineValidationScript` | string | No | T-SQL validation per database before quench begins for that database. |
| `UpdateFillFactor` | bool | No | Default: `true`. When `true`, the table quench process updates index fill factors to match the JSON definition. |

### Example

```json
{
    "Name": "Main",
    "DatabaseIdentificationScript": "SELECT [name] FROM master.sys.databases WHERE [name] = '{{MainDB}}'",
    "VersionStampScript": "EXEC sys.sp_updateextendedproperty @name = N'ReleaseVersion', @value = '{{ReleaseVersion}}'"
}
```

---

## Script Folders

Each template contains a fixed set of script folders. SQL files placed in these folders are executed during the corresponding quench phase.

| Folder | Quench Slot | Execution Behavior |
|---|---|---|
| `MigrationScripts/Before` | Before | Sequential, tracked (run once unless `[ALWAYS]`) |
| `Schemas` | Objects | Looped with dependency retry |
| `DataTypes` | Objects | Looped with dependency retry |
| `FullTextCatalogs` | Objects | Looped with dependency retry |
| `FullTextStopLists` | Objects | Looped with dependency retry |
| `XMLSchemaCollections` | Objects | Looped with dependency retry |
| `Functions` | Objects | Looped with dependency retry |
| `Views` | Objects | Looped with dependency retry |
| `Procedures` | Objects | Looped with dependency retry |
| `Triggers` | AfterTablesObjects | Looped with dependency retry (after table quench) |
| `DDLTriggers` | AfterTablesObjects | Looped with dependency retry (after table quench) |
| `TableData` | TableData | Looped with dependency retry |
| `MigrationScripts/After` | After | Sequential, tracked (run once unless `[ALWAYS]`) |

For details on execution behavior per slot, see [Script Folders](schemaquench/script-folders.md).

---

## Quench Slots

Quench slots control the order of execution within a template. Each database is quenched in this sequence (for the full detailed execution flow including infrastructure deployment and validation, see [SchemaQuench Overview](schemaquench/README.md#database-quench-sequence)):

1. **Before** — Migration scripts executed sequentially. Each script runs once and is tracked in the `CompletedMigrationScripts` table. Scripts with `[ALWAYS]` in the filename run every time.
2. **Objects** — Database objects (schemas, types, functions, views, procedures) executed in a dependency retry loop. Scripts that fail due to unresolved dependencies are retried on subsequent iterations until all succeed or no progress is made.
3. **Table Quench** — Table definitions from `Tables/*.json` are applied via the `SchemaSmith.TableQuench` stored procedure. Creates, alters, or drops columns, indexes, constraints, and other table components to match the JSON definition.
4. **AfterTablesObjects** — Triggers and DDL triggers executed in a dependency retry loop after table structures are finalized.
5. **TableData** — Data scripts executed in a dependency retry loop.
6. **After** — Final migration scripts executed sequentially with the same tracking as Before scripts.

---

## Validation Scripts

Both `Product.json` and `Template.json` support validation scripts that run before the quench proceeds:

- **Product.ValidationScript** — Runs once against the target server before any templates are processed. Aborts the entire quench if the result is falsy.
- **Product.BaselineValidationScript** — Runs once after server validation, before template processing.
- **Template.BaselineValidationScript** — Runs per database before quenching that database.

All validation scripts support token replacement.

---

## Version Stamp Scripts

- **Product.VersionStampScript** — Runs once after all templates complete successfully.
- **Template.VersionStampScript** — Runs per database after that database's quench completes.

---

## Related Documentation

- [Schema Packages](schema-packages.md)
- [Script Tokens](script-tokens.md)
- [Script Folders](schemaquench/script-folders.md)
- [Migration Scripts](schemaquench/migration-scripts.md)
