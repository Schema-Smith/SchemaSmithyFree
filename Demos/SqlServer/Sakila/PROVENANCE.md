# Sakila (SQL Server)

## Source

| | |
|---|---|
| **Source** | SchemaSmith team port from jOOQ/sakila MySQL and PostgreSQL originals |
| **License** | BSD-2-Clause (derived from jOOQ source) |
| **Self-port** | Yes — jOOQ SQL Server variant has incomplete procs/functions/triggers |

## Extraction Notes

- Hand-crafted T-SQL port combining features from both MySQL and PostgreSQL jOOQ Sakila variants
- 16 tables (15 base + film_text shadow table), 7 views, 4 functions, 3 procedures, 17 triggers
- 15 data tables — film_text excluded (populated by insert/update/delete triggers on film)
- Data reused from MySQL extraction (identical row data, column names match)
- MergeType: `Insert/Update` on all data tables

### Type Mapping

- MySQL `varchar`/`char`/`text` (utf8mb3) to `NVARCHAR`/`NCHAR`/`NVARCHAR(MAX)` (Unicode)
- MySQL `year` to `SMALLINT`
- MySQL `enum` to `NVARCHAR(10)` with CHECK constraint
- MySQL `set` to `NVARCHAR(255)`
- MySQL `mediumblob` to `VARBINARY(MAX)`
- MySQL `timestamp`/`datetime` to `DATETIME`
- MySQL `AUTO_INCREMENT` to `IDENTITY`

### Foreign Key Adjustments

All FK UpdateAction changed from CASCADE to NO ACTION. SQL Server rejects multiple cascade paths to the same table (e.g., payment references both customer and rental, which also references customer). Since all PKs are identity columns that never change, CASCADE UPDATE is functionally unused.

`fk_payment_rental` DeleteAction also changed from SET NULL to NO ACTION (same cascade path restriction).

### Object Sources

| Object | Source | Notes |
|--------|--------|-------|
| Tables | MySQL | Type-mapped to SQL Server equivalents |
| Views (7) | Both | Converted GROUP_CONCAT/custom aggregate to STRING_AGG (SQL Server 2017+) |
| Scalar functions (4) | PostgreSQL | get_customer_balance, inventory_held_by_customer, inventory_in_stock, last_day |
| Procedures (3) | MySQL | film_in_stock, film_not_in_stock, rewards_report |
| last_updated triggers (14) | PostgreSQL | AFTER UPDATE triggers setting last_update = GETDATE() |
| film_text triggers (3) | MySQL | AFTER INSERT/UPDATE/DELETE maintaining film_text shadow table |
| CHECK constraint | MySQL enum | CK_film_rating for rating column values |

### Not Ported

- Payment partition tables and rules (PostgreSQL only — SQL Server has native partitioning but not needed for demo)
- Custom aggregate group_concat (PostgreSQL only — replaced by STRING_AGG)
- Domain type year (PostgreSQL only — mapped to SMALLINT)
- Enum type mpaa_rating (PostgreSQL only — mapped to CHECK constraint)
- Sequences (PostgreSQL only — replaced by IDENTITY columns)

## Validation

Full round-trip validated: quench to clean database, exact row counts match MySQL/PostgreSQL, idempotency verified.
