# Script Tokens Reference

Applies to: SchemaQuench, SchemaTongs, DataTongs — SQL Server, PostgreSQL, and MySQL.

---

Script tokens are how one schema package becomes ten environments. Define a token once, reference it everywhere, override it per environment without touching a script file. The simple form is just two curly braces and a name -- but tokens go far beyond that. They can pull in file contents, execute server-side queries to generate values at deployment time, embed entire table schemas as JSON, and surface custom metadata you've attached through `Extensions`. One feature, many superpowers, all in the free toolset.

## Token Syntax

The basic form:

```
{{TokenName}}
```

Token replacement is **case-insensitive**. These all resolve to the same value:

```sql
SELECT * FROM [{{MainDB}}].dbo.Customers
SELECT * FROM [{{maindb}}].dbo.Customers
SELECT * FROM [{{MAINDB}}].dbo.Customers
```

Tokens that appear in scripts but have no matching definition are left in place unchanged -- there's no silent corruption from an unresolved token.

---

## Where Tokens Are Resolved

Tokens are replaced in every place SchemaSmith processes script content -- both in JSON expression fields and in `.sql` files.

**Product-level JSON properties:**

- `ValidationScript`
- `BaselineValidationScript`
- `VersionStampScript`

**Template-level JSON properties:**

- `DatabaseIdentificationScript`
- `VersionStampScript`
- `BaselineValidationScript`

**Table-component expression fields** (table JSON files):

- `ShouldApplyExpression` on tables, columns, indexes, foreign keys, check constraints, indexed views, materialized views, and other supported components
- `Default` on columns
- `CheckExpression` on columns
- `Expression` on table-level check constraints
- `FilterExpression` on indexes (where supported)

**SQL script files** in every product and template script folder:

| Folder | Scope |
|---|---|
| `Before Scripts/` (template-level) | Template |
| `Schemas/` | Template |
| `DataTypes/` (SQL Server) / `Domain Types/`, `Enum Types/`, `Composite Types/` (PostgreSQL) / equivalents | Template |
| `Functions/`, `Views/`, `Procedures/` | Template |
| `Triggers/`, `DDLTriggers/` | Template |
| `Table Data/` | Template |
| `After Scripts/` (template-level) | Template |
| `Before Product/`, `After Product/` | Product |
| Any custom script folders defined in `Template.json` `ScriptFolders` | Template |

The exact set of default folders depends on the product's `Platform`. Anything you add via custom `ScriptFolders` participates in token resolution exactly the same way as the defaults.

---

## Product Tokens

Define tokens in `Product.json` under `ScriptTokens`. These are available across every template and product-level script.

```json
{
  "Name": "SaasProduct",
  "TemplateOrder": ["Registry", "Client"],
  "ScriptTokens": {
    "RegistryDb": "Registry",
    "MigrationVersion": "1.0.1"
  }
}
```

In this example, `{{RegistryDb}}` resolves to `Registry` everywhere -- in every template's scripts and in every SQL file across the product.

---

## Template Tokens

Define tokens in `Template.json` under `ScriptTokens`. Template tokens override product tokens with the same key for the duration of that template's execution.

```json
{
  "Name": "Reporting",
  "ScriptTokens": {
    "MainDB": "ReportingAlias",
    "SchemaOwner": "rpt"
  }
}
```

Within the Reporting template, `{{MainDB}}` resolves to `ReportingAlias` regardless of what the product defined. The `{{SchemaOwner}}` token is available only inside this template.

---

## Automatic Tokens

SchemaSmith adds these tokens automatically. You don't define them -- they appear when relevant.

| Token | Value | Available in |
|---|---|---|
| `{{ProductName}}` | The `Name` from `Product.json` | All product and template scripts |
| `{{TemplateName}}` | The `Name` from the current `Template.json` | Template scripts only |
| `{{TableSchema}}` | Full serialized JSON of every table in the current template, with single quotes escaped (`''`) for safe embedding in SQL string literals | Template scripts |
| `{{IndexedViewSchema}}` | Full serialized JSON of every indexed view in the current template (SQL Server) | Template scripts |
| `{{MaterializedViewSchema}}` | Full serialized JSON of every materialized view in the current template (PostgreSQL) | Template scripts |
| `{{TableSchema_<TemplateName>}}` | Same as `{{TableSchema}}` but reaches across templates -- name another template explicitly to read its table set | Any template |
| `{{IndexedViewSchema_<TemplateName>}}` | Cross-template indexed view JSON | Any template |
| `{{MaterializedViewSchema_<TemplateName>}}` | Cross-template materialized view JSON | Any template |
| `{{ObjectScripts_<TemplateName>}}` | Cross-template inventory of programmable object scripts (functions, views, procedures, triggers) | Any template |
| `{{QueryTokens_<TemplateName>}}` | Cross-template inventory of query-style tokens for sharing | Any template |

**Why this matters.** `{{TableSchema}}` is the entire current template's table model serialized as JSON, ready to drop into a stored procedure parameter, a `JSON_VALUE`/`json_each`/`JSON_TABLE` query, or an audit row. Combined with the [Custom Properties](custom-properties.md) feature, you can write a single migration script that introspects your table definitions and reads your team's custom metadata at deployment time -- without ever leaving SchemaSmith.

The cross-template variants (`{{TableSchema_OtherTemplate}}`) are how a deployment script in one template can read the schema of another. This unlocks coordination between linked templates without copy-pasting JSON.

### Custom property tokens

Anything you put in an `Extensions` object on a table component is also available as a token in that component's expression fields. Bare names from the component's own Extensions, `Table.`-prefixed names from the parent table. See [Custom Properties](custom-properties.md) for the full mechanism.

```json
{
  "Name": "[Orders]",
  "Extensions": { "Environment": "Production" },
  "Indexes": [
    {
      "Name": "[IX_Orders_CreatedDate]",
      "IndexColumns": "[CreatedDate]",
      "ShouldApplyExpression": "SELECT CASE WHEN '{{Table.Environment}}' = 'Production' THEN 1 ELSE 0 END"
    }
  ]
}
```

---

## Advanced Token Tags

Here's where tokens go from "find and replace" to "deployment-time content engine." A token's *value* in `ScriptTokens` can start with a special tag that tells SchemaSmith to resolve it at deployment time -- by reading a file, by querying the live database, or by embedding a serialized object's JSON. The token name in your scripts stays a plain `{{TokenName}}`; the magic happens in how the value is computed before substitution.

The advanced tags:

| Tag | Purpose |
|---|---|
| `<*File*>relative\path\file.sql` | Replace the token value with the contents of a text file, resolved relative to the product directory |
| `<*BinaryFile*>relative\path\image.png` | Replace the token value with the file contents as a platform-appropriate binary literal -- `0x<hex>` for SQL Server and MySQL, `E'\\x<hex>'::bytea` for PostgreSQL |
| `<*Query*>SELECT ... FROM ...` | Execute a SQL query against the deployment target before substitution and replace the token value with the first column's rows joined by newlines |
| `<*QueryFile*>relative\path\query.sql` | Same as `<*Query*>` but the query body is loaded from a file first |
| `<*SpecificTable*>schema.tablename` | Replace the token value with the full serialized JSON of one specific table in the current template |
| `<*SpecificIndexedView*>schema.viewname` | Same as `<*SpecificTable*>` but for SQL Server indexed views |
| `<*SpecificMaterializedView*>schema.viewname` | Same as `<*SpecificTable*>` but for PostgreSQL materialized views |

All resolution happens automatically. Your SQL scripts just see `{{TokenName}}` and the right value lands there at deployment time.

### Example — embed a file's contents

You have a long stored-procedure body or a static reference dataset stored in a `.sql` file outside your script folders, and you want to drop its contents into a migration script verbatim:

```json
{
  "Name": "DataMigration",
  "ScriptTokens": {
    "ReferenceData": "<*File*>resources/reference-data.sql"
  }
}
```

In any script in this template:

```sql
-- The next line will be replaced with the entire contents of resources/reference-data.sql
{{ReferenceData}}
```

### Example — embed binary content as hex

For test images, certificate blobs, signing keys -- anything you need to inject as binary into a column at deployment time:

```json
{
  "ScriptTokens": {
    "DefaultLogo": "<*BinaryFile*>resources/logo.png"
  }
}
```

```sql
INSERT dbo.BrandAssets(Name, Image) VALUES('Default', {{DefaultLogo}});
```

The token resolves to the platform-appropriate binary literal form automatically, chosen from the product's `Platform`:

- **SQL Server** — `0x89504E47...` (`VARBINARY` literal)
- **MySQL** — `0x89504E47...` (`BLOB` literal)
- **PostgreSQL** — `E'\\x89504E47...'::bytea` (`BYTEA` literal with escape-string + explicit cast)

The same `<*BinaryFile*>` token works across all three engines with no per-environment editing. The resolver reads the file once and emits the correct literal form for the target; your SQL script just sees `{{DefaultLogo}}` land as whatever that engine will accept.

### Example — query the target server for a value

Your deployment script needs a value from the target database itself -- a tenant ID, a feature flag, the next batch number, the result of a row count check. Resolve it at deployment time, before any of your scripts run, against the actual server you're deploying to.

**SQL Server:**

```json
{
  "ScriptTokens": {
    "ActiveTenants": "<*Query*>SELECT TenantId FROM dbo.Tenants WHERE Active = 1"
  }
}
```

```sql
-- After resolution, {{ActiveTenants}} contains one tenant ID per line
EXEC dbo.ProvisionAuditTables @TenantIdList = '{{ActiveTenants}}';
```

**PostgreSQL:**

```json
{
  "ScriptTokens": {
    "ActiveTenants": "<*Query*>SELECT tenant_id FROM public.tenants WHERE active = true"
  }
}
```

```sql
-- After resolution, {{ActiveTenants}} contains one tenant ID per line
CALL public.provision_audit_tables('{{ActiveTenants}}');
```

**MySQL:**

```json
{
  "ScriptTokens": {
    "ActiveTenants": "<*Query*>SELECT `TenantId` FROM `Tenants` WHERE `Active` = 1"
  }
}
```

```sql
-- After resolution, {{ActiveTenants}} contains one tenant ID per line
SET @TenantIdList = '{{ActiveTenants}}';
CALL ProvisionAuditTables(@TenantIdList);
```

### Example — query body in a file

Long queries stay readable when they live in their own files. The token value points at the file; the file contents become the query.

```json
{
  "ScriptTokens": {
    "DriftReport": "<*QueryFile*>queries/drift-report.sql"
  }
}
```

### Example — embed one table's JSON

When you only need *one* table's metadata in a script -- not the whole template -- the specific-table tag is the surgical option. The token value names one table; the resolved content is that table's full JSON, ready to hand to a stored procedure that introspects columns, indexes, or custom metadata.

**SQL Server:**

```json
{
  "ScriptTokens": {
    "OrdersTable": "<*SpecificTable*>dbo.Orders"
  }
}
```

```sql
DECLARE @TableJson NVARCHAR(MAX) = '{{OrdersTable}}';
EXEC dbo.GenerateAuditTriggerForTable @TableJson;
```

**PostgreSQL:**

```json
{
  "ScriptTokens": {
    "OrdersTable": "<*SpecificTable*>public.orders"
  }
}
```

```sql
DO $$
DECLARE
  v_table_json TEXT := '{{OrdersTable}}';
BEGIN
  CALL public.generate_audit_trigger_for_table(v_table_json);
END $$;
```

**MySQL:**

```json
{
  "ScriptTokens": {
    "OrdersTable": "<*SpecificTable*>Orders"
  }
}
```

```sql
SET @TableJson = '{{OrdersTable}}';
CALL GenerateAuditTriggerForTable(@TableJson);
```

The same pattern works for indexed views (`<*SpecificIndexedView*>`, SQL Server) and materialized views (`<*SpecificMaterializedView*>`, PostgreSQL).

### Resolution order for advanced tags

1. File tags (`<*File*>`, `<*BinaryFile*>`, `<*QueryFile*>`) are resolved first, against the product directory.
2. Specific-object tags (`<*SpecificTable*>`, `<*SpecificIndexedView*>`, `<*SpecificMaterializedView*>`) are resolved against the loaded template.
3. Query tags (`<*Query*>`, `<*QueryFile*>` after its file is loaded) are deferred until just before each script runs and execute against the open deployment connection.
4. After all resolution, the remaining `{{TokenName}}` substitutions are performed in your script content.

If a file is missing, a query fails, or a specific table can't be found, the deployment **stops with a clear error** -- you find out at the start, not after a half-applied database. Safe by default.

---

## Config-Level Overrides

This is where tokens earn their keep. Override product token values in a settings file without modifying the schema package. Same package, different environments, different values.

Add a `ScriptTokens` section to your tool's settings file (e.g., `SchemaQuench.settings.json`):

```json
{
  "Target": {
    "Server": "staging-server"
  },
  "ScriptTokens": {
    "RegistryDb": "Registry_Staging",
    "MigrationVersion": "1.0.1-rc1"
  }
}
```

Config overrides only apply to tokens that **already exist** in `Product.json`. You can't introduce new tokens via configuration alone -- the package declares the contract, the environment fills in the values.

---

## Environment Variable Tokens

Override product token values using environment variables. The naming pattern is:

```
SmithySettings_ScriptTokens__TokenName=Value
```

Note the prefix `SmithySettings_` (single underscore) and the double underscore `__` before the token name. This follows the .NET configuration environment variable convention.

```bash
# Linux/macOS
export SmithySettings_ScriptTokens__RegistryDb="Registry_CI"
export SmithySettings_ScriptTokens__MigrationVersion="1.0.1-ci.42"

# Windows (cmd)
set SmithySettings_ScriptTokens__RegistryDb=Registry_CI
set SmithySettings_ScriptTokens__MigrationVersion=1.0.1-ci.42

# Windows (PowerShell)
$env:SmithySettings_ScriptTokens__RegistryDb = "Registry_CI"
$env:SmithySettings_ScriptTokens__MigrationVersion = "1.0.1-ci.42"
```

Like config-level overrides, environment variable overrides only apply to tokens that already exist in `Product.json`.

---

## Token Resolution Order

When the same token name appears in multiple places, the most specific definition wins. Tokens resolve in layers, from lowest to highest priority:

| Priority | Source | Scope |
|---|---|---|
| 1 (lowest) | `Product.json` `ScriptTokens` | All scripts |
| 2 | Settings file `ScriptTokens` section | Overrides matching product keys |
| 3 | Environment variables (`SmithySettings_ScriptTokens__*`) | Overrides matching product keys |
| 4 (highest) | `Template.json` `ScriptTokens` | Template scripts only |

**How it works step by step:**

1. Product tokens are loaded from `Product.json`.
2. Config file and environment variable overrides replace matching product token values. (Steps 2 and 3 are handled together by the .NET configuration layering.)
3. The automatic `ProductName` token is added.
4. When each template loads, its `ScriptTokens` are merged on top of the resolved product tokens. Template tokens with matching keys win.
5. The automatic `TemplateName`, `TableSchema`, `IndexedViewSchema`, and `MaterializedViewSchema` tokens are added.
6. Cross-template `*_<TemplateName>` tokens are added once all templates have loaded.
7. Custom property tokens from `Extensions` are merged in per component when the table model is processed.
8. Advanced tag values (`<*File*>`, `<*Query*>`, etc.) resolve as described above, then the simple `{{TokenName}}` substitution runs against the final token map.

---

## Practical Examples

### Multi-environment deployment

A SaaS product uses tokens to manage database names and version stamps across development, staging, and production.

**Product.json** defines the baseline:

```json
{
  "Name": "SaasProduct",
  "TemplateOrder": ["Registry", "Client"],
  "ScriptTokens": {
    "RegistryDb": "Registry",
    "MigrationVersion": "3.2.0"
  }
}
```

**A migration script** announces the deployment target using the resolved tokens. The token shape is identical across platforms; only the logging statement differs.

**SQL Server:**

```sql
PRINT 'Deploying {{ProductName}} {{MigrationVersion}} against {{RegistryDb}}';
```

**PostgreSQL:**

```sql
DO $$ BEGIN
  RAISE NOTICE 'Deploying {{ProductName}} {{MigrationVersion}} against {{RegistryDb}}';
END $$;
```

**MySQL:**

```sql
SELECT 'Deploying {{ProductName}} {{MigrationVersion}} against {{RegistryDb}}' AS deployment_banner;
```

For staging, drop a `ScriptTokens.RegistryDb` override into `SchemaQuench.settings.json` and ship the same package. For CI, do the same via `SmithySettings_ScriptTokens__RegistryDb`. The schema package is unchanged across all three environments; only the resolved token values differ.

### Cross-database references with multiple tokens

When templates need to reference sibling schema managed by other templates, product-level tokens keep the references consistent. The token pattern is the same across platforms; the naming the tokens encode differs because each engine isolates differently -- SQL Server and MySQL use separate databases, PostgreSQL uses schemas within one database.

**SQL Server** -- separate databases, three-part names:

```json
{
  "Name": "ECommerce",
  "TemplateOrder": ["Catalog", "Orders", "Reporting"],
  "ScriptTokens": {
    "CatalogDb": "ProductCatalog",
    "OrdersDb": "OrderProcessing",
    "ReportDb": "Analytics"
  }
}
```

```sql
CREATE OR ALTER VIEW dbo.SalesSummary AS
SELECT o.OrderDate, p.ProductName, o.Quantity, o.Total
FROM [{{OrdersDb}}].dbo.Orders o
JOIN [{{CatalogDb}}].dbo.Products p ON o.ProductId = p.Id;
```

**PostgreSQL** -- one database, separate schemas:

```json
{
  "Name": "ECommerce",
  "TemplateOrder": ["Catalog", "Orders", "Reporting"],
  "ScriptTokens": {
    "CatalogSchema": "product_catalog",
    "OrdersSchema": "order_processing",
    "ReportSchema": "analytics"
  }
}
```

```sql
CREATE OR REPLACE VIEW analytics.sales_summary AS
SELECT o.order_date, p.product_name, o.quantity, o.total
FROM {{OrdersSchema}}.orders o
JOIN {{CatalogSchema}}.products p ON o.product_id = p.id;
```

**MySQL** -- separate databases, db-qualified names:

```json
{
  "Name": "ECommerce",
  "TemplateOrder": ["Catalog", "Orders", "Reporting"],
  "ScriptTokens": {
    "CatalogDb": "product_catalog",
    "OrdersDb": "order_processing",
    "ReportDb": "analytics"
  }
}
```

```sql
CREATE OR REPLACE VIEW `analytics`.`SalesSummary` AS
SELECT o.`OrderDate`, p.`ProductName`, o.`Quantity`, o.`Total`
FROM `{{OrdersDb}}`.`Orders` o
JOIN `{{CatalogDb}}`.`Products` p ON o.`ProductId` = p.`Id`;
```

> **PostgreSQL note:** PG can't join across separate databases natively -- the idiomatic pattern is one database with multiple schemas, and SchemaSmith deploys each template against its own schema within that database. Cross-database queries via `postgres_fdw` or `dblink` are possible but add setup that's out of scope for most deployments; if you need them, the token approach still works -- define foreign-server tokens and reference them the same way.

### Generating audit DDL from `{{TableSchema}}`

A migration script that consumes the live table model to drive its own logic. The stored procedure walks the JSON, reads each table's columns and any `Extensions.AuditScope` you've attached, and emits the right `CREATE TRIGGER` statements -- one declarative source of truth, one runtime that adapts to it.

**SQL Server:**

```sql
DECLARE @TableSchema NVARCHAR(MAX) = '{{TableSchema}}';
EXEC dbo.GenerateAuditTriggers @TableSchema;
```

**PostgreSQL:**

```sql
DO $$
DECLARE
  v_table_schema TEXT := '{{TableSchema}}';
BEGIN
  CALL public.generate_audit_triggers(v_table_schema);
END $$;
```

**MySQL:**

```sql
SET @TableSchema = '{{TableSchema}}';
CALL GenerateAuditTriggers(@TableSchema);
```

### Pulling deployment-time data from the target

A `Before` migration script needs to know which rows on the server are flagged for the new feature. The query runs once against the actual server you're deploying to, and the resolved value is substituted into every script that references the token -- no manual configuration shuffling, no hardcoded environment lists.

**SQL Server:**

```json
{
  "ScriptTokens": {
    "EnabledTargets": "<*Query*>SELECT TargetName FROM dbo.FeatureFlags WHERE FlagName = 'NewBilling' AND Enabled = 1"
  }
}
```

```sql
-- {{EnabledTargets}} resolves to a newline-separated list at deployment time
PRINT 'Applying new billing schema to: {{EnabledTargets}}';
```

**PostgreSQL:**

```json
{
  "ScriptTokens": {
    "EnabledTargets": "<*Query*>SELECT target_name FROM public.feature_flags WHERE flag_name = 'NewBilling' AND enabled = true"
  }
}
```

```sql
-- {{EnabledTargets}} resolves to a newline-separated list at deployment time
DO $$ BEGIN
  RAISE NOTICE 'Applying new billing schema to: {{EnabledTargets}}';
END $$;
```

**MySQL:**

```json
{
  "ScriptTokens": {
    "EnabledTargets": "<*Query*>SELECT `TargetName` FROM `FeatureFlags` WHERE `FlagName` = 'NewBilling' AND `Enabled` = 1"
  }
}
```

```sql
-- {{EnabledTargets}} resolves to a newline-separated list at deployment time
SELECT 'Applying new billing schema to: {{EnabledTargets}}' AS status;
```

---

## Related Documentation

- [Schema Packages Reference](schema-packages.md) -- Product and template structure
- [Custom Properties](custom-properties.md) -- Attach metadata via `Extensions` and consume it through tokens
- [Configuration Reference](configuration.md) -- Settings files and environment variables
- [SchemaQuench Reference](schemaquench.md) -- Where token resolution fits into deployment
