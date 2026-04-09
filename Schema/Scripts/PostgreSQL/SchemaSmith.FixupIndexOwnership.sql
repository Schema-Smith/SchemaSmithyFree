-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."FixupIndexOwnership"
(p_ProductName VARCHAR(50))
    LANGUAGE plpgsql
AS $$
BEGIN
  RAISE NOTICE 'Add Missing Index Product Ownership';
  INSERT INTO "SchemaSmith"."ProductOwnership" 
    ("Schema", "TableName", "IndexName", "ProductName")
    SELECT ti."TableSchema", ti."TableName", ti."Name", p_ProductName
      FROM temp_indexes ti
      WHERE NOT EXISTS (SELECT 1 FROM "SchemaSmith"."ProductOwnership" po WHERE po."Schema" = ti."TableSchema" AND po."TableName" = ti."TableName" AND po."IndexName" = ti."Name");

  RAISE NOTICE 'Remove Product Ownership for Obsolete Indexes';
  DELETE FROM "SchemaSmith"."ProductOwnership" po
    WHERE "ProductName" = p_ProductName
      AND "IndexName" IS NOT NULL
      AND EXISTS (SELECT 1
                    FROM temp_tables t
                    WHERE t."Schema" = po."Schema"
                      AND t."Name" = po."TableName")
      AND NOT EXISTS (SELECT 1
                        FROM temp_indexes i
                        WHERE i."TableSchema" = po."Schema"
                          AND i."TableName" = po."TableName"
                          AND i."Name" = po."IndexName");
END $$;
