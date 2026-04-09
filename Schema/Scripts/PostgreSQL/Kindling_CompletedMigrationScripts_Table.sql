-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

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
      "IndexColumns": "ScriptPath,ProductName,QuenchSlot"
    }
  ]
}', p_DropUnknownIndexes := TRUE, p_DropTablesRemovedFromProduct := FALSE);