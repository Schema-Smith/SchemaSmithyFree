# SchemaQuench Reference

SchemaQuench is the deployment engine for SchemaSmith. It reads a schema package, connects to the target SQL Server, identifies which databases to update, and transforms each database to match the desired state defined in the package. SchemaQuench is designed to be run repeatedly -- it compares current state against desired state, makes only the changes necessary, and tracks migration scripts so they execute only once.

---

## Installation and Invocation

SchemaQuench is included in the SchemaSmith distribution. Run it from the directory containing `SchemaQuench.settings.json`:

```bash
SchemaQuench
```

Common switches:

```bash
SchemaQuench --ConfigFile:path/to/alternate.settings.json
SchemaQuench --LogPath:path/to/logs
SchemaQuench --ConnectionString:"data source=myserver;Initial Catalog=master;User ID=sa;Password=secret;TrustServerCertificate=True;"
```

The `--ConnectionString` switch bypasses all `Target` settings and passes the value directly to the SQL Server driver.

---

## Configuration Reference

SchemaQuench reads configuration from `SchemaQuench.settings.json` (or the file specified by `--ConfigFile`), environment variables with the `SmithySettings_` prefix, and command-line switches. Later sources override earlier ones.

### Target Connection Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Target:Server` | string | `"localhost"` | SQL Server instance name or address. |
| `Target:Port` | string | _(empty)_ | Port number. Appended to the server as `server,port`. |
| `Target:User` | string | _(empty)_ | SQL Server login. When blank, Windows authentication is used. |
| `Target:Password` | string | _(empty)_ | SQL Server password. When blank, Windows authentication is used. |
| `Target:ConnectionProperties` | object | `{}` | Arbitrary key-value pairs appended to the connection string (e.g., `TrustServerCertificate`, `Encrypt`). |

### Behavior Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SchemaPackagePath` | string | _(required)_ | Path to the schema package directory or ZIP file. |
| `WhatIfONLY` | bool | `false` | Dry-run mode. Table quench generates SQL without executing. Script slots are logged but not executed. |
| `KindleTheForge` | bool | `true` | Deploy SchemaSmith helper procedures and the migration tracking table to each target database before quenching. |
| `UpdateTables` | bool | `true` | Apply table structure changes (columns, indexes, constraints, foreign keys) from the schema package. |
| `DropTablesRemovedFromProduct` | bool | `true` | Drop tables that exist in the database but are not defined in the schema package. |
| `RunScriptsTwice` | bool | `false` | Run object scripts twice to resolve cross-dependencies between objects. |
| `ScriptTokens` | object | `{}` | Config-level overrides for product script tokens. Keys are token names, values are replacement strings. |

### Full Settings File Example

```json
{
    "Target": {
        "Server": "localhost",
        "Port": "",
        "User": "",
        "Password": "",
        "ConnectionProperties": {
            "TrustServerCertificate": "True"
        }
    },
    "WhatIfONLY": false,
    "SchemaPackagePath": "./MyProduct",
    "KindleTheForge": true,
    "UpdateTables": true,
    "DropTablesRemovedFromProduct": true,
    "RunScriptsTwice": false,
    "ScriptTokens": {}
}
```

### Environment Variable Mapping

All configuration keys can be overridden via environment variables using the `SmithySettings_` prefix. Nested keys use double underscores:

| Configuration Key | Environment Variable |
|---|---|
| `Target:Server` | `SmithySettings_Target__Server` |
| `Target:Port` | `SmithySettings_Target__Port` |
| `Target:User` | `SmithySettings_Target__User` |
| `Target:Password` | `SmithySettings_Target__Password` |
| `Target:ConnectionProperties:<name>` | `SmithySettings_Target__ConnectionProperties__<name>` |
| `WhatIfONLY` | `SmithySettings_WhatIfONLY` |
| `SchemaPackagePath` | `SmithySettings_SchemaPackagePath` |
| `KindleTheForge` | `SmithySettings_KindleTheForge` |
| `UpdateTables` | `SmithySettings_UpdateTables` |
| `DropTablesRemovedFromProduct` | `SmithySettings_DropTablesRemovedFromProduct` |
| `RunScriptsTwice` | `SmithySettings_RunScriptsTwice` |
| `ScriptTokens:<name>` | `SmithySettings_ScriptTokens__<name>` |

---

## Deployment Execution Flow

When SchemaQuench runs, `ProductQuencher.Quench()` executes these steps in order:

1. **Log product info** -- Logs the product name, template order, validation script, and any configured script tokens.
2. **Test server connection** -- Opens a connection to the target server and executes `SELECT @@SERVERNAME` to verify connectivity. Aborts if the connection fails.
3. **Clean up old debug files** -- Removes any `SchemaQuench - Quench Tables*.sql` files from a previous run.
4. **Validate server** -- If `Product.ValidationScript` is configured, executes it against the `master` database. Aborts if the result is falsy.
5. **Validate baseline** -- If `Product.BaselineValidationScript` is configured, executes it. Aborts if the result is falsy.
6. **Product Before scripts** -- Executes scripts from the `Before Product` folder. In WhatIf mode, logs what would run without executing.
7. **Quench each template** -- For each template name in `Product.TemplateOrder`:
   - Loads `Template.json` and merges template-level `ScriptTokens` over the product token set.
   - Executes `DatabaseIdentificationScript` against `master` to discover target databases.
   - Creates a `DatabaseQuencher` for each discovered database and runs them sequentially.
   - If any database quench fails, logs the failure and exits with code 2.
8. **Product After scripts** -- Executes scripts from the `After Product` folder.
9. **Stamp product version** -- If `Product.VersionStampScript` is configured, executes it.
10. **Log completion** -- Logs "Completed quench of {ProductName}".

After `Quench()` returns, the calling program backs up log files to a numbered directory and exits with code 0 (see [Exit Codes](configuration.md#exit-codes)).

---

## Database Quench Sequence

For each database identified by a template's `DatabaseIdentificationScript`, `DatabaseQuencher.Quench()` runs the following sequence. All steps execute on the identified database (not `master`).

1. **Kindle the Forge** -- Deploys SchemaSmith helper procedures, functions, and the migration tracking table. Skipped if `KindleTheForge` is `false`.
2. **Validate baseline** -- Executes `Template.BaselineValidationScript` if configured. Aborts if falsy.
3. **Object scripts (first pass)** -- Executes scripts from all Objects-slot folders (Schemas, DataTypes, FullTextCatalogs, FullTextStopLists, XMLSchemaCollections, Functions, Views, Procedures) using the dependency retry loop. If `RunScriptsTwice` is enabled, resets and runs a second pass.
4. **Parse table JSON** -- Serializes all `Tables/*.json` definitions into temp tables for the modular procedures to consume.
5. **MissingTableAndColumnQuench** -- Creates missing tables and adds missing columns.
6. **Object scripts (second opportunity)** -- Re-attempts any Objects-slot scripts that failed in step 3, now that missing tables exist.
7. **Before scripts** -- Executes migration scripts from `MigrationScripts/Before`. Sequential and tracked.
8. **ModifiedTableQuench** -- Alters existing columns (type changes, nullability, defaults, computed columns) and manages indexes and check constraints.
9. **Object scripts (third opportunity)** -- Re-attempts any remaining failed Objects-slot scripts now that table modifications are complete.
10. **BetweenTablesAndKeys scripts** -- Executes migration scripts from `MigrationScripts/BetweenTablesAndKeys`. Sequential and tracked. Runs after table structures are finalized but before FK constraints.
11. **MissingIndexesAndConstraintsQuench** -- Creates missing indexes, check constraints, default constraints, and statistics.
12. **AfterTablesScripts** -- Executes migration scripts from `MigrationScripts/AfterTablesScripts`. Sequential and tracked.
13. **AfterTablesObjects scripts** -- Executes scripts from Triggers and DDLTriggers folders using the dependency retry loop. Also retries any still-unresolved Objects-slot scripts.
14. **Table Data scripts** -- Executes scripts from the `Table Data` folder using the dependency retry loop.
15. **ForeignKeyQuench** -- Creates, drops, and modifies foreign keys to match the schema package.
16. **Indexed view quench** -- If the template defines indexed views, deploys them via `SchemaSmith.IndexedViewQuench`.
17. **After scripts** -- Executes migration scripts from `MigrationScripts/After`. Sequential and tracked.
18. **Stamp version** -- Executes `Template.VersionStampScript` if configured.

When `UpdateTables` is `false`, steps 4 through 16 are skipped entirely. When `IndexOnlyTableQuenches` is enabled on a template, steps 4–8 (ParseJson, MissingTableAndColumnQuench, second Objects pass, Before scripts, and ModifiedTableQuench) are replaced by a single call to `SchemaSmith.IndexOnlyQuench`. Steps 9–16 still execute, except that MissingIndexesAndConstraintsQuench (step 11) and ForeignKeyQuench (step 15) are individually skipped.

---

## Execution Slots

SchemaQuench assigns every script folder to a quench slot that determines when the folder's scripts execute and how they are handled.

### Template Quench Slots

| Slot | Folders | Execution Style |
|------|---------|-----------------|
| `Before` | `MigrationScripts/Before` | Sequential, tracked |
| `Objects` | `Schemas`, `DataTypes`, `FullTextCatalogs`, `FullTextStopLists`, `XMLSchemaCollections`, `Functions`, `Views`, `Procedures` | Dependency retry loop |
| `BetweenTablesAndKeys` | `MigrationScripts/BetweenTablesAndKeys` | Sequential, tracked |
| `AfterTablesScripts` | `MigrationScripts/AfterTablesScripts` | Sequential, tracked |
| `AfterTablesObjects` | `Triggers`, `DDLTriggers` | Dependency retry loop |
| `TableData` | `Table Data` | Dependency retry loop |
| `After` | `MigrationScripts/After` | Sequential, tracked |

**Sequential, tracked** means scripts run in alphabetical order and are recorded in `CompletedMigrationScripts` so they only run once (unless marked `[ALWAYS]`).

**Dependency retry loop** means scripts are retried in rounds until all succeed or no progress is made. See [Dependency Retry Loop](#dependency-retry-loop) below.

### Product Quench Slots

| Slot | Folder | Execution Style |
|------|--------|-----------------|
| `Before` | `Before Product` | Sequential |
| `After` | `After Product` | Sequential |

Product scripts run against the `master` database connection, outside the per-database template loop.

---

## WhatIf Mode

Set `WhatIfONLY` to `true` to perform a dry run. In WhatIf mode:

- **Validation scripts** execute normally (server validation, baseline validation).
- **Table quench procedures** run with `@WhatIf = 1`, generating the SQL that would be executed and logging it without applying changes.
- **Migration scripts** (Before, BetweenTablesAndKeys, AfterTablesScripts, After) show detailed status for each script:
  - `Would APPLY: {script}` for scripts that have not yet been tracked.
  - `Would SKIP (previously quenched): {script}` for scripts already recorded in `CompletedMigrationScripts`.
- **Object scripts** (Objects, AfterTablesObjects, Table Data) are logged but not executed.
- **Product Before/After scripts** are logged but not executed.
- **Version stamp scripts** are not executed; a log message indicates the stamp would occur.

### Debug SQL Output

During both normal and WhatIf runs, SchemaQuench writes the SQL generated by the table quench process to files in the working directory:

- `SchemaQuench - ParseJson {DatabaseName}.sql`
- `SchemaQuench - MissingTableAndColumnQuench {DatabaseName}.sql`
- `SchemaQuench - ModifiedTableQuench {DatabaseName}.sql`
- `SchemaQuench - MissingIndexesAndConstraintsQuench {DatabaseName}.sql`
- `SchemaQuench - ForeignKeyQuench {DatabaseName}.sql`
- `SchemaQuench - IndexedViewQuench {DatabaseName}.sql`
- `SchemaQuench - IndexOnlyQuench {DatabaseName}.sql` (when `IndexOnlyTableQuenches` is enabled)

These files can be reviewed to understand exactly what structural changes were (or would be) made.

### Example Usage

```bash
# Via settings file
# Set "WhatIfONLY": true in SchemaQuench.settings.json, then:
SchemaQuench

# Via environment variable
SmithySettings_WhatIfONLY=true SchemaQuench
```

---

## KindleTheForge

KindleTheForge deploys the SchemaSmith infrastructure to each target database. This infrastructure includes:

- **SchemaSmith schema** -- All helper objects are created in the `SchemaSmith` schema.
- **Helper functions** -- `fn_SafeBracketWrap`, `fn_StripBracketWrapping`, `fn_StripParenWrapping`, `fn_FormatJson`.
- **PrintWithNoWait** -- Real-time progress logging using `RAISERROR WITH NOWAIT`.
- **Table quench procedures** -- `MissingTableAndColumnQuench`, `ModifiedTableQuench`, `MissingIndexesAndConstraintsQuench`, `ForeignKeyQuench`, `TableQuench` (orchestrator with embedded JSON parsing), `IndexOnlyQuench`.
- **Indexed view procedure** -- `IndexedViewQuench`.
- **Reverse-engineering procedures** -- `GenerateTableJson`, `GenerateIndexedViewJson` (used by SchemaTongs).
- **CompletedMigrationScripts table** -- Tracks which migration scripts have been executed for each product and quench slot.

KindleTheForge runs on every quench to ensure the helper procedures match the version of SchemaQuench being used. Set `KindleTheForge` to `false` only when you are certain the infrastructure is already current and want to skip the deployment for performance.

---

## Table Quench Modular Procedures

The table quench is broken into modular stored procedures, each handling a specific aspect of the table schema. These procedures are deployed during the KindleTheForge step and called in sequence during the database quench.

### ParseTableJsonIntoTempTables

Not a stored procedure but an inline SQL script. Parses the table JSON definitions from the schema package into temporary tables that the subsequent procedures consume. This runs once per database and the temp tables persist across the procedure calls within the same connection.

### SchemaSmith.MissingTableAndColumnQuench

Creates tables that exist in the schema package but not in the database. Adds columns that exist in the table definition but are missing from the existing table. Does not modify existing columns -- that is handled by `ModifiedTableQuench`.

### SchemaSmith.ModifiedTableQuench

Alters existing columns to match the schema package definitions. Handles data type changes, nullability changes, default constraint changes, computed column updates, and identity property changes. Also manages indexes and check constraints on modified tables. Drops tables removed from the product when `DropTablesRemovedFromProduct` is enabled. Drops unknown indexes when `DropUnknownIndexes` is enabled on the product.

### SchemaSmith.MissingIndexesAndConstraintsQuench

Creates indexes, check constraints, default constraints, and statistics that exist in the schema package but are missing from the database. Runs after `ModifiedTableQuench` to handle newly created or restructured tables.

### SchemaSmith.ForeignKeyQuench

Creates foreign keys defined in the schema package that are missing from the database. Drops foreign keys that exist in the database but are not defined in the schema package (for tables managed by the product). Runs late in the sequence so that all referenced tables and columns exist.

### SchemaSmith.IndexOnlyQuench

An alternative to the full table quench sequence. When `IndexOnlyTableQuenches` is enabled on a template, this single procedure replaces the `MissingTableAndColumnQuench` / `ModifiedTableQuench` / `MissingIndexesAndConstraintsQuench` / `ForeignKeyQuench` sequence. It manages indexes only -- it does not create tables, add columns, or manage foreign keys.

### SchemaSmith.IndexedViewQuench

Deploys indexed views with diff-based change detection. Creates missing indexed views, updates views whose definitions have changed, and manages the clustered and non-clustered indexes on each view.

---

## Migration Script Tracking

Migration scripts (scripts in the Before, BetweenTablesAndKeys, AfterTablesScripts, and After slots) are tracked in the `SchemaSmith.CompletedMigrationScripts` table. This table records:

| Column | Description |
|--------|-------------|
| `ProductName` | The product name from `Product.json`. |
| `QuenchSlot` | The slot the script belongs to (Before, BetweenTablesAndKeys, AfterTablesScripts, After). |
| `ScriptPath` | The relative path of the script within the template. |
| `QuenchDate` | Timestamp when the script was executed (`GETDATE()` at insertion). |

### Execution Rules

- On each quench run, SchemaQuench checks which scripts in each slot have already been recorded.
- Scripts that appear in the tracking table are skipped.
- Scripts that do not appear are executed, and on success, a tracking entry is inserted.

### The [ALWAYS] Suffix

Scripts with `[ALWAYS]` in the filename (before the `.sql` extension) run on every quench regardless of tracking:

```
001_SeedReferenceData [ALWAYS].sql
002_RefreshPermissions [ALWAYS].sql
```

`[ALWAYS]` scripts are never recorded in the tracking table. They execute every time SchemaQuench processes the slot.

### Ordering

Migration scripts within each slot execute in **alphabetical order by filename**. Use numeric prefixes to control execution order:

```
001_CreateStagingTable.sql
002_MigrateData.sql
003_DropStagingTable.sql
```

### Obsolete Entry Cleanup

When SchemaQuench processes a slot, it compares the tracking table entries against the scripts currently present in the package. Entries for scripts that no longer exist in the package are automatically removed from the tracking table.

### Forcing Re-Execution

To force a tracked script to run again, either:
- Delete the corresponding row from `SchemaSmith.CompletedMigrationScripts` in the target database.
- Rename the script file (tracking is by path, so a renamed script is treated as new).

---

## Dependency Retry Loop

Scripts in the Objects, AfterTablesObjects, and TableData slots execute using a dependency retry loop rather than simple sequential execution:

1. Execute all pending (not yet quenched) scripts in the slot.
2. For each script, attempt to execute all its batches. If any batch fails, record the error and move on.
3. If at least one script succeeded in this iteration, loop back to step 1 with only the remaining failed scripts.
4. If zero scripts succeeded in an iteration, the loop terminates.

On the **final attempt** (the last pass when errors are reported), failures are logged as errors and the quench fails.

This mechanism allows scripts with interdependencies to coexist in the same folder without requiring a specific naming order. For example, if View B references View A, and View B is alphabetically first, it will fail on the first pass but succeed on the retry after View A has been created.

The Objects slot gets four opportunities to resolve: (1) before the table quench, (2) after missing tables are created, (3) after table modifications are complete, and (4) during the AfterTablesObjects pass alongside triggers. This handles cases where a view or function references a table column that does not yet exist on the first pass.

---

## DropTablesRemovedFromProduct

When `DropTablesRemovedFromProduct` is `true` (the default), the `ModifiedTableQuench` procedure drops tables that:

- Exist in the target database.
- Are not defined in any table JSON file in the schema package.
- Were previously managed by this product.

This keeps the database clean as tables are removed from the schema package over time. Set to `false` if you want SchemaQuench to leave unmanaged tables in place -- for example, when the database contains tables managed by other applications.

---

## RunScriptsTwice

When `RunScriptsTwice` is `true`, the Objects-slot scripts are executed twice in succession during step 3 of the database quench sequence. On the second pass, all scripts are reset to unquenched and processed through the dependency retry loop again.

Use this when your schema has complex cross-dependencies between objects that cannot be fully resolved in a single dependency retry cycle. A common scenario is when functions reference views that reference other functions -- the first pass creates all objects, and the second pass updates them with their final definitions now that all dependencies exist.

This setting defaults to `false` because most schemas resolve within a single pass.

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Successful quench. All databases quenched, logs backed up. |
| 2 | One or more database quenches failed. At least one `DatabaseQuencher` reported `QuenchSuccessful = false`. |
| 3 | Unhandled exception. An unexpected error occurred outside the normal quench flow. |
| 4 | Unable to back up log files. The log backup process itself failed (e.g., permission error on the log directory). |

---

## Related Documentation

- [Configuration Reference](configuration.md) -- Shared configuration system, CLI switches, environment variables
- [Schema Packages Reference](schema-packages.md) -- Package structure, folder layout, execution order
- [Script Tokens Reference](script-tokens.md) -- Token replacement in SQL scripts
- [SchemaTongs Reference](schematongs.md) -- Extraction tool that creates schema packages
- [Quick Start -- Deploy with SchemaQuench](../guide/02-quick-start.md#step-4-deploy-with-schemaquench) -- Hands-on first deployment
- [Day-to-Day Workflows -- WhatIf Mode](../guide/04-day-to-day-workflows.md#whatif-mode-as-safety-net) -- Using WhatIf in practice
- [Edge Cases -- Migration Scripts](../guide/06-edge-cases.md#migration-scripts) -- When and how to use migration slots
