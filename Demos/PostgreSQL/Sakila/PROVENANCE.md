# Sakila (PostgreSQL)

## Source

| | |
|---|---|
| **Repository** | [jOOQ/sakila](https://github.com/jOOQ/sakila) |
| **Path** | `postgres-sakila-db/` |
| **Version** | Latest main (downloaded 2026-03-22) |
| **License** | BSD-2-Clause |
| **Self-port** | No — jOOQ native PostgreSQL variant |

## Extraction Notes

- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 21 tables (15 base + 6 payment partition tables), 7 views, 9 functions, 1 aggregate, 13 sequences, 6 rules, 15 triggers, 1 domain type, 1 enum type
- 15 data tables — partition tables excluded (empty; populated via rules from `payment`)
- MergeType: `Insert/Update` on all data tables
- `payment` table uses `MergeDisableRules: true` — PostgreSQL cannot execute MERGE on tables with rules; rules are disabled individually by name during data delivery, then re-enabled
- Full round-trip validated: quench to clean database, exact row counts, idempotency verified

## Replaces DVDRental

This product replaces the previous PostgreSQL DVDRental demo product, which was extracted from an undocumented source (the postgresqltutorial.com DVDRental sample — a repackaged Sakila). The jOOQ Sakila source is richer (payment partitions, rules, more functions/triggers) and has documented provenance with a clear BSD-2-Clause license.
