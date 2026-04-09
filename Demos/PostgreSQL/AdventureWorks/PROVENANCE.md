# AdventureWorks (PostgreSQL)

## Source

| | |
|---|---|
| **Repository** | [lorint/AdventureWorks-for-Postgres](https://github.com/lorint/AdventureWorks-for-Postgres) |
| **Version** | Latest main (downloaded 2026-03-21) |
| **License** | MIT |
| **Self-port** | No — community port based on AdventureWorks 2014 OLTP |

## Extraction Notes

- Source loaded via `install.sql` from the lorint repo
- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 68 tables, 87 views (20 base + 67 shorthand), 2 materialized views, 21 functions, 36 sequences, 10 schemas, 6 domain types, 3 composite types, 0 extraction errors
- All 68 data tables configured with `MergeType: Insert/Update`
- Materialized views: `person.vstateprovincecountryregion`, `production.vproductanddescription` (with unique indexes)
- Full round-trip validated: quench to clean database, zero structural differences, idempotency verified
- Check constraint expressions stored without `CHECK ((...))` wrapper — SchemaQuench normalizes for comparison
- FillFactor 100 (PostgreSQL default) explicitly set on quenched indexes — cosmetically different from source but functionally identical
