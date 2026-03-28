# Edge Cases and Escape Hatches

Most of the time, SchemaSmith's [state-based approach](03-core-concepts.md#state-based-vs-migration-based) handles everything. You declare the desired state, deploy, and the tooling figures out the diff. But some situations are inherently imperative — data transforms, rename operations, dependency tangles, version constraints. For those, SchemaSmith provides deliberate escape hatches. You will not need these every day, but when you do, they are ready.

## Migration scripts

State-based deployment excels at structural changes: add a column, change a data type, create an index. But some operations require explicit steps that cannot be inferred from a before-and-after comparison. Splitting a table in two. Backfilling a new column from existing data. Running a one-time data cleanup. These are migration script territory.

### How tracking works

Migration scripts live under `MigrationScripts/` within a template folder. SchemaQuench tracks each script in a `CompletedMigrationScripts` table using the script's relative path as the key. Once a script runs successfully against a database, it will not run again on subsequent deployments.

This makes migration scripts safe to leave in your schema package permanently. They serve as a historical record of imperative changes — a built-in audit trail.

### The four migration slots

Migration scripts run at specific points in the deployment sequence. Choose the slot that matches when your script needs to execute:

| Slot | Folder path | Runs... |
|------|-------------|---------|
| **Before** | `MigrationScripts/Before` | Before any object scripts. Use for data transforms that must happen before schema changes. |
| **BetweenTablesAndKeys** | `MigrationScripts/BetweenTablesAndKeys` | After tables are created/altered but before foreign keys and indexes. Use when you need tables to exist but constraints would block your operation. |
| **AfterTablesScripts** | `MigrationScripts/AfterTablesScripts` | After tables, keys, and indexes are complete. Use for data migrations that depend on the full table structure being in place. |
| **After** | `MigrationScripts/After` | After all object scripts (views, procedures, triggers, etc.) have run. Use for final cleanup or validation. |

Scripts within each slot run in alphabetical order by filename. A common convention is to prefix with a sequence number:

```
MigrationScripts/
  Before/
    001_BackfillStatusColumn.sql
    002_SplitAddressTable.sql
  After/
    001_CleanupOrphanedRecords.sql
```

### The [ALWAYS] suffix

Most migration scripts should run once. But some need to run on every deployment — reseeding reference data, refreshing permissions, rebuilding certain configurations. Add `[ALWAYS]` to the filename:

```
MigrationScripts/Before/
  001_SeedReferenceData [ALWAYS].sql
  002_RefreshPermissions [ALWAYS].sql
```

The `[ALWAYS]` marker is case-insensitive and can appear anywhere in the filename, though placing it at the end before `.sql` is the clearest convention.

### Escape hatch, not default workflow

Reach for migration scripts when state-based cannot express what you need. If you find yourself writing migration scripts for routine schema changes, step back and consider whether the declarative approach can handle it instead. The goal is a schema package where the JSON and SQL files describe the target state, with migration scripts reserved for the genuinely imperative operations.

> For the complete deployment sequence and slot details, see [SchemaQuench Reference](../reference/schemaquench.md).

## Table and column renames via OldName

Renaming is the classic case where state-based falls short. If you change a table name from `CustomerOrders` to `Orders` in your JSON, SchemaSmith sees "drop `CustomerOrders`, create `Orders`" — losing all your data. The `OldName` property bridges that gap.

### Renaming a column

Suppose you need to rename the `Qty` column to `Quantity` on your `OrderLines` table.

**Step 1: Update the column definition and set OldName.**

```json
{
  "Name": "Quantity",
  "DataType": "INT",
  "Nullable": false,
  "OldName": "Qty"
}
```

**Step 2: Deploy.**

SchemaSmith sees `OldName: "Qty"` on the column named `Quantity` and executes `sp_rename` instead of a drop-and-recreate. Your data stays intact.

**Step 3: Verify and clean up.**

After the deployment succeeds and all environments have been updated, remove the `OldName` property:

```json
{
  "Name": "Quantity",
  "DataType": "INT",
  "Nullable": false
}
```

Leaving `OldName` in place after all environments have been renamed is harmless but unnecessary. Cleaning it up keeps your schema package tidy.

### Renaming a table

The same pattern works at the table level. In the table's JSON file:

```json
{
  "Name": "Orders",
  "OldName": "CustomerOrders",
  "Columns": [ ... ]
}
```

Deploy, verify, remove `OldName`. The workflow is identical.

> For the full table and column JSON schema, see [Schema Packages Reference](../reference/schema-packages.md).

## RunScriptsTwice

Object scripts (views, functions, stored procedures) can have cross-dependencies. View A references Function B, but Function B has not been deployed yet when View A runs. SchemaQuench handles most of these automatically — it runs object scripts, collects failures, then retries the failures after more objects have been created.

For deeply chained dependencies where the built-in retry loop is not enough, there is `RunScriptsTwice`. When enabled, SchemaQuench runs the entire set of object scripts a second time after the first pass completes. This resolves chains where Object A depends on B depends on C, and the single retry was not sufficient.

**Enable it in your SchemaQuench configuration:**

```json
{
  "RunScriptsTwice": true
}
```

The cost is straightforward: object scripts execute twice, so deployment takes roughly double the time for that phase. Use it only when you encounter unresolvable dependency errors during deployment, and remove it when the dependency chain is simplified.

> See [SchemaQuench Reference](../reference/schemaquench.md) for the full configuration options.

## MinimumVersion gating

Some SQL Server features only exist on certain versions. Temporal tables require SQL Server 2016 or later. If your schema package uses version-specific features, `MinimumVersion` in `Product.json` prevents accidental deployment to an unsupported server.

```json
{
  "Name": "MyProduct",
  "MinimumVersion": "Sql2016",
  "ValidationScript": "SELECT CAST(1 AS BIT)"
}
```

The supported version values are:

| Value | SQL Server version |
|-------|--------------------|
| `Sql2016` | SQL Server 2016 |
| `Sql2017` | SQL Server 2017 |
| `Sql2019` | SQL Server 2019 |
| `Sql2022` | SQL Server 2022 |
| `Sql2025` | SQL Server 2025 |

When `MinimumVersion` is set, SchemaQuench checks the target server's version before deployment begins. If the server is below the minimum, deployment stops with a clear error rather than failing partway through with cryptic SQL errors.

When `MinimumVersion` is omitted (the default), no version check is performed.

> See the [Schema Packages Reference](../reference/schema-packages.md) for the full Product.json specification.

## Orphan handling strategies

When SchemaTongs extracts your database schema, it may find objects on the server that do not exist in your schema package — views someone created manually, tables from an old feature, procedures that were never removed. These are orphans. SchemaTongs offers three strategies for dealing with them:

| Mode | Behavior |
|------|----------|
| `Detect` | Reports orphaned objects in the extraction log. No files are created or deleted. This is the default. |
| `DetectWithCleanupScripts` | Reports orphans and generates DROP scripts for review. You decide which scripts to keep and execute. |
| `DetectDeleteAndCleanup` | Reports orphans, generates DROP scripts, and removes the orphaned files from your schema package automatically. |

**Configure it in your SchemaTongs settings:**

```json
{
  "OrphanHandling": "Detect"
}
```

Start with `Detect` to understand what is out there. Move to `DetectWithCleanupScripts` when you are ready to act but want manual review. Use `DetectDeleteAndCleanup` only when you trust the extraction and want a fully clean schema package.

> For the complete extraction configuration, see [SchemaTongs Reference](../reference/schematongs.md).

## Complex type handling in DataTongs

DataTongs generates MERGE scripts from live data. Most column types serialize to JSON naturally, but a few require special handling. DataTongs manages all of these automatically — no configuration needed. This section documents the behavior so you know what to expect.

**Geography and Geometry** — Stored as Well-Known Text (WKT) with the SRID preserved separately. On import, `geography::STGeomFromText()` reconstructs the spatial value. Round-trip fidelity is maintained.

**HierarchyID** — Converted to its canonical string representation (e.g., `/1/3/`) via `.ToString()`. Reconstructed on import.

**XML** — Cast to `NVARCHAR(MAX)` for JSON serialization. XML schema collection bindings are preserved in the MERGE script's type declarations.

**NTEXT / TEXT / IMAGE** — Legacy LOB types are cast to their modern equivalents (`NVARCHAR(MAX)`, `VARCHAR(MAX)`, `VARBINARY(MAX)`) for serialization.

**Identity columns** — DataTongs detects identity columns and wraps the MERGE statement with `SET IDENTITY_INSERT ON/OFF` automatically.

**Computed columns** — Automatically excluded from both SELECT and INSERT operations since their values are derived.

**ROWGUIDCOL columns** — Excluded from update operations to avoid conflicts with SQL Server's internal GUID management.

**Excluded types** — `sql_variant`, `rowversion`, and `timestamp` columns are excluded entirely, as these types cannot be reliably round-tripped through JSON.

> For the full DataTongs configuration, see [DataTongs Reference](../reference/datatongs.md).

## Filesystem-illegal characters

SQL Server allows characters in object names that filesystems do not — colons, asterisks, question marks, angle brackets, pipes, and others. When SchemaSmith writes files for objects with these characters, it encodes them using percent-encoding (the same scheme used in URLs).

For example, a view named `Sales:Summary` becomes the file `Sales%3ASummary.sql`. A table in a schema with a trailing space becomes `MySchema%20`. SchemaSmith decodes these names automatically when reading files back, so the round-trip is transparent.

The encoded characters are: `\`, `/`, `:`, `*`, `?`, `"`, `<`, `>`, `|`, and the `%` sign itself. Leading and trailing spaces and dots are also encoded since some filesystems silently strip them. Windows reserved names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`) receive encoding on their first character to avoid filesystem conflicts.

You will rarely need to think about this. It matters most when you are searching your schema package for an object with unusual characters in its name — look for the percent-encoded version.

## Custom connection properties

Modern SQL Server configurations often require additional connection string parameters — TLS settings, read-intent routing, encryption modes. Rather than building a custom connection string, SchemaSmith supports a `ConnectionProperties` dictionary in the target configuration.

**In your SchemaQuench appsettings.json:**

```json
{
  "Target": {
    "Server": "myserver.database.windows.net",
    "User": "deploy_user",
    "Password": "s3cret",
    "ConnectionProperties": {
      "TrustServerCertificate": "True",
      "Encrypt": "True",
      "ApplicationIntent": "ReadWrite",
      "Connection Timeout": "60"
    }
  }
}
```

Each key-value pair is appended to the connection string as `Key=Value;`. This works with any connection string property that Microsoft.Data.SqlClient supports. Common uses include:

- **TrustServerCertificate** — Required when connecting to servers with self-signed certificates (common in development and Docker containers).
- **Encrypt** — Controls whether the connection is encrypted. Required for Azure SQL.
- **ApplicationIntent** — Set to `ReadOnly` for read-intent routing to availability group secondaries.
- **Connection Timeout** — Override the default timeout for slow-to-connect servers.

SchemaTongs uses the same `ConnectionProperties` structure under its `Source` configuration section.

> For the complete connection configuration, see [Configuration Reference](../reference/configuration.md).

---

Between the daily workflows, power features, and these escape hatches, you have everything you need. If something goes wrong, the next chapter will help you diagnose it. [Troubleshooting](07-troubleshooting.md)
