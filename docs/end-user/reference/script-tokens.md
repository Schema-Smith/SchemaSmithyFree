# Script Tokens Reference

Applies to: SchemaQuench, SchemaTongs, DataTongs, SchemaHammer (SQL Server, Community)

---

## Token Syntax

Script tokens use double curly braces:

```
{{TokenName}}
```

Token replacement is **case-insensitive**. These all resolve to the same value:

```sql
SELECT * FROM [{{MainDB}}].dbo.Customers
SELECT * FROM [{{maindb}}].dbo.Customers
SELECT * FROM [{{MAINDB}}].dbo.Customers
```

Tokens that appear in scripts but have no matching definition are left in place unchanged.

---

## Where Tokens Can Appear

Tokens are replaced in every SQL context SchemaSmith processes:

**Product-level JSON properties:**

- `ValidationScript`
- `BaselineValidationScript`
- `VersionStampScript`

**Template-level JSON properties:**

- `DatabaseIdentificationScript`
- `VersionStampScript`
- `BaselineValidationScript`

**SQL script files** in all product and template folders:

| Folder | Scope |
|---|---|
| `Before Product/` | Product |
| `After Product/` | Product |
| `MigrationScripts/Before/` | Template |
| `Schemas/` | Template |
| `DataTypes/` | Template |
| `FullTextCatalogs/` | Template |
| `FullTextStopLists/` | Template |
| `XMLSchemaCollections/` | Template |
| `Functions/` | Template |
| `Views/` | Template |
| `Procedures/` | Template |
| `MigrationScripts/BetweenTablesAndKeys/` | Template |
| `MigrationScripts/AfterTablesScripts/` | Template |
| `Triggers/` | Template |
| `DDLTriggers/` | Template |
| `Table Data/` | Template |
| `MigrationScripts/After/` | Template |

Every `.sql` file in every folder listed above has its tokens replaced before execution.

---

## Product Tokens

Define tokens in `Product.json` under the `ScriptTokens` property. These are available across all templates and product-level scripts.

```json
{
    "Name": "SaasProduct",
    "ValidationScript": "SELECT CAST(CASE WHEN EXISTS(SELECT * FROM master.sys.databases WHERE [Name] = '{{RegistryDb}}') THEN 1 ELSE 0 END AS BIT)",
    "TemplateOrder": ["Registry", "Client"],
    "ScriptTokens": {
        "RegistryDb": "Registry",
        "MigrationVersion": "1.0.1"
    }
}
```

In this example, `{{RegistryDb}}` resolves to `Registry` everywhere -- in the `ValidationScript`, in every template's scripts, and in every SQL file across the product.

---

## Template Tokens

Define tokens in `Template.json` under the `ScriptTokens` property. Template tokens override product tokens with the same key for the duration of that template's execution.

```json
{
    "Name": "Reporting",
    "DatabaseIdentificationScript": "SELECT [Name] FROM master.sys.databases WHERE [Name] = '{{ReportDB}}'",
    "ScriptTokens": {
        "MainDB": "ReportingAlias",
        "SchemaOwner": "rpt"
    }
}
```

Within the Reporting template, `{{MainDB}}` resolves to `ReportingAlias` instead of whatever the product defined. The `{{SchemaOwner}}` token is available only within this template. Other templates and product-level scripts are not affected.

---

## Automatic Tokens

SchemaSmith adds two tokens automatically. You do not need to define them.

| Token | Value | Available in |
|---|---|---|
| `{{ProductName}}` | The `Name` from `Product.json` | All product and template scripts |
| `{{TemplateName}}` | The `Name` from `Template.json` | Template scripts only |

`ProductName` is added after config/environment overrides are applied, so it always reflects the product name from the package (it cannot be overridden via config).

`TemplateName` is added after template tokens are merged, so it always reflects the current template's name.

**Example using automatic tokens:**

```sql
-- VersionStampScript in Template.json
IF NOT EXISTS(SELECT * FROM dbo.ProductVersion WHERE Product = '{{ProductName}}' AND Version = '{{MigrationVersion}}')
    INSERT dbo.ProductVersion(Product, Version) VALUES('{{ProductName}}', '{{MigrationVersion}}')
```

---

## Config-Level Overrides

Override product token values in a settings file without modifying the schema package. This lets you deploy the same package to different environments with different token values.

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

Config overrides only apply to tokens that **already exist** in `Product.json`. You cannot introduce new tokens via configuration alone.

---

## Environment Variable Tokens

Override product token values using environment variables. The naming pattern is:

```
SmithySettings_ScriptTokens__TokenName=Value
```

Note the prefix `SmithySettings_` (single underscore) and the double underscore `__` before the token name. This follows the .NET configuration environment variable convention.

**Examples:**

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

Like config-level overrides, environment variable overrides only apply to tokens that **already exist** in `Product.json`.

---

## Token Resolution Order

Tokens resolve in layers, from lowest to highest priority:

| Priority | Source | Scope |
|---|---|---|
| 1 (lowest) | `Product.json` `ScriptTokens` | All scripts |
| 2 | Settings file `ScriptTokens` section | Overrides matching product keys |
| 3 | Environment variables (`SmithySettings_ScriptTokens__*`) | Overrides matching product keys |
| 4 (highest) | `Template.json` `ScriptTokens` | Template scripts only |

**How it works step by step:**

1. Product tokens are loaded from `Product.json`.
2. Config file and environment variable overrides replace matching product token values. (Steps 2 and 3 are handled together by .NET configuration layering.)
3. The automatic `ProductName` token is added.
4. When each template loads, its `ScriptTokens` are merged on top of the resolved product tokens. Template tokens with matching keys win.
5. The automatic `TemplateName` token is added.

Config and environment overrides apply at the product level only. They do not directly override template-level tokens. However, since template tokens take priority over product tokens, a template token will still win over a config/environment override for the same key.

---

## Token Preview in SchemaHammer

SchemaHammer provides a **Preview** toggle on SQL scripts that expands `{{Token}}` placeholders to their resolved values. This lets you verify token replacement before running SchemaQuench. See the [SchemaHammer Reference](schemahammer.md#token-preview) for details. For a practical walkthrough of using tokens across environments, see [Power Workflows -- Script Tokens](../guide/05-power-workflows.md#script-tokens).

---

## Practical Examples

### Scenario: Multi-Environment Deployment

A SaaS product uses tokens to manage database names and version stamps across development, staging, and production.

**Product.json** defines the baseline:

```json
{
    "Name": "SaasProduct",
    "ValidationScript": "SELECT CAST(CASE WHEN EXISTS(SELECT * FROM master.sys.databases WHERE [Name] = '{{RegistryDb}}') THEN 1 ELSE 0 END AS BIT)",
    "TemplateOrder": ["Registry", "Client"],
    "ScriptTokens": {
        "RegistryDb": "Registry",
        "MigrationVersion": "3.2.0"
    }
}
```

**Template.json** (Client) uses product tokens and automatic tokens:

```json
{
    "Name": "Client",
    "DatabaseIdentificationScript": "SELECT [DatabaseName] FROM {{RegistryDb}}.dbo.ClientDBs WHERE [IsEnabled] = 1",
    "VersionStampScript": "IF NOT EXISTS(SELECT * FROM dbo.ProductVersion WHERE Product = '{{ProductName}}' AND Version = '{{MigrationVersion}}') INSERT dbo.ProductVersion(Product, Version) VALUES('{{ProductName}}', '{{MigrationVersion}}')"
}
```

**A migration script** (`MigrationScripts/Before/SyncRegistryLink.sql`) references the registry:

```sql
IF NOT EXISTS (SELECT * FROM sys.servers WHERE name = 'RegistryLink')
    EXEC sp_addlinkedserver @server = 'RegistryLink', @srvproduct = '',
        @datasrc = '{{RegistryDb}}'
```

#### Development (no overrides needed)

Tokens resolve to `Product.json` values:

| Token | Resolved Value |
|---|---|
| `{{RegistryDb}}` | `Registry` |
| `{{MigrationVersion}}` | `3.2.0` |
| `{{ProductName}}` | `SaasProduct` |
| `{{TemplateName}}` | `Client` |

The migration script becomes:

```sql
IF NOT EXISTS (SELECT * FROM sys.servers WHERE name = 'RegistryLink')
    EXEC sp_addlinkedserver @server = 'RegistryLink', @srvproduct = '',
        @datasrc = 'Registry'
```

#### Staging (settings file override)

`SchemaQuench.settings.json`:

```json
{
    "Target": { "Server": "staging-db" },
    "ScriptTokens": {
        "RegistryDb": "Registry_Staging"
    }
}
```

Now `{{RegistryDb}}` resolves to `Registry_Staging`. The migration script becomes:

```sql
IF NOT EXISTS (SELECT * FROM sys.servers WHERE name = 'RegistryLink')
    EXEC sp_addlinkedserver @server = 'RegistryLink', @srvproduct = '',
        @datasrc = 'Registry_Staging'
```

#### CI Pipeline (environment variable override)

```bash
export SmithySettings_ScriptTokens__RegistryDb="Registry_CI_$BUILD_NUMBER"
export SmithySettings_ScriptTokens__MigrationVersion="3.2.0-ci.$BUILD_NUMBER"

SchemaQuench --ConfigFile ci.settings.json
```

Environment variables take priority over the settings file, so `{{RegistryDb}}` resolves to `Registry_CI_1234` and `{{MigrationVersion}}` resolves to `3.2.0-ci.1234`.

### Scenario: Cross-Database References

When templates need to reference databases managed by other templates, product-level tokens keep the references consistent:

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

A view in the Reporting template can reference the other databases:

```sql
CREATE OR ALTER VIEW dbo.SalesSummary AS
SELECT o.OrderDate, p.ProductName, o.Quantity, o.Total
FROM [{{OrdersDb}}].dbo.Orders o
JOIN [{{CatalogDb}}].dbo.Products p ON o.ProductId = p.Id
```

Changing `OrdersDb` or `CatalogDb` in one place updates every script that references them.

---

## Related Documentation

- [Schema Packages Reference](schema-packages.md) -- product and template structure
- [Configuration Reference](configuration.md) -- settings files and environment variables
- [SchemaHammer Reference](schemahammer.md) -- token preview toggle and token navigation
