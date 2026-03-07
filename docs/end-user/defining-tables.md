# Defining Tables

Applies to: SchemaQuench, SchemaTongs (SQL Server, Community)

---

## Overview

Tables are defined as JSON files in the `Tables/` directory of each template. Each file defines one table and is named `schema.tablename.json` (e.g., `dbo.Customer.json`).

SchemaTongs generates these files during extraction. SchemaQuench reads them during deployment and uses the `SchemaSmith.TableQuench` stored procedure to create, alter, or restructure tables to match the definition.

---

## Table Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Schema` | string | `"dbo"` | Schema name |
| `Name` | string | _(required)_ | Table name |
| `CompressionType` | string | `"NONE"` | Data compression: `"NONE"`, `"ROW"`, or `"PAGE"` |
| `IsTemporal` | bool | `false` | Marks the table as a system-versioned temporal table |
| `OldName` | string | | Previous table name. When set, the table is renamed from `OldName` to `Name` during quench. |
| `Columns` | array | | Column definitions |
| `Indexes` | array | | Index and constraint definitions |
| `XmlIndexes` | array | | XML index definitions |
| `ForeignKeys` | array | | Foreign key relationship definitions |
| `CheckConstraints` | array | | Table-level check constraint definitions |
| `Statistics` | array | | Statistics definitions |
| `FullTextIndex` | object | | Full-text index configuration |

---

## Columns

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | _(required)_ | Column name |
| `DataType` | string | _(required)_ | SQL Server data type with precision/scale/length (e.g., `"int"`, `"nvarchar(255)"`, `"decimal(18,2)"`) |
| `Nullable` | bool | `false` | Whether the column allows NULL |
| `Default` | string | | Default constraint expression (e.g., `"(0)"`, `"(getdate())"`) |
| `CheckExpression` | string | | Column-level check constraint expression |
| `ComputedExpression` | string | | Computed column formula. When set, `DataType` is ignored. |
| `Persisted` | bool | `false` | Whether a computed column is persisted to disk |
| `Sparse` | bool | `false` | Whether the column uses sparse storage |
| `Collation` | string | | Column collation override (e.g., `"SQL_Latin1_General_CP1_CI_AS"`) |
| `DataMaskFunction` | string | | Dynamic data masking function (e.g., `"default()"`, `"email()"`) |
| `OldName` | string | | Previous column name for rename detection |

### Identity Columns

Identity columns are specified using the SQL Server identity syntax within the `DataType` property:

```json
{ "Name": "Id", "DataType": "int IDENTITY(1,1)", "Nullable": false }
```

### Computed Columns

```json
{ "Name": "FullName", "ComputedExpression": "[FirstName] + ' ' + [LastName]", "Persisted": true }
```

---

## Indexes

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | _(required)_ | Index or constraint name |
| `Clustered` | bool | `false` | Clustered index |
| `Unique` | bool | `false` | Unique index |
| `UniqueConstraint` | bool | `false` | `UNIQUE` constraint (as opposed to unique index) |
| `PrimaryKey` | bool | `false` | Primary key constraint |
| `ColumnStore` | bool | `false` | Columnstore index |
| `CompressionType` | string | `"NONE"` | Index compression: `"NONE"`, `"ROW"`, or `"PAGE"` |
| `FillFactor` | int | `0` | Fill factor percentage (0 = server default) |
| `IndexColumns` | string | _(required)_ | Comma-separated column names with optional sort direction (e.g., `"LastName ASC, FirstName ASC"`) |
| `IncludeColumns` | string | | Comma-separated INCLUDE columns |
| `FilterExpression` | string | | Filtered index WHERE clause (e.g., `"[IsActive] = 1"`) |

### Examples

```json
{
    "Name": "PK_Customer",
    "PrimaryKey": true,
    "Clustered": true,
    "IndexColumns": "CustomerId"
}
```

```json
{
    "Name": "IX_Customer_Email",
    "Unique": true,
    "IndexColumns": "Email",
    "FilterExpression": "[Email] IS NOT NULL"
}
```

```json
{
    "Name": "IX_Customer_Name",
    "IndexColumns": "LastName ASC, FirstName ASC",
    "IncludeColumns": "Email, Phone"
}
```

---

## XML Indexes

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | _(required)_ | Index name |
| `IsPrimary` | bool | `false` | `true` for a PRIMARY XML INDEX |
| `Column` | string | _(required)_ | Name of the XML column |
| `PrimaryIndex` | string | | Name of the primary XML index (required for secondary indexes) |
| `SecondaryIndexType` | string | | `"PATH"`, `"VALUE"`, or `"PROPERTY"` (for secondary indexes) |

---

## Foreign Keys

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | _(required)_ | Constraint name |
| `Columns` | string | _(required)_ | Comma-separated local column names |
| `RelatedTableSchema` | string | `"dbo"` | Schema of the referenced table |
| `RelatedTable` | string | _(required)_ | Referenced table name |
| `RelatedColumns` | string | _(required)_ | Comma-separated referenced column names |
| `DeleteAction` | string | `"NO ACTION"` | `"NO ACTION"`, `"CASCADE"`, `"SET NULL"`, `"SET DEFAULT"` |
| `UpdateAction` | string | `"NO ACTION"` | `"NO ACTION"`, `"CASCADE"`, `"SET NULL"`, `"SET DEFAULT"` |

### Example

```json
{
    "Name": "FK_Order_Customer",
    "Columns": "CustomerId",
    "RelatedTable": "Customer",
    "RelatedColumns": "CustomerId",
    "DeleteAction": "CASCADE"
}
```

---

## Check Constraints

| Property | Type | Description |
|---|---|---|
| `Name` | string | Constraint name |
| `Expression` | string | Boolean expression (e.g., `"[Quantity] > 0"`) |

---

## Statistics

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | _(required)_ | Statistics name |
| `Columns` | string | _(required)_ | Comma-separated column names |
| `SampleSize` | int | `0` | Sampling percentage (0-100, 0 = default sampling) |
| `FilterExpression` | string | | Filtered statistics WHERE clause |

---

## Full-Text Index

| Property | Type | Default | Description |
|---|---|---|---|
| `FullTextCatalog` | string | _(required)_ | Full-text catalog name |
| `KeyIndex` | string | _(required)_ | Name of the unique index used as the full-text key |
| `ChangeTracking` | string | `"OFF"` | `"OFF"`, `"MANUAL"`, or `"AUTO"` |
| `StopList` | string | | Stop list name |
| `Columns` | string | | JSON array of column configurations with language and type weight |

---

## Complete Example

```json
{
    "Schema": "dbo",
    "Name": "Customer",
    "CompressionType": "PAGE",
    "Columns": [
        { "Name": "CustomerId", "DataType": "int IDENTITY(1,1)", "Nullable": false },
        { "Name": "FirstName", "DataType": "nvarchar(100)", "Nullable": false },
        { "Name": "LastName", "DataType": "nvarchar(100)", "Nullable": false },
        { "Name": "Email", "DataType": "nvarchar(255)", "Nullable": true },
        { "Name": "CreatedDate", "DataType": "datetime2(7)", "Nullable": false, "Default": "(getutcdate())" },
        { "Name": "FullName", "ComputedExpression": "[FirstName] + ' ' + [LastName]", "Persisted": true }
    ],
    "Indexes": [
        { "Name": "PK_Customer", "PrimaryKey": true, "Clustered": true, "IndexColumns": "CustomerId" },
        { "Name": "IX_Customer_Email", "Unique": true, "IndexColumns": "Email", "FilterExpression": "[Email] IS NOT NULL" },
        { "Name": "IX_Customer_Name", "IndexColumns": "LastName ASC, FirstName ASC", "IncludeColumns": "Email" }
    ],
    "ForeignKeys": [],
    "CheckConstraints": [
        { "Name": "CK_Customer_Email", "Expression": "[Email] LIKE '%@%.%'" }
    ],
    "Statistics": [
        { "Name": "ST_Customer_CreatedDate", "Columns": "CreatedDate", "SampleSize": 50 }
    ]
}
```

---

## Related Documentation

- [Schema Packages](schema-packages.md)
- [Products and Templates](products-and-templates.md)
- [SchemaQuench Overview](schemaquench/README.md)
