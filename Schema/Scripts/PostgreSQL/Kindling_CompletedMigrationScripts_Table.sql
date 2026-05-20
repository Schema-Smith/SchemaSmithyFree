-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Default value escape note: the JSON-embedded "Default": "''''" survives three layers of
-- parsing to produce a SQL DEFAULT of empty string ''. The outer SQL string literal
-- (single-quoted, wrapping the whole JSON) unescapes '' -> ' twice, leaving the JSON
-- parser to see "''" (two single quotes inside a JSON string). TableQuench reads the
-- decoded JSON value '' and emits it verbatim as the column DEFAULT clause: DEFAULT ''.

CALL "SchemaSmith"."TableQuench"(p_ProductName := 'SchemaQuench', p_TableDefinitions := '{
  "Schema": "SchemaSmith",
  "Name": "CompletedMigrationScripts",
  "Columns": [
    {
      "Name": "ScriptPath",
      "DataType": "VARCHAR(800)",
      "Nullable": false
    },
    {
      "Name": "ProductName",
      "DataType": "VARCHAR(100)",
      "Nullable": false
    },
    {
      "Name": "QuenchSlot",
      "DataType": "VARCHAR(30)",
      "Nullable": false
    },
    {
      "Name": "template_name",
      "DataType": "VARCHAR(256)",
      "Nullable": false,
      "Default": "''''"
    },
    {
      "Name": "schema_name",
      "DataType": "VARCHAR(256)",
      "Nullable": false,
      "Default": "''''"
    },
    {
      "Name": "QuenchDate",
      "DataType": "TIMESTAMP",
      "Nullable": false,
      "Default": "NOW()"
    }
  ],
  "Indexes": [
    {
      "Name": "PK_CompletedMigrationScripts",
      "PrimaryKey": true,
      "Unique": true,
      "UniqueConstraint": false,
      "Clustered": true,
      "FillFactor": 0,
      "IndexColumns": "ScriptPath,ProductName,QuenchSlot,template_name,schema_name"
    }
  ]
}', p_DropUnknownIndexes := TRUE, p_DropTablesRemovedFromProduct := FALSE);

-- Secondary index for GetCompletedEntriesBySlot lookups. The PK leads with ScriptPath but
-- every migration-tracking lookup filters by (ProductName, QuenchSlot, template_name, schema_name)
-- and was previously serviced by sequential / clustered-index scan. This idempotent index
-- covers the lookup leading columns. Kept outside TableQuench's Indexes JSON because the
-- block is run on every KindleTheForge and CREATE INDEX IF NOT EXISTS keeps it cheap to
-- no-op when already present.
CREATE INDEX IF NOT EXISTS ix_completedmigrationscripts_slot_scope
    ON "SchemaSmith"."CompletedMigrationScripts" ("ProductName", "QuenchSlot", template_name, schema_name);