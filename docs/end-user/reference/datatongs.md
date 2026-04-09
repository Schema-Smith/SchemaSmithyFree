# DataTongs Reference

Lookup tables, configuration rows, seed data -- every database has reference data that needs to travel with the schema. DataTongs grips that data from a live database and produces self-contained synchronization scripts that bring it into any target. Point it at a source, list the tables you care about, and it produces one SQL file per table -- ready to drop into a schema package's `Table Data` folder or run directly against any compatible instance.

DataTongs supports **SQL Server**, **PostgreSQL**, and **MySQL**. The script syntax adapts per platform: SQL Server and PostgreSQL use `MERGE`, MySQL uses `INSERT ... ON DUPLICATE KEY UPDATE` (or `REPLACE` where appropriate). The configuration shape and the workflow are identical across all three.

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
    "Platform": "SqlServer",
    "ConnectionProperties": {
      "TrustServerCertificate": "True"
    }
  },
  "OutputPath": ".",
  "Tables": [
    { "Name": "dbo.Country",  "KeyColumns": "CountryCode" },
    { "Name": "dbo.Currency", "KeyColumns": "CurrencyCode", "Filter": "IsActive = 1" }
  ],
  "ShouldCast": {
    "DisableTriggers": false,
    "MergeUpdate": true,
    "MergeDelete": true
  }
}
```

### Source connection

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Source:Platform` | string | _(required)_ | One of `"SqlServer"`, `"PostgreSQL"`, or `"MySQL"`. Selects the script generator. |
| `Source:Server` | string | _(required)_ | Database server hostname or IP. |
| `Source:Port` | string | platform default | TCP port. SQL Server `1433`, PostgreSQL `5432`, MySQL `3306`. |
| `Source:User` | string | _(empty)_ | Login username. SQL Server allows blank for Windows authentication. |
| `Source:Password` | string | _(empty)_ | Login password. |
| `Source:Database` | string | _(required)_ | Source database to extract data from. |
| `Source:ConnectionProperties` | object | `{}` | Arbitrary key-value pairs appended to the connection string. Platform-specific keys -- see the [Configuration Reference](configuration.md#connection-configuration). |

The `--ConnectionString` switch bypasses all `Source` settings and passes the provided value directly to the platform-appropriate driver. `Source:Platform` is still required so the right script generator is used.

### Output

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `OutputPath` | string | `"."` | Directory where generated scripts are written. Created automatically if it doesn't exist. |

### Tables array

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Name` | string | _(required)_ | Table name in `schema.table` format. If no schema prefix is given, the platform default is assumed (`dbo` on SQL Server, `public` on PostgreSQL, the connection database on MySQL). |
| `KeyColumns` | string | _(auto-detected)_ | Comma-separated column names for the row-matching key. When blank, auto-detected from the table's primary key or best unique index. Prefix a column with `*` for NULL-safe comparison on nullable keys. |
| `Filter` | string | _(empty)_ | SQL `WHERE` clause (without the `WHERE` keyword) to filter which rows are extracted. Also applied to the delete clause when `MergeDelete` is enabled. |

### Script generation flags (ShouldCast)

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ShouldCast:DisableTriggers` | bool | `false` | Wraps the generated script with platform-appropriate trigger disable/enable. |
| `ShouldCast:MergeUpdate` | bool | `true` | Includes the update branch (matched rows whose data has changed). |
| `ShouldCast:MergeDelete` | bool | `true` | Includes the delete branch (target rows missing from the source). MySQL note: `MergeDelete` is not supported via `INSERT ... ON DUPLICATE KEY UPDATE`; use `REPLACE` semantics or hand-author migration scripts when full delete sync is required on MySQL. |

For environment variable mapping, see [Configuration Reference -- Environment Variables](configuration.md#environment-variables). The `Tables` array can't be configured via environment variables -- use the JSON configuration file for table definitions.

---

## Generated Script Anatomy

What DataTongs writes to disk depends on the platform. The data extraction step is the same: query the source table in deterministic key order and serialize each row to JSON. The substitution into a sync script is platform-specific.

### SQL Server

SQL Server uses `MERGE` with `OPENJSON` to parse the embedded data:

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
  INSERT ([CompanyName], [Phone], [ShipperID])
  VALUES (Source.[CompanyName], Source.[Phone], Source.[ShipperID])

WHEN NOT MATCHED BY SOURCE THEN                        -- if MergeDelete
  DELETE
;

SET IDENTITY_INSERT [dbo].[Shippers] OFF;              -- if identity column exists
ALTER TABLE [dbo].[Shippers] ENABLE TRIGGER ALL;       -- if DisableTriggers
```

### PostgreSQL

PostgreSQL 15 and later support `MERGE` natively. DataTongs generates a `MERGE` against a `jsonb` literal source:

```sql
MERGE INTO "public"."shippers" AS "Target"
USING (
  SELECT "company_name", "phone", "shipper_id"
    FROM jsonb_to_recordset('[
      {"company_name":"Speedy Express","phone":"(503) 555-9831","shipper_id":1},
      {"company_name":"United Package","phone":"(503) 555-3199","shipper_id":2}
    ]') AS x("company_name" varchar(40), "phone" varchar(24), "shipper_id" integer)
) AS "Source"
ON "Source"."shipper_id" = "Target"."shipper_id"

WHEN MATCHED AND (...change detection...) THEN
  UPDATE SET "company_name" = "Source"."company_name",
             "phone"        = "Source"."phone"

WHEN NOT MATCHED THEN
  INSERT ("company_name", "phone", "shipper_id")
  VALUES ("Source"."company_name", "Source"."phone", "Source"."shipper_id")

WHEN NOT MATCHED BY SOURCE THEN
  DELETE
;
```

The `MERGE INTO ... ONLY` clause is used when partitioned tables should not propagate to descendants.

### MySQL

MySQL doesn't have a `MERGE` statement. DataTongs generates the appropriate idiom based on the configured behavior:

- **Insert + Update (no delete)** -- `INSERT ... ON DUPLICATE KEY UPDATE`
- **Insert only (seed)** -- `INSERT IGNORE`
- **Replace semantics** -- `REPLACE INTO` (used when targeting full row replacement)

```sql
INSERT INTO `northwind`.`shippers` (`company_name`, `phone`, `shipper_id`) VALUES
  ('Speedy Express',   '(503) 555-9831', 1),
  ('United Package',   '(503) 555-3199', 2),
  ('Federal Shipping', '(503) 555-9931', 3)
ON DUPLICATE KEY UPDATE
  `company_name` = VALUES(`company_name`),
  `phone`        = VALUES(`phone`);
```

The MySQL generator infers the right idiom from `MergeUpdate` and `MergeDelete`. Full delete sync (the equivalent of `WHEN NOT MATCHED BY SOURCE THEN DELETE`) is not directly expressible in `ON DUPLICATE KEY UPDATE`; for tables that need that semantic on MySQL, hand-author a migration script that performs the delete pass.

---

## Table Configuration

### Table name

Specify tables in `schema.table` format. If you omit the schema, the platform default is assumed:

```json
{ "Name": "dbo.Country" }                  // SQL Server: defaults to dbo
{ "Name": "config.feature_flags" }         // PostgreSQL: explicit schema
{ "Name": "Products" }                     // SQL Server: dbo.Products
```

DataTongs validates that each table exists in the source database before attempting extraction. Tables that don't exist are skipped with an error message.

### Key columns

Key columns define the row-matching predicate -- the `ON` clause on platforms that support `MERGE`, the unique-key match on MySQL.

**Auto-detection:** When `KeyColumns` is blank, DataTongs queries the table's indexes and selects the primary key if one exists. If there's no primary key, it falls back to the first available unique index. Nullable columns in the detected key are automatically prefixed with `*` for NULL-safe comparison. This handles the vast majority of tables without any manual configuration.

**Manual override:** When you specify `KeyColumns`, DataTongs uses your list instead of auto-detection. Separate multiple columns with commas:

```json
{ "Name": "dbo.OrderLine", "KeyColumns": "OrderID, LineNumber" }
```

**Nullable key columns:** If a key column allows NULLs, prefix it with `*`. This generates NULL-safe matching:

```json
{ "Name": "dbo.Mapping", "KeyColumns": "SourceCode, *TargetCode" }
```

The generated `ON` clause becomes:

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
2. **Which target rows are eligible for deletion** when `MergeDelete` is enabled (SQL Server / PostgreSQL)

```json
{ "Name": "dbo.FeatureFlags", "KeyColumns": "FlagName", "Filter": "IsActive = 1" }
```

This extracts only active flags and, if `MergeDelete` is on, only deletes active flags that no longer exist in the source. Inactive flags in the target are untouched.

---

## ShouldCast Options

### DisableTriggers

When enabled, DataTongs wraps each generated script with platform-appropriate trigger control. Use this when the target table has audit or notification triggers whose side effects are undesirable during a bulk data refresh. The generated scripts are plain SQL files -- you can edit them to disable specific triggers rather than all of them when fine-grained control is needed.

### MergeUpdate

When enabled, the script updates existing rows whose data has changed. When disabled, the script only inserts new rows and (on MERGE platforms, if `MergeDelete` is on) deletes missing rows -- existing rows are left untouched regardless of whether their data differs.

### MergeDelete (SQL Server / PostgreSQL)

When enabled, the script deletes rows from the target that don't exist in the source data. When a `Filter` is configured, the delete clause respects the filter so that rows outside the filter are never removed. When disabled, the script only inserts and updates -- no rows are ever deleted from the target.

### Delivery scenarios

These settings compose into three practical patterns:

| Scenario | MergeUpdate | MergeDelete | Effect |
|----------|-------------|-------------|--------|
| **Full sync** (default) | `true` | `true` | Insert missing, update changed, delete removed. Target matches source exactly. |
| **Add and update, no deletes** | `true` | `false` | Insert missing, update changed, leave extra rows alone. Good when targets have environment-specific additions. |
| **Seed only** | `false` | `false` | Insert missing rows only. Existing rows untouched, nothing deleted. Good for seed data without overwriting local customizations. |

The demo products use insert+update with no deletes (`MergeDelete: false`) and `DisableTriggers: true`. They deliver data for every table, so if delete were enabled, any rows a user added while experimenting would be removed on the next deployment.

---

## Special Type Handling

DataTongs detects column types and applies the correct extraction and restoration strategy for each. The full set varies by platform; the highlights:

### SQL Server

- **Identity columns** -- detected automatically. The script wraps the merge with `SET IDENTITY_INSERT ON`/`OFF` and includes the identity in the `INSERT` clause but excludes it from `UPDATE SET`.
- **Computed columns** -- auto-excluded from extraction and all script sections.
- **Geography** -- full round-trip via WKT (`.ToString()`) plus the SRID captured separately.
- **Geometry** -- extracted as WKT (basic support; restoration may need manual adjustment for complex cases).
- **HierarchyID** -- canonical string round-trip.
- **XML / NTEXT / TEXT / IMAGE** -- mapped through the OPENJSON layer with type-appropriate cast for change detection.
- **Auto-excluded:** `sql_variant`, `rowversion` / `timestamp`, ROWGUIDCOL columns. A warning is logged for each skipped column.

### PostgreSQL

- **Identity columns** -- handled via `OVERRIDING SYSTEM VALUE` where appropriate.
- **Generated columns** -- auto-excluded.
- **JSON / JSONB** -- preserved through the round-trip.
- **Auto-excluded with warning:** `tsvector`, `tsquery`, `money`, geometric types (`box`, `circle`, `line`, `lseg`, `path`), and user-defined composite types. These are skipped because their JSON representation can't reliably round-trip without manual intervention.

### MySQL

- **Auto-increment columns** -- handled at the `INSERT` level so explicit values can be carried.
- **Generated / virtual columns** -- auto-excluded.
- **Spatial types** -- basic support via WKT.
- **TEXT / BLOB families** -- supported through string and binary literals.

When a column is excluded from extraction, DataTongs logs a warning naming the table, the column, and the underlying type so you know exactly what was skipped.

---

## Source Query

DataTongs extracts data using a deterministic, JSON-shaped query. The exact form differs per platform (SQL Server uses `FOR JSON AUTO`, PostgreSQL uses `jsonb_agg(row_to_json(...))`, MySQL uses `JSON_OBJECT` aggregation), but the principles are the same:

- **Read-friendly hints** -- Where the platform supports a non-blocking read hint (`WITH (NOLOCK)` on SQL Server, no special hint needed on PostgreSQL or MySQL), DataTongs uses it to avoid blocking production workloads. This is appropriate because DataTongs is extracting a snapshot of reference data, not transactional data requiring strict consistency.
- **Deterministic order** -- Results are ordered by the key columns to produce diff-friendly output. Run DataTongs twice against unchanged data and you get identical files.
- **Empty tables** -- When the query returns no data, DataTongs skips script generation for that table entirely.

---

## Output

### File naming

Each table produces one file:

```
Populate <schema>.<tablename>.sql
```

For example:

| Table | Output File |
|-------|-------------|
| `dbo.Country` | `Populate dbo.Country.sql` |
| `HumanResources.Department` | `Populate HumanResources.Department.sql` |
| `public.feature_flags` | `Populate public.feature_flags.sql` |
| `northwind.shippers` | `Populate northwind.shippers.sql` |

Table and schema names containing characters that are illegal in file names are percent-encoded (see [Schema Packages -- Filesystem-Illegal Character Encoding](schema-packages.md#filesystem-illegal-character-encoding)).

### Output directory

Files are written to the directory specified by `OutputPath`. The directory is created automatically if it doesn't exist. The typical placement is a schema package's `Table Data` folder:

```json
"OutputPath": "C:\\SchemaPackage\\Templates\\Main\\Table Data"
```

When the output lands inside a `Table Data` folder of a schema package, SchemaQuench picks it up automatically on the next deployment via the `TableData` quench slot. Reference data ships alongside schema, in the same package, in the same release.

---

## Change Detection

Nobody wants a script that updates every row just because it can. The update branch of the generated script fires only when the source row actually differs from the target row. DataTongs generates a type-aware comparison for every non-key, non-identity column.

For most columns:

```sql
NOT (Target.[Column] = Source.[Column]
     OR (Target.[Column] IS NULL AND Source.[Column] IS NULL))
```

This NULL-safe comparison treats two NULLs as equal (no update needed) and a NULL versus a non-NULL as different (update needed).

For special types -- geography, XML, large text/binary types -- the comparison is wrapped with the appropriate cast or normalization function (e.g., `.ToString()` for geography on SQL Server, `CAST(... AS NVARCHAR(MAX))` for XML and NTEXT).

This approach means running DataTongs twice against unchanged data produces a script that matches every row but updates none -- the script becomes a no-op for existing data. Diffs are clean. Reviews are honest.

---

## Related Documentation

- [Configuration Reference](configuration.md) -- Shared configuration system, CLI switches, environment variables
- [Schema Packages Reference](schema-packages.md) -- How `Table Data` scripts fit into a schema package
- [SchemaQuench Reference](schemaquench.md) -- The tool that executes the generated scripts against target databases
- [Custom Properties](custom-properties.md) -- Attach team-defined metadata to the tables DataTongs round-trips
