# DataTongs

Applies to: DataTongs (SQL Server, Community)

---

## What DataTongs Does

DataTongs extracts table data from a live SQL Server database and generates standalone MERGE scripts for data synchronization. These scripts can be placed into a schema package's `Table Data` or `MigrationScripts` folders for execution by SchemaQuench.

---

## How It Works

1. **Connect** to the source database
2. **For each configured table:**
   1. Validate the table exists
   2. Query the table's column metadata (data types, identity, computed columns)
   3. Extract data as JSON using `FOR JSON AUTO`
   4. Generate a MERGE script with the extracted data embedded inline
3. **Write output** — Each table produces a `Populate schema.tablename.sql` file in the configured output directory

---

## Output

DataTongs generates one SQL file per table named `Populate schema.tablename.sql`. Each file contains a complete, self-contained MERGE statement that can be executed against any target database with the matching table structure.

### Generated MERGE Script Structure

```sql
DECLARE @v_json NVARCHAR(MAX) = '<extracted JSON data>';

ALTER TABLE [schema].[table] DISABLE TRIGGER ALL;        -- if DisableTriggers
SET IDENTITY_INSERT [schema].[table] ON;                  -- if identity column exists

MERGE INTO [schema].[table] AS Target
USING (
    SELECT <columns>
    FROM OPENJSON(@v_json)
    WITH (<column type definitions>)
) AS Source
ON Source.[KeyCol] = Target.[KeyCol]

WHEN MATCHED AND (<change detection>) THEN
    UPDATE SET <column assignments>                       -- if MergeUpdate

WHEN NOT MATCHED BY TARGET THEN
    INSERT (<columns>) VALUES (<source columns>)

WHEN NOT MATCHED BY SOURCE AND (<filter>) THEN
    DELETE                                                -- if MergeDelete
;

SET IDENTITY_INSERT [schema].[table] OFF;                 -- if identity column exists
ALTER TABLE [schema].[table] ENABLE TRIGGER ALL;          -- if DisableTriggers
```

---

## Table Selection

Tables to extract are configured in the `Tables` array in `DataTongs.settings.json`:

```json
"Tables": [
    { "Name": "dbo.Country", "KeyColumns": "CountryCode" },
    { "Name": "dbo.Currency", "KeyColumns": "CurrencyCode", "Filter": "IsActive = 1" }
]
```

| Field | Required | Description |
|-------|----------|-------------|
| `Name` | Yes | Table name in `schema.table` format. If no schema is specified, `dbo` is assumed. |
| `KeyColumns` | No | Comma-separated column names used in the MERGE `ON` clause. When blank, auto-detected from the table's primary key or best unique index. Prefix a column with `*` to indicate it is nullable (e.g., `*NullableCode`). |
| `Filter` | No | SQL `WHERE` clause (without the `WHERE` keyword) to filter which rows are extracted. Also applied to `WHEN NOT MATCHED BY SOURCE` when `MergeDelete` is enabled. |

Empty tables are automatically skipped during extraction.

---

## Special Column Handling

- **Identity columns** — Detected automatically. `SET IDENTITY_INSERT ON/OFF` is added to the script. Identity columns are included in `INSERT` but excluded from `UPDATE`.
- **Computed columns** — Excluded from extraction and all MERGE clauses.
- **ROWGUIDCOL columns** — Excluded from extraction.
- **Geography columns** — Extracted using `STAsText()` (WKT format) with `STSrid`. Restored using `geography::STGeomFromText()`.
- **Geometry columns** — Extracted using `STAsText()` (WKT format) with `STSrid`. Restored using `geometry::STGeomFromText()`.
- **HierarchyID columns** — Extracted as canonical string. Restored using `hierarchyid::Parse()`.
- **XML columns** — Cast to `NVARCHAR(MAX)` for comparison in change detection.
- **NTEXT columns** — Cast to `NVARCHAR(MAX)` for comparison.
- **TEXT columns** — Cast to `VARCHAR(MAX)` for comparison.
- **IMAGE columns** — Cast to `VARBINARY(MAX)` for comparison.
- **sql_variant, rowversion, timestamp columns** — Automatically excluded from extraction.

---

## Change Detection

The `WHEN MATCHED` clause only fires when the source row differs from the target row. Change detection uses type-aware comparison with NULL-safe equality (both NULL = equal, one NULL = different).

---

## Source Query

Data is extracted using `SELECT ... FROM [schema].[table] WITH (NOLOCK)` with `FOR JSON AUTO` formatting. When a `Filter` is specified, it is applied as a `WHERE` clause.

---

## Related Documentation

- [DataTongs Configuration](configuration.md)
- [CLI Options](../cli-options.md)
- [Getting Started](../getting-started.md)
