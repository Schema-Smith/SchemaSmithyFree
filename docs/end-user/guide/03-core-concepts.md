# Core Concepts

The Quick Start walked you through the full cycle: extract, browse, deploy, change, redeploy. This chapter explains the mental model behind what you just did, so the patterns make sense as your schema packages grow more complex.

## State-based vs migration-based

Most database deployment tools are migration-based. You write ordered scripts that describe *how to change* the database: "add this column, rename that index, drop this constraint." Each migration builds on the one before it, creating a chain. Break one link and everything downstream fails.

SchemaSmith is state-based. You declare *what the database should look like*, and the tool computes the delta.

Here is what the difference looks like in practice. Suppose the Products table needs a new `DiscountPercent` column.

**Migration approach** — you write the change steps:

```sql
-- Migration_042_AddDiscountPercent.sql
ALTER TABLE [dbo].[Products]
    ADD [DiscountPercent] DECIMAL(5,2) NULL
        CONSTRAINT [DF_Products_DiscountPercent] DEFAULT (0);
GO
```

This script must run exactly once, in the right order, after every prior migration. If someone already added that column on staging but not dev, the script fails. If you need to undo it, you write another migration. The migration folder becomes a growing ledger of every change ever made, and the question "what does the table look like right now?" requires reading all of them in sequence.

**State-based approach** — you declare the desired result. You edit the JSON table definition to include the new column:

```json
{
  "Name": "[DiscountPercent]",
  "DataType": "DECIMAL(5,2)",
  "Nullable": true,
  "Default": "0"
}
```

SchemaQuench reads this declaration, queries the target database, sees that `DiscountPercent` does not exist, and generates the ALTER statement itself. Run the same package against dev, staging, and production — each gets exactly the changes it needs, regardless of what state it was in before.

The benefits compound over time:

- **No ordering bugs.** There is no migration chain to break.
- **No drift.** Every deployment converges to the same declared state.
- **Readable reviews.** Pull requests show the table as it will be, not a sequence of mutations to decipher.
- **Repeatable deploys.** Deploy the same package to any environment. SchemaQuench computes the right delta for each.

## Products and Templates

A **product** is a deployable unit — the top-level container for everything SchemaSmith manages. It is defined by a `Product.json` file at the root of your schema package:

```json
{
  "Name": "Northwind",
  "ValidationScript": "SELECT CAST(1 AS BIT)",
  "TemplateOrder": [
    "Initialize",
    "Northwind"
  ],
  "ScriptTokens": {
    "NorthwindDb": "Northwind"
  },
  "Platform": "SqlServer"
}
```

The product defines cross-cutting concerns: a validation script that confirms the deployment target is correct, script tokens for environment-specific values, and the order in which templates execute.

A **template** targets a specific database (or set of databases). It lives in a subdirectory under `Templates/` and has its own `Template.json`:

```json
{
  "Name": "Northwind",
  "DatabaseIdentificationScript": "SELECT [Name] FROM master.sys.databases WHERE [Name] = '{{NorthwindDb}}'",
  "UpdateFillFactor": true,
  "ScriptTokens": {}
}
```

The `DatabaseIdentificationScript` is the key mechanism — it returns the names of databases this template should be applied to. In the simple case above, it targets a single database. But the script can return multiple rows, which means one template can deploy the same schema to many databases at once (useful for multi-tenant systems where each tenant has a separate database).

**The hierarchy:**

```
Northwind/                        <-- Product root
  Product.json                    <-- Product definition
  Templates/
    Initialize/                   <-- First template (creates the DB)
      MigrationScripts/Before/
    Northwind/                    <-- Main template (the schema)
      Template.json               <-- Template definition
      Tables/                     <-- JSON table definitions
      Procedures/                 <-- SQL procedure files
      Views/                      <-- SQL view files
      Functions/                  <-- SQL function files
      ...
```

The Northwind demo uses two templates: Initialize (which creates the database if it does not exist) and Northwind (which manages all the schema objects). Most real projects follow a similar pattern. Multi-template products come into play when a single deployment needs to touch multiple databases — for example, a shared reference database alongside the application database.

For the full list of Product.json and Template.json fields, see the [Schema Packages reference](../reference/schema-packages.md).

## Schema packages

A schema package is the folder structure that holds your entire database definition. The organizing principle is straightforward: **structure is data, behavior is code.**

**Structural objects** — tables and indexed views — are defined as JSON files. JSON is diffable, mergeable, and machine-readable. SchemaSmith parses these definitions, compares them against the live database, and computes precise ALTER statements. You never write ALTERs by hand.

**Behavioral objects** — stored procedures, functions, views, triggers — are plain `.sql` files containing CREATE OR ALTER statements. These are code: they get deployed as-is, replacing whatever currently exists. There is no diff to compute; the file *is* the definition.

Here is what a typical template folder contains:

```
Templates/Northwind/
  Template.json
  Tables/                          <-- JSON: table definitions
  Indexed Views/                   <-- JSON: indexed view definitions
  Schemas/                         <-- SQL: database schemas
  DataTypes/                       <-- SQL: user-defined types
  Functions/                       <-- SQL: scalar/table-valued functions
  Views/                           <-- SQL: view definitions
  Procedures/                      <-- SQL: stored procedures
  Triggers/                        <-- SQL: DML triggers
  DDLTriggers/                     <-- SQL: DDL triggers
  FullTextCatalogs/                <-- SQL: full-text catalog setup
  FullTextStopLists/               <-- SQL: full-text stop lists
  XMLSchemaCollections/            <-- SQL: XML schema collections
  Table Data/                      <-- SQL: MERGE scripts for reference data
  MigrationScripts/
    Before/                        <-- SQL: run before schema changes
    BetweenTablesAndKeys/          <-- SQL: run after tables, before keys
    AfterTablesScripts/            <-- SQL: run after table+key changes
    After/                         <-- SQL: run after everything else
```

You do not need all these folders. Most projects use Tables, Procedures, Views, and Functions. The rest exist when you need them. SchemaTongs creates the full structure automatically when it extracts a database.

For the complete folder reference and file naming conventions, see [Schema Packages](../reference/schema-packages.md).

## The tool lifecycle

The four SchemaSmith tools form a cycle that covers the full schema management workflow:

```
                    Live Database
                   /             \
          Extract /               \ Deploy
                 /                 \
        SchemaTongs          SchemaQuench
                 \                 /
                  \               /
                Schema Package (files in git)
                        |
                  SchemaHammer
                  (visual review)

        DataTongs: Live Database --> MERGE Scripts
```

**SchemaTongs** extracts a live database into a schema package. Tables become JSON files, procedures become SQL files, everything organized into the folder structure described above. This is how you onboard an existing database — run SchemaTongs once, commit the output, and you have a versioned baseline.

**SchemaHammer** is a desktop viewer for browsing schema packages. Open a package, navigate the tree view, inspect table definitions with syntax highlighting. It turns a folder of JSON and SQL files into something a DBA can review visually without needing to understand the file format.

**SchemaQuench** deploys a schema package to a database. It reads your declared state, queries the target, computes the delta, and applies the changes. This is the deployment engine — the tool that makes state-based management work.

**DataTongs** captures reference data from a live database as MERGE scripts. Lookup tables, configuration rows, seed data — anything that should travel with the schema. The output goes into the `Table Data/` folder and deploys alongside structural changes.

The tools do not impose a rigid sequence. A typical flow looks like:

1. Extract with SchemaTongs (onboarding or re-baselining)
2. Edit the schema package files directly (day-to-day development)
3. Deploy to a test database with SchemaQuench
4. Review in SchemaHammer or via git diff
5. Deploy to production with SchemaQuench

Once your schema is in files, most daily work is editing JSON and SQL directly — you do not re-extract every time.

## The deployment model

SchemaQuench follows a clear sequence when deploying a schema package:

1. **Read the declared state** — parse every JSON table definition and SQL file in the schema package.
2. **Query the current state** — inspect the target database's actual tables, columns, indexes, keys, and objects.
3. **Compute the delta** — determine what needs to be created, altered, or dropped to make the database match the declaration.
4. **Apply changes in execution slots** — run the computed changes in a controlled order.

The execution slots give you precise control over ordering when it matters. Within a single template, changes execute in this sequence:

1. Before migration scripts
2. Programmable objects (schemas, types, functions, views, procedures)
3. Table structure changes (columns, indexes)
4. Between-tables-and-keys migration scripts
5. Foreign keys and constraints
6. After-tables migration scripts
7. After-tables objects (triggers, DDL triggers)
8. Table data (MERGE scripts)
9. After migration scripts

Most of the time you do not think about slots — tables and objects just deploy correctly. The migration script slots exist for cases where you need to run something at a specific point in the sequence, like populating data before a NOT NULL constraint takes effect.

For the full deployment flow, execution slot details, and configuration options, see [SchemaQuench](../reference/schemaquench.md).

## JSON table definitions

Table definitions are where you will spend most of your editing time. Here is an actual table from the Northwind demo — `dbo.Products`:

```json
{
  "Schema": "[dbo]",
  "Name": "[Products]",
  "CompressionType": "NONE",
  "Columns": [
    {
      "Name": "[ProductID]",
      "DataType": "INT IDENTITY(1, 1)",
      "Nullable": false
    },
    {
      "Name": "[ProductName]",
      "DataType": "NVARCHAR(40)",
      "Nullable": false
    },
    {
      "Name": "[CategoryID]",
      "DataType": "INT",
      "Nullable": true
    },
    {
      "Name": "[UnitPrice]",
      "DataType": "MONEY",
      "Nullable": true,
      "Default": "0",
      "CheckExpression": "[UnitPrice]>=(0)"
    },
    {
      "Name": "[Discontinued]",
      "DataType": "BIT",
      "Nullable": false,
      "Default": "0"
    }
  ],
  "Indexes": [
    {
      "Name": "[PK_Products]",
      "PrimaryKey": true,
      "Unique": true,
      "Clustered": true,
      "IndexColumns": "[ProductID]"
    },
    {
      "Name": "[CategoriesProducts]",
      "PrimaryKey": false,
      "Unique": false,
      "Clustered": false,
      "IndexColumns": "[CategoryID]"
    }
  ],
  "ForeignKeys": [
    {
      "Name": "[FK_Products_Categories]",
      "Columns": "[CategoryID]",
      "RelatedTableSchema": "[dbo]",
      "RelatedTable": "[Categories]",
      "RelatedColumns": "[CategoryID]",
      "DeleteAction": "NO ACTION",
      "UpdateAction": "NO ACTION"
    },
    {
      "Name": "[FK_Products_Suppliers]",
      "Columns": "[SupplierID]",
      "RelatedTableSchema": "[dbo]",
      "RelatedTable": "[Suppliers]",
      "RelatedColumns": "[SupplierID]",
      "DeleteAction": "NO ACTION",
      "UpdateAction": "NO ACTION"
    }
  ]
}
```

*(Some columns and default properties trimmed for readability. SchemaTongs extracts the full definition including every property.)*

Reading this top to bottom:

- **Schema and Name** identify the table. Names use SQL Server bracket notation.
- **Columns** list every column with its data type, nullability, defaults, and check constraints. `"DataType": "INT IDENTITY(1, 1)"` means an auto-incrementing integer. `"Default": "0"` means the column gets a default constraint. `"CheckExpression"` adds a CHECK constraint inline with the column.
- **Indexes** define the primary key and any secondary indexes. The primary key is a clustered unique index named `PK_Products` on `ProductID`. Additional indexes cover the foreign key columns.
- **ForeignKeys** define relationships to other tables. Each entry names the constraint, the local column(s), the related table, and the referential actions.

If you have used SSMS to view a table's properties, this is the same information — columns tab, indexes, foreign keys — expressed as a single file you can diff, review in a pull request, and merge without conflicts.

Why JSON instead of SQL DDL? Three reasons:

1. **Diffable.** Adding a column is a clean diff — one new object in the Columns array. In DDL, you would see an entirely rewritten CREATE TABLE or an ALTER statement that does not show context.
2. **Mergeable.** Two developers adding different columns to the same table produce a clean git merge in JSON. In SQL, they produce a conflict.
3. **Machine-readable.** SchemaQuench parses the JSON to compute precise deltas. Parsing arbitrary DDL reliably is much harder.

For the complete field reference covering every column, index, and constraint property, see [Schema Packages — JSON Table Format](../reference/schema-packages.md#json-table-format).

---

Now that you understand the model, let's look at how this plays out in daily development. [Day-to-Day Workflows](04-day-to-day-workflows.md)
