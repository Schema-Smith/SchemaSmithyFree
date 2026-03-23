# Viewing Schemas

Applies to: SchemaHammer (SQL Server, Community)

---

## Product Editor

Select the root product node in the tree to open the Product editor. It shows:

- Product name and platform
- Validation scripts and baseline validation script
- Template order
- Script tokens (on the Script Tokens tab)

---

## Template Editor

Select a template node to open the Template editor. It shows:

- Template name
- Database identification script
- Validation scripts and baseline validation script
- Fill factor
- Script tokens (on the Script Tokens tab)

---

## Table Editor

Select a table node to open the Table editor. It shows the table's properties:

- Schema and name
- Data compression setting
- Temporal table configuration
- Fill factor
- Old name (used by SchemaQuench for rename tracking)

Expand the table node in the tree to browse its child objects — columns, indexes, foreign keys, check constraints, statistics, XML indexes, and full-text indexes. Selecting any child node opens a dedicated editor for that item.

---

## Child Object Editors

Each child object type has its own editor showing all properties for the selected item:

- **Column** — Name, data type, nullability, default value, computed expression, identity settings, and more.
- **Index** — Name, type, uniqueness, included columns, filter expression, fill factor, and storage options.
- **Foreign Key** — Name, referenced table and columns, and update/delete actions.
- **Check Constraint** — Name and constraint expression.
- **Statistic** — Name, columns, and filter expression.
- **XML Index** — Name, type, and associated primary XML index.
- **Full-Text Index** — Columns, language, catalog, and change tracking settings.
- **Indexed View** — Treated like a table, with its own columns, indexes, and statistics.

---

## SQL Script Viewer

Selecting a script file node opens the SQL script viewer with T-SQL syntax highlighting.

**Token preview** — Scripts may contain `{{{Token}}}` placeholders. Click the **Preview** button to toggle between the raw script (showing placeholder text) and a preview with placeholders expanded to their resolved values based on the current product and template token definitions.

---

## Script Tokens Tab

The Product and Template editors each include a **Script Tokens** tab listing all key/value pairs defined at that level. Template tokens supplement and override product tokens when SchemaQuench runs against a specific database.

---

## Update Schema Files

**Tools > Update Schema Files** regenerates the `.json-schemas/` validation files for the current product. These files enable JSON schema validation in editors such as Visual Studio Code when editing `Product.json`, `Template.json`, and table JSON files directly. Run this after upgrading SchemaSmith to pick up any changes to the schema format.

---

## About Dialog

**Help > About** shows the current SchemaHammer version number and a link to the SchemaSmith Community GitHub repository.
