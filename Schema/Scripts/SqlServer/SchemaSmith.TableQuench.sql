-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('SchemaSmith.TableQuench', 'P') IS NOT NULL DROP PROCEDURE SchemaSmith.TableQuench
GO
CREATE PROCEDURE SchemaSmith.TableQuench
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

  -- Sanitize the parsed working set for the detected target version BEFORE any emit/detection pass: below a
  -- feature's intro version the declared feature is refused ('fail') or neutralized in place ('warn'), so the
  -- quench procs stay gate-free. See SchemaSmith.DegradeUnsupportedFeatures.
  EXEC SchemaSmith.DegradeUnsupportedFeatures

  EXEC SchemaSmith.MissingTableAndColumnQuench @WhatIf
  EXEC SchemaSmith.ModifiedTableQuench @ProductName, @WhatIf, @DropUnknownIndexes, @DropTablesRemovedFromProduct
  EXEC SchemaSmith.MissingIndexesAndConstraintsQuench @ProductName, @WhatIf
  EXEC SchemaSmith.ForeignKeyQuench @ProductName, @WhatIf
  SET NOCOUNT OFF
END TRY
BEGIN CATCH
    DECLARE @v_RethrowMsg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@v_RethrowMsg, 16, 1);
END CATCH