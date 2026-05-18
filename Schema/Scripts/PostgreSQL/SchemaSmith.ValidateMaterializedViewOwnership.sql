-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- TRANSITIONAL (slice 3 audit B1 of schema-templates): same template_name scoping note as
-- ValidateTableOwnership. The conflict-detection JOIN intentionally does NOT filter on
-- template_name — a cross-product conflict is a cross-product conflict regardless of which
-- template was claiming the materialized view. Tracked in the Community roadmap under
-- "Slice 3 transitional aids — ProductOwnership template_name extension".
CREATE OR REPLACE PROCEDURE "SchemaSmith"."ValidateMaterializedViewOwnership"
(p_ProductName VARCHAR(50),
 p_WhatIf BOOLEAN = FALSE,
 p_TemplateName VARCHAR(256) = '')
    LANGUAGE plpgsql
AS $$
DECLARE
  sql_script TEXT = '';
BEGIN
    RAISE NOTICE 'Validate Materialized View Ownership';
    SELECT STRING_AGG('RAISE NOTICE ''  Materialized View ' || tp."Schema" || '.' || tp."TableName" || ' owned by different product. [' || tp."ProductName" || '] <> [' || p_ProductName || ']'';', CHR(10))
      INTO sql_script
      FROM temp_materialized_views t
      JOIN "SchemaSmith"."ProductOwnership" tp ON tp."Schema" = t."Schema"
                                              AND tp."TableName" = t."Name"
                                              AND tp."IndexName" IS NULL
      WHERE tp."ProductName" <> p_ProductName;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    IF EXISTS (SELECT 1
                 FROM temp_materialized_views t
                 JOIN "SchemaSmith"."ProductOwnership" tp ON t."Schema" = tp."Schema" AND t."Name" = tp."TableName" AND tp."IndexName" IS NULL
                 WHERE tp."ProductName" <> p_ProductName) THEN
        RAISE EXCEPTION 'One or more materialized views in this quench are already owned by another product';
    END IF;

END $$;
