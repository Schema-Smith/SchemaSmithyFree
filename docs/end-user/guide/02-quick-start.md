# Quick Start

In the next 15 minutes, you will extract a real database into version-controlled files, browse those files visually, deploy them to a fresh empty database, make a schema change, and watch SchemaSmith compute the exact ALTER script to bring the target in line. By the end, you will have completed the full SchemaSmith cycle and seen what state-based schema management feels like in practice.

## Prerequisites

You need three things:

**SchemaSmith tools.** Download self-contained ZIP packages from the [latest GitHub release](https://github.com/Schema-Smith/SchemaSmithyFree/releases/latest) and extract them anywhere on your PATH. No .NET runtime required. On Windows, you can also install via Chocolatey:

```bash
choco install schemaquench-dotnetcore10
choco install schematongs-dotnetcore10
choco install datatongs-dotnetcore10
```

SchemaHammer is available from the same GitHub release as a standalone desktop app.

**SQL Server.** Either an existing instance or Docker (recommended). The demo includes a Docker Compose file that starts SQL Server for you.

**The demo files.** Clone the repository or download just the `demo/` folder:

```bash
git clone https://github.com/Schema-Smith/SchemaSmithyFree.git
cd SchemaSmithyFree
```

## Step 1: Start the Demo Environment

From the repository root, start the demo Docker environment:

```bash
docker compose -f demo/docker-compose.yml up -d
```

This starts a SQL Server instance on port 1450, then deploys the Northwind and AdventureWorks demo databases using SchemaQuench. The credentials are in `demo/.env`:

| Setting  | Value |
|----------|-------|
| Server   | `localhost,1450` |
| User     | `TestUser` |
| Password | `aCa2d805-41E5@40c4!98e7#92F93zzxo176` |

Wait for the setup to finish -- you can watch progress with `docker compose -f demo/docker-compose.yml logs -f`. When you see the `completed` service exit, both databases are ready.

## Step 2: Extract with SchemaTongs

Now let's go the other direction. Pretend you have a database and want to bring it under SchemaSmith management. Create a SchemaTongs configuration file called `tongs-extract.json`:

```json
{
    "Source": {
        "Server": "localhost,1450",
        "User": "TestUser",
        "Password": "aCa2d805-41E5@40c4!98e7#92F93zzxo176",
        "Database": "Northwind"
    },
    "Product": {
        "Path": "./my-northwind",
        "Name": "Northwind"
    },
    "Template": {
        "Name": "Northwind"
    },
    "ShouldCast": {
        "Tables": true,
        "Schemas": true,
        "UserDefinedTypes": true,
        "UserDefinedFunctions": true,
        "Views": true,
        "StoredProcedures": true,
        "TableTriggers": true,
        "Catalogs": true,
        "StopLists": true,
        "DDLTriggers": true,
        "XMLSchemaCollections": true,
        "ScriptDynamicDependencyRemovalForFunctions": false,
        "ObjectList": ""
    }
}
```

Run the extraction:

```bash
SchemaTongs --ConfigFile:tongs-extract.json
```

Look at what appeared in the `my-northwind/` folder. You now have a complete schema package:

```
my-northwind/
  Product.json
  Templates/
    Northwind/
      Template.json
      Tables/
        dbo.Categories.json
        dbo.Customers.json
        dbo.Employees.json
        dbo.Orders.json
        dbo.Products.json
        dbo.Shippers.json
        ... (13 tables total)
      Views/
        dbo.Alphabetical list of products.sql
        dbo.Products by Category.sql
        ... (16 views)
      Procedures/
        dbo.CustOrderHist.sql
        dbo.SalesByCategory.sql
        ... (7 procedures)
      Functions/
      Triggers/
      Schemas/
```

Every table is a JSON file describing its columns, indexes, and constraints. Every stored procedure and view is a plain SQL file. This is your database, materialized as readable, diffable, reviewable source files. Commit this to Git and you have a complete history of every schema change from this point forward. For the full set of extraction options, see the [SchemaTongs Reference](../reference/schematongs.md).

## Step 3: Explore with SchemaHammer

Launch SchemaHammer and point it at the demo product:

```bash
SchemaHammer demo/Northwind
```

Or launch SchemaHammer without arguments and use **File > Choose Product** to select `demo/Northwind/Product.json`.

The left panel shows the product tree: templates, tables, views, procedures, and scripts organized exactly as they appear on disk. Click on `dbo.Categories` under Tables -- the right panel shows the table's columns (CategoryID, CategoryName, Description, Picture) and its indexes. Click on a stored procedure like `dbo.CustOrderHist` and you see the full SQL definition with syntax highlighting.

This is your schema browser. No server connection needed -- SchemaHammer reads the files directly. Take a minute to click around. Every object in the Northwind database is here, structured and browsable. For everything SchemaHammer can do, see the [SchemaHammer Reference](../reference/schemahammer.md).

## Step 4: Deploy with SchemaQuench

Here is where it gets powerful. Let's deploy the Northwind schema to a completely empty database on the same server. Create a SchemaQuench configuration file called `quench-deploy.json`:

```json
{
    "Target": {
        "Server": "localhost,1450",
        "User": "TestUser",
        "Password": "aCa2d805-41E5@40c4!98e7#92F93zzxo176"
    },
    "WhatIfONLY": false,
    "SchemaPackagePath": "./demo/Northwind",
    "ScriptTokens": {
        "NorthwindDb": "NorthwindClone"
    }
}
```

Notice the `ScriptTokens` section: we are telling SchemaQuench to use `NorthwindClone` as the database name instead of `Northwind`. The `{{NorthwindDb}}` token in the template's scripts will resolve to this value, creating a brand-new database. You will learn more about tokens in [Core Concepts](03-core-concepts.md), and the full token system is documented in the [Script Tokens Reference](../reference/script-tokens.md).

Run the deployment:

```bash
SchemaQuench --ConfigFile:quench-deploy.json
```

SchemaQuench reads the schema package, connects to the target server, and builds the entire database from scratch: creates `NorthwindClone`, runs migration scripts, creates all 13 tables with their columns and indexes, deploys all views and stored procedures. A complete, reproducible database from source files. Connect to `localhost,1450` with any SQL client and you will find `NorthwindClone` with the full Northwind schema.

This is the core promise: your schema package is a single source of truth, and SchemaQuench can make any target match it.

## Step 5: Make a Change

Now for the real magic. Let's modify the schema and watch SchemaSmith figure out exactly what needs to change.

Open `demo/Northwind/Templates/Northwind/Tables/dbo.Shippers.json`. Here is what it looks like right now:

```json
{
  "Schema": "[dbo]",
  "Name": "[Shippers]",
  "CompressionType": "NONE",
  "OldName": "",
  "Columns": [
    {
      "Name": "[CompanyName]",
      "DataType": "NVARCHAR(40)",
      "Nullable": false,
      "Persisted": false,
      "Sparse": false,
      "Collation": "",
      "DataMaskFunction": "",
      "OldName": ""
    },
    {
      "Name": "[Phone]",
      "DataType": "NVARCHAR(24)",
      "Nullable": true,
      "Persisted": false,
      "Sparse": false,
      "Collation": "",
      "DataMaskFunction": "",
      "OldName": ""
    },
    {
      "Name": "[ShipperID]",
      "DataType": "INT IDENTITY(1, 1)",
      "Nullable": false,
      "Persisted": false,
      "Sparse": false,
      "Collation": "",
      "DataMaskFunction": "",
      "OldName": ""
    }
  ],
  "Indexes": [
    {
      "Name": "[PK_Shippers]",
      "CompressionType": "NONE",
      "PrimaryKey": true,
      "Unique": true,
      "UniqueConstraint": false,
      "Clustered": true,
      "ColumnStore": false,
      "FillFactor": 0,
      "IndexColumns": "[ShipperID]"
    }
  ]
}
```

Add a new column for tracking email addresses. Insert this after the `[Phone]` column entry:

```json
    {
      "Name": "[Email]",
      "DataType": "NVARCHAR(100)",
      "Nullable": true,
      "Persisted": false,
      "Sparse": false,
      "Collation": "",
      "DataMaskFunction": "",
      "OldName": ""
    },
```

You just declared the desired state: "the Shippers table should have an Email column." You did not write an ALTER TABLE script. You did not figure out whether the column already exists. You described what the table should look like.

Now let's see what SchemaSmith will do -- without actually touching the database. Run SchemaQuench in WhatIf mode:

```json
{
    "Target": {
        "Server": "localhost,1450",
        "User": "TestUser",
        "Password": "aCa2d805-41E5@40c4!98e7#92F93zzxo176"
    },
    "WhatIfONLY": true,
    "SchemaPackagePath": "./demo/Northwind",
    "ScriptTokens": {
        "NorthwindDb": "Northwind"
    }
}
```

Save this as `quench-whatif.json` and run:

```bash
SchemaQuench --ConfigFile:quench-whatif.json
```

In the output, you will see `[WhatIf]` entries showing the computed changes. SchemaQuench compared the declared state (your JSON with the new Email column) against the live database (which has no Email column) and determined that an ALTER TABLE ADD is needed. No changes were applied -- WhatIf mode is read-only.

Now apply it for real. Change `"WhatIfONLY": true` to `"WhatIfONLY": false` (or use the `quench-deploy.json` from Step 4 with `"NorthwindDb": "Northwind"`) and run again:

```bash
SchemaQuench --ConfigFile:quench-deploy.json
```

SchemaQuench connects to the Northwind database, sees that `dbo.Shippers` is missing the `[Email]` column, and adds it. Every other table, view, and procedure is already in sync, so nothing else changes. Exactly the right delta, computed automatically.

## Step 6: See It in SchemaHammer

Open SchemaHammer again (or press **F5** if it is still open to reload the tree):

```bash
SchemaHammer demo/Northwind
```

Navigate to `dbo.Shippers` under Tables. There it is: the `[Email]` column, right where you declared it. The file is the truth, the database matches, and SchemaHammer shows you exactly what the truth says.

## The Cycle Is Complete

You just completed the full SchemaSmith workflow:

1. **Extract** -- SchemaTongs pulled a live database into structured, version-controlled files
2. **Browse** -- SchemaHammer let you visually explore every object without a server connection
3. **Deploy** -- SchemaQuench built a complete database from those files, reproducibly
4. **Change** -- You edited a JSON file to declare a new column
5. **Preview** -- WhatIf mode showed you the computed ALTER without touching the database
6. **Apply** -- SchemaQuench made the target match the declared state, changing only what needed to change

No migration scripts. No ordered chains of ALTERs. No guessing what the target looks like. You declare the state you want, and SchemaSmith gets you there.

## What's Next

You just did state-based schema management. Now let's understand what is happening under the hood -- products, templates, tokens, and the deployment pipeline that makes it all work.

Next: [Core Concepts](03-core-concepts.md)
