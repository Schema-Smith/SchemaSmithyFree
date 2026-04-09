# Chinook (SQL Server)

## Source

| | |
|---|---|
| **Repository** | [lerocha/chinook-database](https://github.com/lerocha/chinook-database) |
| **File** | `ChinookDatabase/DataSources/Chinook_SqlServer_AutoIncrementPKs.sql` |
| **Version** | v1.4.5 |
| **License** | MIT |
| **Self-port** | No — lerocha native SQL Server variant |

## Extraction Notes

- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 11 tables, all data (275 artists, 347 albums, 3503 tracks, 412 invoices, etc.)
- MergeType: `Insert/Update` on all tables except PlaylistTrack (`Insert`)
- Full round-trip validated: quench to clean database, exact row counts, idempotency verified
