-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CALL "SchemaSmith"."TableQuench"(p_ProductName := 'SchemaQuench', p_TableDefinitions := '{
  "Schema": "SchemaSmith",
  "Name": "ProductOwnership",
  "Columns": [
    {
      "Name": "Schema",
      "DataType": "VARCHAR(256)",
      "Nullable": false
    },
    {
      "Name": "TableName",
      "DataType": "VARCHAR(256)",
      "Nullable": false
    },
    {
      "Name": "IndexName",
      "DataType": "VARCHAR(256)",
      "Nullable": true
    },
    {
      "Name": "ProductName",
      "DataType": "VARCHAR(100)",
      "Nullable": false
    }
  ],
  "Indexes": [
    {
      "Name": "PK_ProductOwnership",
      "PrimaryKey": false,
      "Unique": true,
      "UniqueConstraint": false,
      "Clustered": true,
      "FillFactor": 0,
      "IndexColumns": "Schema,TableName,IndexName,ProductName"
    }
  ]
}', p_DropUnknownIndexes := TRUE, p_DropTablesRemovedFromProduct := FALSE);