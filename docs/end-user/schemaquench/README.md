# SchemaQuench

Applies to: SchemaQuench (SQL Server, Community)

---

## What SchemaQuench Does

SchemaQuench is the deployment engine for schema packages. It reads a schema package, identifies the target databases for each template, and transforms each database to match the desired state defined in the package.

SchemaQuench is designed to be run repeatedly. It compares the current state of each database against the desired state and makes only the changes necessary. Migration scripts are tracked so they run only once (unless marked `[ALWAYS]`).

---

## Quench Execution Flow

When SchemaQuench runs, it executes the following steps:

1. **Load product** — Reads `Product.json`, resolves script tokens, validates the product platform.
2. **Test server connection** — Validates connectivity to the target server.
3. **Validate server** — Executes `Product.ValidationScript` if configured. Aborts if the result is falsy.
4. **Validate baseline** — Executes `Product.BaselineValidationScript` if configured.
5. **Product Before scripts** — Executes scripts from `ProductScripts/Before`. Each script runs once and is tracked. Scripts with `[ALWAYS]` in the filename run every time.
6. **Quench each template** — For each template in `TemplateOrder`:
   1. Load `Template.json`, merge template-level `ScriptTokens` over the product token set
   2. Execute `DatabaseIdentificationScript` to discover target databases
   3. For each database, execute the database quench sequence (see below)
7. **Product After scripts** — Executes scripts from `ProductScripts/After` with the same tracking as Product Before scripts.
8. **Stamp product version** — Executes `Product.VersionStampScript` if configured.
9. **Backup logs and exit**

### Database Quench Sequence

For each database identified by a template:

1. **Kindle the Forge** — Deploys SchemaSmith helper procedures and the migration tracking table to the target database.
2. **Validate baseline** — Executes `Template.BaselineValidationScript` if configured.
3. **Before scripts** — Executes migration scripts from `MigrationScripts/Before`. Each script runs once and is tracked. Scripts with `[ALWAYS]` in the filename run every time.
4. **Objects scripts** — Executes scripts from all Objects-slot folders (Schemas, DataTypes, FullTextCatalogs, FullTextStopLists, XMLSchemaCollections, Functions, Views, Procedures) in a dependency retry loop.
5. **Table quench (structures)** — Applies table column definitions and indexes from `Tables/*.json`. Creates new tables and modifies existing columns and indexes.
6. **BetweenTablesAndKeys scripts** — Executes migration scripts from `MigrationScripts/BetweenTablesAndKeys`. Sequential and tracked. Runs after table structures exist but before FK constraints are applied.
7. **Table quench (keys and constraints)** — Applies foreign key constraints and remaining table constraints from `Tables/*.json`.
8. **AfterTablesScripts** — Executes migration scripts from `MigrationScripts/AfterTablesScripts`. Sequential and tracked. Runs after the full table quench but before triggers.
9. **AfterTablesObjects scripts** — Executes scripts from Triggers and DDLTriggers folders in a dependency retry loop. Includes any unresolved Objects scripts from step 4.
10. **Table Data scripts** — Executes scripts from the `Table Data` folder in a dependency retry loop.
11. **After scripts** — Executes migration scripts from `MigrationScripts/After` with the same tracking as Before scripts.
12. **Stamp version** — Executes `Template.VersionStampScript` if configured.

---

## SchemaSmith Infrastructure

SchemaQuench deploys helper procedures and objects to each target database during the "Kindle the Forge" step:

- **SchemaSmith schema** — All helper objects live in the `SchemaSmith` schema.
- **SchemaSmith.MissingTableAndColumnQuench** — Creates missing tables and adds missing columns.
- **SchemaSmith.ModifiedTableQuench** — Alters existing columns (type, nullability, defaults, etc.).
- **SchemaSmith.MissingIndexesAndConstraintsQuench** — Creates missing indexes, constraints, and statistics.
- **SchemaSmith.ForeignKeyQuench** — Creates and drops foreign keys.
- **SchemaSmith.IndexOnlyQuench** — Index-only management mode (used when `IndexOnlyTableQuenches` is enabled).
- **SchemaSmith.IndexedViewQuench** — Deploys indexed views with diff-based change detection.
- **SchemaSmith.GenerateTableJson** — Generates table JSON from an existing table (used by SchemaTongs).
- **SchemaSmith.GenerateIndexedViewJson** — Generates indexed view JSON from existing views (used by SchemaTongs).
- **SchemaSmith.PrintWithNoWait** — Real-time progress logging using RAISERROR WITH NOWAIT.
- **Helper functions** — `fn_SafeBracketWrap`, `fn_StripBracketWrapping`, `fn_StripParenWrapping`, `fn_FormatJson`.
- **CompletedMigrationScripts table** — Tracks which migration scripts have been executed for each product.

These objects are created or updated on every quench run to ensure they match the version of SchemaQuench being used.

---

## Dependency Retry Loop

Scripts in the Objects, AfterTablesObjects, and Table Data quench slots are executed using a dependency retry loop:

1. Execute all pending scripts in the slot.
2. If any script fails (typically due to an unresolved dependency), mark it as failed.
3. If at least one script succeeded in this iteration, loop back to step 1 with only the failed scripts.
4. If no scripts succeeded in an iteration, the loop terminates and remaining failures are reported as errors.

This allows scripts with interdependencies to be placed in the same folder without requiring a specific execution order. For example, a view that references another view will succeed on a later iteration after its dependency has been created.

---

## WhatIf Mode

When `WhatIfONLY` is set to `true` in configuration, SchemaQuench performs a dry run:

- Validation scripts execute normally
- Table quench runs in WhatIf mode, generating the SQL that would be executed without applying it
- Migration scripts show detailed status: "Would APPLY" for pending scripts, "Would SKIP (previously quenched)" for already-executed scripts
- Stored procedures execute in WhatIf mode to report what changes they would make
- Before, Objects, BetweenTablesAndKeys, AfterTablesScripts, AfterTablesObjects, Table Data, After, and product Before/After scripts are logged but not executed

The generated SQL is written to `SchemaQuench - Quench Tables {DatabaseName}.sql` in the log directory.

---

## Debug SQL Output

During a normal quench run, SchemaQuench writes the SQL generated by the table quench process to a file named `SchemaQuench - Quench Tables {DatabaseName}.sql` in the log directory. This file can be reviewed to understand exactly what structural changes were made to each database.

---

## Related Documentation

- [SchemaQuench Configuration](configuration.md)
- [Migration Scripts](migration-scripts.md)
- [Script Folders](script-folders.md)
- [Products and Templates](../products-and-templates.md)
