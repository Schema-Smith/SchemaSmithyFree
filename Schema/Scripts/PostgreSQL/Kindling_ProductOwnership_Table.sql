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
    },
    {
      "Name": "template_name",
      "DataType": "VARCHAR(256)",
      "Nullable": false,
      "Default": "''''"
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
      "IndexColumns": "Schema,TableName,IndexName,ProductName,template_name"
    }
  ]
}', p_DropUnknownIndexes := TRUE, p_DropTablesRemovedFromProduct := FALSE);
