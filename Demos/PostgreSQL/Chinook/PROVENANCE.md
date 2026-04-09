# Chinook (PostgreSQL)

## Source

| | |
|---|---|
| **Repository** | [lerocha/chinook-database](https://github.com/lerocha/chinook-database) |
| **File** | `ChinookDatabase/DataSources/Chinook_PostgreSql_AutoIncrementPKs.sql` |
| **Version** | v1.4.5 |
| **License** | MIT |
| **Self-port** | No — lerocha native PostgreSQL variant |

## Extraction Notes

- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 11 tables, 10 sequences, all data
- MergeType: `Insert/Update` on all tables except playlist_track (`Insert`)
- Identity columns use sequence-based defaults (`nextval()`) instead of `GENERATED ALWAYS AS IDENTITY` — workaround for SchemaQuench identity column quench issue
- Full round-trip validated: quench to clean database, exact row counts, idempotency verified
