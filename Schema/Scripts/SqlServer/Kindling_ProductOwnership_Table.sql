-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Table definition lives in the sibling Kindling_ProductOwnership.json resource.
-- ForgeKindler substitutes the JSON body into this script's TableDef token before execution.
-- BootstrapTableQuench has zero SchemaSmith_* dependencies, so this script runs early
-- in the kindling pipeline (right after the BootstrapTableQuench proc itself is created).
--
-- On SQL Server this table tracks ownership ONLY for tables that cannot carry the ProductName
-- extended property -- i.e. memory-optimized (Hekaton) tables, which reject sp_addextendedproperty
-- with "Operations that require a change to the schema version ... are not supported with memory
-- optimized tables." Regular tables continue to be tracked by the extended property. It is the same
-- table-based ownership fallback PostgreSQL and MySQL use (they have no extended properties at all).

EXEC SchemaSmith.BootstrapTableQuench @TableDefinitions = '{{TableDef}}'
