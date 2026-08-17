DECLARE @graph NVARCHAR(MAX) = N'{{TableSchema}}';
DECLARE @replicated NVARCHAR(MAX);

SELECT @replicated = N'[' + STRING_AGG(CAST(t.[value] AS NVARCHAR(MAX)), N',') + N']'
FROM OPENJSON(@graph) t
WHERE JSON_VALUE(t.[value], '$.Extensions.ReplicationEnabled') = 'true';

IF @replicated IS NOT NULL
  EXEC Shop_Replica.SchemaSmith.TableQuench
      @ProductName = N'{{ProductName}}',
      @TableDefinitions = @replicated,
      @WhatIf = 0,
      @DropUnknownIndexes = 0,
      @DropTablesRemovedFromProduct = 0,
      @UpdateFillFactor = 1;
