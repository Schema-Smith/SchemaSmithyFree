# AdventureWorks (SQL Server)

## Source

| | |
|---|---|
| **Repository** | [microsoft/sql-server-samples](https://github.com/microsoft/sql-server-samples) |
| **File** | `AdventureWorks2022.bak` |
| **License** | MIT |
| **Self-port** | No — Microsoft official |

## Extraction Notes

- Restored fresh `.bak` to SQL Server 2022 Docker
- Extracted with SchemaSmith Community toolset (SchemaTongs + DataTongs)
- 71 tables, 20 views, 10 procedures, 11 functions, 10 triggers, 5 schemas, 6 UDTs, 6 XML schema collections, 1 full-text catalog, 8 XML indexes, 3 full-text indexes
- All 68 data tables configured with `MergeType: Insert/Update`
- `dbo.ufnLeadingZeros` re-extracted with `ScriptDynamicDependencyRemoval=true` — this function has `WITH SCHEMABINDING` and is referenced by a computed column on `Sales.Customer.AccountNumber`
- Data delivery excludes `dbo.DatabaseLog` (populated by DDL trigger) and `Production.TransactionHistory` (populated by DML triggers)
- `HumanResources.Employee` and `Purchasing.Vendor` use `MergeType=Insert/Update` (no delete) because they have INSTEAD OF DELETE triggers
- Full round-trip validated: quench to clean database, structural comparison, idempotency verified
