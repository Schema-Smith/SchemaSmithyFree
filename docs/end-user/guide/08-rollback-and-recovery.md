# Rollback and Recovery

What if rolling back a database change was as simple as deploying the previous version? With state-based deployments, it's exactly that. Deploy the prior release's schema package, and SchemaQuench computes the delta to bring the database back to that state. No rollback scripts. No pre-planned undo steps. The same tool that deploys forward also deploys backward, on **SQL Server**, **PostgreSQL**, **MySQL**, and **MariaDB** alike.

## What rolls back automatically

When you deploy a prior release's schema package, SchemaQuench automatically reverts all schema objects to match that state.

**Table structure:**
- Tables added in the newer release are dropped
- Columns added are dropped; columns removed are re-added
- Column modifications (data type, nullability, defaults) revert
- Indexes, constraints, and foreign keys revert to prior definitions
- Check constraints and computed / generated columns revert
- Platform-specific components revert as well -- SQL Server indexed views, PostgreSQL materialized views and exclude constraints, MySQL full-text indexes

**SQL objects:**
- Stored procedures restore to the prior version
- Functions restore to the prior version
- Views restore to the prior version
- Triggers, DDL triggers, rules restore to the prior version
- All scripted objects in your product definition revert

The key advantage: stored procedures, functions, and views restore automatically because your schema package contains the full definition of every object. SchemaQuench simply reapplies the prior version. No manually maintained rollback scripts required.

## A platform note on transactional DDL

SchemaSmith's rollback semantics are the same across platforms, but the individual DDL statements behave differently inside transactions:

- **PostgreSQL** supports fully transactional DDL. Many migration patterns can be wrapped in `BEGIN` / `COMMIT`, and a failed statement rolls back the whole batch.
- **SQL Server** supports transactional DDL for most statements, but some (like `ALTER DATABASE`) cannot participate in user transactions.
- **MySQL and MariaDB** do **not** support transactional DDL. Each DDL statement is implicitly committed. A failed multi-statement deployment on MySQL or MariaDB cannot be rolled back by the engine itself -- recovery means re-quenching the prior package.

> **Warning:** Regardless of platform, rolling back with SchemaSmith means deploying the prior package. On MySQL and MariaDB especially, test rollbacks carefully because the engine won't unwind a half-applied deployment for you.

## What needs migration scripts

While schema structure reverts automatically, **data preservation** for destructive operations requires planning. SchemaSmith handles the schema; you handle the data when needed.

| Scenario | What you need |
|---|---|
| **Dropping columns with data** | If a column added in the newer release contains data you want to preserve, write a migration script to copy that data before SchemaQuench drops the column. |
| **Restoring dropped columns** | If the newer release dropped a column, SchemaSmith recreates the structure but the original data is gone. You need a backup or a migration script that preserved the data before the original drop. |
| **Table drops with data** | Tables removed from your product definition are dropped. Use migration scripts to preserve data before rollback. |
| **Data transformations** | Complex data migrations (splitting columns, merging tables) require migration scripts to transform data appropriately during rollback. |

Most rollbacks don't require migration scripts. Data preservation is only needed when rolling back involves data loss -- either from columns added in the newer release, or when restoring data that was dropped. The declarative model handles structure; you handle data when the operation is destructive.

## How to roll back a release

1. **Get the prior release's product definition.** Check out the tagged release from source control, or retrieve the schema package artifact from your prior deployment.

2. **Review what will change.** Run SchemaQuench in WhatIf mode to see what the rollback will modify:

   ```bash
   SmithySettings_WhatIfONLY=true SchemaQuench
   ```

   Read the generated SQL. Identify any data-destructive operations (column drops, table drops) that need preservation scripts.

3. **Run data preservation scripts if needed.** If the WhatIf output shows columns or tables being dropped that contain data you need, run your preservation scripts first.

4. **Configure for the target environment.** Ensure your environment variables or settings file point to the correct server and database.

5. **Run SchemaQuench.**

   ```bash
   SchemaQuench
   ```

6. **Verify the deployment.** Review the SchemaQuench progress log to confirm all changes applied successfully.

## Best practices

**Tag releases.** Keep tagged releases of your product definition in source control. This makes it trivial to retrieve any prior version for rollback -- `git checkout v1.4.2` (or your VCS equivalent) and you have everything you need.

**Note data dependencies.** When adding columns or tables that will contain important data, document whether rollback requires data preservation scripts. Include this in your release notes.

**Test rollbacks regularly.** Practice rollback procedures in dev or staging. When you need to roll back production, you'll already know the process works.

**Back up major rollbacks.** For significant rollbacks, take a database backup first. This gives you a safety net if you discover unexpected data dependencies after the rollback completes. Especially important on MySQL and MariaDB, where the engine can't unwind partial DDL on its own.

**WhatIf first, always.** Never roll back production without running WhatIf mode and reading every line of the generated SQL. The same discipline that applies to forward deployments applies to rollbacks.

**Choose your auto-drop posture.** `DropTablesRemovedFromProduct` defaults to `true`, so rolling back to a prior package removes the tables the newer release added -- clean convergence, but an outright drop destroys their data. Two safe postures fit a rollback-friendly deployment: (1) set `DropTablesRemovedFromProduct: false` so the rollback leaves those tables in place until you're sure it's permanent, then clean them up explicitly -- the conservative default when you have no other safety net; or (2) keep auto-drops on but install the `SchemaSmith.CustomTableDrop` / `SchemaSmith.CustomTableRestore` recyclebin hooks that move a removed table to a recoverable holding area instead of destroying it, so the rollback converges *and* the data is recoverable. Either way, **a dropped column is never caught by these mechanisms** -- preserve column data yourself before a rollback that drops it. See [SchemaQuench -- DropTablesRemovedFromProduct](../reference/schemaquench.md#droptablesremovedfromproduct) and [Recyclebin -- Soft-Drop and Restore Hooks](../reference/recyclebin.md).

> **Tip:** When one specific table must never be dropped -- not on a rollback, not when the package is trimmed -- set `PreventDrop: true` on it. The protection is *sticky*: SchemaSmith persists it in the database, so the table is skipped even after you remove it from the package entirely. See [SchemaQuench -- PreventDrop](../reference/schemaquench.md#preventdrop).

---

Rollback is the safety net that makes forward progress possible. Know you can go back, move forward with confidence. Now let's look at the power features that make SchemaSmith scale -- tokens, multi-database products, reference data, and execution slots. [Power Workflows](09-power-workflows.md)
