# Script Folders

Applies to: SchemaQuench (SQL Server, Community)

---

## How Script Folders Work

Each template in a schema package contains a fixed set of script folders. SQL files (`.sql`) placed in these folders are loaded and executed during the quench phase corresponding to the folder's assigned quench slot.

Before execution, all script tokens (`{{TokenName}}`) in the SQL content are replaced with their resolved values. Scripts are then split into batches on `GO` statements and each batch is executed separately.

---

## Default Folders

Each template includes 15 script folders with fixed quench slot assignments. These folders are created automatically when SchemaTongs initializes a new template. For the complete folder table with quench slot assignments, see [Products and Templates — Script Folders](../products-and-templates.md#script-folders).

In addition, a schema package can include product-level script folders (`ProductScripts/Before` and `ProductScripts/After`) that run outside the template loop. See [Products and Templates — Product Before/After Scripts](../products-and-templates.md#product-beforeafter-scripts).

---

## Execution Behavior by Slot

### Before and After (Sequential, Tracked)

Scripts in the `Before` and `After` slots execute in alphabetical order by filename. Each script is tracked in the `CompletedMigrationScripts` table and only runs once unless the filename contains `[ALWAYS]`. See [Migration Scripts](migration-scripts.md).

### Objects and AfterTablesObjects (Dependency Retry Loop)

Scripts in these slots execute using a [dependency retry loop](README.md#dependency-retry-loop). Scripts that fail due to unresolved dependencies are retried automatically until all succeed or no progress is made.

The `AfterTablesObjects` slot runs after the full table quench (including FK constraints), ensuring that triggers can reference columns, indexes, and keys that were just created or modified. It also includes any unresolved scripts from the `Objects` slot, giving them another chance to succeed after tables have been created.

### BetweenTablesAndKeys and AfterTablesScripts (Sequential, Tracked)

These slots are sequential and tracked, like `Before` and `After`.

**BetweenTablesAndKeys** (`MigrationScripts/BetweenTablesAndKeys`) runs after table structures are created but before foreign key constraints are applied. Use this for data migration that must happen after the table row structure exists but before FK constraints would prevent inserting or modifying rows (for example, populating a new NOT NULL column before the constraint is enforced).

**AfterTablesScripts** (`MigrationScripts/AfterTablesScripts`) runs after the full table quench (including FK constraints) but before triggers are deployed. Use this for scripts that depend on the finalized table and key structure but must run before trigger logic is active.

### TableData (Dependency Retry Loop)

Scripts in the `TableData` slot also use the [dependency retry loop](README.md#dependency-retry-loop). This handles cases where data scripts depend on other tables or data being present.

---

## File Naming

Scripts are loaded in alphabetical order by filename within each folder. While the dependency retry loop removes the strict need for ordered execution, a naming convention with numeric prefixes can make the package easier to read:

```
001_CreateHelperSchema.sql
002_CreateLookupTypes.sql
```

---

## Related Documentation

- [Migration Scripts](migration-scripts.md)
- [Products and Templates](../products-and-templates.md)
- [SchemaQuench Overview](README.md)
