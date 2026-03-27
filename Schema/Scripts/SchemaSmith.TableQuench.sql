-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR ALTER PROCEDURE SchemaSmith.TableQuench
    @ProductName NVARCHAR(50),
    @TableDefinitions NVARCHAR(MAX),
    @WhatIf BIT = 0,
    @DropUnknownIndexes BIT = 0,
    @DropTablesRemovedFromProduct BIT = 1,
    @UpdateFillFactor BIT = 1
AS
BEGIN TRY
    SET NOCOUNT ON
{{ParseJson}}

  EXEC SchemaSmith.MissingTableAndColumnQuench @WhatIf
  EXEC SchemaSmith.ModifiedTableQuench @ProductName, @WhatIf, @DropUnknownIndexes, @DropTablesRemovedFromProduct
  EXEC SchemaSmith.MissingIndexesAndConstraintsQuench @ProductName, @WhatIf
  EXEC SchemaSmith.ForeignKeyQuench @ProductName, @WhatIf
  SET NOCOUNT OFF
END TRY
BEGIN CATCH
    THROW
END CATCH
