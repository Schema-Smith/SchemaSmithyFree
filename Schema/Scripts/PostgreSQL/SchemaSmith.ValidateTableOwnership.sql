-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."ValidateTableOwnership"
(p_ProductName VARCHAR(50),
 p_WhatIf BOOLEAN = FALSE)
    LANGUAGE plpgsql
AS $$
DECLARE
  sql_script TEXT = '';
BEGIN
    RAISE NOTICE 'Validate Table Ownership';
    SELECT STRING_AGG('RAISE NOTICE ''  Table ' || tp."Schema" || '.' || tp."TableName" || ' owned by different product. [' || tp."ProductName" || '] <> [' || p_ProductName || ']'';', CHR(10))
      INTO sql_script
      FROM temp_tables t
      JOIN "SchemaSmith"."ProductOwnership" tp ON tp."Schema" = t."Schema"
                                              AND tp."TableName" = t."Name"
                                              AND tp."IndexName" IS NULL -- table level ownership
      WHERE tp."ProductName" <> p_ProductName;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    IF EXISTS (SELECT 1
                 FROM temp_tables t
                 JOIN "SchemaSmith"."ProductOwnership" tp ON t."Schema" = tp."Schema" AND t."Name" = tp."TableName" AND tp."IndexName" IS NULL -- table level ownership
                 WHERE tp."ProductName" <> p_ProductName) THEN
        RAISE EXCEPTION 'One or more tables in this quench are already owned by another product';
    END IF;

    RAISE NOTICE 'Collect Existing Column Definitions';
    DROP TABLE IF EXISTS temp_product_ownership;
    CREATE TEMPORARY TABLE temp_product_ownership AS
      SELECT "Schema", "TableName", "IndexName"
        FROM "SchemaSmith"."ProductOwnership"
        WHERE "ProductName" = p_ProductName;

END $$;