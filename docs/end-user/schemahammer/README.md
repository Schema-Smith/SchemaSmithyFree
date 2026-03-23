# SchemaHammer

Applies to: SchemaHammer (SQL Server, Community)

---

## What SchemaHammer Does

SchemaHammer is a desktop schema viewer for SchemaSmith schema packages. It lets you browse the contents of a product — its templates, tables, scripts, and configuration — without connecting to a server or making any changes.

Use SchemaHammer when you want to understand what a schema package contains, inspect table definitions, read migration scripts, or review script tokens, all from a clean visual interface.

---

## How to Open a Product

1. Run the SchemaHammer executable.
2. Go to **File > Choose Product** and select the `Product.json` file for the product you want to browse.
3. The product tree loads in the left panel. Select any node to view its details on the right.

Recently opened products appear under **File > Recent Products** for quick access.

---

## Key Features

- **Product tree** — Hierarchical view of your entire schema package: product, templates, tables, indexed views, and scripts, all in one panel.
- **Read-only editors** — View properties for every object type: products, templates, tables, columns, indexes, foreign keys, constraints, statistics, and more.
- **SQL syntax highlighting** — Script files are displayed with T-SQL syntax highlighting for easy reading.
- **Script token preview** — Toggle the Preview button on any SQL script to expand `{{{Token}}}` placeholders to their resolved values.
- **Tree search** — Filter the product tree by name to find any object quickly.
- **Code search** — Search across all script file contents, table metadata, and script token values.
- **In-editor find bar** — Search within the currently open script using Ctrl+F.
- **Dark and light themes** — Switch themes to match your preference.
- **Last selection restored** — SchemaHammer remembers your last-selected node and restores it when you reopen the same product.

---

## What SchemaHammer Does Not Do

SchemaHammer is read-only. It cannot:

- Edit or save changes to any schema object
- Deploy changes to a database
- Connect to a SQL Server

For deployment, use **SchemaQuench**. To extract schema from an existing database, use **SchemaTongs**.

---

## Related Documentation

- [Navigating the Product Tree](navigation.md)
- [Searching](search.md)
- [Viewing Schemas](viewing-schemas.md)
