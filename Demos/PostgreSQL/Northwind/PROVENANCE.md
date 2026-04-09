# Northwind (PostgreSQL)

## Source

| | |
|---|---|
| **Repository** | [pthom/northwind_psql](https://github.com/pthom/northwind_psql) |
| **File** | `northwind.sql` |
| **Version** | Latest main (downloaded 2026-03-22) |
| **License** | See repo LICENSE |
| **Self-port** | No — community PostgreSQL port |

## Extraction Notes

- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 14 tables (classic 13 + `us_states`), tables and data only (no views or procedures — pthom source is tables-only)
- 12 data tables (customer_customer_demo and customer_demographics are empty reference tables)
- MergeType: `Insert/Update` on all data tables

### Differences from SQL Server Northwind

The pthom PostgreSQL port differs from the Microsoft SQL Server source in a few areas:
- `us_states` table (51 rows) — not in the SQL Server source
- `shippers` has 6 rows vs 3 in SQL Server
- No views or stored procedures (SQL Server has 16 views, 7 procedures)
- Snake_case naming convention (e.g., `order_details` vs `Order Details`)

### Deep Validation

- All 14 tables: exact row counts match source
- All columns: identical types, lengths, nullability
- All indexes: identical
- All foreign keys: identical
- Idempotency: second quench clean — zero structural changes
