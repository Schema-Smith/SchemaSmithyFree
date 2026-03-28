# Rollback and Recovery

What if rolling back a database change was as simple as deploying the previous version? With state-based deployments, it is. Deploy the prior release's schema package, and SchemaQuench computes the delta to bring the database back to that state. No rollback scripts. No pre-planned undo steps. The same tool that deploys forward also deploys backward.

## What rolls back automatically

When you deploy a prior release's schema package, SchemaQuench automatically reverts all schema objects to match that state.

**Table structure:**
- Tables added in the newer release are dropped
- Columns added are dropped; columns removed are re-added
- Column modifications (data type, nullability, defaults) revert
- Indexes, constraints, and foreign keys revert to prior definitions
- Check constraints and computed columns revert

**SQL objects:**
- Stored procedures restore to the prior version
- Functions restore to the prior version
- Views restore to the prior version
- Triggers restore to the prior version
- All scripted objects in your product definition revert

The key advantage: stored procedures, functions, and views restore automatically because your schema package contains the full definition of every object. SchemaQuench simply reapplies the prior version. No manually maintained rollback scripts required.

## What needs migration scripts

While schema structure reverts automatically, **data preservation** for destructive operations requires planning. SchemaSmith handles the schema; you handle the data when needed.

| Scenario | What you need |
|---|---|
| **Dropping columns with data** | If a column added in the newer release contains data you want to preserve, write a migration script to copy that data before SchemaQuench drops the column. |
| **Restoring dropped columns** | If the newer release dropped a column, SchemaSmith recreates the structure but the original data is gone. You need a backup or a migration script that preserved the data before the original drop. |
| **Table drops with data** | Tables removed from your product definition are dropped. Use migration scripts to preserve data before rollback. |
| **Data transformations** | Complex data migrations (splitting columns, merging tables) require migration scripts to transform data appropriately during rollback. |

Most rollbacks do not require migration scripts. Data preservation is only needed when rolling back involves data loss — either from columns added in the newer release, or when restoring data that was dropped. The declarative model handles structure; you handle data when the operation is destructive.

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

**Tag releases in source control.** Keep tagged releases of your product definition in Git. This makes it trivial to retrieve any prior version for rollback — `git checkout v1.4.2` and you have everything you need.

**Document data dependencies.** When adding columns or tables that will contain important data, document whether rollback requires data preservation scripts. Include this in your release notes.

**Test rollbacks regularly.** Practice rollback procedures in dev or staging. When you need to roll back production, you will already know the process works.

**Back up before major rollbacks.** For significant rollbacks, take a database backup first. This gives you a safety net if you discover unexpected data dependencies after the rollback completes.

**WhatIf first, always.** Never roll back production without running WhatIf mode and reading every line of the generated SQL. The same discipline that applies to forward deployments applies to rollbacks.

---

Rollback is the safety net that makes forward progress possible. When you know you can go back, you move forward with confidence. Now let's look at the power features that make SchemaSmith scale — tokens, multi-database products, reference data, and execution slot mastery. [Power Workflows](09-power-workflows.md)
