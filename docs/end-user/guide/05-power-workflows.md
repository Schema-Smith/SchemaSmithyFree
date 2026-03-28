# Power Workflows

You are comfortable with the [daily rhythm](04-day-to-day-workflows.md). Now here is where it gets really interesting. These features turn SchemaSmith from a convenient deployment tool into serious infrastructure — handling multi-environment deployments, multi-database products, reference data pipelines, and CI/CD integration. Each one solves a real problem that would otherwise require manual intervention or fragile scripting.

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

The precedence chain is: environment variable > template > product. So when you deploy to production with this variable set, the deployed script becomes:

```sql
      JOIN [ProdReporting].[dbo].[Regions] r ON s.RegionID = r.RegionID
```

Two tokens are always available automatically: `{{ProductName}}` (from `Product.json` Name) and `{{TemplateName}}` (from `Template.json` Name). Token replacement is case-insensitive.

For the full list of behaviors and edge cases, see [Script Tokens Reference](../reference/script-tokens.md).

## Multi-database products

Some applications span more than one database — a main transactional database plus a reporting database, or a primary database plus an audit log. SchemaSmith handles this as a single product with multiple templates.

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

Each template has its own complete folder structure — `Tables/`, `Procedures/`, `MigrationScripts/`, and so on. SchemaQuench deploys them in the order specified by `TemplateOrder`. One run, both databases updated, all in a single atomic operation from the pipeline's perspective.

For the full package structure, see [Schema Packages Reference](../reference/schema-packages.md).

## Reference data management with DataTongs

Lookup tables — countries, status codes, permission types — need to be consistent across all environments. DataTongs extracts reference data from a source database and generates idempotent MERGE scripts that become part of your schema package.

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

**The golden source pattern:** Extract from production (the source of truth), commit the generated scripts to your repository, and deploy to all other environments via SchemaQuench. Every environment gets exactly the same reference data. Changes are tracked in version control like any other schema change.

For configuration details including key column detection, nullable key handling, and filter options, see [DataTongs Reference](../reference/datatongs.md).

## CI/CD integration

SchemaSmith tools are self-contained executables. There is no SDK or runtime to install in your pipeline — just download and run.

**Configuration via environment variables** means no secrets in config files. Every setting from a `.settings.json` file can be overridden with an environment variable using the `SmithySettings_` prefix. Nested properties use double underscores:

| Setting file path | Environment variable |
|---|---|
| `Target:Server` | `SmithySettings_Target__Server` |
| `Target:Password` | `SmithySettings_Target__Password` |
| `ScriptTokens:ReportingDB` | `SmithySettings_ScriptTokens__ReportingDB` |
| `SchemaPackagePath` | `SmithySettings_SchemaPackagePath` |

**Conceptual pipeline stages:**

```
Pull Request Pipeline:
  1. Build/test application code
  2. SchemaQuench --WhatIf against a disposable database
     → Catches SQL errors, missing tokens, broken references
     → Fails the PR check if deployment would fail

Merge-to-Main Pipeline:
  1. Build/test application code
  2. SchemaQuench deploy to staging
  3. Run integration tests against staging
  4. SchemaQuench deploy to production (manual approval gate)
```

The WhatIf mode is particularly valuable in PR pipelines. It runs the full deployment logic — token replacement, validation scripts, object creation — against a real database, but rolls back instead of committing. You catch deployment failures before the code merges.

For the complete environment variable mapping and configuration precedence, see the [Configuration Reference](../reference/configuration.md#environment-variables). For SchemaQuench-specific deployment settings, see the [SchemaQuench Reference](../reference/schemaquench.md#configuration-reference).

## Validation scripts

The `ValidationScript` in Product.json runs before SchemaQuench deploys anything. It is a gate: if the script returns 0 or false, deployment stops. This prevents accidentally deploying to the wrong server or an unprepared environment.

**Common use cases:**

Verify the target database exists:
```sql
SELECT CAST(CASE WHEN EXISTS(
    SELECT * FROM master.dbo.sysdatabases WHERE [name] = '{{MainDB}}'
) THEN 1 ELSE 0 END AS BIT)
```

Verify a minimum SQL Server version:
```sql
SELECT CAST(CASE WHEN SERVERPROPERTY('ProductMajorVersion') >= 15
    THEN 1 ELSE 0 END AS BIT)
```

Confirm expected state before a migration:
```sql
SELECT CAST(CASE WHEN EXISTS(
    SELECT * FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'AppConfig' AND TABLE_SCHEMA = 'dbo'
) THEN 1 ELSE 0 END AS BIT)
```

The validation script supports token replacement, so you can use `{{ProductName}}`, `{{MainDB}}`, or any custom token. If validation fails, SchemaQuench logs the failure and exits without modifying anything.

Products also support `BaselineValidationScript`, which runs only during initial baseline deployments to verify the target is in the expected starting state.

## Script folder execution order

SchemaQuench deploys scripts in a precise order. Understanding the execution slots lets you place scripts exactly where they need to run in the deployment lifecycle.

**Product-level slots (run once per deployment):**

| Order | Folder | Purpose |
|---|---|---|
| First | `Before Product/` | Runs before any template deploys. Server-level setup, cross-database preparation. |
| Last | `After Product/` | Runs after all templates complete. Cross-database foreign keys, final verification. |

**Template-level slots (run once per template, in TemplateOrder sequence):**

| Order | Folder | Purpose |
|---|---|---|
| 1 | `MigrationScripts/Before/` | Pre-migration: rename columns, move data, prepare for schema changes |
| 2 | `Schemas/` | Database schemas (dbo, sales, hr) |
| 3 | `DataTypes/` | User-defined types |
| 4 | `FullTextCatalogs/` | Full-text catalog definitions |
| 5 | `FullTextStopLists/` | Full-text stop list definitions |
| 6 | `XMLSchemaCollections/` | XML schema collections |
| 7 | `Functions/` | User-defined functions |
| 8 | `Views/` | Views |
| 9 | `Procedures/` | Stored procedures |
| 10 | `MigrationScripts/BetweenTablesAndKeys/` | Runs after tables exist but before foreign keys. Data transformations that need the new column structure. |
| 11 | `MigrationScripts/AfterTablesScripts/` | Runs after tables and keys, before triggers. Post-structure cleanup. |
| 12 | `Triggers/` | Table triggers |
| 13 | `DDLTriggers/` | DDL triggers |
| 14 | `Table Data/` | Reference data MERGE scripts (from DataTongs or hand-written) |
| 15 | `MigrationScripts/After/` | Post-migration: final data fixes, cleanup, verification |

**Why BetweenTablesAndKeys exists:** When you split a table or restructure foreign keys, you need a window after the new tables are created but before SchemaSmith tries to create the foreign keys. This slot gives you that window to move data into the new structure so the FK constraints succeed.

**Why AfterTablesScripts exists:** Sometimes you need to run scripts after the full table structure (including keys and indexes) is in place, but before triggers are enabled. Data backfills that must satisfy constraints but should not fire triggers go here.

**When to use Product Before/After:** These run outside any template context — they execute against the server, not a specific database. Use them for cross-database operations, linked server setup, or anything that spans multiple templates.

For the complete deployment flow including table and indexed view processing within each slot, see [SchemaQuench Reference](../reference/schemaquench.md#execution-slots).

## Extraction intelligence

SchemaTongs does more than dump scripts to flat folders. It has features that keep large schema packages organized and clean.

**Subfolder preservation.** You can organize scripts by domain — `Tables/Sales/`, `Tables/HR/`, `Procedures/Reporting/`. When SchemaTongs extracts, it preserves existing subfolder locations. If `dbo.Orders.json` already lives in `Tables/Sales/`, the next extraction updates it in place rather than creating a duplicate in the root `Tables/` folder. New objects that have not been organized yet go to the root folder.

**Orphan detection.** When a database object is dropped, its script file becomes an orphan. SchemaTongs offers three modes for handling this:

| Mode | Behavior |
|---|---|
| `Detect` | Logs orphaned files but takes no action. This is the default. |
| `DetectWithCleanupScripts` | Logs orphans and generates cleanup scripts you can review and apply. |
| `DetectDeleteAndCleanup` | Deletes orphaned files and generates cleanup scripts automatically. |

**Script validation.** With `ValidateScripts` enabled, SchemaTongs checks each extracted script against the database to verify it parses correctly. Invalid scripts are saved with a `.sqlerror` extension instead of `.sql`, making them visible but excluded from deployment until you fix them.

**CheckConstraintStyle.** Controls whether check constraints are extracted as column-level properties (inside the table JSON) or as table-level constraints. The default is `ColumnLevel`. If you prefer `TableLevel`, set it in Product.json or the SchemaTongs config — but be consistent, because the style is locked to whatever Product.json specifies once the product exists.

For the full set of extraction options, filtering, and configuration, see [SchemaTongs Reference](../reference/schematongs.md).

---

These are the tools for 95% of what you will encounter. For the remaining 5% — the edge cases and special situations — the next chapter has the escape hatches. [Edge Cases & Escape Hatches](06-edge-cases.md)
