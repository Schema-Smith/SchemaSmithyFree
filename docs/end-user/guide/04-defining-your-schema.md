# Defining Your Schema

You understand the [core concepts](03-core-concepts.md). Now it's time to give your database form. This chapter covers the workflows you'll reach for every day — adding tables, shaping columns, writing stored procedures, casting changes from live databases, and bootstrapping new environments from scratch. Each one replaces a manual, error-prone process with something you can trust. And they build on each other naturally.

## Adding a table

Your team needs a `Promotions` table to track discount campaigns. Here's what you do.

**1. Create the JSON file.** Add `dbo.Promotions.json` to your package's `Tables/` folder:

```json
{
  "Schema": "[dbo]",
  "Name": "[Promotions]",
  "CompressionType": "NONE",
  "Columns": [
    {
      "Name": "[PromotionID]",
      "DataType": "INT IDENTITY(1, 1)",
      "Nullable": false
    },
    {
      "Name": "[PromotionName]",
      "DataType": "NVARCHAR(100)",
      "Nullable": false
    },
    {
      "Name": "[DiscountPercent]",
      "DataType": "DECIMAL(5,2)",
      "Nullable": false,
      "Default": "0",
      "CheckExpression": "[DiscountPercent]>=(0) AND [DiscountPercent]<=(100)"
    },
    {
      "Name": "[StartDate]",
      "DataType": "DATE",
      "Nullable": false
    },
    {
      "Name": "[EndDate]",
      "DataType": "DATE",
      "Nullable": true
    },
    {
      "Name": "[IsActive]",
      "DataType": "BIT",
      "Nullable": false,
      "Default": "1"
    }
  ],
  "Indexes": [
    {
      "Name": "[PK_Promotions]",
      "PrimaryKey": true,
      "Unique": true,
      "Clustered": true,
      "IndexColumns": "[PromotionID]"
    },
    {
      "Name": "[IX_Promotions_StartDate]",
      "IndexColumns": "[StartDate]"
    }
  ]
}
```

That's the entire table definition. Every column, every constraint, every index — all in one readable file. For every property available in a table JSON file, see the [Schema Packages Reference](../reference/schema-packages.md#json-table-format).

**2. Quench it.** Run SchemaQuench against your development database:

```bash
SchemaQuench
```

SchemaQuench reads the JSON, sees that `dbo.Promotions` doesn't exist in the target database, and generates a `CREATE TABLE` statement. One file. One command. Done.

**Compare this to the traditional approach:** write a `CREATE TABLE` script, write a migration file with a sequence number, make sure the sequence number doesn't collide with anyone else's, add an `IF NOT EXISTS` guard, add a corresponding rollback script, update a migrations tracking table. With SchemaSmith, you created one file and ran one command. No migration scripts. No dependency ordering. No collision worries.

## Modifying a table

The `Promotions` table needs changes. Marketing wants a description field, the discount column needs more precision, and you need an index on the active flag for a dashboard query. All three edits happen in the same JSON file — you shape the table right where it lives.

**Add a column.** Insert a new entry in the `Columns` array:

```json
{
  "Name": "[Description]",
  "DataType": "NVARCHAR(500)",
  "Nullable": true
}
```

**Change a data type.** Find the `DiscountPercent` column and edit its `DataType`:

```json
"DataType": "DECIMAL(7,4)"
```

**Add an index.** Add a new entry in the `Indexes` array:

```json
{
  "Name": "[IX_Promotions_IsActive]",
  "IndexColumns": "[IsActive]",
  "FilterExpression": "[IsActive] = 1"
}
```

Now preview the changes before touching the database. Run SchemaQuench in WhatIf mode:

```bash
SmithySettings_WhatIfONLY=true SchemaQuench
```

SchemaQuench generates the SQL it *would* execute — an `ALTER TABLE ... ADD` for the new column, an `ALTER TABLE ... ALTER COLUMN` for the data type change, and a `CREATE INDEX` for the new filtered index — and logs it all without applying anything. Read the generated SQL, confirm it looks right, then run SchemaQuench normally to quench the changes into your database.

Three changes to one file. One preview. One command. No scripts to write, number, or maintain.

## Adding and updating programmable objects

Stored procedures, functions, views, and triggers work differently from tables. Instead of JSON, they're plain `.sql` files. Each object gets its own file in the matching folder:

| Object type | Folder |
|---|---|
| Stored procedures | `Procedures/` |
| Functions | `Functions/` |
| Views | `Views/` |
| Triggers | `Triggers/` |

Here's a stored procedure that returns the order history for a customer. Create `dbo.CustOrderHist.sql` in the `Procedures/` folder:

```sql
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE CustOrderHist @CustomerID nchar(5)
AS

SELECT ProductName, Total=SUM(Quantity)
FROM Products P, [Order Details] OD, Orders O, Customers C
WHERE C.CustomerID = @CustomerID
AND C.CustomerID = O.CustomerID AND O.OrderID = OD.OrderID AND OD.ProductID = P.ProductID
GROUP BY ProductName

GO
```

The key detail: `CREATE OR ALTER`. This is idempotent. It works whether the procedure exists or not. No `IF EXISTS ... DROP` guard. No separate create-vs-alter logic. SchemaQuench runs the script as-is, and SQL Server handles the rest. For the full list of object types and their folder locations, see the [Schema Packages Reference](../reference/schema-packages.md#complete-folder-structure).

Views work the same way. Here's `dbo.Products Above Average Price.sql` in the `Views/` folder:

```sql
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW "Products Above Average Price" AS

SELECT Products.ProductName, Products.UnitPrice
FROM Products
WHERE Products.UnitPrice > (SELECT AVG(UnitPrice) FROM Products)

GO
```

Need to update an existing procedure? Edit the `.sql` file and quench. The `CREATE OR ALTER` takes care of whether it's new or changed. There's no separate "alter" workflow — you always declare the full object definition, and SchemaQuench applies it.

## Extracting changes from a live database

Someone changed the database directly. Maybe a DBA added a column in production during an incident. Maybe a developer used SSMS to tweak an index on staging. The database has drifted from the package.

Cast the current state back into your package with SchemaTongs:

```bash
SchemaTongs
```

SchemaTongs connects to the database, reads every table, procedure, view, function, and trigger, and writes the current definitions to the package files. Changed objects update in place. New objects get new files. For the full extraction configuration, including filtering by object type and partial extraction, see the [SchemaTongs Reference](../reference/schematongs.md).

Now the power of files shows up. Run `git diff`:

```
$ git diff
--- a/Templates/Northwind/Tables/dbo.Products.json
+++ b/Templates/Northwind/Tables/dbo.Products.json
@@ -48,6 +48,12 @@
     },
+    {
+      "Name": "[BackorderThreshold]",
+      "DataType": "INT",
+      "Nullable": true,
+      "Default": "10"
+    },
```

The diff reads like a sentence: "someone added a BackorderThreshold column to the Products table with a default of 10." Compare that to trying to figure out what changed by comparing two database snapshots or reading through audit logs. The drift is captured. The mystery is over.

## Extraction intelligence

SchemaTongs does more than dump scripts to flat folders. When you cast your database schema, the tool brings real intelligence to the extraction.

**Subfolder preservation.** You can organize scripts by domain — `Tables/Sales/`, `Tables/HR/`, `Procedures/Reporting/`. When SchemaTongs casts, it preserves existing subfolder locations. If `dbo.Orders.json` already lives in `Tables/Sales/`, the next extraction updates it in place rather than creating a duplicate in the root `Tables/` folder. New objects that haven't been organized yet go to the root folder. Your organization stays intact.

**Orphan detection.** When a database object is dropped, its script file becomes an orphan. SchemaTongs offers three modes for handling this:

| Mode | Behavior |
|---|---|
| `Detect` | Logs orphaned files but takes no action. This is the default. |
| `DetectWithCleanupScripts` | Logs orphans and generates cleanup scripts you can review and apply. |
| `DetectDeleteAndCleanup` | Deletes orphaned files and generates cleanup scripts automatically. |

**Script validation.** With `ValidateScripts` enabled, SchemaTongs checks each extracted script against the database to verify it parses correctly. Invalid scripts are saved with a `.sqlerror` extension instead of `.sql`, making them visible but excluded from deployment until you fix them.

**CheckConstraintStyle.** Controls whether check constraints are extracted as column-level properties (inside the table JSON) or as table-level constraints. The default is `ColumnLevel`. If you prefer `TableLevel`, set it in Product.json or the SchemaTongs config — but be consistent, because the style is locked to whatever Product.json specifies once the product exists.

For the full set of extraction options, filtering, and configuration, see [SchemaTongs Reference](../reference/schematongs.md).

## The Initialize template pattern

Some products need to create their target database from scratch. CI pipelines spin up fresh containers. Docker Compose environments start from nothing. New developers clone the repo and need a working database in one command. The Initialize template pattern handles all of these.

Three pieces work together. Here's how the Northwind demo product sets it up.

**1. The Initialize template identifies itself out of the deployment.** In `Templates/Initialize/Template.json`:

```json
{
  "Name": "Initialize",
  "DatabaseIdentificationScript": "SELECT [Name] FROM master.sys.databases WHERE [Name] = 'TestMain' AND NOT EXISTS (SELECT 1 FROM master.sys.databases WHERE [Name] = '{{NorthwindDb}}')"
}
```

The `DatabaseIdentificationScript` is the key. It returns a result only when the target database doesn't yet exist — it matches `TestMain` (a database that always exists on the server) but only when `NorthwindDb` is missing. On the first run, this template activates and creates the database. On every subsequent run, the script returns no rows, SchemaQuench skips the template entirely, and deployment proceeds straight to the main template.

**2. A migration script creates the database idempotently.** In `Templates/Initialize/MigrationScripts/Before/Create Northwind [ALWAYS].sql`:

```sql
IF NOT EXISTS (SELECT 1 FROM master.sys.databases WHERE [Name] = '{{NorthwindDb}}')
BEGIN
    CREATE DATABASE [{{NorthwindDb}}]
END
```

The `[ALWAYS]` marker tells SchemaQuench to run this script every time the Initialize template is active — no version tracking needed. The `IF NOT EXISTS` guard makes the script safe to re-run, though in practice it only executes once because the template self-selects out after the database exists.

**3. Product.json defines the template order.** In `Product.json`:

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

`TemplateOrder` ensures Initialize runs first. If the database doesn't exist, Initialize creates it, then the Northwind template deploys the full schema. If the database already exists, Initialize is skipped and Northwind deploys any pending changes.

Both demo products — Northwind and AdventureWorks — use this exact pattern. One `docker compose up` bootstraps everything from an empty SQL Server. Subsequent runs skip Initialize automatically and apply only schema changes. Fresh environment or existing environment, same command, same result.

---

That's how you shape your schema — adding tables, modifying columns, writing procedures, casting changes, and bootstrapping new environments. Clean, repeatable, no surprises. When you're ready to bring your team into the process, the next chapter shows how schema-as-files transforms collaboration. [Working with Your Team](05-working-with-your-team.md)
