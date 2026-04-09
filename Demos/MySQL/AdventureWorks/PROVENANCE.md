# AdventureWorks (MySQL)

## Source

| | |
|---|---|
| **Source** | SchemaSmith team port from Microsoft AdventureWorks2022 |
| **License** | MIT (derived from Microsoft source) |
| **Self-port** | Yes — no community MySQL port with acceptable license and completeness |

## Extraction Notes

- Source created via automated conversion from SQL Server extracted JSON table definitions + manual T-SQL to MySQL conversion for views, functions, procedures, and triggers
- Data loaded from SQL Server `.tabledata` JSON files (69 tables, ~200K rows)
- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 71 tables, 12 views, 9 functions, 5 procedures, 4 triggers, 6 generated columns, 87 check constraints, 90 foreign keys, 0 extraction errors
- All 69 data tables configured with `MergeType: Insert/Update`

### Data Type Mapping

NVARCHAR to VARCHAR, MONEY to DECIMAL(19,4), UNIQUEIDENTIFIER to CHAR(36), HIERARCHYID to VARCHAR(255), XML to TEXT, FLAG/NAMESTYLE to TINYINT(1), IDENTITY to AUTO_INCREMENT

### Schema Mapping

SQL Server schemas mapped to table name prefixes: `HumanResources.Employee` to `HumanResources_Employee`, `dbo.*` to no prefix

### Non-Portable Objects Skipped

7 XML-dependent views, 4 HIERARCHYID procedures, 2 XML triggers, 1 DDL trigger, 1 Full-Text Search procedure, 1 schemabinding function, 1 table-valued function. Non-portable computed columns skipped: OrganizationLevel and DocumentLevel (HIERARCHYID.GetLevel()), AccountNumber (references user function). Time-dependent check constraints skipped (MySQL doesn't allow non-deterministic functions in CHECK).

## Validation

Full round-trip validated: quench to clean database, deep structural comparison (columns, types, nullability, generated expressions, indexes, FKs, check constraints) — zero differences. Idempotency verified.
