-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."FixupMaterializedViewOwnership"
(p_ProductName VARCHAR(50))
    LANGUAGE plpgsql
AS $$
DECLARE
    sql_script TEXT = '';
BEGIN
  RAISE NOTICE 'Add missing Product ownership to materialized views';
  INSERT INTO "SchemaSmith"."ProductOwnership"
    ("Schema", "TableName", "IndexName", "ProductName")
    SELECT t."Schema", t."Name", NULL, p_ProductName
      FROM temp_materialized_views t
      WHERE NOT EXISTS (SELECT 1 FROM "SchemaSmith"."ProductOwnership" po WHERE po."Schema" = t."Schema" AND po."TableName" = t."Name" AND po."IndexName" IS NULL);

  RAISE NOTICE 'Remove Product Ownership for Obsolete Materialized Views';
  DELETE FROM "SchemaSmith"."ProductOwnership" po
    WHERE "ProductName" = p_ProductName
      AND "IndexName" IS NULL
      AND NOT EXISTS (SELECT 1
                        FROM temp_materialized_views t
                        WHERE t."Schema" = po."Schema"
                          AND t."Name" = po."TableName")
      AND NOT EXISTS (SELECT 1
                        FROM temp_tables t
                        WHERE t."Schema" = po."Schema"
                          AND t."Name" = po."TableName");
END $$;
