-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Default value escape note: the JSON-embedded "Default": "''''" survives three layers of
-- parsing to produce a SQL DEFAULT of empty string ''. The outer SQL string literal
-- (single-quoted, wrapping the whole JSON) unescapes '' -> ' twice, leaving the JSON
-- parser to see "''" (two single quotes inside a JSON string). TableQuench reads the
-- decoded JSON value '' and emits it verbatim as the column DEFAULT clause: DEFAULT ''.

EXEC SchemaSmith.TableQuench @ProductName = 'SchemaQuench', @TableDefinitions = '{
  "Schema": "[SchemaSmith]",
  "Name": "[CompletedMigrationScripts]",
  "CompressionType": "NONE",
  "Columns": [
    {
      "Name": "[ScriptPath]",
      "DataType": "VARCHAR(800)",
      "Nullable": false
    },
    {
      "Name": "[ProductName]",
      "DataType": "VARCHAR(100)",
      "Nullable": false
    },
    {
      "Name": "[QuenchSlot]",
      "DataType": "VARCHAR(30)",
      "Nullable": false
    },
    {
      "Name": "[template_name]",
      "DataType": "NVARCHAR(256)",
      "Nullable": false,
      "Default": "''''"
    },
    {
      "Name": "[schema_name]",
      "DataType": "NVARCHAR(256)",
      "Nullable": false,
      "Default": "''''"
    },
    {
      "Name": "[QuenchDate]",
      "DataType": "DATETIME",
      "Nullable": false,
      "Default": "GETDATE()"
    }
  ],
  "Indexes": [
    {
      "Name": "[PK_CompletedMigrationScripts]",
      "CompressionType": "NONE",
      "PrimaryKey": true,
      "Unique": true,
      "UniqueConstraint": false,
      "Clustered": true,
      "ColumnStore": false,
      "FillFactor": 0,
      "IndexColumns": "[ScriptPath],[Productname],[QuenchSlot],[template_name],[schema_name]"
    }
  ]
}', @DropUnknownIndexes = 1, @DropTablesRemovedFromProduct = 0