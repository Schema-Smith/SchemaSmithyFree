# Day-to-Day Workflows

You understand the concepts. Now let's look at how your Tuesday actually goes with SchemaSmith. These are the patterns you will use over and over — adding tables, modifying columns, writing stored procedures, syncing with live databases, and reviewing changes in pull requests. Each one is simpler than the traditional alternative, and they build on each other naturally.

## Adding a table

Your team needs a `Promotions` table to track discount campaigns. Here is what you do.

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

That is the entire table definition. Every column, every constraint, every index — all in one readable file.

**2. Deploy it.** Run SchemaQuench against your development database:

```bash
SchemaQuench
```

SchemaQuench reads the JSON, sees that `dbo.Promotions` does not exist in the target database, and generates a `CREATE TABLE` statement. Done.

**Compare this to the traditional approach:** write a `CREATE TABLE` script, write a migration file with a sequence number, make sure the sequence number does not collide with anyone else's, add an `IF NOT EXISTS` guard, add a corresponding rollback script, update a migrations tracking table. With SchemaSmith, you created one file and ran one command.

## Modifying a table

The `Promotions` table needs changes. Marketing wants a description field, the discount column needs more precision, and you need an index on the active flag for a dashboard query. All three edits happen in the same JSON file.

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

SchemaQuench generates the SQL it *would* execute — an `ALTER TABLE ... ADD` for the new column, an `ALTER TABLE ... ALTER COLUMN` for the data type change, and a `CREATE INDEX` for the new filtered index — and logs it all without applying anything. Read the generated SQL, confirm it looks right, then run SchemaQuench normally to apply.

Three changes to one file. One preview. One deployment command. No migration scripts to write, number, or maintain.

## Adding and updating programmable objects

Stored procedures, functions, views, and triggers work differently from tables. Instead of JSON, they are plain `.sql` files. Each object gets its own file in the matching folder:

| Object type | Folder |
|---|---|
| Stored procedures | `Procedures/` |
| Functions | `Functions/` |
| Views | `Views/` |
| Triggers | `Triggers/` |

Here is a stored procedure that returns the order history for a customer. Create `dbo.CustOrderHist.sql` in the `Procedures/` folder:

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

The key detail: `CREATE OR ALTER`. This is idempotent. It works whether the procedure exists or not. No `IF EXISTS ... DROP` guard. No separate create-vs-alter logic. SchemaQuench runs the script as-is, and SQL Server handles the rest.

Views work the same way. Here is `dbo.Products Above Average Price.sql` in the `Views/` folder:

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

Need to update an existing procedure? Edit the `.sql` file and deploy. The `CREATE OR ALTER` takes care of whether it is new or changed. There is no separate "alter" workflow — you always declare the full object definition, and SchemaQuench applies it.

## Extracting changes from a live database

Someone changed the database directly. Maybe a DBA added a column in production during an incident. Maybe a developer used SSMS to tweak an index on staging. The database has drifted from the package.

Run SchemaTongs to pull the current state back into your package:

```bash
SchemaTongs
```

SchemaTongs connects to the database, reads every table, procedure, view, function, and trigger, and writes the current definitions to the package files. Changed objects update in place. New objects get new files.

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

The diff reads like a sentence: "someone added a BackorderThreshold column to the Products table with a default of 10." Compare that to trying to figure out what changed by comparing two database snapshots or reading through audit logs.

**Orphan detection** catches the other direction — objects that were dropped from the database but still have files in the package. SchemaTongs logs orphaned files so you can decide whether to remove them or restore the missing objects. For details on orphan handling modes, see [SchemaTongs Reference — Orphan Detection](../reference/schematongs.md#orphan-detection).

## Working with source control

Schema packages are files. That means they work with git exactly the way your application code does.

**Branching works naturally.** Create a feature branch, add your table, modify your procedures, commit, push. Another developer does the same on their branch. Git handles the merge.

**Pull requests become readable.** Here is what a PR diff looks like when someone adds a column to a table:

```diff
  "Columns": [
    ...
+   {
+     "Name": "[LoyaltyTier]",
+     "DataType": "NVARCHAR(20)",
+     "Nullable": true,
+     "Default": "'Standard'"
+   },
    ...
  ]
```

A reviewer sees immediately: "This adds a nullable LoyaltyTier column with a default of 'Standard'." No need to mentally execute an ALTER script to figure out the end state.

Compare that to reviewing a migration script:

```sql
ALTER TABLE [dbo].[Customers] ADD [LoyaltyTier] NVARCHAR(20) NULL
    CONSTRAINT [DF_Customers_LoyaltyTier] DEFAULT ('Standard');
```

The migration is less context. You see the change but not the table it lives in. Is this column next to related columns? Are there indexes that should cover it? You have to open the full table definition separately to know. With the JSON diff, the whole table is right there.

**Merge conflicts are simpler.** Two developers adding different columns to the same table? In JSON, they are adding entries to the `Columns` array — git auto-merges cleanly in most cases, and when it does conflict, the resolution is obvious (keep both entries). In migration scripts, two developers touching the same table means two separate ALTER scripts with sequence numbers that may collide, and the reviewer has to verify both scripts compose correctly.

This is where "just like source code" really shines. Your schema evolves in pull requests, with reviews, approvals, and a full history — exactly like your application code already does.

## Team collaboration patterns

Here is a typical workflow for a team using SchemaSmith:

1. **Developer** creates a feature branch and adds a `[LoyaltyPoints]` column to the Customers table JSON.
2. **Developer** adds a stored procedure `dbo.CalculateLoyaltyPoints.sql` in the `Procedures/` folder.
3. **Developer** runs SchemaQuench against their local database to verify the changes deploy cleanly.
4. **Developer** opens a pull request. The diff shows exactly one new column and one new procedure.
5. **DBA** reviews the table structure in the PR diff — or opens the package in SchemaHammer to browse the full table with its indexes and foreign keys side by side.
6. **Reviewer** approves. The branch merges.
7. **CI/CD** deploys the package to staging using SchemaQuench. Same package, same command.
8. **Release manager** deploys to production using SchemaQuench in WhatIf mode first, reviews the generated SQL, then runs the real deployment.

Nobody wrote a deployment script. Nobody maintained a migration chain. Nobody worried about whether staging and production are at the same migration version. The same package deploys everywhere, and SchemaQuench computes the right delta for each target.

## WhatIf mode as safety net

WhatIf mode is the preview button for your database. Run it before every deployment — especially production.

```bash
SmithySettings_WhatIfONLY=true SchemaQuench
```

SchemaQuench does everything it normally does — connects to the target database, computes the delta between the declared state and the current state, generates the SQL — but stops short of executing. The generated SQL is written to log files in the working directory so you can review every statement.

Build this into your workflow:

- **Development:** Optional. Deploy directly if you are comfortable.
- **Staging:** Recommended. Review the WhatIf output to catch surprises before they hit production-like data.
- **Production:** Non-negotiable. Always WhatIf first. Read every line of generated SQL. Then deploy.

The cost is one extra command. The benefit is never being surprised by what a deployment does to your production database.

For the full details on WhatIf behavior and output files, see [SchemaQuench Reference — WhatIf Mode](../reference/schemaquench.md#whatif-mode).

## Using SchemaHammer for review

SchemaHammer is the visual side of SchemaSmith. Open a product to browse its full structure — tables, columns, indexes, procedures, views — in a navigable tree.

During code review, SchemaHammer adds context that a raw diff cannot provide:

- **Browse the full table.** A PR diff shows the column you added. SchemaHammer shows the column in context with every other column, index, and foreign key on the table.
- **Code search across scripts.** Wondering which stored procedures reference the column you are about to rename? Use code search to find every reference across all procedures, functions, views, and triggers in the package.
- **Token preview.** Script tokens let you parameterize environment-specific values. SchemaHammer shows you what the resolved script looks like, so you can verify token substitution before deployment.

SchemaHammer is not required for any workflow — everything works from the command line. But when you want to understand the big picture or investigate cross-cutting changes, it is the fastest path to answers.

For the full feature set, see [SchemaHammer Reference](../reference/schemahammer.md).

---

These workflows cover most of what you will do day to day. When you are ready to tap into the more advanced capabilities, the next chapter has you covered. [Power Workflows](05-power-workflows.md)
