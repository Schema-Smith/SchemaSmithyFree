# Migration Scripts

Applies to: SchemaQuench (SQL Server, Community)

---

## Overview

Migration scripts are SQL files placed in the `MigrationScripts/Before` and `MigrationScripts/After` folders within a template. Unlike object scripts (which run in a dependency retry loop every quench), migration scripts are tracked and run only once per product.

---

## Folder Locations

| Folder | Quench Slot | When It Runs |
|---|---|---|
| `MigrationScripts/Before` | Before | After Kindling, before Objects scripts |
| `MigrationScripts/After` | After | After all table changes and TableData scripts |

---

## Execution Behavior

Migration scripts are executed **sequentially in alphabetical order** by filename. Each script is split into batches on `GO` statements and each batch is executed separately.

After a script executes successfully, its name is recorded in the `SchemaSmith.CompletedMigrationScripts` table for the current product. On subsequent quench runs, scripts that appear in this table are skipped.

---

## The [ALWAYS] Suffix

Scripts with `[ALWAYS]` in the filename (case-insensitive) are executed on every quench run, regardless of whether they have been previously tracked:

```
001_SeedData [ALWAYS].sql
002_RefreshPermissions [ALWAYS].sql
```

The `[ALWAYS]` marker can appear anywhere in the filename. It is typically placed at the end before the `.sql` extension for readability.

---

## CompletedMigrationScripts Table

SchemaQuench maintains a `SchemaSmith.CompletedMigrationScripts` table in each target database. This table records:

- The product name
- The script filename (relative path within the template)
- The completion timestamp

This table is created during the Kindling phase if it does not already exist.

---

## Forcing Re-Execution

To force a tracked script to run again:

1. Delete the corresponding row from `SchemaSmith.CompletedMigrationScripts` in the target database.
2. Run SchemaQuench again. The script will execute because it is no longer tracked.

Alternatively, rename the script file. Since tracking is by filename, a renamed script is treated as a new script.

---

## Obsolete Entry Cleanup

When SchemaQuench processes a template's migration scripts, it compares the tracked entries in `CompletedMigrationScripts` against the scripts currently present in the package. Entries for scripts that no longer exist in the package are removed from the tracking table.

---

## Batch Splitting

Migration scripts (and all SQL scripts) are split into batches on `GO` statements. The batch splitter:

- Recognizes `GO` as a batch terminator when it appears on its own line (with optional whitespace)
- Respects string literals (single and double quotes) — `GO` inside a string is not treated as a terminator
- Respects comments (`--` line comments and `/* */` block comments)
- Respects bracket-quoted identifiers (`[GO]` is not treated as a terminator)

---

## Idempotency Best Practices

Migration scripts should be written to be idempotent where possible. Although tracked scripts only run once under normal conditions, idempotency protects against:

- Manual re-execution via tracking table cleanup
- Partial execution if a script fails partway through a multi-batch script

Common patterns:

```sql
-- Guard against re-running a data migration
IF NOT EXISTS (SELECT 1 FROM dbo.Config WHERE [Key] = 'MigrationV2Complete')
BEGIN
    -- migration logic
    INSERT INTO dbo.Config ([Key], [Value]) VALUES ('MigrationV2Complete', '1');
END
```

```sql
-- Guard against re-creating an object
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = 'MyTempTable' AND type = 'U')
BEGIN
    CREATE TABLE dbo.MyTempTable (Id INT);
END
```

---

## Related Documentation

- [SchemaQuench Overview](README.md)
- [Script Folders](script-folders.md)
- [Products and Templates](../products-and-templates.md)
