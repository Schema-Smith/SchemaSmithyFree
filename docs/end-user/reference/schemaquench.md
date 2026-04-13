# SchemaQuench Reference

Take your declared schema and harden it onto a live database -- that's what SchemaQuench does. It reads a schema package, connects to the target server, and transforms each database to match the desired state. No hand-written ALTER scripts, no guessing what changed. Run it against dev, staging, and production with the same package, the same confidence, and the same boring, predictable result every time. SchemaQuench compares current state against desired state, makes only the changes necessary, and tracks migration scripts so they execute only once.

One executable, three platforms. The product's `Platform` value (`SqlServer`, `PostgreSQL`, or `MySQL`) tells SchemaQuench which adapter, which DDL flavor, and which set of helper procedures to use. Everything else looks the same.

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

# SQL Server
SchemaQuench --ConnectionString:"Data Source=myserver;Initial Catalog=master;User ID=sa;Password=secret;TrustServerCertificate=True;"

# PostgreSQL
SchemaQuench --ConnectionString:"Host=myserver;Port=5432;Database=postgres;Username=deploy;Password=secret;"

# MySQL
SchemaQuench --ConnectionString:"Server=myserver;Port=3306;Database=mysql;User=deploy;Password=secret;"
```

The `--ConnectionString` switch bypasses all `Target` settings and passes the value directly to the platform-appropriate driver.

---

## Configuration Reference

SchemaQuench reads configuration from `SchemaQuench.settings.json` (or the file specified by `--ConfigFile`), environment variables with the `SmithySettings_` prefix, and command-line switches. Later sources override earlier ones. For the shared configuration system see the [Configuration Reference](configuration.md).

### Target Connection Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Target:Server` | string | _(required)_ | Database server hostname or IP. |
| `Target:Port` | string | platform default | TCP port. SQL Server `1433`, PostgreSQL `5432`, MySQL `3306`. |
| `Target:User` | string | _(empty)_ | Login username. SQL Server allows blank for Windows auth. |
| `Target:Password` | string | _(empty)_ | Login password. |
| `Target:SecondaryServers` | string | _(empty)_ | **SQL Server only.** Comma-separated list of additional servers (Availability Group secondaries) to quench in parallel with the primary. See [Secondary Servers](#secondary-servers). |
| `Target:ConnectionProperties` | object | `{}` | Arbitrary key-value pairs appended to the connection string. Platform-specific keys -- see the [Configuration Reference](configuration.md#connection-configuration). |

### Behavior Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SchemaPackagePath` | string | _(required)_ | Path to the schema package directory or ZIP file. |
| `WhatIfONLY` | bool | `false` | Dry-run mode. Generates SQL without executing. |
| `KindleTheForge` | bool | `true` | Deploy SchemaSmith helper procedures and the migration tracking table to each target database before quenching. |
| `UpdateTables` | bool | `true` | Apply table structure changes (columns, indexes, constraints, foreign keys) from the schema package. |
| `DropTablesRemovedFromProduct` | bool | `true` | Drop tables that exist in the database but aren't defined in the schema package. |
| `RunScriptsTwice` | bool | `false` | Run object scripts twice to verify idempotency. A CI/testing tool. |
| `TrackRunOnceMigrations` | bool | `true` | Track run-once migration scripts. When `false`, all scripts run on every deployment. |
| `PruneObsoleteMigrationTracking` | bool | `true` | Remove tracking entries for scripts no longer in the package. |
| `MaxThreads` | int | `10` | Number of parallel database operations. Range 1--20. |
| `VerboseLogging` | bool | `false` | Include `PRINT` / `RAISE NOTICE` / equivalent informational output from user scripts in logs. |
| `ScriptTokens` | object | `{}` | Config-level overrides for product script tokens. |

### Full settings file example

```json
{
  "Target": {
    "Server": "localhost",
    "Port": "",
    "User": "",
    "Password": "",
    "SecondaryServers": "",
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
  "TrackRunOnceMigrations": true,
  "PruneObsoleteMigrationTracking": true,
  "MaxThreads": 10,
  "VerboseLogging": false,
  "ScriptTokens": {}
}
```

For environment variable mapping, see [Configuration Reference -- Environment Variables](configuration.md#environment-variables).

---

## Secondary Servers

SQL Server deployments targeting Availability Groups can quench to a primary plus one or more secondary servers in parallel. Configure secondaries on `Target`:

```json
{
  "Target": {
    "Server": "primary-replica",
    "SecondaryServers": "secondary-1,secondary-2"
  }
}
```

When a secondary list is configured, SchemaQuench routes each product-level folder to the right server based on its `ServerToQuench` setting (`Primary`, `Secondary`, or `Both`). Templates target the primary; product-level scripts can target either side. See [Schema Packages -- Secondary Servers](schema-packages.md#secondary-servers) for the package side of the configuration.

> **PostgreSQL and MySQL** deployments use a single connection. Replication and read-only standbys are typically managed at the database engine level, not by the deployment tool.

---

## Deployment Execution Flow

When SchemaQuench runs, the product quench executes these steps in order:

1. **Log product info** -- Logs the product name, platform, template order, validation script, and any configured script tokens.
2. **Test server connection** -- Opens a connection to the target server and runs a platform-appropriate liveness check. Aborts if the connection fails.
3. **Validate server** -- If `Product.ValidationScript` is configured, executes it against the platform's administrative database (`master` on SQL Server, `postgres` on PostgreSQL, the connection database on MySQL). Aborts if the result is falsy.
4. **Validate baseline** -- If `Product.BaselineValidationScript` is configured, executes it. Aborts if the result is falsy.
5. **Product Before scripts** -- Executes scripts from `Before Product` folder(s). On SQL Server with secondary servers, scripts run in parallel to all eligible servers.
6. **Quench each template** -- For each template name in `Product.TemplateOrder`:
   - Loads `Template.json` and merges template-level `ScriptTokens` over the product token set.
   - Executes `DatabaseIdentificationScript` against the administrative database to discover target databases.
   - Creates a database quench task for each discovered database and runs them in parallel (up to `MaxThreads` concurrent operations).
   - If any database quench fails, logs the failure and exits with code 2.
7. **Product After scripts** -- Executes scripts from `After Product` folder(s).
8. **Stamp product version** -- If `Product.VersionStampScript` is configured, executes it.
9. **Log completion** -- Logs "Completed quench of {ProductName}".

After the quench returns, the calling program backs up log files to a numbered directory and exits with code 0 (see [Exit Codes](configuration.md#exit-codes)).

---

## Database Quench Sequence

For each database identified by a template's `DatabaseIdentificationScript`, the database quench runs the following sequence. All steps execute on the identified database.

1. **Kindle the Forge** -- Deploys SchemaSmith helper procedures, functions, and the migration tracking table for the active platform. Skipped if `KindleTheForge` is `false`.
2. **Validate baseline** -- Executes `Template.BaselineValidationScript` if configured. Aborts if falsy.
3. **Object scripts (first pass)** -- Executes scripts from all `Objects`-slot folders using the dependency retry loop. If `RunScriptsTwice` is enabled, resets all scripts and runs a complete second pass to verify idempotency.
4. **Parse table JSON** -- Serializes all `Tables/*.json` definitions into temp/staging tables for the modular procedures to consume.
5. **MissingTableAndColumnQuench** -- Creates missing tables and adds missing columns.
6. **Object scripts (second opportunity)** -- Re-attempts any Objects-slot scripts that failed in step 3, now that missing tables exist.
7. **Before scripts** -- Executes migration scripts from any folder in the `Before` slot. Sequential and tracked.
8. **ModifiedTableQuench** -- Alters existing columns (type changes, nullability, defaults, computed/generated columns) and manages indexes and check constraints.
9. **Object scripts (third opportunity)** -- Re-attempts any remaining failed Objects-slot scripts now that table modifications are complete.
10. **BetweenTablesAndKeys scripts** -- Executes migration scripts from any folder in the `BetweenTablesAndKeys` slot. Sequential and tracked.
11. **MissingIndexesAndConstraintsQuench** -- Creates missing indexes, check constraints, default constraints, and (where supported) statistics.
12. **AfterTablesScripts** -- Executes migration scripts from any folder in the `AfterTablesScripts` slot. Sequential and tracked.
13. **AfterTablesObjects scripts** -- Executes scripts from `AfterTablesObjects`-slot folders (triggers, DDL triggers, rules, post-table views) using the dependency retry loop. Also retries any still-unresolved Objects-slot scripts.
14. **Table Data scripts** -- Executes scripts from any folder in the `TableData` slot using the dependency retry loop.
15. **ForeignKeyQuench** -- Creates, drops, and modifies foreign keys to match the schema package.
16. **Indexed view / materialized view quench** -- If the template defines indexed views (SQL Server) or materialized views (PostgreSQL), deploys them via the platform's view quench procedure.
17. **After scripts** -- Executes migration scripts from any folder in the `After` slot. Sequential and tracked.
18. **Stamp version** -- Executes `Template.VersionStampScript` if configured.

When `UpdateTables` is `false`, steps 4 through 16 are skipped entirely. When `IndexOnlyTableQuenches` is enabled on a template, steps 4--8 (parse JSON, missing tables, second Objects pass, Before scripts, modified tables) are replaced by a single call to the platform's `IndexOnlyQuench` procedure. Steps 9--16 still execute, with `MissingIndexesAndConstraintsQuench` (step 11) and `ForeignKeyQuench` (step 15) skipped.

---

## Quench Slots

SchemaQuench assigns every script folder to a quench slot that determines when the folder's scripts execute and how they are handled. The slot list is the same on every platform; the **default folders** vary by platform (see [Schema Packages -- Default Folders](schema-packages.md#default-folders)).

### Template quench slots

| Slot | Execution Style |
|------|-----------------|
| `Before` | Sequential, tracked |
| `Objects` | Dependency retry loop |
| `BetweenTablesAndKeys` | Sequential, tracked |
| `AfterTablesScripts` | Sequential, tracked |
| `AfterTablesObjects` | Dependency retry loop |
| `TableData` | Dependency retry loop |
| `After` | Sequential, tracked |

### Product quench slots

| Slot | Execution Style |
|------|-----------------|
| `Before` | Sequential |
| `After` | Sequential |

Product scripts run against the administrative connection, outside the per-database template loop.

**Sequential, tracked** -- scripts run in alphabetical order and are recorded in `CompletedMigrationScripts` so they only run once (unless marked `[ALWAYS]`).

**Dependency retry loop** -- scripts are retried in rounds until all succeed or no progress is made. See [Dependency Retry Loop](#dependency-retry-loop).

---

## WhatIf Mode

See exactly what SchemaQuench would do before it touches a single table. Set `WhatIfONLY` to `true` to perform a dry run. In WhatIf mode:

- **Validation scripts** execute normally (server validation, baseline validation).
- **Table quench procedures** run with `@WhatIf = 1`, generating the SQL that would be executed and logging it without applying changes.
- **Migration scripts** show detailed status for each script:
  - `Would APPLY: {script}` for scripts that haven't yet been tracked.
  - `Would SKIP (previously quenched): {script}` for scripts already recorded in `CompletedMigrationScripts`.
- **Object scripts** (Objects, AfterTablesObjects, Table Data) are logged but not executed.
- **Product Before/After scripts** are logged but not executed.
- **Version stamp scripts** aren't executed; a log message indicates the stamp would occur.

**Important limitation:** WhatIf shows the top level of changes, not the full cascade. Because nothing actually executes, WhatIf can't show ripple effects that depend on earlier changes having been applied. For example, if an object script drops an index, that script doesn't run in WhatIf mode, so the index still exists when WhatIf analyzes table changes -- meaning the table diff won't show the index as needing to be recreated. WhatIf is a confidence check, not a guarantee. It catches the majority of issues but the full deployment may produce additional changes that WhatIf couldn't predict.

### Debug SQL output

During both normal and WhatIf runs, SchemaQuench writes the SQL generated by the table quench process to files in the working directory:

- `SchemaQuench - ParseJson {DatabaseName}.sql`
- `SchemaQuench - MissingTableAndColumnQuench {DatabaseName}.sql`
- `SchemaQuench - ModifiedTableQuench {DatabaseName}.sql`
- `SchemaQuench - MissingIndexesAndConstraintsQuench {DatabaseName}.sql`
- `SchemaQuench - ForeignKeyQuench {DatabaseName}.sql`
- `SchemaQuench - IndexedViewQuench {DatabaseName}.sql` (SQL Server)
- `SchemaQuench - MaterializedViewQuench {DatabaseName}.sql` (PostgreSQL)
- `SchemaQuench - IndexOnlyQuench {DatabaseName}.sql` (when `IndexOnlyTableQuenches` is enabled)

These files can be reviewed to understand exactly what structural changes were (or would be) made.

### When to use WhatIf

| Environment | Guidance |
|-------------|----------|
| Development | Optional -- quench directly if you're comfortable with the changes. |
| Staging | Recommended -- review WhatIf output to catch surprises before production-like data. |
| Production | Non-negotiable -- always WhatIf first, read the generated SQL, then deploy in a separate run. |

---

## KindleTheForge

Before SchemaQuench can shape your database, it needs its tools in place. KindleTheForge deploys the SchemaSmith infrastructure to each target database. The infrastructure includes a per-platform set of helper functions, modular table-quench procedures, the indexed view or materialized view procedure where applicable, the reverse-engineering procedures used by SchemaTongs, and the `CompletedMigrationScripts` tracking table.

KindleTheForge runs on every quench to ensure the helper procedures match the version of SchemaQuench being used. In a normal release pipeline, always leave this `true`.

**When to set false -- the datafix pipeline:** When the deployment user has read/write access but no DDL modification ability, and you're running only migration scripts to fix data. Turning off KindleTheForge (along with `UpdateTables: false` and `DropTablesRemovedFromProduct: false`) reduces the scope of what executes and the permissions required. This is a deliberate permission boundary -- data fixes shouldn't make structural changes that the next full release would overwrite or conflict with.

---

## Modular Quench Procedures

The table quench is broken into modular stored procedures, each handling a specific aspect of the table schema. The procedures are deployed during the KindleTheForge step and called in sequence during the database quench. Every platform ships its own implementation, but the responsibilities are the same.

| Procedure | Responsibility |
|---|---|
| **MissingTableAndColumnQuench** | Creates tables that exist in the schema package but not in the database. Adds columns that exist in the table definition but are missing from the existing table. |
| **ModifiedTableQuench** | Alters existing columns to match the schema package definitions. Handles data type, nullability, default constraint, and computed/generated column changes. Drops removed tables when `DropTablesRemovedFromProduct` is enabled. |
| **MissingIndexesAndConstraintsQuench** | Creates indexes, check constraints, default constraints, and (where supported) statistics that exist in the schema package but are missing from the database. |
| **ForeignKeyQuench** | Creates, modifies, and drops foreign keys to match the schema package. Runs late in the sequence so all referenced tables and columns exist. |
| **IndexOnlyQuench** | Alternative to the full sequence. Manages indexes only -- doesn't create tables, add columns, or manage foreign keys. Used when `IndexOnlyTableQuenches` is enabled on a template. |
| **IndexedViewQuench** | Deploys indexed views with diff-based change detection (SQL Server). |
| **MaterializedViewQuench** | Deploys PostgreSQL materialized views, including their indexes. |

The implementation lives in the deployed SQL on the target database -- which means a DBA can read it on the server with `sp_helptext` (SQL Server), `\sf` (PostgreSQL), or `SHOW CREATE PROCEDURE` (MySQL). No black boxes.

### Calling Procedures Directly from Migration Scripts

The quench procedures are deployed to the target database during the KindleTheForge step and remain there afterward. You can call them directly from Before Scripts, After Scripts, or any migration script to bootstrap specific tables or views as part of a data migration.

The typical pattern uses specific-object tokens to quench individual objects that your migration script depends on, rather than passing the entire schema. First, define a token in your `Product.json` or `Template.json`:

```json
{
  "ScriptTokens": {
    "AuditLogTable": "<*SpecificTable*>dbo.AuditLog"
  }
}
```

Then call the procedure from your migration script using the token:

**TableQuench** -- ensures a specific table exists with the right structure before your migration script runs:

> **SQL Server:**
> ```sql
> -- Bootstrap the AuditLog table so we can insert into it during this migration
> EXEC SchemaSmith.TableQuench
>     @ProductName = '{{ProductName}}',
>     @TableDefinitions = '[{{AuditLogTable}}]',
>     @WhatIf = 0,
>     @DropUnknownIndexes = 0,
>     @DropTablesRemovedFromProduct = 0,
>     @UpdateFillFactor = 1;
> ```

> **PostgreSQL:**
> ```sql
> CALL "SchemaSmith"."TableQuench"(
>     '{{ProductName}}',
>     '[{{AuditLogTable}}]',
>     FALSE,  -- p_WhatIf
>     FALSE,  -- p_DropUnknownIndexes
>     FALSE,  -- p_DropTablesRemovedFromProduct
>     TRUE    -- p_UpdateFillFactor
> );
> ```

> **MySQL:**
> ```sql
> CALL SchemaSmith_TableQuench(
>     '{{ProductName}}',
>     '{{MainDB}}',
>     '[{{AuditLogTable}}]',
>     0,  -- p_WhatIf
>     0,  -- p_DropUnknownIndexes
>     0   -- p_DropTablesRemovedFromProduct
> );
> ```

The same pattern works for views. Define the token, then pass it to the procedure:

```json
{
  "ScriptTokens": {
    "OrderSummaryView": "<*SpecificIndexedView*>dbo.vw_OrderSummary",
    "ActiveOrdersView": "<*SpecificMaterializedView*>reporting.active_orders"
  }
}
```

**IndexedViewQuench** (SQL Server):

> ```sql
> EXEC SchemaSmith.IndexedViewQuench
>     @ProductName = '{{ProductName}}',
>     @IndexedViewSchema = '[{{OrderSummaryView}}]',
>     @WhatIf = 0,
>     @UpdateFillFactor = 0;
> ```

**MaterializedViewQuench** (PostgreSQL):

> ```sql
> CALL "SchemaSmith"."MaterializedViewQuench"(
>     '{{ProductName}}',
>     '[{{ActiveOrdersView}}]',
>     FALSE,  -- p_WhatIf
>     TRUE    -- p_UpdateFillFactor
> );
> ```

You can also pass the full schema tokens (`{{TableSchema}}`, `{{IndexedViewSchema}}`, `{{MaterializedViewSchema}}`) to quench all objects of that type, but the specific-object pattern is more common in migration scripts where you need one table or view to exist before proceeding.

**Parameter reference:**

| Parameter | TableQuench | IndexedViewQuench | MaterializedViewQuench |
|-----------|:-----------:|:-----------------:|:----------------------:|
| ProductName | Required | Required | Required |
| Definitions (JSON) | Required | Required | Required |
| DatabaseName (MySQL only) | Required | -- | -- |
| WhatIf | Default: off | Default: off | Default: off |
| DropUnknownIndexes | Default: off | -- | -- |
| DropTablesRemovedFromProduct | Default: on | -- | -- |
| UpdateFillFactor | Default: on | Default: off | Default: on |

**When to use direct calls:** When a migration script needs a table or view to exist before it can run -- for example, bootstrapping an audit table in a Before Script before inserting migration tracking data, or ensuring a materialized view is deployed before populating dependent tables.

---

## Migration Script Tracking

SchemaQuench remembers what it has already run, so you never have to worry about a migration script executing twice. Migration scripts (scripts in the `Before`, `BetweenTablesAndKeys`, `AfterTablesScripts`, and `After` slots) are tracked in the `SchemaSmith.CompletedMigrationScripts` table:

| Column | Description |
|--------|-------------|
| `ProductName` | The product name from `Product.json`. |
| `QuenchSlot` | The slot the script belongs to. |
| `ScriptPath` | The relative path of the script within the template. |
| `QuenchDate` | Timestamp when the script was executed. |

### Execution rules

- On each quench run, SchemaQuench checks which scripts in each slot have already been recorded.
- Scripts that appear in the tracking table are skipped.
- Scripts that don't appear are executed, and on success a tracking entry is inserted.

### The [ALWAYS] suffix

Scripts with `[ALWAYS]` in the filename (before the `.sql` extension) run on every quench regardless of tracking:

```
001_SeedReferenceData [ALWAYS].sql
002_RefreshPermissions [ALWAYS].sql
```

`[ALWAYS]` scripts are never recorded in the tracking table.

### Common [ALWAYS] Patterns

**Refreshing PostgreSQL materialized views** -- SchemaQuench deploys materialized view *definitions* via the MaterializedViewQuench procedure, but it does not refresh their data on every deployment. If your materialized views need periodic refreshing, use an `[ALWAYS]` script in the `After Scripts` folder:

```sql
-- After Scripts/001_RefreshMaterializedViews [ALWAYS].sql
REFRESH MATERIALIZED VIEW CONCURRENTLY "reporting"."active_orders";
REFRESH MATERIALIZED VIEW CONCURRENTLY "reporting"."monthly_summary";
```

The `CONCURRENTLY` keyword allows the refresh to happen without locking out concurrent reads -- but it requires a unique index on the materialized view. If your view has no unique index, drop the `CONCURRENTLY` keyword (which will block reads during refresh).

This runs on every deployment, keeping your materialized view data current with the underlying tables. For views that are expensive to refresh, consider gating the refresh with a condition or scheduling it outside of deployment.

### Ordering

Migration scripts within each slot execute in **alphabetical order by filename**. Use numeric prefixes to control execution order:

```
001_CreateStagingTable.sql
002_MigrateData.sql
003_DropStagingTable.sql
```

### Obsolete entry cleanup

When SchemaQuench processes a slot, it compares the tracking table entries against the scripts currently present in the package. Entries for scripts that no longer exist in the package are automatically removed.

### Forcing re-execution

To force a tracked script to run again, either delete the corresponding row from `SchemaSmith.CompletedMigrationScripts` in the target database, or rename the script file (tracking is by path, so a renamed script is treated as new).

---

## Dependency Retry Loop

You shouldn't have to name your files in dependency order just so they deploy correctly. Scripts in the `Objects`, `AfterTablesObjects`, and `TableData` slots execute using a dependency retry loop rather than simple sequential execution:

1. Execute all pending (not yet quenched) scripts in the slot.
2. For each script, attempt to execute all its batches. If any batch fails, record the error and move on.
3. If at least one script succeeded in this iteration, loop back to step 1 with only the remaining failed scripts.
4. If zero scripts succeeded in an iteration, the loop terminates.

On the **final attempt** (the last pass when errors are reported), failures are logged as errors and the quench fails.

This mechanism allows scripts with interdependencies to coexist in the same folder without requiring a specific naming order. For example, if View B references View A and is alphabetically first, it will fail on the first pass but succeed on the retry after View A has been created.

The Objects slot gets four opportunities to resolve: (1) before the table quench, (2) after missing tables are created, (3) after table modifications are complete, and (4) during the AfterTablesObjects pass alongside triggers. This handles cases where a view or function references a table column that doesn't yet exist on the first pass.

---

## DropTablesRemovedFromProduct

When `DropTablesRemovedFromProduct` is `true` (the default), `ModifiedTableQuench` drops tables that:

- Exist in the target database.
- Aren't defined in any table JSON file in the schema package.
- Were previously managed by this product.

This keeps the database clean as tables are removed from the schema package over time.

**Environment guidance:**

- **CI and local dev** -- `true`. Catch product areas that reference tables you plan to remove.
- **Test/staging** -- `true`. Same rationale, but verify the drop is intentional before promoting to production.
- **Production** -- Often `false`. Dropping a table is a hard drop with no built-in recovery. Teams that need rollback-friendly deployments should leave this off in production. Instead, write specialized migration scripts to rename or archive the table for a retention period before the actual drop.

**The rollback-friendly removal pattern:**
1. Remove the table from your product definition.
2. Keep `DropTablesRemovedFromProduct: false` in the production config.
3. Write a migration script that renames or archives the table (or verifies no dependencies remain).
4. After the retention period, either enable the setting for one deployment or add an explicit DROP in a migration script.

---

## RunScriptsTwice

When `RunScriptsTwice` is `true`, the Objects-slot scripts are executed twice in succession during step 3 of the database quench sequence. On the second pass, all scripts are reset to unquenched and processed through the dependency retry loop again. Both runs must succeed -- if either fails, the deployment fails.

This is an **idempotency testing** tool, not a dependency resolution mechanism. Dependency resolution is already handled by the [retry loop](#dependency-retry-loop), which retries failed scripts as long as progress is being made. RunScriptsTwice answers a different question: "Can my `[ALWAYS]` scripts and object scripts run again safely?"

**When to use:**

- **CI pipelines** -- Verify that `[ALWAYS]` scripts are truly idempotent. If a script fails on the second run, you have caught an idempotency bug before it reaches production.
- **Local development** -- Verify idempotency as you author `[ALWAYS]` scripts.

**When not to use:**

- **Production deployments** -- It doubles the execution time for the object script phase with no production benefit. This is a testing tool.

---

## TrackRunOnceMigrations

When `TrackRunOnceMigrations` is `false`, SchemaQuench treats all migration scripts as if they had the `[ALWAYS]` suffix -- no script is recorded in `CompletedMigrationScripts`, no script is skipped based on prior runs. Every migration script in every slot runs on every deployment.

This is designed for **datafix and patch pipelines** where the package is deliberately partial -- it contains only the scripts needed for this specific fix, not the full release set. Tracking would prevent re-running a fix script if needed, and the partial package would cause the pruning logic to delete tracking records from prior full releases.

When tracking is off, `PruneObsoleteMigrationTracking` is forced off regardless of its configured value.

**The datafix profile:**

```json
{
  "KindleTheForge": false,
  "UpdateTables": false,
  "DropTablesRemovedFromProduct": false,
  "TrackRunOnceMigrations": false
}
```

---

## PruneObsoleteMigrationTracking

When `PruneObsoleteMigrationTracking` is `true` (the default), SchemaQuench removes entries from `CompletedMigrationScripts` for scripts that no longer exist in the current package. This is correct for full release deployments where the package represents the complete truth.

When `false`, existing tracking entries are left alone regardless of what scripts are in the current package. This is correct for datafix and patch deployments where the package is deliberately partial -- without this setting, a datafix package containing only two scripts would cause SchemaQuench to delete the tracking records for every other migration script from prior releases.

This setting is ignored when `TrackRunOnceMigrations` is `false` (no tracking means no pruning).

---

## ShouldApplyExpression and Conditional Deployment

`ShouldApplyExpression` is a SchemaQuench feature that lives on the schema package side. Whenever SchemaQuench evaluates a table component that has a `ShouldApplyExpression` set, it resolves any tokens in the expression, runs the expression against the target database, and skips the component if the result is falsy. This means a single table file can declare components that only apply on certain databases, certain environments, or certain server versions -- no per-environment file copies, no branching logic in your deployment pipeline.

See [Schema Packages -- Conditional Application](schema-packages.md#conditional-application) for the JSON shape and worked examples, and [Custom Properties](custom-properties.md) for how to drive `ShouldApplyExpression` values from team-defined metadata.

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Successful quench. All databases quenched, logs backed up. |
| 2 | One or more database quenches failed. |
| 3 | Unhandled exception. An unexpected error occurred outside the normal quench flow. |
| 4 | Unable to back up log files. |

---

## Related Documentation

- [Configuration Reference](configuration.md) -- Shared configuration system, CLI switches, environment variables
- [Schema Packages Reference](schema-packages.md) -- Package structure, folder layout, execution order
- [Custom Properties](custom-properties.md) -- The Extensions carrier and how it drives `ShouldApplyExpression`
- [Script Tokens Reference](script-tokens.md) -- Token replacement, advanced tags, automatic tokens
- [SchemaTongs Reference](schematongs.md) -- Extraction tool that creates schema packages
