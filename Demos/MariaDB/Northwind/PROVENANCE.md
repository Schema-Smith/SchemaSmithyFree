# Northwind (MySQL)

## Source

| | |
|---|---|
| **Source** | SchemaSmith team port from Microsoft Northwind (instnwnd.sql) |
| **License** | MIT (derived from Microsoft source) |
| **Self-port** | Yes — no community MySQL port with faithful classic schema |

## Extraction Notes

- Hand-crafted MySQL port from SQL Server extraction
- 13 tables, 16 views, 7 procedures, all data (11 data tables)
- MergeType: `Insert/Update` on all data tables

### Type Mapping

- `NVARCHAR(N)` to `VARCHAR(N)`, `NCHAR(N)` to `CHAR(N)`, `NTEXT` to `TEXT`
- `IMAGE` to `MEDIUMBLOB`
- `MONEY` to `DECIMAL(19,4)`
- `REAL` to `FLOAT`
- `BIT` to `TINYINT(1)`
- `INT IDENTITY` to `INT AUTO_INCREMENT`

### Non-Portable Items

- `CK_Birthdate` check constraint skipped (uses `getdate()` — MySQL doesn't allow non-deterministic functions in CHECK)

### Validation

Full round-trip validated: quench to clean database, row counts match SQL Server source exactly, idempotency verified.
