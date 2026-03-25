# Schema Packages

Applies to: SchemaQuench, SchemaTongs, DataTongs (SQL Server, Community)

---

## What Is a Schema Package?

A schema package is a folder (or ZIP archive) containing a complete definition of one or more database schemas. It is the unit of deployment for SchemaQuench and the output of SchemaTongs.

The schema package represents the **desired end state** of the target databases. When SchemaQuench processes a package, it compares the desired state against the current state of each target database and makes only the changes necessary to bring the database into alignment.

---

## Folder Structure

```
MyProduct/
  Product.json
  Templates/
    Main/
      Template.json
      Tables/
        dbo.Customer.json
        dbo.Order.json
      Schemas/
      DataTypes/
      FullTextCatalogs/
      FullTextStopLists/
      XMLSchemaCollections/
      Functions/
      Views/
      Procedures/
      Triggers/
      DDLTriggers/
      MigrationScripts/
        Before/
        After/
      Table Data/
    Secondary/
      Template.json
      Tables/
      ...
```

- **Product.json** — Root configuration. Defines the product name, template order, script tokens, and optional validation scripts. See [Products and Templates](products-and-templates.md).
- **Templates/** — Contains one or more named template directories. Each template targets a set of databases identified by a SQL query.
- **Template.json** — Template-level configuration within each template directory. See [Products and Templates](products-and-templates.md).
- **Tables/** — Table definitions as JSON files. See [Defining Tables](defining-tables.md).
- **Script folders** — SQL script files organized by object type and execution phase. See [Script Folders](schemaquench/script-folders.md).

---

## How Tools Interact with Packages

| Tool | Role |
|------|------|
| **SchemaTongs** | Creates schema packages by extracting objects from a live database. Initializes the folder structure, generates Table JSON and SQL script files. |
| **SchemaQuench** | Reads schema packages and applies them to target databases. Consumes Product.json, Template.json, Table JSON files, and all SQL scripts. |
| **DataTongs** | Generates MERGE scripts from live table data. Output scripts can be placed into a schema package's `Table Data` or `MigrationScripts` folders. |

---

## ZIP Package Support

Schema packages can be consumed as ZIP archives. When `SchemaPackagePath` in SchemaQuench's configuration points to a `.zip` file, SchemaQuench extracts and reads the package contents from the archive. The internal structure of the ZIP must match the standard folder layout.

---

## Related Documentation

- [Products and Templates](products-and-templates.md)
- [Defining Tables](defining-tables.md)
- [Script Folders](schemaquench/script-folders.md)
