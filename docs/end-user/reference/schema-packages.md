# Schema Packages Reference

A schema package is your database's source of truth -- the complete, version-controlled definition of what your databases should look like. Every tool in the SchemaSmith toolset revolves around this one structure: SchemaTongs casts packages from live databases, SchemaQuench quenches them onto target servers, and DataTongs adds reference data alongside your schema. The same package format works for **SQL Server**, **PostgreSQL**, and **MySQL** -- one shape, one workflow, three engines.

This document is the authoritative reference for every file, folder, property, and format in a schema package.

---

## Product.json

The `Product.json` file sits at the root of the schema package and is the top-level configuration -- the starting point for every deployment. Every package must have exactly one.

### Properties

| Property | Type | Default | Required | Description |
|---|---|---|---|---|
| `Name` | string | | Yes | Product name. Automatically added as a `{{ProductName}}` script token. Used for migration script tracking and version stamping. |
| `Platform` | string | | Yes | Target platform. Valid values: `"SqlServer"`, `"PostgreSQL"`, `"MySQL"`. Determines which platform adapter handles deployment, extraction, and the default folder set. |
| `ValidationScript` | string | | Yes | SQL expression evaluated before quench begins. Must return a truthy value or the quench aborts. Supports token replacement. |
| `TemplateOrder` | string[] | `[]` | No | Ordered list of template directory names. Templates are quenched in this order. |
| `ScriptTokens` | object | `{}` | No | Key-value pairs for `{{TokenName}}` replacement in scripts and SQL properties. See the [Script Tokens Reference](script-tokens.md). |
| `BaselineValidationScript` | string | | No | SQL expression evaluated after server validation but before template processing. |
| `VersionStampScript` | string | | No | SQL executed once after all templates complete successfully. Typically records the release version on the server. |
| `DropUnknownIndexes` | bool | `false` | No | When `true`, the table quench drops indexes on managed tables that aren't defined in the table JSON. |
| `MinimumVersion` | string | | No | Minimum target server version. Currently metadata only -- displayed in tooling and available for future version-adaptive features. Use `ValidationScript` for runtime version enforcement. |
| `CheckConstraintStyle` | string | `"ColumnLevel"` | No | Controls how SchemaTongs writes check constraints during extraction: `"ColumnLevel"` (inline `CheckExpression` on the column) or `"TableLevel"` (named constraints in the `CheckConstraints` array). |
| `ScriptFolders` | array | `[]` | No | Optional product-level folder definitions. Used to add custom folder paths or assign secondary-server filtering. See [Custom Script Folders](#custom-script-folders). |
| `BranchNameFile` | string | `"{{repo_path}}/.git/HEAD"` | No | Path to the file SchemaSmith reads to derive the `{{BranchName}}` automatic token. Default points at Git's `HEAD`. Use any VCS that exposes the current branch as a single-line file (Mercurial's `.hg/branch`, Subversion working-copy markers, etc.); the only requirement is that the file exists and contains the branch identifier somewhere on its first line. |
| `BeforeBranchNameMask` | string | `"ref: refs/heads/"` | No | Prefix to strip from the line read out of `BranchNameFile`. Default matches Git's `ref: refs/heads/<branch>` format. Set to `""` for VCSs whose branch file already contains the bare branch name. |
| `AfterBranchNameMask` | string | `""` | No | Suffix to strip after the prefix is removed. Default empty. Set when your VCS appends extra text after the branch name. |
| `Extensions` | any | `null` | No | Reserved. `Product.json` does not currently use `Extensions` for custom properties. |

### Settings intent

**Platform** -- The single most important property. Once set, every tool that touches the package routes through the platform's adapter: SchemaTongs uses the right system catalog queries, SchemaQuench uses the right DDL flavors, DataTongs uses the right merge syntax. The platform value is also what triggers the correct default folder set when a template doesn't override `ScriptFolders`.

**ValidationScript** -- Answers "am I deploying to the correct server?" This is a safety gate that prevents deploying to the wrong target, not a package validation check. Common patterns: verifying a database exists, checking for expected infrastructure, confirming the server version.

**BaselineValidationScript** -- Answers "is this environment at the expected state for this deployment?" Prevents accidental rollback -- if production is at v2.5 and someone runs the v2.3 package, the baseline check aborts before touching anything. Pair with `VersionStampScript`: the stamp records the version after deployment, the baseline checks for it before the next one.

**VersionStampScript** -- Executes after deployment completes. Script tokens are resolved first, so you can pass in values like `{{ReleaseVersion}}`. Single batch only -- no `GO` separators (SQL Server), no compound DO blocks (PostgreSQL) -- but a single statement, procedure call, or `CALL` is fine. Product-level runs once on the server connection; template-level runs per database.

**DropUnknownIndexes** -- Enforces index alignment, but only when you're ready. Default `false` for good reason: most teams adopting SchemaSmith inherit environments with years of index drift. Turn this on after capturing every needed index in your repository. See [the adoption approach](../guide/11-edge-cases.md#the-adoption-approach) in the guide for the phased rollout pattern.

### Example (SQL Server, Northwind demo)

```json
{
  "Name": "Northwind",
  "Platform": "SqlServer",
  "ValidationScript": "SELECT CAST(1 AS BIT)",
  "TemplateOrder": [
    "Initialize",
    "Northwind"
  ],
  "ScriptTokens": {
    "NorthwindDb": "Northwind"
  }
}
```

### Example (PostgreSQL)

```json
{
  "Name": "Northwind",
  "Platform": "PostgreSQL",
  "ValidationScript": "SELECT 1",
  "TemplateOrder": ["Northwind"],
  "ScriptTokens": {
    "NorthwindDb": "northwind"
  }
}
```

### Example (MySQL)

```json
{
  "Name": "Northwind",
  "Platform": "MySQL",
  "ValidationScript": "SELECT 1",
  "TemplateOrder": ["Northwind"],
  "ScriptTokens": {
    "NorthwindDb": "northwind"
  }
}
```

### Notes

- `ProductName` is added automatically to `ScriptTokens` -- you don't need to define it.
- Script token values defined in `Product.json` can be overridden at runtime via the tool's settings file or environment variables in the `ScriptTokens` section.
- All script properties (`ValidationScript`, `BaselineValidationScript`, `VersionStampScript`) support `{{TokenName}}` replacement and the [advanced token tags](script-tokens.md#advanced-token-tags).

---

## Template.json

Each template directory under `Templates/` must contain a `Template.json` file. A template targets one or more databases identified by its `DatabaseIdentificationScript`.

### Properties

| Property | Type | Default | Required | Description |
|---|---|---|---|---|
| `Name` | string | | Yes | Template name. Must match the containing directory name. Automatically added as a `{{TemplateName}}` script token. |
| `DatabaseIdentificationScript` | string | | Yes | SQL query that returns one or more database names. SchemaQuench reads the first column of each row. Supports token replacement. |
| `VersionStampScript` | string | | No | SQL executed per database after that database's quench completes successfully. |
| `UpdateFillFactor` | bool | `true` | No | When `true`, the table quench updates index fill factors to match the JSON definitions. OR'd with table-level and index-level `UpdateFillFactor` settings. |
| `IndexOnlyTableQuenches` | bool | `false` | No | When `true`, the table quench only manages indexes, statistics, XML/full-text indexes, exclude constraints. Skips table creation, column changes, and foreign key management. Tables that don't exist are silently skipped. |
| `BaselineValidationScript` | string | | No | SQL validation executed per database before quenching that database. |
| `RequireAtLeastOneTarget` | bool | `true` | No | When `true`, deployment fails if discovery returns no targets -- zero matching databases for a regular template, or zero matching `(database, schema)` pairs for a schema template. Catches misconfigured identification scripts that silently skip an entire template. Replaces the prior `Required` field (renamed in v2.1). |
| `SkipIfReadOnly` | bool | `false` | No | When `true`, databases that are read-only are silently skipped instead of failing the quench. Enables Availability Group secondary handling on SQL Server and replica handling on other platforms. |
| `ScriptFolders` | array | `[]` | No | Optional list of `TemplateFolder` definitions. When empty, the platform's default folder set is used. When non-empty, this array fully replaces the defaults -- so include every folder you want active. See [Custom Script Folders](#custom-script-folders). |
| `ScriptTokens` | object | `{}` | No | Key-value pairs that override matching product-level tokens for this template. Template tokens take precedence over product tokens with the same key. |
| `SchemaIdentificationScript` | string | | No | SQL Server / PostgreSQL only. Query returning one column, N rows; each row is a schema name to iterate over. Presence activates schema-template mode. See [Schema Templates](#schema-templates). |
| `CreateSchemaIfMissing` | bool | `false` | No | Schema templates only. When `true`, the engine creates any discovered schema that doesn't yet exist before running that iteration. See [Schema Templates](#schema-templates). |
| `AllowParallel` | bool | `true` | No | Schema templates only. When `false`, iterations of this template run serially even when the global thread pool has capacity. See [Schema Templates](#schema-templates). |
| `ContinueOnSchemaFailure` | bool | `true` | No | Schema templates only. When `false`, the first failing iteration aborts all subsequent iterations for this template. See [Schema Templates](#schema-templates). |
| `ContinueOnDatabaseFailure` | bool | `true` | No | Regular templates only. When `false`, the first failing database iteration aborts all subsequent database iterations for this template. Ignored on schema templates -- failure isolation there is governed by `ContinueOnSchemaFailure`. See [Schema Templates](#schema-templates) for the schema-iteration analog and [SchemaQuench &mdash; ContinueOnDatabaseFailure](schemaquench.md#continueondatabasefailure) for the runtime behavior detail. |

### Template settings intent

**UpdateFillFactor** -- Three levels (template, table, index) are OR'd together: if any level is true for a given index, its fill factor gets updated. Template defaults to `true` (enforce from the start for new products). For teams managing existing drift, set template-level to `false` and enable per-index or per-table as you verify alignment.

**RequireAtLeastOneTarget** -- A safety net for misconfigured `DatabaseIdentificationScript` (and `SchemaIdentificationScript`) queries. When discovery returns zero targets and `RequireAtLeastOneTarget` is `true` (the default), SchemaQuench aborts immediately rather than silently deploying nothing. Set to `false` only for templates that legitimately target zero databases (or zero `(database, schema)` pairs) in some environments.

**SkipIfReadOnly** -- Enables graceful handling of read-only replicas. On SQL Server, this is the Availability Group secondary handling. On PostgreSQL and MySQL, the same flag covers logical/physical replicas exposed as databases. With `SkipIfReadOnly: true`, read-only databases are silently skipped and the deployment continues with the writable primaries. Independent of `RequireAtLeastOneTarget` -- a template can require at least one target while still skipping individual read-only databases within the result set.

**IndexOnlyTableQuenches** -- Lets you manage indexes on tables you don't own. Two primary use cases: different indexing on replicated databases (tuned for the consumer's workload, not the producer's), and adding indexes to third-party products where you can't modify the table structure. Scripted objects (procedures, views, functions) still deploy when this flag is on -- so you can deploy custom views and procedures alongside supplementary indexes.

### Example (PostgreSQL)

```json
{
  "Name": "Northwind",
  "DatabaseIdentificationScript": "SELECT datname FROM pg_database WHERE datname = '{{NorthwindDb}}'",
  "UpdateFillFactor": true,
  "ScriptTokens": {}
}
```

---

## Custom Script Folders

By default, every template uses the platform's standard folder layout (see [Default Folders](#default-folders) below). When you need to **add a folder** that isn't in the defaults, **rename a folder**, or **change which slot a folder runs in**, you declare your own `ScriptFolders` array on `Template.json`.

The array is an explicit replacement, not a merge. The moment you provide `ScriptFolders` with at least one entry, the defaults are skipped entirely -- so you should include every folder you want loaded.

### TemplateFolder properties

| Property | Type | Required | Description |
|---|---|---|---|
| `FolderPath` | string | Yes | Relative path under the template directory. Forward or back slashes both work. |
| `QuenchSlot` | string | Yes | Which execution slot the folder runs in. See [Quench Slot Reference](#quench-slot-reference) below for valid values. |
| `ObjectType` | string | No | When the folder contains programmable objects (functions, views, procedures, triggers, etc.), tag it with the corresponding object type so SchemaQuench can route it through the dependency-retry loop correctly. |
| `ShouldApplyExpression` | string | No | Optional SQL predicate evaluated against the target at deploy time (tokens resolved first): true deploys the folder, false skips it (logged), blank always deploys. See [Conditional Deployment](schemaquench.md#shouldapplyexpression-and-conditional-deployment). |

### Example -- adding a custom folder for an extra migration step

```json
{
  "Name": "Reporting",
  "DatabaseIdentificationScript": "SELECT [Name] FROM master.sys.databases WHERE [Name] = '{{ReportDB}}'",
  "ScriptFolders": [
    { "FolderPath": "Before Scripts", "QuenchSlot": "Before" },
    { "FolderPath": "Schemas", "QuenchSlot": "Objects", "ObjectType": "Schemas" },
    { "FolderPath": "Functions", "QuenchSlot": "Objects", "ObjectType": "Functions" },
    { "FolderPath": "Views", "QuenchSlot": "Objects", "ObjectType": "Views" },
    { "FolderPath": "Procedures", "QuenchSlot": "Objects", "ObjectType": "Procedures" },
    { "FolderPath": "BetweenTablesAndKeys", "QuenchSlot": "BetweenTablesAndKeys" },
    { "FolderPath": "AfterTables", "QuenchSlot": "AfterTablesScripts" },
    { "FolderPath": "Triggers", "QuenchSlot": "AfterTablesObjects", "ObjectType": "Triggers" },
    { "FolderPath": "Table Data", "QuenchSlot": "TableData" },
    { "FolderPath": "After Scripts", "QuenchSlot": "After" }
  ]
}
```

In this example, the team kept the standard folders but added two extra slots (`BetweenTablesAndKeys`, `AfterTablesScripts`) that aren't part of the default set. They can write migration scripts that run after the table structure exists but before foreign keys, or after the table structure but before triggers, without writing any extra glue.

### Why this matters

Custom script folders are how you make the schema package fit *your* deployment lifecycle, not the other way around. Need a folder called `Permissions` that runs in the `After` slot? Done. Need to split your large `Procedures` directory into `Procedures/Public` and `Procedures/Internal` for code review? Done. Need an entirely new slot for your team's idempotent post-deploy data fixes? Drop a folder, point it at `After`, and you're done.

### Custom product-level folders

`Product.json` can also declare custom folders via its `ScriptFolders` array (the property name is the same as `Template.json`'s). The shape is similar but uses `ProductQuenchSlot` (`Before` or `After`) and supports a `ServerToQuench` setting that controls whether the folder runs on the primary, secondaries, or both. See [Secondary Servers](#secondary-servers) below. Product folders also accept a `ShouldApplyExpression`, evaluated per server against the admin connection -- see [Conditional Deployment](schemaquench.md#shouldapplyexpression-and-conditional-deployment).

---

## Default Folders

When `Template.json` does not declare its own `ScriptFolders`, SchemaSmith fills in a platform-specific default set. Each platform's defaults reflect the object types and lifecycle stages that platform actually supports.

### SQL Server default folders

| Folder | Quench Slot | Object Type |
|---|---|---|
| `Before Scripts/` | Before | — |
| `Schemas/` | Objects | Schemas |
| `DataTypes/` | Objects | DataTypes |
| `FullTextCatalogs/` | Objects | FullTextCatalogs |
| `FullTextStopLists/` | Objects | FullTextStopLists |
| `XMLSchemaCollections/` | Objects | XMLSchemaCollections |
| `Functions/` | Objects | Functions |
| `Views/` | Objects | Views |
| `Procedures/` | Objects | Procedures |
| `Triggers/` | AfterTablesObjects | Triggers |
| `DDLTriggers/` | AfterTablesObjects | DDLTriggers |
| `Table Data/` | TableData | — |
| `After Scripts/` | After | — |

> **Legacy fallback (SQL Server only):** If your existing package has `MigrationScripts/Before/`, `MigrationScripts/After/`, or a `TableData/` folder (no space) on disk, SchemaSmith will use them in place of `Before Scripts/`, `After Scripts/`, and `Table Data/` respectively. This keeps older packages working without a folder rename.

### PostgreSQL default folders

| Folder | Quench Slot | Object Type |
|---|---|---|
| `Before Scripts/` | Before | — |
| `Schemas/` | Objects | Schemas |
| `Domain Types/` | Objects | DomainTypes |
| `Enum Types/` | Objects | EnumTypes |
| `Composite Types/` | Objects | CompositeTypes |
| `Functions/` | Objects | Functions |
| `Trigger Functions/` | Objects | TriggerFunctions |
| `Window Functions/` | Objects | WindowFunctions |
| `Aggregates/` | Objects | Aggregates |
| `Procedures/` | Objects | Procedures |
| `Sequences/` | Objects | Sequences |
| `Rules/` | AfterTablesObjects | Rules |
| `Triggers/` | AfterTablesObjects | Triggers |
| `Views/` | AfterTablesObjects | Views |
| `Table Data/` | TableData | — |
| `After Scripts/` | After | — |

### MySQL default folders

| Folder | Quench Slot | Object Type |
|---|---|---|
| `Before Scripts/` | Before | — |
| `Events/` | Objects | Events |
| `Functions/` | Objects | Functions |
| `Procedures/` | Objects | Procedures |
| `Triggers/` | AfterTablesObjects | Triggers |
| `Views/` | AfterTablesObjects | Views |
| `Table Data/` | TableData | — |
| `After Scripts/` | After | — |

Tables are always loaded from `Tables/` regardless of platform. SQL Server adds `Indexed Views/`; PostgreSQL adds `Materialized Views/`. These are not script folders -- they hold structured JSON object definitions, not `.sql` files.

---

## Quench Slot Reference

`TemplateQuenchSlot` controls when in the deployment lifecycle a folder's scripts run.

| Slot | Behavior |
|---|---|
| `Before` | One-time migration scripts that run after initial object creation and new table creation, but before table modifications. Use for data preparation that must happen before columns are altered or dropped. Sequential, tracked. |
| `Objects` | Database objects that may have cross-dependencies (schemas, types, catalogs, functions, views, procedures). The retry loop resolves creation order automatically. |
| `BetweenTablesAndKeys` | Migration scripts that need the table structure to exist but must run before foreign key constraints are enforced. Typical use: populating a new NOT NULL column before FKs block the data load. Sequential, tracked. |
| `AfterTablesScripts` | Migration scripts that depend on the final table and key structure but must run before triggers are deployed. Sequential, tracked. |
| `AfterTablesObjects` | Triggers, DDL triggers, rules, and views that depend on the completed table structure. Dependency retry loop. |
| `TableData` | Data population scripts (MERGE statements, INSERT/UPDATE seeds). Run after triggers are deployed but before foreign key constraints are applied. Dependency retry loop. |
| `After` | Final migration scripts. Run after all database objects and data are deployed. Sequential, tracked. |

`ProductQuenchSlot` has only two values:

| Slot | Behavior |
|---|---|
| `Before` | Product-level scripts that run before any template processing begins. Sequential, untracked (run every deployment). |
| `After` | Product-level scripts that run after all templates complete. Sequential, untracked. |

### Execution behaviors

**Sequential, untracked** -- Product-level scripts run in alphabetical order on every deployment. They aren't recorded in any tracking table. Write these scripts to be idempotent.

**Sequential, tracked** -- Template-level migration scripts run in alphabetical order. Each script's completion is recorded in the `CompletedMigrationScripts` table and won't run again on subsequent quenches. Scripts with `[ALWAYS]` in the filename run every time regardless of tracking.

**Dependency retry loop** -- All scripts in the slot are attempted. Scripts that fail due to unresolved dependencies are retried on the next iteration. The loop continues until all scripts succeed or no progress is made on an iteration.

---

## Secondary Servers

For **SQL Server** deployments targeting Availability Groups, SchemaQuench can quench to a primary plus one or more secondary servers in parallel, automatically routing each script folder to the right server based on its `ServerToQuench` setting.

Configure secondaries in `SchemaQuench.settings.json`:

```json
{
  "Target": {
    "Server": "primary-replica",
    "SecondaryServers": "secondary-1,secondary-2"
  }
}
```

Then declare `ServerToQuench` on your product-level folders:

```json
{
  "Name": "MyProduct",
  "Platform": "SqlServer",
  "ScriptFolders": [
    { "FolderPath": "Before Product",        "QuenchSlot": "Before", "ServerToQuench": "Both" },
    { "FolderPath": "Linked Server Setup",   "QuenchSlot": "Before", "ServerToQuench": "Primary" },
    { "FolderPath": "Local Cache Build",     "QuenchSlot": "After",  "ServerToQuench": "Secondary" }
  ]
}
```

`ServerToQuench` accepts `Primary`, `Secondary`, or `Both`. The default is `Primary`. Scripts in folders with `Both` (or scripts marked for the active server type) execute on every server in the list, in parallel. The CLI logs the per-server progress so you can see exactly what landed where.

> **Multi-platform note:** PostgreSQL and MySQL deployments use a single connection only -- the secondary-server feature is SQL Server-specific because Availability Groups are a SQL Server concept.

---

## Complete Folder Structure

A schema package is a predictable directory tree. Every tool in the SchemaSmith toolset discovers what it needs by convention -- `Product.json` at the root, `Templates/` one level down, platform-specific script folders inside each template -- so you never have to configure paths or point a tool at individual files. Marker files and generated artifacts sit alongside the files you author, and the whole layout maps cleanly onto source control.

```
MyProduct/
  Product.json                      Product configuration (required)
  .json-schemas/                    JSON Schema files for IDE validation (generated)
    products.<platform>.schema      <platform> = sqlserver, postgresql, or mysql
    templates.<platform>.schema
    tables.<platform>.schema
    indexedviews.sqlserver.schema             SQL Server packages
    materializedviews.postgresql.schema       PostgreSQL packages
  .community                        Marker file (generated by SchemaTongs)
  Before Product/                   SQL scripts run before all templates
  After Product/                    SQL scripts run after all templates
  Templates/
    TemplateName/
      Template.json                 Template configuration (required)
      Tables/                       Table definition JSON files (all platforms)
        public.customer.json
        sales.order.json
      Indexed Views/                Indexed view definitions (SQL Server only)
        dbo.vw_OrderSummary.json
      Materialized Views/           Materialized view definitions (PostgreSQL only)
        public.mv_active_orders.json
      <platform default folders>    See Default Folders above
      <custom folders>              Anything you declared in ScriptFolders
```

Table JSON files are named `schema.tablename.json` (e.g., `dbo.Customer.json`, `public.order_lines.json`). For MySQL the `schema` segment is the database/schema name. If a table or schema name contains filesystem-illegal characters, the encoded form is used in the filename (see [Filesystem-Illegal Character Encoding](#filesystem-illegal-character-encoding)).

### File naming

How you name table JSON files depends on whether the template is a regular template or a schema template.

**Regular templates** use schema-prefixed filenames. The prefix tells SchemaQuench which schema the table belongs to: `dbo.Customers.json`, `Sales.Orders.json`, `public.order_lines.json`. No prefix means no schema, which is an error on SQL Server and PostgreSQL.

**Schema templates** use unqualified filenames. The schema is the iteration variable -- SchemaQuench resolves `{{SchemaName}}` at runtime for every iteration, so `Customers.json` deploys into `acme.Customers` for the `acme` tenant, `globex.Customers` for the `globex` tenant, and so on. Filenames with a schema prefix (e.g., `dbo.Customers.json`) inside a schema template's `Tables/` folder are rejected by the engine at load time.

**Cross-schema exception.** A schema-prefix filename inside a schema template's `Tables/` folder means the schema template owns a table in that specific named schema rather than in the iteration schema -- for example, `dbo.SharedConfig.json` inside a `TenantWorkspace/Tables/` folder deploys a single `dbo.SharedConfig` table regardless of which tenant iteration is running. This pattern is legal but unusual; most cross-schema work belongs in the accompanying regular template that runs first in `TemplateOrder`.

The accompanying **Shared** regular template (if present) follows the standard prefix convention: `dbo.Tenants.json`, `public.countries.json`. Its filenames are unchanged by the presence of a schema template in the same product.

For a worked layout showing these conventions side by side, see [Multi-Tenant Deployments](../guide/10-multi-tenant-deployments.md).

SQL script files can be organized into subdirectories within any script folder. All `.sql` files are discovered recursively and sorted alphabetically by full path.

---

## Table JSON Format -- Shared Properties

Every table definition file declares exactly one table. The shared properties below appear on every platform; platform-specific extensions follow in the next section.

| Property | Type | Default | Required | Description |
|---|---|---|---|---|
| `Name` | string | | Yes | Table name. Use the platform's quote style: `[Customer]` (SQL Server), `"customer"` (PostgreSQL), `` `customer` `` (MySQL). Bare names are also accepted. |
| `Columns` | array | `[]` | Yes | Column definitions. See [Columns](#columns). |
| `Indexes` | array | `[]` | No | Index and constraint definitions. See [Indexes](#indexes). |
| `ForeignKeys` | array | `[]` | No | Foreign key definitions. See [Foreign Keys](#foreign-keys). |
| `CheckConstraints` | array | `[]` | No | Table-level check constraint definitions. See [Check Constraints](#check-constraints). |
| `ShouldApplyExpression` | string | | No | SQL expression evaluated at quench time. If it returns false (or `0`), the entire table is skipped on this database. Tokens are resolved before evaluation. See [Conditional Application](#conditional-application). |
| `VariantName` | string | | No | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `OldName` | string | `""` | No | Previous table name. When set, the table is renamed during quench. Clear after the rename has been deployed everywhere. |
| `DataDelivery` | object | `null` | No | Declarative data delivery configuration for this table. See [DataDelivery](#datadelivery). |
| `Extensions` | object | `null` | No | Open metadata bag. See [Custom Properties](custom-properties.md). |

The `ShouldApplyExpression` field appears on **tables, columns, indexes, foreign keys, check constraints, indexed views, materialized views**, and several platform-specific components. Wherever it appears, it works the same way: the engine resolves tokens, runs the expression against the target database, and skips the component if the result is falsy.

---

## Platform-Specific Table Properties

Each platform's table definition extends the shared properties with engine-specific fields.

### SQL Server (`SqlServerTable`)

| Property | Type | Default | Description |
|---|---|---|---|
| `Schema` | string | `"dbo"` | Database schema. Use bracket notation in extracted files (e.g., `"[Production]"`). |
| `CompressionType` | string | `"NONE"` | Data compression: `"NONE"`, `"ROW"`, `"PAGE"`. |
| `IsTemporal` | bool | `false` | When `true`, SchemaQuench manages the table as system-versioned temporal: emits `ALTER TABLE ... SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = <Schema>.<Name>_Hist))` and protects the period columns (`ValidFrom` / `ValidTo`) from drop detection. When toggled back to `false`, SchemaQuench emits `SET (SYSTEM_VERSIONING = OFF)`. The history table itself (`<Name>_Hist` in the same schema) is not auto-created -- declare it as a sibling table JSON in your package or rely on a Before script that creates it before the `IsTemporal=true` table is quenched. |
| `XmlIndexes` | array | `[]` | XML index definitions. See [XML Indexes (SQL Server)](#xml-indexes-sql-server). |
| `Statistics` | array | `[]` | Custom statistics definitions. See [Statistics (SQL Server)](#statistics-sql-server). |
| `FullTextIndex` | object or array | `null` | Full-text index on the table -- a single definition, or an array of conditional variants. See [Full-Text Index (SQL Server)](#full-text-index-sql-server). |
| `UpdateFillFactor` | bool | `false` | When `true`, index fill factors on this table are updated to match JSON definitions during quench. |
| `EnableCDC` | bool | `false` | When `true`, the table is enabled for change data capture. |

### PostgreSQL (`PostgreSqlTable`)

| Property | Type | Default | Description |
|---|---|---|---|
| `Schema` | string | `"public"` | Database schema. Use double-quote notation in extracted files (e.g., `"\"sales\""`). |
| `Statistics` | array | `[]` | Extended statistics definitions. |
| `ExcludeConstraints` | array | `[]` | Exclusion constraint definitions. See [Exclude Constraints (PostgreSQL)](#exclude-constraints-postgresql). |
| `RowLevelSecurity` | bool | `false` | Enables row-level security on the table. |
| `ForceRowLevelSecurity` | bool | `false` | When `true`, row-level security is enforced even for the table owner. |
| `AccessMethod` | string | `null` | Storage access method (e.g., `"heap"`). |
| `PersistenceType` | string | `null` | Persistence override (e.g., `"UNLOGGED"`, `"TEMPORARY"`). |
| `UpdateFillFactor` | bool | `false` | Enables fill-factor reconciliation for this table. |
| `FillFactor` | short (0--100) | `0` | Table fill factor. `0` means use the server default. |

### MySQL (`MySqlTable`)

| Property | Type | Default | Description |
|---|---|---|---|
| `Engine` | string | `"InnoDB"` | Storage engine. |
| `RowFormat` | string | `null` | Row format: `"DYNAMIC"`, `"COMPACT"`, `"COMPRESSED"`, or `"REDUNDANT"`. |
| `CharacterSet` | string | `null` | Default character set for the table. |
| `Collation` | string | `null` | Default collation for the table. |
| `Comment` | string | `null` | Table comment. |
| `AutoIncrementValue` | ulong | `null` | Initial auto-increment value. |
| `FullTextIndexes` | array | `[]` | Full-text index definitions (MySQL supports multiple per table). See [Full-Text Indexes (MySQL)](#full-text-indexes-mysql). |

### Minimal example (PostgreSQL)

```json
{
  "Name": "region",
  "Schema": "public",
  "Columns": [
    { "Name": "region_id",          "DataType": "INTEGER", "Nullable": false },
    { "Name": "region_description", "DataType": "VARCHAR(50)", "Nullable": false }
  ],
  "Indexes": [
    {
      "Name": "pk_region",
      "PrimaryKey": true,
      "Unique": true,
      "IndexColumns": "region_id"
    }
  ]
}
```

### Minimal example (MySQL)

```json
{
  "Name": "region",
  "Engine": "InnoDB",
  "Columns": [
    { "Name": "region_id",          "DataType": "INT",          "Nullable": false },
    { "Name": "region_description", "DataType": "VARCHAR(50)", "Nullable": false }
  ],
  "Indexes": [
    {
      "Name": "PRIMARY",
      "PrimaryKey": true,
      "Unique": true,
      "IndexColumns": "region_id"
    }
  ]
}
```

---

## Columns

Every entry in the `Columns` array defines one column. The shared shape is small; platform-specific column subclasses add fields where the engine genuinely differs.

### Shared column properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | | Column name. |
| `DataType` | string | | Platform-appropriate data type with precision/scale/length. SQL Server: `NVARCHAR(50)`, `INT IDENTITY(1,1)`. PostgreSQL: `VARCHAR(50)`, `INTEGER GENERATED ALWAYS AS IDENTITY`. MySQL: `VARCHAR(50)`, `INT AUTO_INCREMENT`. |
| `Nullable` | bool | `false` | Whether the column allows NULL. |
| `Default` | string | | Default constraint expression -- e.g., `"getdate()"` (SQL Server), `"now()"` (PostgreSQL), `"CURRENT_TIMESTAMP"` (MySQL). |
| `ShouldApplyExpression` | string | | Conditional inclusion. See [Conditional Application](#conditional-application). |
| `VariantName` | string | | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `OldName` | string | `""` | Previous column name for rename detection. Clear after the rename has deployed everywhere. |
| `Extensions` | object | `null` | Custom metadata for this column. See [Custom Properties](custom-properties.md). |

### SQL Server column extras

`CheckExpression`, `ComputedExpression`, `Persisted`, `Sparse`, `Collation`, `DataMaskFunction`. Identity is part of the `DataType` string (`INT IDENTITY(1,1)`); `ROWGUIDCOL` likewise (`UNIQUEIDENTIFIER ROWGUIDCOL`).

### PostgreSQL column extras

`GenerationExpression` (for stored generated columns), `Collation`, identity columns expressed via `DataType` (`INTEGER GENERATED BY DEFAULT AS IDENTITY` or `GENERATED ALWAYS`).

### MySQL column extras

`GenerationExpression`, `CharacterSet`, `Collation`, `Comment`, auto-increment via `DataType` (`INT AUTO_INCREMENT`).

### User-defined types

When a database uses user-defined types (`CREATE TYPE` / `CREATE DOMAIN`), the `DataType` value is the type name. The type must be created in the appropriate types script folder (`DataTypes/` on SQL Server, `Domain Types/`/`Enum Types/`/`Composite Types/` on PostgreSQL) before the table quench runs.

### Computed/generated columns

```json
{ "Name": "FullName", "ComputedExpression": "[FirstName] + ' ' + [LastName]", "Persisted": true }
```

(SQL Server uses `ComputedExpression`; PostgreSQL and MySQL use `GenerationExpression` with the platform's syntax.)

---

## Indexes

Every entry in the `Indexes` array defines an index or key constraint on the table. The shared shape covers the common cases; per-platform index types extend it.

### Shared index properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | | Index or constraint name. |
| `PrimaryKey` | bool | `false` | `true` for a primary key constraint. |
| `Unique` | bool | `false` | `true` for a unique index. |
| `UniqueConstraint` | bool | `false` | `true` for a UNIQUE constraint (as opposed to a unique index). |
| `IndexColumns` | string | | Comma-separated column names with optional sort direction. |
| `IncludeColumns` | string | | Comma-separated INCLUDE/covering columns where supported. |
| `FilterExpression` | string | | Filtered/partial index WHERE clause. |
| `ShouldApplyExpression` | string | | Conditional inclusion. See [Conditional Application](#conditional-application). |
| `VariantName` | string | | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | `null` | Custom metadata. See [Custom Properties](custom-properties.md). |

### SQL Server index extras

`CompressionType` (NONE/ROW/PAGE), `Clustered`, `ColumnStore`, `FillFactor`, `UpdateFillFactor`.

### PostgreSQL index extras

`AccessMethod` (e.g., `btree`, `gin`, `gist`), `Tablespace`, `IncludeColumns` (PostgreSQL 11+ covering indexes).

### MySQL index extras

`IndexType` (e.g., `BTREE`, `HASH`), `Visible`/`Invisible` flag where supported.

### Examples

```json
// Primary key (SQL Server)
{ "Name": "[PK_Product_ProductID]", "PrimaryKey": true, "Unique": true, "Clustered": true, "IndexColumns": "[ProductID]" }

// Filtered unique index (PostgreSQL)
{ "Name": "ux_customer_email_active", "Unique": true, "IndexColumns": "email", "FilterExpression": "is_active = true" }

// Covering index (SQL Server)
{ "Name": "[IX_Customer_Name]", "IndexColumns": "[LastName] ASC, [FirstName] ASC", "IncludeColumns": "[Email], [Phone]" }
```

---

## Foreign Keys

Referential integrity lives in the table JSON alongside columns and indexes, not in separate migration scripts. The `ForeignKeys` array captures each relationship: the local columns, the related table they reference, and the optional cascade actions for deletes and updates. SchemaQuench reads the array, diffs it against the live database, and adds, drops, or recreates constraints to match.

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | | Constraint name. |
| `Columns` | string | | Comma-separated local column names. |
| `RelatedTableSchema` | string | platform default | Schema of the referenced table (`dbo` on SQL Server, `public` on PostgreSQL, omitted on MySQL). |
| `RelatedTable` | string | | Referenced table name. |
| `RelatedColumns` | string | | Comma-separated referenced column names. |
| `DeleteAction` | string | `null` | `"NO ACTION"`, `"CASCADE"`, `"SET NULL"`, `"SET DEFAULT"`, or `"RESTRICT"` where supported. |
| `UpdateAction` | string | `null` | Same values as `DeleteAction`. |
| `ShouldApplyExpression` | string | | Conditional inclusion. |
| `VariantName` | string | | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | `null` | Custom metadata. |

For composite foreign keys, list all columns in both `Columns` and `RelatedColumns` in matching order.

### FKs in schema templates

In a schema template, `RelatedTableSchema` is optional when the referenced table lives in the same iteration schema. If you omit it, SchemaQuench resolves it to `{{SchemaName}}` at runtime -- so `FK_Orders_Customer` with no `RelatedTableSchema` wires up correctly to `acme.Customers` during the `acme` iteration without any extra configuration.

For foreign keys that cross schema boundaries -- for example, a tenant's `Orders` table referencing `dbo.Countries` on SQL Server or `public.countries` on PostgreSQL -- set `RelatedTableSchema` explicitly to the target schema name. That explicit literal is preserved as-is through every iteration.

```jsonc
{
  "Name": "FK_Orders_Country",
  "Columns": "CountryCode",
  "RelatedTable": "Countries",
  "RelatedTableSchema": "[dbo]",
  "RelatedColumns": "Code"
}
```

For a complete cross-schema FK example with both same-iteration and cross-schema references in one table, see the [Multi-Tenant Deployments](../guide/10-multi-tenant-deployments.md#cross-schema-references) chapter.

---

## DataDelivery

Lookup tables, configuration rows, the reference data every environment needs to run -- that data is a schema-management problem as much as the tables themselves. The `DataDelivery` block declares how those rows land in the target database, alongside the table definition, so reference data travels with the schema it depends on.

Declare how a table's data gets merged into the target database and let SchemaQuench handle the rest. Add a `DataDelivery` block to the table JSON, point it at a `.tabledata` file, tell it what a "match" looks like, and SchemaQuench delivers the data in foreign-key order -- no hand-rolled merge scripts.

Tables without a `DataDelivery` block are left alone. Tables that declare one are picked up automatically during the data delivery step -- see [SchemaQuench -- Table Data Delivery](schemaquench.md#table-data-delivery) for the runtime behavior.

### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `ContentFile` | string | | Path to the row data, relative to the template root. Typically produced by DataTongs as a `.tabledata` file (raw JSON array). |
| `MergeType` | string | | One of `Insert`, `Insert/Update`, `Insert/Update/Delete`. See [MergeType](#mergetype) below. |
| `MatchColumns` | string | | Comma-separated column names that identify a row. Prefix a column with `*` for NULL-safe comparison on nullable keys. Matches the `KeyColumns` concept in DataTongs. |
| `MergeFilter` | string | `""` | Optional SQL `WHERE` clause (without the `WHERE` keyword). Scopes both the rows considered for matching and, when delete is enabled, the rows eligible for deletion. |
| `MergeDisableTriggers` | bool | `false` | Wrap the merge with platform-appropriate trigger disable/enable. |
| `MergeDisableRules` | bool | `false` | **PostgreSQL.** Disable rewrite rules on the table during the merge. |
| `MergeUpdateDescendents` | bool | `false` | **PostgreSQL.** When `true`, the merge targets descendant partitions as well as the specified table. When `false` (the default), the merge uses `ONLY` so descendant tables are left untouched. |

### MergeType

| Value | Behavior |
|---|---|
| `Insert` | Missing rows inserted. Existing rows and extra rows left alone. The seed-data pattern. |
| `Insert/Update` | Missing rows inserted, changed rows updated. Extra rows left alone. Good for reference tables where environments can append local rows. |
| `Insert/Update/Delete` | Full sync. Missing rows inserted, changed rows updated, and target rows that don't exist in the source data are deleted. The demo products use this. When `MergeFilter` is set, deletes are scoped by the filter so rows outside it are never removed. |

The chosen idiom is platform-specific -- `MERGE` on SQL Server and PostgreSQL, `INSERT ... ON DUPLICATE KEY UPDATE` with a conditional delete step on MySQL -- but the declarative contract is the same on every platform.

### FK-aware delivery

When multiple tables declare `DataDelivery`, SchemaQuench orders them by their declared foreign keys:

- **Pass 1** runs every table whose required (NOT NULL) FK parents are already loaded. Nullable FK columns that point to tables still awaiting delivery are deferred -- the pass-1 merge writes NULL into those columns and records the table for a second pass.
- **Pass 2** revisits each deferred table and back-fills the deferred columns with their actual values, now that every parent row exists.

This is automatic. You don't order the tables yourself; SchemaQuench computes the dependency graph from the `ForeignKeys` arrays in the table JSON. A cycle among NOT NULL foreign keys fails delivery with a clear log message -- break the cycle by making one side nullable, or separate the data load into explicit phases.

### Example

```json
{
  "Name": "[Employee]",
  "Schema": "HumanResources",
  "Columns": [
    { "Name": "[EmployeeID]",   "DataType": "INT",          "Identity": true, "Nullable": false },
    { "Name": "[ManagerID]",    "DataType": "INT",          "Nullable": true },
    { "Name": "[DepartmentID]", "DataType": "INT",          "Nullable": false },
    { "Name": "[FullName]",     "DataType": "NVARCHAR(100)","Nullable": false }
  ],
  "Indexes": [
    { "Name": "[PK_Employee]", "PrimaryKey": true, "Unique": true, "IndexColumns": "[EmployeeID]" }
  ],
  "ForeignKeys": [
    { "Name": "[FK_Employee_Manager]",    "Columns": "[ManagerID]",    "RelatedTable": "[Employee]",   "RelatedColumns": "[EmployeeID]" },
    { "Name": "[FK_Employee_Department]", "Columns": "[DepartmentID]", "RelatedTable": "[Department]", "RelatedColumns": "[DepartmentID]" }
  ],
  "DataDelivery": {
    "ContentFile": "data/HumanResources.Employee.tabledata",
    "MergeType": "Insert/Update",
    "MatchColumns": "[EmployeeID]",
    "MergeDisableTriggers": true
  }
}
```

The self-referential `ManagerID` is nullable, so pass 1 loads every employee with `ManagerID = NULL`, and pass 2 back-fills the manager chain once every row exists. The mandatory `DepartmentID` forces `Department` to deliver first.

### Generating DataDelivery blocks with DataTongs

You don't have to write these blocks by hand. Point DataTongs at a source database with `--ConfigureDataDelivery` and it writes the `DataDelivery` section into each table JSON, including the match columns and merge type -- see [DataTongs -- --ConfigureDataDelivery](datatongs.md#--configuredatadelivery).

---

## Check Constraints

Table-level check constraints in the `CheckConstraints` array. Used when `CheckConstraintStyle` is `"TableLevel"` in `Product.json`, or when a check constraint spans multiple columns.

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | | Constraint name. |
| `Expression` | string | | Boolean SQL expression. |
| `ShouldApplyExpression` | string | | Conditional inclusion. |
| `VariantName` | string | | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | `null` | Custom metadata. |

When `CheckConstraintStyle` is `"ColumnLevel"` (the default), single-column check constraints are written as `CheckExpression` on the column instead. Multi-column constraints always use the `CheckConstraints` array.

---

## XML Indexes (SQL Server)

XML index definitions in the `XmlIndexes` array. A primary XML index must be created before secondary XML indexes on the same column.

| Property | Type | Description |
|---|---|---|
| `Name` | string | Index name. |
| `IsPrimary` | bool | `true` for a PRIMARY XML INDEX. |
| `Column` | string | Name of the XML column being indexed. |
| `PrimaryIndex` | string | Name of the primary XML index. Required for secondary indexes. |
| `SecondaryIndexType` | string | `"VALUE"`, `"PATH"`, or `"PROPERTY"`. Required for secondary indexes. |
| `ShouldApplyExpression` | string | Conditional inclusion. |
| `VariantName` | string | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | Custom metadata. |

---

## Statistics (SQL Server / PostgreSQL)

Custom statistics definitions in the `Statistics` array. SQL Server uses traditional statistics; PostgreSQL uses extended statistics objects.

**SQL Server:**

| Property | Type | Description |
|---|---|---|
| `Name` | string | Statistics name. |
| `Columns` | string | Comma-separated column names. |
| `SampleSize` | byte (0--100) | Sampling percentage. `0` means default sampling. |
| `FilterExpression` | string | Filtered statistics WHERE clause. |
| `ShouldApplyExpression` | string | Conditional inclusion. |
| `VariantName` | string | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | Custom metadata. |

**PostgreSQL extended statistics** include kinds like `ndistinct`, `dependencies`, and `mcv`. The PostgreSQL statistic object adds `Kinds` and `Schema` fields.

---

## Full-Text Index (SQL Server)

SQL Server allows one full-text index per table. Declare it as a single `FullTextIndex` object -- or as an **array of conditional variants** when different targets need different definitions, such as a different full-text catalog per region. At deploy time, each variant's `ShouldApplyExpression` runs against the target and the matching variant deploys.

| Property | Type | Description |
|---|---|---|
| `FullTextCatalog` | string | Name of the full-text catalog. |
| `KeyIndex` | string | Name of the unique index used as the full-text key. |
| `ChangeTracking` | string | `"OFF"`, `"MANUAL"`, or `"AUTO"`. |
| `StopList` | string | Name of a full-text stop list. |
| `Columns` | string | Column specification for the full-text index. |
| `ShouldApplyExpression` | string | Boolean SQL expression evaluated on the target; the index (or variant) applies only when it is true. |
| `VariantName` | string | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | Custom metadata. |

**Variant rules:**

- **One match per target.** With more than one variant, *every* variant must declare a `ShouldApplyExpression`, and those expressions must be mutually exclusive on any given target. Two variants matching the same target fails the deployment with a clear error.
- **No match means none.** When no variant matches a target, the table behaves as if no full-text index were declared there -- any existing full-text index is removed.
- **No-op when unchanged.** When the deployed index already matches the selected variant, re-deployment performs no full-text work: no drop, no repopulation.

> **Note:** When no variant matches a target, an existing full-text index on that table is dropped -- the absence of a match is treated as "no full-text index here," not "leave it alone."

```json
"FullTextIndex": [
  {
    "FullTextCatalog": "[Catalog_ProdEast]",
    "KeyIndex": "[PK_Documents]",
    "Columns": "[Title],[Body]",
    "ShouldApplyExpression": "EXISTS (SELECT 1 FROM dbo.RegionConfig WHERE Region = 'East')"
  },
  {
    "FullTextCatalog": "[Catalog_ProdWest]",
    "KeyIndex": "[PK_Documents]",
    "Columns": "[Title],[Body]",
    "ShouldApplyExpression": "EXISTS (SELECT 1 FROM dbo.RegionConfig WHERE Region = 'West')"
  }
]
```

---

## Full-Text Indexes (MySQL)

MySQL allows **multiple** full-text indexes per table, so the property is `FullTextIndexes` (an array).

| Property | Type | Description |
|---|---|---|
| `Name` | string | Index name. |
| `Columns` | string | Comma-separated column names. |
| `Parser` | string | Optional parser name (e.g., `"ngram"`). |
| `Comment` | string | Index comment. |
| `ShouldApplyExpression` | string | Conditional inclusion. |
| `VariantName` | string | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | Custom metadata. |

---

## Exclude Constraints (PostgreSQL)

PostgreSQL exclusion constraints are declared in the `ExcludeConstraints` array.

| Property | Type | Description |
|---|---|---|
| `Name` | string | Constraint name. |
| `AccessMethod` | string | Index access method backing the constraint (e.g., `"gist"`). |
| `ExcludeColumns` | array | One or more `{ "Column", "Operator" }` pairs. |
| `FilterExpression` | string | Optional WHERE clause. |
| `ShouldApplyExpression` | string | Conditional inclusion. |
| `VariantName` | string | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Deferrable` | bool | Whether the constraint is deferrable. |
| `InitiallyDeferred` | bool | Whether the constraint defers by default. |
| `Extensions` | object | Custom metadata. |

### Example -- non-overlapping reservation periods

```json
{
  "Name": "no_overlapping_reservations",
  "AccessMethod": "gist",
  "ExcludeColumns": [
    { "Column": "room_id",        "Operator": "="  },
    { "Column": "reserved_period", "Operator": "&&" }
  ]
}
```

---

## Indexed View JSON Format (SQL Server)

Indexed views are defined as JSON files in the `Indexed Views/` directory of each template. Each file defines one indexed view.

| Property | Type | Description |
|---|---|---|
| `Name` | string | View name. |
| `Schema` | string | Schema name. Defaults to `"dbo"`. |
| `Definition` | string | The complete view definition SQL (the SELECT statement). |
| `Indexes` | array | Indexes on the view. An indexed view must have a unique clustered index. |
| `ShouldApplyExpression` | string | Conditional inclusion. |
| `VariantName` | string | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | Custom metadata. |

### Example

```json
{
  "Name": "[vw_ProductInventory]",
  "Schema": "[Production]",
  "Definition": "SELECT p.ProductID, p.Name, SUM(i.Quantity) AS TotalQuantity, COUNT_BIG(*) AS CountRows FROM Production.Product p INNER JOIN Production.ProductInventory i ON p.ProductID = i.ProductID GROUP BY p.ProductID, p.Name",
  "Indexes": [
    {
      "Name": "[IX_vw_ProductInventory]",
      "Unique": true,
      "Clustered": true,
      "IndexColumns": "[ProductID]"
    }
  ]
}
```

---

## Materialized View JSON Format (PostgreSQL)

Materialized views live in the `Materialized Views/` directory of each template.

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | | View name. |
| `Schema` | string | `"public"` | Schema name. |
| `Definition` | string | | The complete view definition SQL. |
| `WithData` | bool | `true` | Whether the materialized view is populated immediately. Set to `false` for `WITH NO DATA`. |
| `Tablespace` | string | `null` | Optional tablespace. |
| `AccessMethod` | string | `null` | Storage access method. |
| `Indexes` | array | `[]` | Indexes on the materialized view (uses the PostgreSQL index shape). |
| `ShouldApplyExpression` | string | | Conditional inclusion. |
| `VariantName` | string | | Optional label for a conditional variant. Appears in deployment log messages when the variant applies, and documents the intent behind the `ShouldApplyExpression`. Max 128 characters. |
| `Extensions` | object | | Custom metadata. |

### Example

```json
{
  "Name": "active_orders",
  "Schema": "public",
  "Definition": "SELECT order_id, customer_id, order_date, total FROM public.orders WHERE status = 'active'",
  "WithData": true,
  "Indexes": [
    {
      "Name": "ux_active_orders_id",
      "Unique": true,
      "IndexColumns": "order_id"
    }
  ]
}
```

A materialized view that should be skipped for non-production environments is one `ShouldApplyExpression` away.

---

## Conditional Application

Dev uses synthetic data and a lean index set. Staging mirrors production's footprint. Production carries the full reporting stack and the regulated columns. Without a declarative answer, those differences turn into per-environment file copies, branching pipelines, or hand-maintained deploy scripts -- the kind of drift that's fine until a column gets forgotten. `ShouldApplyExpression` is that declarative answer: a SQL fragment attached to a component that decides, at deployment time, whether the component should apply to the current target.

`ShouldApplyExpression` is the universal opt-in/opt-out switch on every component that supports it. The expression is a SQL fragment that returns a single scalar value. When SchemaQuench evaluates it before deploying that component, it considers the result false if it's `0`, the literal string `false`, or empty/null. Anything else means apply the component normally.

```json
{
  "Name": "[Orders]",
  "Extensions": { "Environment": "Production" },
  "Indexes": [
    {
      "Name": "[IX_Orders_Reporting]",
      "IndexColumns": "[ReportingDate]",
      "ShouldApplyExpression": "'{{Table.Environment}}' = 'Production'"
    }
  ]
}
```

The same property is available on tables, columns, indexes, foreign keys, check constraints, indexed views, materialized views, statistics, exclude constraints, full-text indexes, and XML indexes. Combined with [Custom Properties](custom-properties.md) and the rest of the [Script Tokens](script-tokens.md) feature surface, you can express deployment-time decisions declaratively without ever writing a separate per-environment script file.

---

## .json-schemas Folder

Your IDE can help you write correct JSON if you point it at the right schemas. The `.json-schemas/` directory at the package root contains JSON Schema definition files generated automatically by SchemaTongs **on the fly** from the live C# domain types -- no embedded files, no shipped artifacts, just a snapshot of the engine's exact current shape.

Each file carries a platform infix matching the package's platform -- `<platform>` is `sqlserver`, `postgresql`, or `mysql`:

| File | Validates |
|---|---|
| `products.<platform>.schema` | `Product.json` |
| `templates.<platform>.schema` | `Template.json` |
| `tables.<platform>.schema` | Table JSON files (`Tables/*.json`) |
| `indexedviews.sqlserver.schema` | Indexed view JSON files (SQL Server packages) |
| `materializedviews.postgresql.schema` | Materialized view JSON files (PostgreSQL packages) |

Because the schemas are regenerated every time SchemaTongs writes a package, they always match the current engine. If you've hand-edited any of them to add a custom validation fragment under `Extensions`, that fragment is preserved through regeneration -- see [Custom Properties: JSON Schema Validation](custom-properties.md#json-schema-validation).

---

## ZIP Package Support

SchemaQuench can consume schema packages as ZIP archives. When the `SchemaPackagePath` configuration value points to a `.zip` file, SchemaQuench reads the package directly from the archive without extracting it to disk first.

Requirements:

- The ZIP must contain the standard schema package folder structure.
- The `Product.json` file must be at the root of the archive (not nested inside an extra directory).
- All relative paths within the package must match the standard layout described in [Complete Folder Structure](#complete-folder-structure).

This is useful for deployment pipelines where the schema package is built as a single artifact.

---

## Filesystem-Illegal Character Encoding

Object names can contain characters that are illegal in file paths on Windows, macOS, or Linux. SchemaTongs uses a percent-encoding scheme to safely map these names to filenames.

| Character | Encoded As |
|---|---|
| `\` | `%5C` |
| `/` | `%2F` |
| `:` | `%3A` |
| `*` | `%2A` |
| `?` | `%3F` |
| `"` | `%22` |
| `<` | `%3C` |
| `>` | `%3E` |
| `\|` | `%7C` |
| `%` | `%25` |

**Additional rules:**

- **Leading spaces and dots** are encoded (`%20` for space, `%2E` for dot) because many filesystems strip or reject them at the start of filenames.
- **Trailing spaces and dots** are similarly encoded.
- **Reserved Windows device names** (CON, PRN, AUX, NUL, COM1--COM9, LPT1--LPT9) have their first character percent-encoded.

SchemaQuench decodes these filenames transparently when reading definitions. You generally don't need to worry about encoding unless you're creating JSON files by hand for tables with unusual names.

---

## Schema Templates

Schema templates fan a single declarative template out across multiple schemas inside one database. You write the template once -- tables, procedures, views, migration scripts -- and SchemaQuench runs it once per schema returned by the `SchemaIdentificationScript` query, injecting the active schema name as the `{{SchemaName}}` token at every step. The most common use is multi-tenant SaaS where each tenant owns their own schema, but any pattern that needs the same object shape replicated across schemas works the same way. For a full narrative walkthrough, see [Multi-Tenant Deployments](../guide/10-multi-tenant-deployments.md).

Schema templates are supported on **SQL Server and PostgreSQL only**. MySQL uses a database-per-tenant model instead -- there is no sub-database schema namespace to fan out across.

### Discovery query

`SchemaIdentificationScript` is the mode switch. When this field is present and non-empty on a `Template.json`, the template becomes a schema template. The value is a SQL query that returns one column and any number of rows; each row is a schema name, and SchemaQuench runs the full template once per returned row.

| Property | Type | Default | Description |
|---|---|---|---|
| `SchemaIdentificationScript` | string | not present | Query returning one column, N rows. Each row is a schema name for one iteration. |

The query runs against each target database identified by `DatabaseIdentificationScript`. If both are present, the engine computes the full cross-product: every `(database, schema)` pair runs as an independent iteration. Token replacement applies to the query body before execution, so you can reference `{{ScriptTokens}}` or `<*Query*>` tokens in the discovery query itself.

The active schema name is available to every part of the iteration as `{{SchemaName}}` -- in table `Name` and `Schema` fields, in procedure and view SQL bodies, in migration script filenames, in `VersionStampScript`, and in user-defined script tokens. See [{{SchemaName}}](script-tokens.md#schemaname) in the Script Tokens reference for availability rules and resolution timing.

#### Reserved schema names

A small set of platform-built-in schemas can't be used as iteration targets. If your discovery query returns one of these names, the engine fails the iteration with an error naming the offending schema and pointing you at the "shared content lives in a regular template" remediation. The reserved sets are:

- **SQL Server:** `dbo`, `sys`, `INFORMATION_SCHEMA`, `guest`, plus the fixed database roles that double as schemas (`db_owner`, `db_accessadmin`, `db_securityadmin`, `db_ddladmin`, `db_backupoperator`, `db_datareader`, `db_datawriter`, `db_denydatareader`, `db_denydatawriter`).
- **PostgreSQL:** `public`, `pg_catalog`, `pg_toast`, `information_schema`, plus any schema matching the `pg_temp_*` or `pg_toast_temp_*` wildcards (Postgres uses these for session-scoped temp objects).

Shared content (lookup tables, audit logs, dimension data) belongs in a regular template that runs once per database, not in a schema-template iteration. The reserved-name guard is the engine's way of catching a discovery query that accidentally returns `public` or `dbo` instead of a real tenant schema.

### Auto-create schemas

| Property | Type | Default | Description |
|---|---|---|---|
| `CreateSchemaIfMissing` | bool | `false` | When `true`, the engine emits `CREATE SCHEMA` for any schema returned by `SchemaIdentificationScript` that does not yet exist on the target database. |

When `false` (the default), an iteration whose schema doesn't exist fails immediately with a clear error. This is the safer behavior: a typo in your discovery query should not silently create schemas in production.

> **Warning:** `CreateSchemaIfMissing: true` requires the deployment user to have `CREATE SCHEMA` permission on SQL Server or `CREATE` on the database on PostgreSQL. The default `false` is intentional fail-fast: if the discovery query returns an unexpected schema name, you want an error, not a new schema.

Set `CreateSchemaIfMissing: true` when you're running a fully automated onboarding pipeline and the deployment user is trusted to create schemas. Many teams prefer to create schemas via an explicit stored procedure (`OnboardTenant`) in the Shared template and leave this `false`.

### AllowParallel

| Property | Type | Default | Description |
|---|---|---|---|
| `AllowParallel` | bool | `true` | When `false`, iterations of this schema template run serially regardless of the global `MaxThreads` setting. |

When `true` (the default), schema iterations can run in parallel up to the `MaxThreads` limit alongside iterations from other templates and databases. Parallel execution is safe: each iteration touches its own schema namespace and converges independently.

> **Tip:** Set `AllowParallel: false` to force this template's iterations to run one at a time -- useful to cap concurrent load on a resource-constrained target, or when the template's own migration scripts perform DDL that can't run concurrently. Parallel iteration is otherwise the production-realistic default; both TenantCRM demos ship `AllowParallel: true`.

### Failure isolation

| Property | Type | Default | Description |
|---|---|---|---|
| `ContinueOnSchemaFailure` | bool | `true` | When `false`, the first failing schema iteration stops the dispatcher: no new iterations start, in-flight iterations drain, and subsequent templates in `TemplateOrder` do not run. |

When `true` (the default), a single schema iteration's failure does not abort the others -- remaining iterations continue and the product run exits non-zero after all iterations have completed or failed. This matches the database-level isolation behavior that most teams already rely on.

Set `ContinueOnSchemaFailure: false` for deployments where any single-tenant failure is a hard stop -- for example, a CI environment where partial deployment is worse than no deployment.

For the execution-flow detail and interaction with `MaxThreads`, see [ContinueOnSchemaFailure](schemaquench.md#continueonschemafailure) in the SchemaQuench reference.

### Complete example

This is the `TenantWorkspace/Template.json` from the SQL Server TenantCRM demo -- a real schema template with all four fields declared:

```json
{
  "Name": "TenantWorkspace",
  "DatabaseIdentificationScript": "SELECT [name] FROM master.sys.databases WHERE [name] = '{{TenantCRMDb}}'",
  "SchemaIdentificationScript": "SELECT [Name] FROM dbo.Tenants WHERE [Status] = N'Active' ORDER BY [Name]",
  "RequireAtLeastOneTarget": false,
  "CreateSchemaIfMissing": false,
  "AllowParallel": true,
  "ContinueOnSchemaFailure": true,
  "VersionStampScript": "PRINT 'TenantCRM TenantWorkspace [{{SchemaName}}] {{ReleaseVersion}}'",
  "ScriptFolders": [
    { "FolderPath": "Before Scripts", "QuenchSlot": "Before" },
    { "FolderPath": "Functions",      "QuenchSlot": "Objects", "ObjectType": "Functions" },
    { "FolderPath": "Views",          "QuenchSlot": "Objects", "ObjectType": "Views" },
    { "FolderPath": "Procedures",     "QuenchSlot": "Objects", "ObjectType": "Procedures" },
    { "FolderPath": "Triggers",       "QuenchSlot": "Objects", "ObjectType": "Triggers" }
  ]
}
```

`RequireAtLeastOneTarget: false` here handles a fresh installation where no tenants have been onboarded yet -- the schema template finds zero rows, treats it as a no-op, and the product run succeeds so the `Initialize` and `Shared` templates still complete. The PostgreSQL TenantCRM demo is identical in structure and ships `AllowParallel: true`, so tenants deploy concurrently.

---

## Related Documentation

- [Custom Properties](custom-properties.md) -- The `Extensions` carrier and how to attach metadata to any component
- [Script Tokens Reference](script-tokens.md) -- Token replacement, advanced tags, automatic tokens
- [Configuration Reference](configuration.md) -- Settings files for SchemaQuench, SchemaTongs, and DataTongs
- [SchemaTongs Reference](schematongs.md) -- Extraction tool that creates schema packages
- [SchemaQuench Reference](schemaquench.md) -- Deployment tool that applies schema packages
- [Core Concepts](../guide/03-core-concepts.md) -- Conceptual overview of products, templates, and the deployment model
- [Defining Your Schema](../guide/04-defining-your-schema.md) -- Practical walkthrough of creating table JSON
