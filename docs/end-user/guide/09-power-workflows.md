# Power Workflows

You've built your schema, validated it, and set up your deployment pipeline. Now it's time to handle the real-world complexity that every production system eventually demands. Script tokens let a single codebase span dev, staging, and production without manual edits. Multi-database products keep related databases in lockstep with a single deployment. DataTongs turns reference data into version-controlled, repeatable scripts. And execution slots give you precise control over exactly when each script runs in the deployment lifecycle. These are the features that let you deploy with confidence at scale — solving problems that would otherwise require manual intervention or fragile scripting.

## Script tokens

Your product deploys to dev, staging, and production. One of your stored procedures needs to query a linked reporting database — but the database has a different name in each environment. Hardcoding the name means maintaining three copies of the script. Script tokens solve this cleanly.

**Step 1: Define the token in Product.json.**

```json
{
  "Name": "MyProduct",
  "ScriptTokens": {
    "ReportingDB": "ReportingDev",
    "ReleaseVersion": "2.1.0"
  }
}
```

**Step 2: Use the token in your SQL scripts.**

Anywhere in your SQL files, reference tokens with double-brace syntax:

```sql
CREATE OR ALTER PROCEDURE [dbo].[GetSalesReport]
AS
BEGIN
    SELECT s.OrderDate, s.Total, r.RegionName
      FROM [dbo].[Sales] s
      JOIN [{{ReportingDB}}].[dbo].[Regions] r ON s.RegionID = r.RegionID
END
```

**Step 3: Override per template (optional).**

If one template in a multi-database product needs a different value, define the same token in that template's `Template.json`. Template-level tokens take priority over product-level tokens:

```json
{
  "Name": "AuditDB",
  "DatabaseIdentificationScript": "SELECT [name] FROM master.dbo.sysdatabases WHERE [name] = '{{AuditDatabase}}'",
  "ScriptTokens": {
    "ReportingDB": "AuditReporting"
  }
}
```

**Step 4: Override per environment via environment variables.**

For deployment to staging or production, override any token without touching the config files:

```
SmithySettings_ScriptTokens__ReportingDB=ProdReporting
```

The precedence chain is: environment variable > template > product. So when you quench to production with this variable set, the deployed script becomes:

```sql
      JOIN [ProdReporting].[dbo].[Regions] r ON s.RegionID = r.RegionID
```

One script. Three environments. Zero manual edits between them.

Two tokens are always available automatically: `{{ProductName}}` (from `Product.json` Name) and `{{TemplateName}}` (from `Template.json` Name). Token replacement is case-insensitive.

For the full list of behaviors and edge cases, see [Script Tokens Reference](../reference/script-tokens.md).

## Multi-database products

Some applications span more than one database — a main transactional database plus a reporting database, or a primary database plus an audit log. SchemaSmith handles this as a single product with multiple templates. One quench, all databases updated.

**Product.json defines the deployment order:**

```json
{
  "Name": "MyProduct",
  "TemplateOrder": [
    "Main",
    "Reporting"
  ],
  "ScriptTokens": {
    "MainDB": "AppMain",
    "ReportingDB": "AppReporting"
  },
  "ValidationScript": "SELECT CAST(CASE WHEN EXISTS(SELECT * FROM master.dbo.sysdatabases WHERE [name] = '{{MainDB}}') THEN 1 ELSE 0 END AS BIT)"
}
```

**Each template identifies its own target database:**

`Templates/Main/Template.json`:
```json
{
  "Name": "Main",
  "DatabaseIdentificationScript": "SELECT [name] FROM master.dbo.sysdatabases WHERE [name] = '{{MainDB}}'"
}
```

`Templates/Reporting/Template.json`:
```json
{
  "Name": "Reporting",
  "DatabaseIdentificationScript": "SELECT [name] FROM master.dbo.sysdatabases WHERE [name] = '{{ReportingDB}}'"
}
```

Each template has its own complete folder structure — `Tables/`, `Procedures/`, `MigrationScripts/`, and so on. SchemaQuench quenches them in the order specified by `TemplateOrder`. One run, both databases updated, all in a single atomic operation from the pipeline's perspective.

For the full package structure, see [Schema Packages Reference](../reference/schema-packages.md).

## Reference data management with DataTongs

Lookup tables — countries, status codes, permission types — need to be consistent across all environments. DataTongs grips your reference data at the source and extracts it into idempotent MERGE scripts that become part of your schema package.

**The workflow:**

1. Configure DataTongs to point at your source database and list the tables to extract.
2. Run DataTongs. It generates one MERGE script per table.
3. Place the generated scripts in your template's `Table Data/` folder.
4. SchemaQuench automatically deploys them during the Table Data execution slot — after tables and keys are in place, before triggers.

**Example DataTongs configuration:**

```json
{
  "Source": {
    "Server": "production-server",
    "Database": "AppMain"
  },
  "OutputPath": "./Templates/Main/Table Data",
  "ShouldCast": {
    "MergeUpdate": true,
    "MergeDelete": true,
    "DisableTriggers": false
  },
  "Tables": [
    { "Name": "dbo.Countries" },
    { "Name": "dbo.StatusCodes" },
    { "Name": "dbo.PermissionTypes", "KeyColumns": "PermissionTypeID", "Filter": "IsActive = 1" }
  ]
}
```

The generated MERGE scripts handle inserts, updates, and deletes. They use JSON-based data embedding, so the scripts are self-contained SQL files with no external dependencies.

**The golden source pattern:** Extract from production (the source of truth), commit the generated scripts to your repository, and quench to all other environments via SchemaQuench. Every environment gets exactly the same reference data. Changes are tracked in version control like any other schema change. No more "staging has stale lookup data" surprises.

For configuration details including key column detection, nullable key handling, and filter options, see [DataTongs Reference](../reference/datatongs.md).

## Script folder execution order

SchemaQuench quenches scripts in a precise order. Understanding the execution slots lets you place scripts exactly where they need to run in the deployment lifecycle. You decide where each script belongs; SchemaQuench handles the sequencing.

**Product-level slots (run once per deployment):**

| Order | Folder | Purpose |
|---|---|---|
| First | `Before Product/` | Runs before any template deploys. Server-level setup, cross-database preparation. |
| Last | `After Product/` | Runs after all templates complete. Cross-database foreign keys, final verification. |

**Template-level slots (run once per template, in TemplateOrder sequence):**

| Order | Folder | Purpose |
|---|---|---|
| 1 | `Schemas/` through `Procedures/` | Programmable objects with dependency retry (schemas, types, catalogs, stop lists, XML collections, functions, views, procedures) |
| 2 | _(Table creation)_ | New tables and columns created from table JSON |
| 3 | `MigrationScripts/Before/` | Pre-modification migration: data preparation before columns are altered or dropped |
| 4 | _(Table modification)_ | Existing tables altered to match JSON (column changes, index updates) |
| 5 | `MigrationScripts/BetweenTablesAndKeys/` | Runs after table structure is final but before FK constraints. Data transformations that need the new column structure. |
| 6 | _(Indexes and constraints)_ | Missing indexes, check constraints, default constraints, statistics |
| 7 | `MigrationScripts/AfterTablesScripts/` | Runs after structure and indexes, before triggers. Post-structure cleanup. |
| 8 | `Triggers/` and `DDLTriggers/` | Table triggers and DDL triggers with dependency retry |
| 9 | `Table Data/` | Reference data MERGE scripts (from DataTongs or hand-written) |
| 10 | _(Foreign keys)_ | Foreign key constraints applied after all data is in place |
| 11 | _(Indexed views)_ | Indexed views created or updated |
| 12 | `MigrationScripts/After/` | Post-migration: final data fixes, cleanup, verification |

**Why BetweenTablesAndKeys exists:** When you split a table or restructure foreign keys, you need a window after the new tables are created but before SchemaSmith tries to create the foreign keys. This slot gives you that window to move data into the new structure so the FK constraints succeed.

**Why AfterTablesScripts exists:** Sometimes you need to run scripts after indexes and constraints are in place but before triggers are enabled. Data backfills that must satisfy constraints but shouldn't fire triggers go here.

**When to use Product Before/After:** These run outside any template context — they execute against the server, not a specific database. Use them for cross-database operations, linked server setup, or anything that spans multiple templates.

For the complete deployment flow including table and indexed view processing within each slot, see [SchemaQuench Reference](../reference/schemaquench.md#execution-slots).

---

Tokens, multi-database, reference data, execution slots. That's the toolkit for 95% of what you'll encounter. For the remaining 5% — the escape hatches — the next chapter has you covered. [Edge Cases & Escape Hatches](10-edge-cases.md)
