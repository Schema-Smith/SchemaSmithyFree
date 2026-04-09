# Northwind (SQL Server)

## Source

| | |
|---|---|
| **Repository** | [microsoft/sql-server-samples](https://github.com/microsoft/sql-server-samples) |
| **File** | `samples/databases/northwind-pubs/instnwnd.sql` |
| **License** | MIT |
| **Self-port** | No — Microsoft official |

## Extraction Notes

- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 13 tables, 16 views, 7 procedures
- 11 data tables (CustomerCustomerDemo and CustomerDemographics are empty reference tables)
- MergeType: `Insert/Update` on all data tables

### Binary Photo/Picture Data

The canonical `instnwnd.sql` script contains inline hex literals for `Categories.Picture` and `Employees.Photo` columns. These hex literals exceed the `sqlcmd` parser's identifier length limit on modern SQL Server, causing insert failures when loaded via `sqlcmd`.

**Workaround:** The database was loaded with Photo/Picture as NULL, then the binary hex values were extracted from the original source script via Python and injected as base64-encoded data into the `.tabledata` files. The data delivery via SchemaQuench (ADO.NET) handles the binary data correctly. Byte lengths verified against the source script — exact match.

### Deep Validation

- All 13 tables: exact row counts match source
- 88 columns: zero type/length/nullable/identity differences
- 40 indexes: all match
- 13 foreign keys: zero differences
- 8 check constraints: match
- Binary data: byte-perfect (8 categories with Picture, 7 of 9 employees with Photo)
- Idempotency: second quench clean — zero structural changes
