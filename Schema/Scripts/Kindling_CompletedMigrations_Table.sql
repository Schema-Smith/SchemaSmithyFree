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
      "IndexColumns": "[ScriptPath],[ProductName],[QuenchSlot]"
    }
  ]
}', @DropUnknownIndexes = 1, @DropTablesRemovedFromProduct = 0