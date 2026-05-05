# Power Workflows

You've built your schema, validated it, and set up your deployment pipeline. Now it's time to handle the real-world complexity that every production system eventually demands. Script tokens let a single codebase span dev, staging, and production without manual edits. Multi-database products keep related databases in lockstep with a single deployment. DataTongs turns reference data into version-controlled, repeatable scripts. Custom metadata via `Extensions` drives deployment-time decisions. Secondary servers keep Availability Groups in sync. And execution slots give you precise control over exactly when each script runs. These are the features that let you deploy with confidence at scale -- solving problems that would otherwise require manual intervention or fragile scripting. And every one of them is in the Community offering, for every supported platform, free.

## Script tokens

Your product deploys to dev, staging, and production. One of your stored procedures needs to query a linked reporting database -- but the database has a different name in each environment. Hardcoding the name means maintaining three copies of the script. Script tokens solve this cleanly.

**Step 1: Define the token in Product.json.**

```json
{
  "Name": "MyProduct",
  "Platform": "SqlServer",
  "ScriptTokens": {
    "ReportingDB": "ReportingDev",
    "ReleaseVersion": "2.1.0"
  }
}
```

**Step 2: Use the token in your SQL scripts.**

```sql
CREATE OR ALTER PROCEDURE [dbo].[GetSalesReport]
AS
BEGIN
  SELECT s.OrderDate, s.Total, r.RegionName
    FROM [dbo].[Sales] s
    JOIN [{{ReportingDB}}].[dbo].[Regions] r ON s.RegionID = r.RegionID
END
```

**Step 3: Override per environment via environment variables.**

For deployment to staging or production, override any token without touching the config files:

```
SmithySettings_ScriptTokens__ReportingDB=ProdReporting
```

One script. Three environments. Zero manual edits between them.

Two tokens are always available automatically: `{{ProductName}}` (from `Product.json` Name) and `{{TemplateName}}` (from `Template.json` Name). Token replacement is case-insensitive.

### Advanced token tags -- the quiet superpower

This is where tokens go from "find and replace" to "deployment-time content engine." The *value* you give a token can start with a special tag that tells SchemaSmith to resolve it dynamically: read a file from disk, query the live server, or embed a specific table's JSON. These aren't teaser features -- they're in Community today, free, for every supported platform.

**Embed a file's contents:**

```json
"ScriptTokens": {
  "ReferenceData": "<*File*>resources/reference-data.sql"
}
```

The `{{ReferenceData}}` token in any script resolves to the entire contents of `resources/reference-data.sql`. Long migration bodies can live in their own files and be included where needed.

**Embed a binary file as hex:**

```json
"ScriptTokens": {
  "DefaultLogo": "<*BinaryFile*>resources/logo.png"
}
```

`{{DefaultLogo}}` becomes a `0x...` hex literal, ready to drop into a `VARBINARY` insert on SQL Server or a `BYTEA` literal on PostgreSQL.

**Query the target server at deployment time:**

```json
"ScriptTokens": {
  "ActiveTenants": "<*Query*>SELECT TenantId FROM dbo.Tenants WHERE Active = 1"
}
```

The query runs once against the actual server you're deploying to, and `{{ActiveTenants}}` resolves to a newline-separated list of tenant IDs. Your migration script can consume that list without any hardcoded environment knowledge.

**Embed one specific table's JSON:**

```json
"ScriptTokens": {
  "OrdersTable": "<*SpecificTable*>dbo.Orders"
}
```

Useful when a script needs to introspect exactly one table's shape -- a schema-aware stored procedure that builds audit triggers, for example, or a diagnostic query that reads column metadata.

And at the template level, the automatic tokens `{{TableSchema}}`, `{{IndexedViewSchema}}` (SQL Server), and `{{MaterializedViewSchema}}` (PostgreSQL) give you the entire current template's table or view model serialized as JSON -- ready to drop into a stored procedure call or a dynamic SQL generator.

For the full token reference -- scopes, custom-property tokens from `Extensions`, resolution order, every advanced tag -- see [Script Tokens Reference](../reference/script-tokens.md).

## Custom properties that drive behavior

Tokens aren't just for deployment parameters. Custom metadata you attach to your tables via the `Extensions` carrier becomes available as tokens in every expression field on that table and its components. This is where SchemaSmith starts to feel like a governance platform instead of just a deployment tool.

**Declare once:**

```json
{
  "Name": "[Orders]",
  "Extensions": {
    "Environment": "Production",
    "DataClassification": "PII",
    "OwningTeam": "Identity"
  },
  "Indexes": [
    {
      "Name": "[IX_Orders_AuditCreatedAt]",
      "IndexColumns": "[CreatedAt]",
      "ShouldApplyExpression": "SELECT CASE WHEN '{{Table.Environment}}' = 'Production' THEN 1 ELSE 0 END"
    }
  ]
}
```

**Consume everywhere:** `{{Table.Environment}}` drives whether the audit index is created. `{{Table.DataClassification}}` could drive a script token that generates masking policies. `{{Table.OwningTeam}}` could drive ownership tags. One declaration, visible in the PR diff, enforceable in CI, actionable at deployment time.

See [Custom Properties](../reference/custom-properties.md) for the full mechanism -- scoping, nested objects, array flattening, preservation through SchemaTongs re-extraction, and the JSON Schema validation pattern for team governance.

## Conditional deployment with ShouldApplyExpression

`ShouldApplyExpression` is how you declare "this thing only exists in certain places." It lives on tables, columns, indexes, foreign keys, check constraints, indexed views, materialized views -- every structural component. Before deploying any component with a `ShouldApplyExpression`, SchemaQuench resolves the tokens, runs the expression against the target database, and skips the component if the result is falsy.

```json
{
  "Name": "[LargeTextColumn]",
  "DataType": "NVARCHAR(MAX)",
  "Nullable": true,
  "ShouldApplyExpression": "SELECT CASE WHEN SERVERPROPERTY('EngineEdition') IN (5, 8) THEN 0 ELSE 1 END"
}
```

This column exists on on-prem SQL Server but not Azure SQL Database. One table definition, two deployments, no per-environment file copies.

```json
{
  "Name": "[IX_Orders_ReplicaOptimized]",
  "IndexColumns": "[OrderDate], [CustomerId]",
  "ShouldApplyExpression": "SELECT CASE WHEN DB_NAME() LIKE '%_replica' THEN 1 ELSE 0 END"
}
```

An index that only exists on tables whose database name ends with `_replica`. Trivial to declare, impossible to get wrong, reviewable in the PR diff.

## Multi-database products

Some applications span more than one database -- a main transactional database plus a reporting database, or a primary database plus an audit log. SchemaSmith handles this as a single product with multiple templates. One quench, all databases updated.

**Product.json defines the deployment order:**

```json
{
  "Name": "MyProduct",
  "Platform": "SqlServer",
  "TemplateOrder": ["Main", "Reporting"],
  "ScriptTokens": {
    "MainDB": "AppMain",
    "ReportingDB": "AppReporting"
  },
  "ValidationScript": "SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM master.sys.databases WHERE [name] = '{{MainDB}}') THEN 1 ELSE 0 END AS BIT)"
}
```

**Each template identifies its own target database:**

```json
{
  "Name": "Main",
  "DatabaseIdentificationScript": "SELECT [name] FROM master.sys.databases WHERE [name] = '{{MainDB}}'"
}
```

Each template has its own complete folder structure -- `Tables/`, `Procedures/`, `Before Scripts/`, and so on. SchemaQuench quenches them in the order specified by `TemplateOrder`. One run, both databases updated, all in a single atomic operation from the pipeline's perspective.

**Multi-tenant in one template.** The `DatabaseIdentificationScript` can return multiple rows. One template, one declaration, every tenant database gets the same schema. Combine with `MaxThreads` in the SchemaQuench settings file and the tenants deploy in parallel up to the configured limit.

## Secondary server support (SQL Server)

For SQL Server deployments targeting Availability Groups, SchemaQuench can quench to a primary replica plus one or more secondaries in parallel. Configure the secondary list on the target connection:

```json
{
  "Target": {
    "Server": "primary-replica",
    "SecondaryServers": "secondary-1,secondary-2"
  }
}
```

Then declare per-folder routing on your product-level folders:

```json
{
  "ScriptFolders": [
    { "FolderPath": "Before Product",       "QuenchSlot": "Before", "ServerToQuench": "Both" },
    { "FolderPath": "Linked Server Setup",  "QuenchSlot": "Before", "ServerToQuench": "Primary" },
    { "FolderPath": "Local Cache Build",    "QuenchSlot": "After",  "ServerToQuench": "Secondary" }
  ]
}
```

`ServerToQuench` accepts `Primary`, `Secondary`, or `Both`. SchemaQuench runs each folder's scripts against exactly the servers you specified, in parallel across the replica set. See [Schema Packages -- Secondary Servers](../reference/schema-packages.md#secondary-servers) for the full mechanics.

## FK-aware data delivery with DataTongs

Lookup tables, seed data, the whole library of reference rows every environment needs -- they're a schema-management problem as much as the tables themselves. SchemaSmith treats reference data the same way it treats structure: declare the state, let the tool do the rest. DataTongs extracts your data, writes it alongside the table JSON, and SchemaQuench delivers it in foreign-key order. You don't write merge scripts. You don't sort the load order. You declare.

**The declarative loop:**

1. Run **DataTongs** with `--ConfigureDataDelivery` against your source database. For every table listed, DataTongs writes a `.tabledata` file (raw JSON rows) and updates the table's JSON with a `DataDelivery` block -- `ContentFile`, `MergeType`, `MatchColumns`.
2. Commit the `.tabledata` files and the updated table JSONs.
3. Run **SchemaQuench**. It discovers every `DataDelivery` block, orders the tables by foreign-key dependencies, and merges the data using the platform's preferred idiom.

**Example DataTongs configuration:**

```json
{
  "Source": {
    "Platform": "SqlServer",
    "Server": "production-server",
    "Database": "AppMain"
  },
  "ContentPath": "./Templates/Main/data",
  "ScriptPath":  "./Templates/Main/Table Data",
  "ShouldCast": {
    "ConfigureDataDelivery": true,
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

After this runs, each configured table's JSON gains a block like:

```json
"DataDelivery": {
  "ContentFile": "data/dbo.PermissionTypes.tabledata",
  "MergeType": "Insert/Update/Delete",
  "MatchColumns": "[PermissionTypeID]",
  "MergeFilter": "IsActive = 1"
}
```

No hand-written merge script. Reference data is part of the table definition now.

### Two-pass FK-aware delivery

Foreign keys turn data loading into a graph problem. SchemaQuench solves it automatically. The pass sequence:

- **Pass 1** -- Tables whose required (NOT NULL) foreign keys point to already-loaded tables are merged first. Rows with nullable FK columns pointing to tables still waiting their turn get inserted with those columns NULL. The merge doesn't block on a constraint that references a row that doesn't exist yet.
- **Pass 2** -- Once every pass-1 table has delivered, each deferred table gets merged again with only the deferred FK columns in play, back-filling the real values.

```
  Delivering dbo.Employee (pass 1 - deferred columns as NULL)
  Delivering dbo.Department (pass 1)
  Delivering dbo.Employee (pass 2 - updating deferred FK columns)
```

Self-referential tables, cross-table cycles broken by nullable columns, complex relational graphs -- all handled declaratively.

### Hand-written scripts still welcome

Sometimes you need procedural control: conditional seeds, one-off data rebuilds, multi-step migrations. Those still work -- drop SQL files into a `TableData`-slot folder (the default `Table Data/` folder works, or declare your own) and they run right after the declarative delivery step. Use whichever style fits the problem.

**The golden source pattern:** Extract from production (the source of truth), commit the generated artifacts to your repository, and quench to every other environment via SchemaQuench. Reference data changes travel through the same review flow as structural changes. No more "staging has stale lookup data" surprises.

For the full DataTongs configuration reference -- key column detection, filter semantics, per-platform type handling, `--ConfigureDataDelivery` mechanics -- see [DataTongs Reference](../reference/datatongs.md). For the `DataDelivery` JSON schema, see [Schema Packages -- DataDelivery](../reference/schema-packages.md#datadelivery).

## Resuming a failed deployment

Long-running deployments fail. A 90-minute production quench hits a transient deadlock at minute 75, or the migration script at stage 12 of 18 trips on unexpected data. Without checkpointing, the recovery is brutal: start over from zero, re-run every step you already applied, hope the interrupted state is self-consistent.

SchemaQuench writes checkpoints as it goes. Every completed step (helper procedure deployment, missing tables, modified tables, indexes, FK-aware data delivery, foreign keys, materialized or indexed view rebuilds) and every completed migration script gets recorded to disk. Re-run with `--ResumeQuench` and everything already recorded is skipped:

```bash
SchemaQuench --ResumeQuench
```

The next run picks up where the last one stopped -- minutes of work instead of hours. On a clean success the checkpoint files are deleted automatically; on a failure they're preserved for the next resume. Point the checkpoint directory somewhere durable if you need them to survive a container restart or agent rotation:

```bash
SchemaQuench --ResumeQuench --CheckpointDirectory:/var/schemasmith/checkpoints
```

For the checkpoint scopes, the full step list, and the practical guidance on when to opt in, see [SchemaQuench -- Checkpoint and Resume](../reference/schemaquench.md#checkpoint-and-resume).

## Custom script folders

The default folder set is a strong starting point, but your team's deployment lifecycle might not match it exactly. Maybe you want a folder called `Permissions` that runs in the `After` slot. Maybe you want to split `Procedures` into `Procedures/Public` and `Procedures/Internal` for code review purposes. Maybe you need a brand-new custom slot for your datafix pipeline.

Declare your own folder layout via `ScriptFolders` in `Template.json`:

```json
{
  "Name": "MyTemplate",
  "DatabaseIdentificationScript": "...",
  "ScriptFolders": [
    { "FolderPath": "Before Scripts",    "QuenchSlot": "Before" },
    { "FolderPath": "Schemas",           "QuenchSlot": "Objects", "ObjectType": "Schemas" },
    { "FolderPath": "Functions",         "QuenchSlot": "Objects", "ObjectType": "Functions" },
    { "FolderPath": "Views",             "QuenchSlot": "Objects", "ObjectType": "Views" },
    { "FolderPath": "Procedures/Public", "QuenchSlot": "Objects", "ObjectType": "Procedures" },
    { "FolderPath": "Procedures/Internal", "QuenchSlot": "Objects", "ObjectType": "Procedures" },
    { "FolderPath": "Triggers",          "QuenchSlot": "AfterTablesObjects", "ObjectType": "Triggers" },
    { "FolderPath": "Table Data",        "QuenchSlot": "TableData" },
    { "FolderPath": "Permissions",       "QuenchSlot": "After" },
    { "FolderPath": "After Scripts",     "QuenchSlot": "After" }
  ]
}
```

When you declare a `ScriptFolders` array, it fully replaces the platform default set -- so include every folder you want active. This is a full replacement, not a merge, which means you're always in control of exactly which folders run.

## Script folder execution order

SchemaQuench quenches scripts in a precise order. Understanding the execution slots lets you place scripts exactly where they need to run in the deployment lifecycle. The full sequence, from first to last:

| Order | Slot | What runs |
|---|---|---|
| 1 | Product `Before` | Cross-database setup, linked servers, server-level migrations |
| 2 | Template `Objects` | Programmable objects with dependency retry (schemas, types, functions, views, procedures) |
| 3 | _(new tables / columns)_ | New tables and columns created from table JSON |
| 4 | Template `Before` | Pre-modification migration scripts |
| 5 | _(table modification)_ | Existing tables altered to match JSON |
| 6 | Template `BetweenTablesAndKeys` | Runs after table structure is final but before FK constraints |
| 7 | _(indexes and constraints)_ | Missing indexes, checks, defaults, statistics |
| 8 | Template `AfterTablesScripts` | Runs after structure and indexes, before triggers |
| 9 | Template `AfterTablesObjects` | Triggers, DDL triggers, rules -- dependency retry |
| 10 | Template `TableData` | Reference data sync scripts |
| 11 | _(foreign keys)_ | Foreign key constraints applied after all data is in place |
| 12 | _(indexed / materialized views)_ | SQL Server indexed views / PostgreSQL materialized views |
| 13 | Template `After` | Post-migration final scripts |
| 14 | Product `After` | Cross-database post-migration |

**Why BetweenTablesAndKeys exists:** When you split a table or restructure foreign keys, you need a window after the new tables are created but before SchemaSmith tries to create the foreign keys. This slot gives you that window to move data into the new structure so the FK constraints succeed.

**Why AfterTablesScripts exists:** Sometimes you need to run scripts after indexes and constraints are in place but before triggers are enabled. Data backfills that must satisfy constraints but shouldn't fire triggers go here.

For the complete deployment flow including per-platform modular procedures, see [SchemaQuench Reference](../reference/schemaquench.md#database-quench-sequence).

---

Tokens, custom properties, `ShouldApplyExpression`, multi-database products, secondary servers, DataTongs, custom script folders, execution slots. That's the toolkit for 95% of what you'll encounter -- and all of it is in Community. For the remaining 5% -- the escape hatches -- the next chapter has you covered. [Edge Cases & Escape Hatches](10-edge-cases.md)
