# Sakila (MySQL)

## Source

| | |
|---|---|
| **Repository** | [jOOQ/sakila](https://github.com/jOOQ/sakila) |
| **Path** | `mysql-sakila-db/` |
| **Version** | Latest main (downloaded 2026-03-22) |
| **License** | BSD-2-Clause |
| **Self-port** | No — original MySQL source |

## Extraction Notes

- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 16 tables, 7 views, 3 functions, 3 procedures, 3 triggers
- 15 data tables — `film_text` excluded (populated by `ins_film`/`upd_film`/`del_film` triggers on `film`)
- MergeType: `Insert/Update` on all data tables
- Character set: `utf8mb3` (matches jOOQ source); database-level `utf8mb4_unicode_ci`
  - **Deliberately diverges from the MySQL twin, which uses `utf8mb4_0900_ai_ci`.** That collation is MySQL-only — MariaDB did not gain it until 11.x, so it fails on any MariaDB at or near SchemaSmith's 10.6 floor with `Unknown collation`. `utf8mb4_unicode_ci` is UCA-based like the original and is present on every supported MariaDB (verified on 10.6). Do not "resync" this value to match the MySQL package.
- Full round-trip validated: quench to clean database, exact row counts, idempotency verified

## Differences from Previous Product

The previous demo product was sourced from the MySQL official Sakila sample database, not jOOQ. Key differences:

- Different character sets (`utf8mb4` vs `utf8mb3`) and data types (e.g., `smallint` vs `int`, different varchar lengths)
- 3 additional triggers (`customer_create_date`, `payment_date`, `rental_date`) not present in jOOQ source
- Legacy `Upsert` MergeType replaced with `Insert/Update`
- Data folder renamed from `TableContents` to `Table Data` (standardized)
