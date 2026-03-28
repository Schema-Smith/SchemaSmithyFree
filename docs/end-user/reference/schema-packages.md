# Schema Packages Reference

A schema package is the complete, deployable definition of one or more database schemas. It is the unit of work for every SchemaSmith tool: SchemaTongs creates packages by extracting from live databases, SchemaQuench deploys packages to target databases, and DataTongs generates data scripts that live inside packages.

This document is the authoritative reference for every file, folder, property, and format in a schema package.

---

## Product.json

The `Product.json` file sits at the root of the schema package and is the top-level configuration. Every package must have exactly one.

### Properties

| Property | Type | Default | Required | Description |
|---|---|---|---|---|
| `Name` | string | | Yes | Product name. Automatically added as a `{{ProductName}}` script token. Used for migration script tracking and version stamping. |
| `ValidationScript` | string | | Yes | T-SQL expression evaluated before quench begins. Must return a truthy value or the quench aborts. Supports token replacement. |
| `Platform` | string | `"SqlServer"` | No | Target platform. Valid values: `"SqlServer"`, `"MSSQL"` (legacy alias). |
| `TemplateOrder` | string[] | `[]` | No | Ordered list of template directory names. Templates are quenched in this order. |
| `ScriptTokens` | object | `{}` | No | Key-value pairs for `{{TokenName}}` replacement in scripts and SQL properties. See the [Script Tokens Reference](script-tokens.md). |
| `BaselineValidationScript` | string | | No | T-SQL expression evaluated after server validation but before template processing. |
| `VersionStampScript` | string | | No | T-SQL executed once after all templates complete successfully. Typically records the release version on the server. |
| `DropUnknownIndexes` | bool | `false` | No | When `true`, the table quench drops indexes on managed tables that are not defined in the table JSON. |
| `MinimumVersion` | string | | No | Minimum SQL Server version required. Valid values: `"Sql2016"`, `"Sql2017"`, `"Sql2019"`, `"Sql2022"`, `"Sql2025"`. When set, enables version-gated features. |
| `CheckConstraintStyle` | string | `"ColumnLevel"` | No | Controls how SchemaTongs writes check constraints during extraction: `"ColumnLevel"` (inline `CheckExpression` on the column) or `"TableLevel"` (named constraints in the `CheckConstraints` array). Has no effect on existing products. |

### Example (Northwind demo)

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

### Example with advanced options

```json
{
  "Name": "AdventureWorks",
  "ValidationScript": "SELECT CAST(1 AS BIT)",
  "TemplateOrder": [
    "Initialize",
    "AdventureWorks"
  ],
  "ScriptTokens": {
    "AdventureWorksDb": "AdventureWorks",
    "ReleaseVersion": "2.1.0"
  },
  "VersionStampScript": "PRINT '{{ReleaseVersion}}'",
  "DropUnknownIndexes": true,
  "MinimumVersion": "Sql2019",
  "Platform": "SqlServer"
}
```

### Notes

- `ProductName` is added automatically to `ScriptTokens` -- you do not need to define it.
- Script token values defined in Product.json can be overridden at runtime via `appsettings.json` or environment variables in the `ScriptTokens` section.
- All script properties (`ValidationScript`, `BaselineValidationScript`, `VersionStampScript`) support `{{TokenName}}` replacement.

---

## Template.json

Each template directory under `Templates/` must contain a `Template.json` file. A template targets one or more databases identified by its `DatabaseIdentificationScript`.

### Properties

| Property | Type | Default | Required | Description |
|---|---|---|---|---|
| `Name` | string | | Yes | Template name. Must match the containing directory name. Automatically added as a `{{TemplateName}}` script token. |
| `DatabaseIdentificationScript` | string | | Yes | T-SQL query that returns a result set with a `Name` column. Each row identifies a database to quench with this template. Supports token replacement. |
| `VersionStampScript` | string | | No | T-SQL executed per database after that database's quench completes successfully. |
| `UpdateFillFactor` | bool | `true` | No | When `true`, the table quench updates index fill factors to match the JSON definitions. OR'd with table-level and index-level `UpdateFillFactor` settings. |
| `IndexOnlyTableQuenches` | bool | `false` | No | When `true`, the table quench only manages indexes, statistics, XML indexes, and full-text indexes. Skips table creation, column changes, and foreign key management. Tables that do not exist are silently skipped. |
| `BaselineValidationScript` | string | | No | T-SQL validation executed per database before quenching that database. |
| `ScriptTokens` | object | `{}` | No | Key-value pairs that override matching product-level tokens for this template. Template tokens take precedence over product tokens with the same key. |

### Example (Northwind demo)

```json
{
  "Name": "Northwind",
  "DatabaseIdentificationScript": "SELECT [Name] FROM master.sys.databases WHERE [Name] = '{{NorthwindDb}}'",
  "UpdateFillFactor": true,
  "ScriptTokens": {}
}
```

### Index-only templates

Set `"IndexOnlyTableQuenches": true` to create a template that only manages indexes. Use cases:

- Adding indexes to a replicated database where the source indexes do not fit the replica's query patterns
- Managing indexes on a third-party database where you do not control the table schema

---

## Complete Folder Structure

```
MyProduct/
  Product.json                          Product configuration (required)
  .json-schemas/                        Generated JSON Schema files for editor validation
    products.schema                       Schema for Product.json
    templates.schema                      Schema for Template.json
    tables.schema                         Schema for table JSON files
    indexedviews.schema                   Schema for indexed view JSON files
  .community                            Marker file (generated by SchemaTongs)
  Before Product/                       SQL scripts run before all templates
  After Product/                        SQL scripts run after all templates
  Templates/
    TemplateName/
      Template.json                     Template configuration (required)
      Tables/                           Table definition JSON files
        dbo.Customer.json
        Sales.Order.json
      Indexed Views/                    Indexed view definition JSON files
        dbo.vw_OrderSummary.json
      Schemas/                          CREATE/ALTER SCHEMA scripts (.sql)
      DataTypes/                        User-defined type scripts (.sql)
      FullTextCatalogs/                 Full-text catalog scripts (.sql)
      FullTextStopLists/                Full-text stop list scripts (.sql)
      XMLSchemaCollections/             XML schema collection scripts (.sql)
      Functions/                        User-defined function scripts (.sql)
      Views/                            View scripts (.sql)
      Procedures/                       Stored procedure scripts (.sql)
      Triggers/                         Table trigger scripts (.sql)
      DDLTriggers/                      DDL trigger scripts (.sql)
      Table Data/                       Data population scripts (.sql)
      MigrationScripts/
        Before/                         Pre-quench migration scripts (.sql)
        BetweenTablesAndKeys/           After table structures, before FK constraints (.sql)
        AfterTablesScripts/             After full table quench, before triggers (.sql)
        After/                          Post-quench migration scripts (.sql)
    AnotherTemplate/
      Template.json
      Tables/
      ...
```

Table JSON files are named `schema.tablename.json` (e.g., `dbo.Customer.json`, `Sales.Order.json`). If the table or schema name contains filesystem-illegal characters, the encoded form is used in the filename (see [Filesystem-Illegal Character Encoding](#filesystem-illegal-character-encoding)).

SQL script files can be organized into subdirectories within any script folder. All `.sql` files are discovered recursively and sorted alphabetically by full path.

---

## Script Folder Execution Order

During deployment, SchemaQuench processes folders in a fixed sequence. The sequence below shows the complete execution order including both product-level and template-level folders. The template-level sequence repeats for each template in `TemplateOrder`, and within each template it repeats for each database identified by `DatabaseIdentificationScript`.

### Full deployment sequence

| Step | Folder / Action | Quench Slot | Execution Behavior |
|------|-----------------|-------------|-------------------|
| 1 | `Before Product` | Product Before | Sequential, tracked (run once unless `[ALWAYS]` in filename) |
| 2 | `Schemas`, `DataTypes`, `FullTextCatalogs`, `FullTextStopLists`, `XMLSchemaCollections`, `Functions`, `Views`, `Procedures` | Objects | Dependency retry loop (first pass) |
| 3 | _(MissingTableAndColumnQuench)_ | _(internal)_ | Creates new tables and adds new columns from table JSON |
| 4 | Objects retry | Objects | Dependency retry loop (second pass — resolves scripts that needed tables) |
| 5 | `MigrationScripts/Before` | Before | Sequential, tracked (run once unless `[ALWAYS]`) |
| 6 | _(ModifiedTableQuench)_ | _(internal)_ | Alters existing tables to match JSON (column changes, drops removed tables if configured) |
| 7 | Objects retry | Objects | Dependency retry loop (third pass) |
| 8 | `MigrationScripts/BetweenTablesAndKeys` | BetweenTablesAndKeys | Sequential, tracked |
| 9 | _(MissingIndexesAndConstraintsQuench)_ | _(internal)_ | Adds missing indexes and constraints from table JSON |
| 10 | `MigrationScripts/AfterTablesScripts` | AfterTablesScripts | Sequential, tracked |
| 11 | `Triggers`, `DDLTriggers` + remaining Objects retries | AfterTablesObjects | Dependency retry loop (final pass — errors are fatal) |
| 12 | `Table Data` | TableData | Dependency retry loop |
| 13 | _(ForeignKeyQuench)_ | _(internal)_ | Applies foreign key constraints after all data is in place |
| 14 | _(IndexedViewQuench)_ | _(internal)_ | Creates or updates indexed views |
| 15 | `MigrationScripts/After` | After | Sequential, tracked |
| 16 | `After Product` | Product After | Sequential, tracked |

Steps 2–15 execute per template (in `TemplateOrder`), per database (as identified by `DatabaseIdentificationScript`). Steps 1 and 16 execute once per quench run against the server connection.

### Execution behaviors

**Sequential, tracked** -- Scripts run in alphabetical order. Each script's completion is recorded in the `CompletedMigrationScripts` table and will not run again on subsequent quenches. Scripts with `[ALWAYS]` in the filename run every time regardless of tracking.

**Dependency retry loop** -- All scripts in the slot are attempted. Scripts that fail due to unresolved dependencies are retried on the next iteration. The loop continues until all scripts succeed or no progress is made on an iteration.

### Quench slot reference

| TemplateQuenchSlot | Purpose |
|---|---|
| `Before` | One-time migration scripts that run after initial object creation and new table creation, but before table modifications. Use for data preparation that must happen before columns are altered or dropped. |
| `Objects` | Database objects that may have cross-dependencies (schemas, types, catalogs, functions, views, procedures). The retry loop resolves creation order automatically. |
| `BetweenTablesAndKeys` | Migration scripts that need the table structure to exist but must run before foreign key constraints are enforced. Typical use: populating a new NOT NULL column before FKs block the data load. |
| `AfterTablesScripts` | Migration scripts that depend on the final table and key structure but must run before triggers are deployed. |
| `AfterTablesObjects` | Triggers and DDL triggers that depend on the completed table structure. |
| `TableData` | Data population scripts (MERGE statements, seed data). Run after triggers are deployed but before foreign key constraints are applied. |
| `After` | Final migration scripts. Run after all database objects and data are deployed. |

| ProductQuenchSlot | Purpose |
|---|---|
| `Before` | Product-level scripts that run once before any template processing begins. Run on the server connection, not scoped to a database. |
| `After` | Product-level scripts that run once after all templates complete. Run on the server connection. |

---

## JSON Table Format

Table definitions live in the `Tables/` directory of each template as individual JSON files. Each file defines exactly one table.

### Table-level properties

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Schema` | string | `"dbo"` | No | 1 | Schema name. Use bracket notation in extracted files (e.g., `"[dbo]"`). |
| `Name` | string | | Yes | 2 | Table name. Use bracket notation in extracted files (e.g., `"[Customer]"`). |
| `CompressionType` | string | `"NONE"` | No | 3 | Data compression. Values: `"NONE"`, `"ROW"`, `"PAGE"`. |
| `IsTemporal` | bool | `false` | No | 4 | Marks the table as a system-versioned temporal table. |
| `Columns` | array | `[]` | Yes | 5 | Column definitions. See [Columns](#columns). |
| `Indexes` | array | `[]` | No | 6 | Index and constraint definitions. See [Indexes](#indexes). |
| `XmlIndexes` | array | `[]` | No | 7 | XML index definitions. See [XML Indexes](#xml-indexes). |
| `ForeignKeys` | array | `[]` | No | 8 | Foreign key definitions. See [Foreign Keys](#foreign-keys). |
| `CheckConstraints` | array | `[]` | No | 9 | Table-level check constraint definitions. See [Check Constraints](#check-constraints). |
| `Statistics` | array | `[]` | No | 10 | Statistics definitions. See [Statistics](#statistics). |
| `FullTextIndex` | object | `null` | No | 11 | Full-text index configuration. See [Full-Text Index](#full-text-index). |
| `OldName` | string | `""` | No | 12 | Previous table name. When set, the table is renamed from `OldName` to `Name` during quench. Clear after the rename has been deployed to all environments. |
| `UpdateFillFactor` | bool | `false` | No | 13 | When `true`, index fill factors on this table are updated to match JSON definitions during quench. OR'd with template-level and index-level settings. |

### Minimal example (Northwind Region)

```json
{
  "Schema": "[dbo]",
  "Name": "[Region]",
  "CompressionType": "NONE",
  "Columns": [
    {
      "Name": "[RegionDescription]",
      "DataType": "NCHAR(50)",
      "Nullable": false
    },
    {
      "Name": "[RegionID]",
      "DataType": "INT",
      "Nullable": false
    }
  ],
  "Indexes": [
    {
      "Name": "[PK_Region]",
      "PrimaryKey": true,
      "Unique": true,
      "IndexColumns": "[RegionID]"
    }
  ]
}
```

### Complex example (AdventureWorks Production.Product)

This table demonstrates identity columns, check expressions, defaults, foreign keys, and table-level check constraints. Only a subset of columns is shown for brevity -- see `demo/AdventureWorks/Templates/AdventureWorks/Tables/Production.Product.json` for the full definition.

```json
{
  "Schema": "[Production]",
  "Name": "[Product]",
  "CompressionType": "NONE",
  "Columns": [
    {
      "Name": "[ProductID]",
      "DataType": "INT IDENTITY(1, 1)",
      "Nullable": false
    },
    {
      "Name": "[Name]",
      "DataType": "NAME",
      "Nullable": false
    },
    {
      "Name": "[Class]",
      "DataType": "NCHAR(2)",
      "Nullable": true,
      "CheckExpression": "upper([Class])='H' OR upper([Class])='M' OR upper([Class])='L' OR [Class] IS NULL"
    },
    {
      "Name": "[ListPrice]",
      "DataType": "MONEY",
      "Nullable": false,
      "CheckExpression": "[ListPrice]>=(0.00)"
    },
    {
      "Name": "[ModifiedDate]",
      "DataType": "DATETIME",
      "Nullable": false,
      "Default": "getdate()"
    },
    {
      "Name": "[FinishedGoodsFlag]",
      "DataType": "FLAG",
      "Nullable": false,
      "Default": "1"
    },
    {
      "Name": "[rowguid]",
      "DataType": "UNIQUEIDENTIFIER ROWGUIDCOL",
      "Nullable": false,
      "Default": "newid()"
    }
  ],
  "Indexes": [
    {
      "Name": "[PK_Product_ProductID]",
      "PrimaryKey": true,
      "Unique": true,
      "Clustered": true,
      "IndexColumns": "[ProductID]"
    },
    {
      "Name": "[AK_Product_Name]",
      "Unique": true,
      "IndexColumns": "[Name]"
    }
  ],
  "ForeignKeys": [
    {
      "Name": "[FK_Product_ProductModel_ProductModelID]",
      "Columns": "[ProductModelID]",
      "RelatedTableSchema": "[Production]",
      "RelatedTable": "[ProductModel]",
      "RelatedColumns": "[ProductModelID]",
      "DeleteAction": "NO ACTION",
      "UpdateAction": "NO ACTION"
    }
  ],
  "CheckConstraints": [
    {
      "Name": "[CK_Product_SellEndDate]",
      "Expression": "[SellEndDate]>=[SellStartDate] OR [SellEndDate] IS NULL"
    }
  ]
}
```

---

## Columns

Each entry in the `Columns` array defines one column on the table.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Name` | string | | Yes | 1 | Column name. |
| `DataType` | string | | Yes | 2 | SQL Server data type with precision/scale/length. Also supports identity syntax and user-defined types. See below. |
| `Nullable` | bool | `false` | No | 3 | Whether the column allows NULL. |
| `Default` | string | | No | 4 | Default constraint expression (e.g., `"getdate()"`, `"0"`, `"newid()"`). |
| `CheckExpression` | string | | No | 5 | Column-level check constraint expression. Used when `CheckConstraintStyle` is `"ColumnLevel"` (the default). |
| `ComputedExpression` | string | | No | 6 | Computed column formula. When set, `DataType` is ignored. |
| `Persisted` | bool | `false` | No | 7 | Whether a computed column is physically stored on disk. Only meaningful when `ComputedExpression` is set. |
| `Sparse` | bool | `false` | No | 8 | Whether the column uses sparse storage. |
| `Collation` | string | | No | 9 | Column-level collation override (e.g., `"SQL_Latin1_General_CP1_CI_AS"`). |
| `DataMaskFunction` | string | | No | 10 | Dynamic data masking function (e.g., `"default()"`, `"email()"`, `"partial(0,\"XXX\",0)"`). |
| `OldName` | string | `""` | No | 11 | Previous column name for rename detection. Clear after the rename has been deployed to all environments. |

### Identity columns

Identity is specified as part of the `DataType` string using standard SQL Server syntax:

```json
{ "Name": "[ProductID]", "DataType": "INT IDENTITY(1, 1)", "Nullable": false }
```

The format is `<base_type> IDENTITY(<seed>, <increment>)`.

### ROWGUIDCOL columns

The `ROWGUIDCOL` property is also specified in the `DataType` string:

```json
{ "Name": "[rowguid]", "DataType": "UNIQUEIDENTIFIER ROWGUIDCOL", "Nullable": false, "Default": "newid()" }
```

### Computed columns

When `ComputedExpression` is set, the column is a computed column. The `DataType` property is ignored. Set `Persisted` to `true` to store the computed value on disk:

```json
{ "Name": "[FullName]", "ComputedExpression": "[FirstName] + ' ' + [LastName]", "Persisted": true }
```

### User-defined types

When the database uses user-defined types (created via `CREATE TYPE`), the `DataType` value is the type name:

```json
{ "Name": "[Name]", "DataType": "NAME", "Nullable": false }
```

The type must be created in the `DataTypes` script folder before the table quench runs.

---

## Indexes

Each entry in the `Indexes` array defines an index or constraint on the table.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Name` | string | | Yes | 1 | Index or constraint name. |
| `CompressionType` | string | `"NONE"` | No | 2 | Index compression. Values: `"NONE"`, `"ROW"`, `"PAGE"`. |
| `PrimaryKey` | bool | `false` | No | 3 | `true` for a primary key constraint. |
| `Unique` | bool | `false` | No | 4 | `true` for a unique index. |
| `UniqueConstraint` | bool | `false` | No | 5 | `true` for a UNIQUE constraint (as opposed to a unique index). |
| `Clustered` | bool | `false` | No | 6 | `true` for a clustered index. |
| `ColumnStore` | bool | `false` | No | 7 | `true` for a columnstore index. |
| `FillFactor` | byte (0--100) | `0` | No | 8 | Fill factor percentage. `0` means use the server default. |
| `IndexColumns` | string | | Yes | 9 | Comma-separated column names with optional sort direction (e.g., `"[LastName] ASC, [FirstName] ASC"`). |
| `IncludeColumns` | string | | No | 10 | Comma-separated INCLUDE columns. |
| `FilterExpression` | string | | No | 11 | Filtered index WHERE clause (e.g., `"[IsActive] = 1"`). |
| `UpdateFillFactor` | bool | `false` | No | 12 | When `true`, this index's fill factor is updated during quench. OR'd with template-level and table-level settings. |

### Primary key

```json
{
  "Name": "[PK_Product_ProductID]",
  "PrimaryKey": true,
  "Unique": true,
  "Clustered": true,
  "IndexColumns": "[ProductID]"
}
```

### Unique index with filter

```json
{
  "Name": "[IX_Customer_Email]",
  "Unique": true,
  "IndexColumns": "[Email]",
  "FilterExpression": "[Email] IS NOT NULL"
}
```

### Covering index with included columns

```json
{
  "Name": "[IX_Customer_Name]",
  "IndexColumns": "[LastName] ASC, [FirstName] ASC",
  "IncludeColumns": "[Email], [Phone]"
}
```

---

## Foreign Keys

Each entry in the `ForeignKeys` array defines a foreign key constraint.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Name` | string | | Yes | 1 | Constraint name. |
| `Columns` | string | | Yes | 2 | Comma-separated local column names. |
| `RelatedTableSchema` | string | `"dbo"` | No | 3 | Schema of the referenced table. |
| `RelatedTable` | string | | Yes | 4 | Referenced table name. |
| `RelatedColumns` | string | | Yes | 5 | Comma-separated referenced column names. |
| `DeleteAction` | string | | No | 6 | Cascade action on delete. Values: `"NO ACTION"`, `"CASCADE"`, `"SET NULL"`, `"SET DEFAULT"`. |
| `UpdateAction` | string | | No | 7 | Cascade action on update. Values: `"NO ACTION"`, `"CASCADE"`, `"SET NULL"`, `"SET DEFAULT"`. |

### Example

```json
{
  "Name": "[FK_Product_ProductModel_ProductModelID]",
  "Columns": "[ProductModelID]",
  "RelatedTableSchema": "[Production]",
  "RelatedTable": "[ProductModel]",
  "RelatedColumns": "[ProductModelID]",
  "DeleteAction": "NO ACTION",
  "UpdateAction": "NO ACTION"
}
```

### Composite foreign keys

For composite foreign keys, list all columns in both `Columns` and `RelatedColumns` in matching order:

```json
{
  "Name": "[FK_OrderDetail_Order]",
  "Columns": "[OrderID], [LineNumber]",
  "RelatedTable": "[Order]",
  "RelatedColumns": "[OrderID], [LineNumber]"
}
```

---

## Check Constraints

Table-level check constraints in the `CheckConstraints` array. These are used when `CheckConstraintStyle` is `"TableLevel"` in `Product.json`, or when a check constraint spans multiple columns.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Name` | string | | Yes | 1 | Constraint name. |
| `Expression` | string | | Yes | 2 | Boolean SQL expression. |

### Example

```json
{
  "Name": "[CK_Product_SellEndDate]",
  "Expression": "[SellEndDate]>=[SellStartDate] OR [SellEndDate] IS NULL"
}
```

When `CheckConstraintStyle` is `"ColumnLevel"` (the default), single-column check constraints are written as `CheckExpression` on the column instead. Multi-column constraints always use the `CheckConstraints` array regardless of the style setting.

---

## Statistics

Custom statistics definitions in the `Statistics` array.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Name` | string | | Yes | 1 | Statistics name. |
| `Columns` | string | | Yes | 2 | Comma-separated column names. |
| `SampleSize` | byte (0--100) | `0` | No | 3 | Sampling percentage. `0` means default sampling. |
| `FilterExpression` | string | | No | 4 | Filtered statistics WHERE clause. |

### Example

```json
{
  "Name": "[ST_Order_CreatedDate]",
  "Columns": "[CreatedDate]",
  "SampleSize": 50,
  "FilterExpression": "[Status] = 'Active'"
}
```

---

## XML Indexes

XML index definitions in the `XmlIndexes` array. A primary XML index must be created before secondary XML indexes on the same column.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Name` | string | | Yes | 1 | Index name. |
| `IsPrimary` | bool | `false` | No | 2 | `true` for a PRIMARY XML INDEX. |
| `Column` | string | | Yes | 3 | Name of the XML column being indexed. |
| `PrimaryIndex` | string | | No | 4 | Name of the primary XML index. Required for secondary indexes. |
| `SecondaryIndexType` | string | | No | 5 | Type of secondary index. Values: `"VALUE"`, `"PATH"`, `"PROPERTY"`. Required for secondary indexes. |

### Example: primary and secondary XML indexes

```json
"XmlIndexes": [
  {
    "Name": "[PXML_Doc_Content]",
    "IsPrimary": true,
    "Column": "[Content]"
  },
  {
    "Name": "[IXML_Doc_Content_Path]",
    "IsPrimary": false,
    "Column": "[Content]",
    "PrimaryIndex": "[PXML_Doc_Content]",
    "SecondaryIndexType": "PATH"
  }
]
```

---

## Full-Text Index

A single `FullTextIndex` object (not an array) on the table. Only one full-text index is allowed per table.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `FullTextCatalog` | string | | Yes | 1 | Name of the full-text catalog. The catalog must exist (create it via the `FullTextCatalogs` script folder). |
| `KeyIndex` | string | | Yes | 2 | Name of the unique index used as the full-text key. |
| `ChangeTracking` | string | | No | 3 | Change tracking mode. Values: `"OFF"`, `"MANUAL"`, `"AUTO"`. |
| `StopList` | string | | No | 4 | Name of a full-text stop list. |
| `Columns` | string | | Yes | 5 | Column specification for the full-text index. |

### Example

```json
"FullTextIndex": {
  "FullTextCatalog": "[FT_Catalog]",
  "KeyIndex": "[PK_Document_DocumentID]",
  "ChangeTracking": "AUTO",
  "Columns": "[Content], [Title]"
}
```

---

## Indexed View JSON Format

Indexed views are defined as JSON files in the `Indexed Views/` directory of each template. Each file defines one indexed view.

| Property | Type | Default | Required | JSON Order | Description |
|---|---|---|---|---|---|
| `Name` | string | `""` | Yes | 1 | View name. |
| `Schema` | string | `"dbo"` | No | 2 | Schema name. |
| `Definition` | string | `""` | Yes | 3 | The complete view definition SQL (the SELECT statement). |
| `Indexes` | array | `[]` | No | 4 | Indexes on the view. Uses the same [Index](#indexes) format as table indexes. An indexed view must have a unique clustered index. |

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

Indexed view files follow the same naming convention as tables: `schema.viewname.json` (e.g., `dbo.vw_OrderSummary.json`).

---

## .json-schemas Folder

The `.json-schemas/` directory at the package root contains JSON Schema definition files generated automatically by SchemaTongs during extraction. These files enable editor validation and autocomplete in IDEs that support JSON Schema (such as VS Code, JetBrains Rider, and Visual Studio).

The folder contains four schema files:

| File | Validates |
|---|---|
| `products.schema` | `Product.json` |
| `templates.schema` | `Template.json` |
| `tables.schema` | Table JSON files (`Tables/*.json`) |
| `indexedviews.schema` | Indexed view JSON files (`Indexed Views/*.json`) |

These files are regenerated each time SchemaTongs initializes or updates a schema package. You do not need to edit them manually. If your editor does not pick up the schemas automatically, configure your IDE's JSON Schema mapping to point `Product.json` at `.json-schemas/products.schema`, and so on.

---

## ZIP Package Support

SchemaQuench can consume schema packages as ZIP archives. When the `SchemaPackagePath` configuration value points to a `.zip` file, SchemaQuench reads the package directly from the archive without extracting it to disk first.

Requirements:

- The ZIP must contain the standard schema package folder structure.
- The `Product.json` file must be at the root of the archive (not nested inside an extra directory).
- All relative paths within the package must match the standard layout described in [Complete Folder Structure](#complete-folder-structure).

This is useful for deployment pipelines where the schema package is built as an artifact and distributed as a single file.

---

## Filesystem-Illegal Character Encoding

Table names, view names, and other database object names can contain characters that are illegal in file paths on Windows, macOS, or Linux. SchemaTongs uses a percent-encoding scheme to safely map these names to filenames.

### Encoded characters

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

### Additional encoding rules

- **Leading spaces and dots** are encoded (`%20` for space, `%2E` for dot) because many filesystems strip or reject them at the start of filenames.
- **Trailing spaces and dots** are similarly encoded to prevent silent truncation.
- **Reserved device names** (CON, PRN, AUX, NUL, COM1--COM9, LPT1--LPT9) have their first character percent-encoded to avoid conflicts with Windows reserved names.

### Examples

| Object Name | Encoded Filename |
|---|---|
| `dbo.Order Details` | `dbo.Order Details.json` (interior spaces are not encoded) |
| `dbo..hidden` | `dbo.%2Ehidden.json` (leading dot after the schema prefix) |
| `dbo.CON` | `dbo.%43ON.json` (reserved name, first char encoded) |
| `dbo.file:name` | `dbo.file%3Aname.json` (colon encoded) |

SchemaQuench decodes these filenames transparently when reading table definitions. You generally do not need to worry about encoding unless you are creating table JSON files by hand for tables with unusual names.

---

## Related Documentation

- [Script Tokens Reference](script-tokens.md) -- token replacement system used in all script properties
- [Configuration Reference](configuration.md) -- appsettings.json for SchemaQuench, SchemaTongs, and DataTongs
- [SchemaTongs Reference](schematongs.md) -- extraction tool that creates schema packages
- [SchemaQuench Reference](schemaquench.md) -- deployment tool that applies schema packages
- [Core Concepts -- Schema Packages](../guide/03-core-concepts.md#schema-packages) -- Conceptual overview of the package structure
- [Core Concepts -- JSON Table Definitions](../guide/03-core-concepts.md#json-table-definitions) -- Guided introduction to the table JSON format
- [Day-to-Day Workflows -- Adding a Table](../guide/04-day-to-day-workflows.md#adding-a-table) -- Practical walkthrough of creating table JSON
