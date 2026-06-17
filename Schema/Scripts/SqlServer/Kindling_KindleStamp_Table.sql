-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Single-row marker holding the SHA-256 of the resolved kindling set. ForgeKindler reads it
-- under a session lock to decide skip-vs-rekindle. Table definition lives in the sibling
-- Kindling_KindleStamp.json resource (substituted into the TableDef token before execution).

EXEC SchemaSmith.BootstrapTableQuench @TableDefinitions = '{{TableDef}}'
