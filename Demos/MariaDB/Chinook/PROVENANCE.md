# Chinook (MySQL)

## Source

| | |
|---|---|
| **Repository** | [lerocha/chinook-database](https://github.com/lerocha/chinook-database) |
| **File** | `ChinookDatabase/DataSources/Chinook_MySql_AutoIncrementPKs.sql` |
| **Version** | v1.4.5 |
| **License** | MIT |
| **Self-port** | No — lerocha native MySQL variant |

## Extraction Notes

- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 11 tables, all data (275 artists, 347 albums, 3503 tracks, 412 invoices, etc.)
- MergeType: `Insert/Update` on all tables except PlaylistTrack (`Insert` — all columns are PK, Insert/Update generates ambiguous column error)
- Full round-trip validated: quench to clean database, exact row counts, idempotency verified
