# SchemaTongs

Applies to: SchemaTongs (SQL Server, Community)

---

## What SchemaTongs Does

SchemaTongs extracts database schema from a live SQL Server and writes it into schema package format. It connects to a source database, reads its objects using direct SQL queries against system views, and generates the JSON and SQL files that make up a schema package.

SchemaTongs can create a new schema package from scratch or update an existing one. When updating, it overwrites object scripts and table definitions with the current state of the database.

---

## How It Works

1. **Connect** — Connects to the source SQL Server database using the configured credentials.
2. **Deploy helpers** — Installs or updates the `SchemaSmith.GenerateTableJson` stored procedure in the source database (used to extract table definitions).
3. **Initialize or update package** — Creates the schema package directory structure if it does not exist, including `Product.json`, `Template.json`, all 13 default script folders, and JSON schema validation files.
4. **Extract objects** — Reads each enabled object type from the database and writes SQL scripts and table JSON files to the appropriate folders.

---

## What It Extracts

| Object Type | ShouldCast Flag | Output Location | File Naming |
|---|---|---|---|
| Tables | `Tables` | `Tables/` | `schema.tablename.json` |
| Schemas | `Schemas` | `Schemas/` | `schemaname.sql` |
| User-defined data types | `UserDefinedTypes` | `DataTypes/` | `schema.typename.sql` |
| User-defined table types | `UserDefinedTypes` | `DataTypes/` | `schema.typename.sql` |
| Scalar and table-valued functions | `UserDefinedFunctions` | `Functions/` | `schema.functionname.sql` |
| Views | `Views` | `Views/` | `schema.viewname.sql` |
| Stored procedures | `StoredProcedures` | `Procedures/` | `schema.procedurename.sql` |
| DML triggers | `TableTriggers` | `Triggers/` | `schema.triggername.sql` |
| Full-text catalogs | `Catalogs` | `FullTextCatalogs/` | `catalogname.sql` |
| Full-text stop lists | `StopLists` | `FullTextStopLists/` | `stoplistname.sql` |
| DDL triggers | `DDLTriggers` | `DDLTriggers/` | `triggername.sql` |
| XML schema collections | `XMLSchemaCollections` | `XMLSchemaCollections/` | `schema.collectionname.sql` |

---

## Filtering

### ShouldCast Flags

Each object type can be individually enabled or disabled via boolean flags in the `ShouldCast` configuration section. All flags default to `true` except `ScriptDynamicDependencyRemovalForFunctions`.

### ObjectList

The `ObjectList` setting accepts a comma or semicolon-separated list of specific object names to extract. When specified, only objects whose name matches an entry in the list are extracted (in addition to respecting the ShouldCast flags).

Object names can be specified with or without a schema prefix. Matching is case-insensitive.

```json
"ObjectList": "dbo.Customer,dbo.vw_ActiveCustomers,usp_GetOrders"
```

When `ObjectList` is empty (the default), all objects matching the enabled ShouldCast flags are extracted.

---

## Script Formatting

SchemaTongs generates SQL scripts with these conventions:

- **CREATE OR ALTER** — Used for stored procedures and functions where supported.
- **SET options** — `SET ANSI_NULLS ON` and `SET QUOTED_IDENTIFIER ON` are included in scripts.
- **IF NOT EXISTS** — Existence checks are included where appropriate.
- **Schema qualification** — Object names are schema-qualified in generated scripts.

---

## Excluded Objects

SchemaTongs automatically excludes:

- **System objects** — Objects flagged as system objects by SQL Server
- **SchemaSmith infrastructure** — Objects in the `SchemaSmith` schema
- **Encrypted objects** — Functions, views, stored procedures, and triggers that are encrypted are skipped with a warning

---

## ScriptDynamicDependencyRemovalForFunctions

When this flag is enabled (`ShouldCast:ScriptDynamicDependencyRemovalForFunctions = true`), SchemaTongs generates a dynamic SQL preamble before each function script. This preamble detects and temporarily removes computed columns, check constraints, default constraints, foreign keys, and indexes that depend on the function, allowing the function to be updated without manually dropping dependents first.

This is useful when functions are referenced by computed columns or constraints, as SQL Server normally prevents altering such functions.

---

## Package Initialization

When SchemaTongs runs against a path that does not yet contain a schema package:

1. Creates the product directory
2. Generates `Product.json` with the configured product name and a `Platform` of `"MSSQL"`
3. Creates the template directory under `Templates/<TemplateName>/`
4. Generates `Template.json` with a `DatabaseIdentificationScript` targeting the source database
5. Creates all 13 default script folders
6. Creates a `.json-schemas/` directory with JSON schema validation files for Product, Template, and Table JSON formats

On subsequent runs against an existing package, SchemaTongs overwrites scripts and table definitions but does not modify `Product.json` or `Template.json`.

---

## Related Documentation

- [SchemaTongs Configuration](configuration.md)
- [Schema Packages](../schema-packages.md)
- [Defining Tables](../defining-tables.md)
