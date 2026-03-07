# Script Folders

Applies to: SchemaQuench (SQL Server, Community)

---

## How Script Folders Work

Each template in a schema package contains a fixed set of script folders. SQL files (`.sql`) placed in these folders are loaded and executed during the quench phase corresponding to the folder's assigned quench slot.

Before execution, all script tokens (`{{TokenName}}`) in the SQL content are replaced with their resolved values. Scripts are then split into batches on `GO` statements and each batch is executed separately.

---

## Default Folders

Each template includes 13 script folders with fixed quench slot assignments. These folders are created automatically when SchemaTongs initializes a new template. For the complete folder table with quench slot assignments, see [Products and Templates — Script Folders](../products-and-templates.md#script-folders).

---

## Execution Behavior by Slot

### Before and After (Sequential, Tracked)

Scripts in the `Before` and `After` slots execute in alphabetical order by filename. Each script is tracked in the `CompletedMigrationScripts` table and only runs once unless the filename contains `[ALWAYS]`. See [Migration Scripts](migration-scripts.md).

### Objects and AfterTablesObjects (Dependency Retry Loop)

Scripts in these slots execute using a [dependency retry loop](README.md#dependency-retry-loop). Scripts that fail due to unresolved dependencies are retried automatically until all succeed or no progress is made.

The `AfterTablesObjects` slot runs after the table quench, ensuring that triggers can reference columns and indexes that were just created or modified. It also includes any unresolved scripts from the `Objects` slot, giving them another chance to succeed after tables have been created.

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
