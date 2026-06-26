-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."TableQuench"
  (p_ProductName VARCHAR(50),
   p_TableDefinitions TEXT,
   p_WhatIf BOOLEAN = FALSE,
   p_DropUnknownIndexes BOOLEAN = FALSE,
   p_DropTablesRemovedFromProduct BOOLEAN = TRUE,
   p_UpdateFillFactor BOOLEAN = TRUE)
  LANGUAGE plpgsql
AS $$
DECLARE
  table_json TEXT = CASE WHEN LEFT(p_TableDefinitions, 1) = '[' THEN p_TableDefinitions ELSE '[' || p_TableDefinitions || ']' END;
  sql_script TEXT = '';
BEGIN
{{ParseJson}}

  CALL "SchemaSmith"."MissingTableAndColumnQuench"(p_WhatIf);
  CALL "SchemaSmith"."ValidateTableOwnership"(p_ProductName, p_WhatIf);
  CALL "SchemaSmith"."ModifiedTableQuench"(p_WhatIf, p_DropUnknownIndexes, p_DropTablesRemovedFromProduct);
  CALL "SchemaSmith"."MissingIndexesAndConstraintsQuench"(p_WhatIf);
  CALL "SchemaSmith"."ForeignKeyQuench"(p_WhatIf);
  CALL "SchemaSmith"."FixupTableOwnership"(p_ProductName, p_WhatIf);
  CALL "SchemaSmith"."FixupIndexOwnership"(p_ProductName, p_WhatIf);
END $$;