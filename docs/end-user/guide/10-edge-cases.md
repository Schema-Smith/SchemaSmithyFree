# Edge Cases and Escape Hatches

Most of the time, SchemaSmith's [state-based approach](03-core-concepts.md#state-based-vs-migration-based) handles everything. You declare the desired state, quench, and the tooling figures out the diff. But some situations are inherently imperative — data transforms, rename operations, dependency tangles, version constraints. For those, SchemaSmith provides deliberate escape hatches. You won't need these every day, but when you do, here's exactly how to reach for them.

## Migration scripts

State-based deployment excels at structural changes: add a column, change a data type, create an index. But some operations require explicit steps that can't be inferred from a before-and-after comparison. Splitting a table in two. Backfilling a new column from existing data. Running a one-time data cleanup. These are migration script territory — the escape hatch for when the declarative approach needs a hand.

### How tracking works

Migration scripts live under `MigrationScripts/` within a template folder. SchemaQuench tracks each script in a `CompletedMigrationScripts` table using the script's relative path as the key. Once a script runs successfully against a database, it won't run again on subsequent deployments.

This makes migration scripts safe to leave in your schema package permanently. They serve as a historical record of imperative changes — a built-in audit trail that stays with your project.

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

The `[ALWAYS]` marker is case-sensitive and must be uppercase. It must appear at the end of the filename (before the `.sql` extension).

### Escape hatch, not default workflow

Reach for migration scripts when state-based can't express what you need. If you find yourself writing migration scripts for routine schema changes, step back — the declarative approach can almost certainly handle it. The goal is a schema package where the JSON and SQL files describe the target state, with migration scripts reserved for the genuinely imperative operations. Keep the escape hatch for when you truly need it.

> For the complete deployment sequence and slot details, see [SchemaQuench Reference](../reference/schemaquench.md).

## Table and column renames via OldName

Renaming is the classic case where state-based falls short. If you change a table name from `CustomerOrders` to `Orders` in your JSON, SchemaSmith sees "drop `CustomerOrders`, create `Orders`" — losing all your data. The `OldName` property bridges that gap, preserving your data through the rename.

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

**Step 2: Quench.**

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

Quench, verify, remove `OldName`. The workflow is identical.

> For the full table and column JSON schema, see [Schema Packages Reference](../reference/schema-packages.md).

## RunScriptsTwice — idempotency testing

Cross-dependencies between object scripts (views, functions, stored procedures) are already handled by SchemaQuench's built-in [dependency retry loop](../reference/schemaquench.md#dependency-retry-loop) — it runs scripts, collects failures, and retries until all succeed or no progress is made. `RunScriptsTwice` isn't about dependency resolution. It's about **idempotency testing**.

When `RunScriptsTwice` is enabled, SchemaQuench runs the entire set of object scripts twice in succession. Both runs must succeed. This answers the question: "Can my `[ALWAYS]` scripts and object scripts run again safely without breaking?" If a script fails on the second run, you have found a bug before it reaches production.

**Enable it in your SchemaQuench configuration:**

```json
{
  "RunScriptsTwice": true
}
```

**When to use it:**
- **CI pipelines** — Enable to verify that `[ALWAYS]` scripts are truly idempotent. If a script fails on the second run, you have caught an idempotency bug.
- **Local development** — Enable when authoring `[ALWAYS]` scripts to verify idempotency as you write them.
- **Not for production deployments** — It doubles the execution time for the object script phase with no production benefit. It's a testing tool.

> See [SchemaQuench Reference -- RunScriptsTwice](../reference/schemaquench.md#runscriptstwice) for the full configuration details.

## MinimumVersion

`MinimumVersion` in `Product.json` declares the minimum SQL Server version this product targets. It's currently **metadata only** — SchemaQuench doesn't enforce it at runtime. An upcoming enhancement will use MinimumVersion to adapt generated tool code to match the target server version, making it functional rather than purely declarative.

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

**If you need version gating today**, use `ValidationScript` with a `SERVERPROPERTY` check:

```json
{
  "Name": "MyProduct",
  "MinimumVersion": "Sql2016",
  "ValidationScript": "SELECT CAST(CASE WHEN SERVERPROPERTY('ProductMajorVersion') >= 13 THEN 1 ELSE 0 END AS BIT)"
}
```

This gives you a hard deployment gate while MinimumVersion remains declarative. Script tokens and T-SQL conditional logic in migration scripts can handle version-specific structural changes.

> See the [Schema Packages Reference](../reference/schema-packages.md) for the full Product.json specification.

## Orphan handling strategies

When SchemaTongs casts your database schema, it may find objects on the server that don't exist in your schema package — views someone created manually, tables from an old feature, procedures that were never removed. These are orphans. SchemaTongs offers three strategies for dealing with them:

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

Start with `Detect` to understand what is out there. Move to `DetectWithCleanupScripts` when you're ready to act but want manual review. Use `DetectDeleteAndCleanup` only when you trust the extraction and want a fully clean schema package. You control the pace.

> For the complete extraction configuration, see [SchemaTongs Reference](../reference/schematongs.md).

## Complex type handling in DataTongs

DataTongs generates MERGE scripts from live data. Most column types serialize to JSON naturally, but a few require special handling. DataTongs manages all of these automatically — no configuration needed. This section documents the behavior so you know what to expect when you encounter these types.

**Geography and Geometry** — Stored as Well-Known Text (WKT) with the SRID preserved separately. On import, `geography::STGeomFromText()` reconstructs the spatial value. Round-trip fidelity is maintained.

**HierarchyID** — Converted to its canonical string representation (e.g., `/1/3/`) via `.ToString()`. Reconstructed on import.

**XML** — Cast to `NVARCHAR(MAX)` for JSON serialization. XML schema collection bindings are preserved in the MERGE script's type declarations.

**NTEXT / TEXT / IMAGE** — Legacy LOB types are cast to their modern equivalents (`NVARCHAR(MAX)`, `VARCHAR(MAX)`, `VARBINARY(MAX)`) for serialization.

**Identity columns** — DataTongs detects identity columns and wraps the MERGE statement with `SET IDENTITY_INSERT ON/OFF` automatically.

**Computed columns** — Automatically excluded from both SELECT and INSERT operations since their values are derived.

**ROWGUIDCOL columns** — Excluded from update operations to avoid conflicts with SQL Server's internal GUID management.

**Excluded types** — `sql_variant`, `rowversion`, and `timestamp` columns are excluded entirely, as these types can't be reliably round-tripped through JSON.

> For the full DataTongs configuration, see [DataTongs Reference](../reference/datatongs.md).

## Filesystem-illegal characters

SQL Server allows characters in object names that filesystems don't — colons, asterisks, question marks, angle brackets, pipes, and others. When SchemaSmith writes files for objects with these characters, it encodes them using percent-encoding (the same scheme used in URLs).

For example, a view named `Sales:Summary` becomes the file `Sales%3ASummary.sql`. A table in a schema with a trailing space becomes `MySchema%20`. SchemaSmith decodes these names automatically when reading files back, so the round-trip is transparent.

The encoded characters are: `\`, `/`, `:`, `*`, `?`, `"`, `<`, `>`, `|`, and the `%` sign itself. Leading and trailing spaces and dots are also encoded since some filesystems silently strip them. Windows reserved names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`) receive encoding on their first character to avoid filesystem conflicts.

You'll rarely need to think about this. It matters most when you're searching your schema package for an object with unusual characters in its name — look for the percent-encoded version.

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

## The adoption approach

Multiple settings in SchemaSmith share the same story: **start conservative, align in stages, enforce once you're ready.** Whether it's indexes, fill factors, table drops, or the whole schema adoption journey — the pattern is the same.

### Why big-bang alignment fails

Attempting to bring everything into alignment in a single deployment is one of the most common mistakes teams make. The consequences are real: hours-long deployment windows when every index on every table gets rebuilt, unexpected performance degradation when indexes are dropped that hadn't been captured in the repository yet, or accidental data loss when tables are dropped that still had dependencies.

One company worked on index drift alignment for over two years only to find that nothing was really better in production because new drift kept happening. The tools that stopped the drift were deployed in controlled stages with careful planning and testing — and only after the repository was complete did they enable enforcement.

### The phased pattern

The practical approach works the same way across all alignment settings:

1. **Extract and inventory.** Use SchemaTongs to capture your current schema. Review what comes out. You may find vast differences across environments — objects in production that don't exist in dev, duplicates, contradictions.

2. **Build up the repository in stages.** Don't try to capture everything in one shot. Add objects to your schema package in controlled stages — maybe one schema area at a time. Deploy each stage, verify performance, confirm the deployment window is acceptable.

3. **Keep enforcement off during alignment.** While building up the repository, you don't want to accidentally drop objects you haven't captured yet. Leave enforcement settings (`DropUnknownIndexes`, `DropTablesRemovedFromProduct`) off until you're confident the repository represents the complete desired state.

4. **Turn on enforcement to prevent drift from returning.** Once the schema definition is clear and you have verified alignment across environments, enable the enforcement settings. Now SchemaSmith prevents drift from coming back the moment you turn your back.

### Settings that follow this pattern

| Setting | What it enforces | What goes wrong if you enable too early |
|---------|-----------------|----------------------------------------|
| `DropUnknownIndexes` | Index alignment | Indexes dropped that you haven't captured yet. Deployment may take much longer than expected, and query performance may degrade. |
| `DropTablesRemovedFromProduct` | Table cleanup | Tables dropped, data gone, rollback requires restore from backup. |
| `UpdateFillFactor` | Fill factor alignment | Every index on every table rebuilt to match declared fill factors — potentially hours of deployment time on large databases. |

### Bite-sized is the only size

You won't get every table or scripted object into the repository in one pass. You'll find alignment problems and environmental dependencies across multiple environments that need to be resolved before those objects can be safely managed by state-based deployment. This is a long-term cure, not a miracle pill that solves everything on day one.

Add what is safe, deploy, verify, repeat. Each iteration brings more of your schema under management and stops more drift from recurring. If you get stumped on how to bring a particular object or pattern into alignment, reach out — [ForgeBarrett@SchemaSmith.com](mailto:ForgeBarrett@SchemaSmith.com). With more than 20 years of doing this with and without proper tooling, we have likely solved a similar problem already.

---

Power workflows for the common path. Escape hatches for the exceptions. You've got everything you need. If something goes wrong, the next chapter helps you diagnose it. [Troubleshooting](11-troubleshooting.md)
