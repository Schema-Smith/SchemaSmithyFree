# DataTongs Reference

Lookup tables, configuration rows, seed data -- every database has reference data that needs to travel with the schema. DataTongs grips that data from a live SQL Server and produces self-contained MERGE scripts that synchronize it into any target database. Point it at a source, list the tables you care about, and it produces one SQL file per table -- ready to drop into a schema package's `Table Data` folder or run directly against any SQL Server instance.

---

## Installation and Invocation

DataTongs is included in the SchemaSmith distribution. Run it from the directory containing your `DataTongs.settings.json` configuration file:

```bash
DataTongs
```

To use a configuration file in a different location:

```bash
DataTongs --ConfigFile:path/to/DataTongs.settings.json
```

DataTongs reads configuration from multiple sources, merged in this precedence order (highest priority last):

1. **Configuration file** -- `DataTongs.settings.json` in the current working directory (or the file specified by `--ConfigFile`)
2. **User secrets** (debug builds only)
3. **Environment variables** with the `SmithySettings_` prefix
4. **Command-line switches** (highest precedence)

For the full list of CLI switches shared by all SchemaSmith tools, see the [Configuration Reference](configuration.md#cli-switch-format).

---

## Configuration Reference

A complete `DataTongs.settings.json`:

```json
{
    "Source": {
        "Server": "localhost",
        "Port": "",
        "User": "",
        "Password": "",
        "Database": "ReferenceData",
        "ConnectionProperties": {
            "TrustServerCertificate": "True"
        }
    },
    "OutputPath": ".",
    "Tables": [
        { "Name": "dbo.Country", "KeyColumns": "CountryCode" },
        { "Name": "dbo.Currency", "KeyColumns": "CurrencyCode", "Filter": "IsActive = 1" }
    ],
    "ShouldCast": {
        "DisableTriggers": false,
        "MergeUpdate": true,
        "MergeDelete": true
    }
}
```

### Source Connection

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Source:Server` | string | _(required)_ | SQL Server instance name or address. |
| `Source:Port` | string | _(empty)_ | SQL Server port. When provided, appended to the server as `server,port`. You can also use the `server,port` format directly in `Server`. |
| `Source:User` | string | _(empty)_ | SQL Server login. When blank, Windows authentication is used. |
| `Source:Password` | string | _(empty)_ | SQL Server password. When blank, Windows authentication is used. |
| `Source:Database` | string | _(required)_ | Source database to extract data from. |
| `Source:ConnectionProperties` | object | _(empty)_ | Arbitrary key-value pairs appended to the connection string. Common properties include `TrustServerCertificate`, `Encrypt`, and `ApplicationIntent`. |

#### --ConnectionString Override

The `--ConnectionString` switch bypasses all `Source` settings and passes the provided value directly to the SQL Server driver:

```bash
DataTongs --ConnectionString:"data source=myserver;Initial Catalog=mydb;User ID=sa;Password=secret;TrustServerCertificate=True;"
```

When `--ConnectionString` is provided, all `Source` keys are ignored.

### Output

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `OutputPath` | string | `"."` | Directory where generated MERGE scripts are written. Created automatically if it does not exist. |

### Tables Array

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Name` | string | _(required)_ | Table name in `schema.table` format. If no schema prefix is given, `dbo` is assumed. |
| `KeyColumns` | string | _(auto-detected)_ | Comma-separated column names for the MERGE `ON` clause. When blank, auto-detected from the table's primary key or best unique index. Prefix a column with `*` to handle nullable keys. |
| `Filter` | string | _(empty)_ | SQL `WHERE` clause (without the `WHERE` keyword) to filter which rows are extracted. Also applied to the `WHEN NOT MATCHED BY SOURCE` clause when `MergeDelete` is enabled. |

### Script Generation (ShouldCast)

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ShouldCast:DisableTriggers` | bool | `false` | When `true`, wraps each MERGE with `DISABLE TRIGGER ALL` / `ENABLE TRIGGER ALL`. |
| `ShouldCast:MergeUpdate` | bool | `true` | When `true`, includes the `WHEN MATCHED THEN UPDATE` clause in MERGE scripts. |
| `ShouldCast:MergeDelete` | bool | `true` | When `true`, includes the `WHEN NOT MATCHED BY SOURCE THEN DELETE` clause in MERGE scripts. |

### Environment Variable Overrides

Any configuration key can be overridden with an environment variable using the `SmithySettings_` prefix. Nested keys use double underscores:

```bash
export SmithySettings_Source__Server="myserver\\SQLEXPRESS"
export SmithySettings_Source__Database="ProductionDB"
export SmithySettings_ShouldCast__MergeDelete="false"
export SmithySettings_OutputPath="/output/scripts"
```

The `Tables` array cannot be configured via environment variables -- use the JSON configuration file for table definitions.

---

## MERGE Script Anatomy

Understanding the generated output helps when you need to troubleshoot or customize. Each generated file contains a single, self-contained MERGE statement. Here is the full structure, using the Northwind `Shippers` table as an example:

```sql
DECLARE @v_json NVARCHAR(MAX) = '[
{"CompanyName":"Speedy Express","Phone":"(503) 555-9831","ShipperID":1},
{"CompanyName":"United Package","Phone":"(503) 555-3199","ShipperID":2},
{"CompanyName":"Federal Shipping","Phone":"(503) 555-9931","ShipperID":3}
]';

ALTER TABLE [dbo].[Shippers] DISABLE TRIGGER ALL;     -- if DisableTriggers
SET IDENTITY_INSERT [dbo].[Shippers] ON;               -- if identity column exists

MERGE INTO [dbo].[Shippers] AS Target
USING (
  SELECT [CompanyName],[Phone],[ShipperID]
    FROM OPENJSON(@v_json)
    WITH (
           [CompanyName] NVARCHAR(40),
           [Phone] NVARCHAR(24),
           [ShipperID] INT
    )
) AS Source
ON Source.[ShipperID] = Target.[ShipperID]

WHEN MATCHED AND (<change detection>) THEN             -- if MergeUpdate
  UPDATE SET
        [CompanyName] = Source.[CompanyName],
        [Phone] = Source.[Phone]

WHEN NOT MATCHED THEN
  INSERT (
        [CompanyName],
        [Phone],
        [ShipperID]
  ) VALUES (
        Source.[CompanyName],
        Source.[Phone],
        Source.[ShipperID]
  )

WHEN NOT MATCHED BY SOURCE THEN                        -- if MergeDelete
  DELETE
;

SET IDENTITY_INSERT [dbo].[Shippers] OFF;              -- if identity column exists
ALTER TABLE [dbo].[Shippers] ENABLE TRIGGER ALL;       -- if DisableTriggers
```

**Walking through each section:**

1. **`DECLARE @v_json`** -- The extracted data is embedded as a JSON array. Single quotes in data are escaped as `''`. Line breaks are inserted between JSON objects for readability.

2. **Trigger and identity preamble** -- If `DisableTriggers` is enabled, triggers are disabled before the MERGE and re-enabled after. If the table has an identity column, `SET IDENTITY_INSERT ON` allows explicit identity values to be inserted.

3. **`MERGE INTO ... AS Target`** -- The target is the destination table.

4. **`USING (SELECT ... FROM OPENJSON ...)`** -- The `OPENJSON` function parses the JSON variable into a relational result set. The `WITH` clause maps each JSON property to its SQL Server column type, preserving full type fidelity.

5. **`ON` clause** -- Matches source rows to target rows using the configured key columns.

6. **`WHEN MATCHED`** -- Updates existing rows, but only when the source differs from the target (see [Change Detection](#change-detection) below).

7. **`WHEN NOT MATCHED`** -- Inserts rows that exist in the source but not in the target.

8. **`WHEN NOT MATCHED BY SOURCE`** -- Deletes rows that exist in the target but not in the source. When a `Filter` is configured, the delete clause includes the filter condition so that rows outside the filter are not affected.

9. **Identity and trigger cleanup** -- Reverses the preamble.

---

## Table Configuration

### Table Name

Specify tables in `schema.table` format. If you omit the schema, `dbo` is assumed:

```json
{ "Name": "dbo.Country" }
{ "Name": "config.FeatureFlags" }
{ "Name": "Products" }              // treated as dbo.Products
```

DataTongs validates that each table exists in the source database before attempting extraction. Tables that do not exist are skipped with an error message.

### Key Columns

Key columns define the MERGE `ON` clause -- they determine how source rows are matched to target rows.

**Auto-detection:** When `KeyColumns` is blank, DataTongs automatically detects the best key by querying the table's indexes. It selects the primary key if one exists. If there is no primary key, it falls back to the first available unique index. Nullable columns in the detected key are automatically prefixed with `*` for NULL-safe comparison. This handles the vast majority of tables without any manual configuration.

**Manual override:** When you specify `KeyColumns`, DataTongs uses your list instead of auto-detection. Separate multiple columns with commas:

```json
{ "Name": "dbo.OrderLine", "KeyColumns": "OrderID, LineNumber" }
```

**Nullable key columns:** If a key column allows NULLs, prefix it with `*`. This generates NULL-safe matching (`IS NULL AND IS NULL` in addition to equality):

```json
{ "Name": "dbo.Mapping", "KeyColumns": "SourceCode, *TargetCode" }
```

Without the `*` prefix, the MERGE generates:

```sql
ON Source.[SourceCode] = Target.[SourceCode]
   AND Source.[TargetCode] = Target.[TargetCode]
```

With the `*` prefix on `TargetCode`:

```sql
ON Source.[SourceCode] = Target.[SourceCode]
   AND (Source.[TargetCode] = Target.[TargetCode]
        OR (Source.[TargetCode] IS NULL AND Target.[TargetCode] IS NULL))
```

When auto-detection discovers a nullable column in a unique index, it automatically applies the `*` behavior.

**No key available:** If a table has no primary key, no unique index, and no `KeyColumns` configured, DataTongs skips the table with an error message.

### Filter

The `Filter` field accepts a SQL `WHERE` clause (without the `WHERE` keyword). It controls two things:

1. **Which rows are extracted** from the source database
2. **Which target rows are eligible for deletion** when `MergeDelete` is enabled

```json
{ "Name": "dbo.FeatureFlags", "KeyColumns": "FlagName", "Filter": "IsActive = 1" }
```

This extracts only active flags and, if `MergeDelete` is on, only deletes active flags that no longer exist in the source. Inactive flags in the target are untouched.

---

## ShouldCast Options

### DisableTriggers

**Default:** `false`

When enabled, DataTongs wraps each MERGE with trigger control:

```sql
ALTER TABLE [dbo].[Country] DISABLE TRIGGER ALL;

MERGE INTO [dbo].[Country] AS Target
-- ... MERGE body ...
;

ALTER TABLE [dbo].[Country] ENABLE TRIGGER ALL;
```

Use this when the target table has triggers that should not fire during data synchronization (for example, audit triggers that would log every MERGE operation as a user change).

### MergeUpdate

**Default:** `true`

When enabled, the MERGE includes a `WHEN MATCHED` clause that updates existing rows whose data has changed. When disabled, the MERGE only inserts new rows and (if `MergeDelete` is on) deletes missing rows -- existing rows are left untouched regardless of whether their data differs.

### MergeDelete

**Default:** `true`

When enabled, the MERGE includes a `WHEN NOT MATCHED BY SOURCE` clause that deletes rows from the target that do not exist in the source data. When a `Filter` is configured, the delete clause includes the filter condition:

```sql
WHEN NOT MATCHED BY SOURCE AND (IsActive = 1) THEN
  DELETE
```

This ensures that only rows matching the filter are candidates for deletion. Rows outside the filter are never removed.

When disabled, the MERGE only inserts and updates -- no rows are ever deleted from the target.

---

## Special Type Handling

SQL Server has a lot of column types, and they don't all serialize to JSON the same way. DataTongs automatically detects column types and applies the correct extraction and restoration strategy for each. No manual configuration is needed.

### Identity Columns

**Detected automatically.** When a table contains an identity column, the generated script wraps the MERGE with:

```sql
SET IDENTITY_INSERT [schema].[table] ON;
-- MERGE ...
SET IDENTITY_INSERT [schema].[table] OFF;
```

Identity columns are included in the `INSERT` clause (so rows retain their original identity values) but excluded from the `UPDATE SET` clause (identity values cannot be updated).

### Computed Columns

**Auto-excluded.** Computed columns are detected via `sys.computed_columns` and excluded from all parts of the script -- extraction query, OPENJSON mapping, INSERT, and UPDATE. SQL Server recalculates them automatically.

### Geography Columns

**Full round-trip support.** Geography data is extracted as Well-Known Text (WKT) using `.ToString()`, with the spatial reference identifier (SRID) captured separately via `.STSrid`. On restoration, the OPENJSON SELECT reconstructs the geography value:

```sql
SELECT geography::STGeomFromText([Location], [Location.STSrid]) AS [Location]
  FROM OPENJSON(@v_json)
  WITH (
    [Location] NVARCHAR(4000),
    [Location.STSrid] INT
  )
```

For change detection in the UPDATE clause, geography columns are compared using `.ToString()` to convert both sides to WKT before comparison.

### Geometry Columns

Geometry data is extracted using `.ToString()` (WKT format) with the SRID captured via `.STSrid`. Note: extraction works correctly, but MERGE script restoration for geometry columns is not fully implemented — geography columns have complete round-trip support while geometry columns may require manual adjustment of the generated scripts.

### HierarchyID Columns

Extracted as a canonical string representation using `.ToString()`. The OPENJSON mapping uses `NVARCHAR(4000)` to carry the string value, which SQL Server implicitly converts back to `hierarchyid` on insert and update.

### XML Columns

XML columns are mapped through OPENJSON using their native XML type (preserving any XML schema collection binding). For change detection in the UPDATE clause, both source and target values are cast to `NVARCHAR(MAX)` before comparison, since XML does not support direct equality.

### NTEXT Columns

Mapped as `NVARCHAR(MAX)` in the OPENJSON `WITH` clause. For change detection, both sides are cast to `NVARCHAR(MAX)`.

### TEXT Columns

Mapped as `VARCHAR(MAX)` in the OPENJSON `WITH` clause. For change detection, both sides are cast to `VARCHAR(MAX)`.

### IMAGE Columns

Mapped as `VARBINARY(MAX)` in the OPENJSON `WITH` clause. For change detection, both sides are cast to `VARBINARY(MAX)`.

### Auto-Excluded Types

The following column types are automatically excluded from extraction and all MERGE clauses:

| Type | Reason |
|------|--------|
| `sql_variant` | Cannot be reliably serialized to JSON and restored with full type fidelity. |
| `rowversion` / `timestamp` | Server-managed binary values that are automatically assigned on insert and update. |
| ROWGUIDCOL columns | Columns marked with the `ROWGUIDCOL` property are excluded from extraction and all MERGE clauses (SELECT, INSERT, UPDATE). |

---

## Source Query

DataTongs extracts data using a query of this form:

```sql
SELECT CAST((
  SELECT [Col1], [Col2], ...
    FROM [schema].[table] WITH (NOLOCK)
    WHERE <filter>                         -- only if Filter is configured
    ORDER BY <key columns>
    FOR JSON AUTO
) AS NVARCHAR(MAX))
```

Key points:

- **`WITH (NOLOCK)`** -- Reads are performed with the NOLOCK hint to avoid blocking production workloads. This is appropriate because DataTongs is extracting a snapshot of reference data, not transactional data requiring strict consistency.
- **`ORDER BY`** -- Results are ordered by the key columns to produce deterministic, diff-friendly output.
- **`FOR JSON AUTO`** -- SQL Server serializes the result set as a JSON array. DataTongs inserts line breaks between objects for readability in the output file.
- **Empty tables** -- When the query returns no data, DataTongs skips script generation for that table entirely.

---

## Output

### File Naming

Each table produces one file named:

```
Populate <schema>.<tablename>.sql
```

For example:

| Table | Output File |
|-------|-------------|
| `dbo.Country` | `Populate dbo.Country.sql` |
| `HumanResources.Department` | `Populate HumanResources.Department.sql` |
| `dbo.Order Details` | `Populate dbo.Order Details.sql` |

Table and schema names containing characters that are illegal in file names (such as `\`, `/`, `:`, `*`, `?`, `"`, `<`, `>`, `|`) are percent-encoded (for example, `*` becomes `%2A`). Leading and trailing spaces or dots are also encoded.

### Output Directory

Files are written to the directory specified by `OutputPath`. The directory is created automatically if it does not exist. The typical placement is a schema package's `Table Data` folder:

```json
"OutputPath": "C:\\SchemaPackage\\Templates\\Main\\Table Data"
```

---

## Change Detection

Nobody wants a MERGE that updates every row just because it can. The `WHEN MATCHED` clause fires only when the source row actually differs from the target row. DataTongs generates a type-aware comparison for every non-key, non-identity column:

**Standard columns:**

```sql
NOT (Target.[Column] = Source.[Column]
     OR (Target.[Column] IS NULL AND Source.[Column] IS NULL))
```

This NULL-safe comparison treats two NULLs as equal (no update needed) and a NULL versus a non-NULL as different (update needed).

**Special type comparisons:**

| Column Type | Comparison Method |
|-------------|-------------------|
| Geography | `.ToString()` on both sides (WKT comparison) |
| XML | `CAST(... AS NVARCHAR(MAX))` on both sides |
| NTEXT | `CAST(... AS NVARCHAR(MAX))` on both sides |
| TEXT | `CAST(... AS VARCHAR(MAX))` on both sides |
| IMAGE | `CAST(... AS VARBINARY(MAX))` on both sides |
| All others | Direct equality |

This approach means running DataTongs twice against unchanged data produces a script that matches every row but updates none -- the MERGE becomes a no-op for existing data.

---

## Related Documentation

- [Configuration Reference](configuration.md) -- Shared configuration system, CLI switches, environment variables
- [Schema Packages Reference](schema-packages.md) -- How Table Data scripts fit into a schema package
- [SchemaQuench Reference](schemaquench.md) -- The tool that executes MERGE scripts against target databases
- [Power Workflows -- Reference Data Management](../guide/05-power-workflows.md#reference-data-management-with-datatongs) -- Practical walkthrough of the DataTongs workflow
