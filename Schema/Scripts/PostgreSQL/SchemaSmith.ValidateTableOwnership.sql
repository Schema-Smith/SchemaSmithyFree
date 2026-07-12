-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- TRANSITIONAL (slice 3 audit B1 of schema-templates) temp_product_ownership is populated
-- STRICTLY on template_name = p_TemplateName so a multi-template products
-- ModifiedTableQuench drop-candidates query only sees rows owned by the CURRENT template.
-- Without this, every template iterations drop pass would see every other templates
-- owned tables as drop candidates. Legacy pre-extension rows have template_name = ''
-- and only appear in iterations whose template_name is also '' (i.e. callers that have
-- not yet been updated to pass a template name) -- they are otherwise harmless residue.
-- Tracked in the Community roadmap under Slice 3 transitional aids -- ProductOwnership
-- template_name extension.
CREATE OR REPLACE PROCEDURE "SchemaSmith"."ValidateTableOwnership"
(p_ProductName VARCHAR(50),
 p_WhatIf BOOLEAN = FALSE,
 p_TemplateName VARCHAR(256) = '',
 p_SchemaName VARCHAR(256) = '')
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

    -- Per-iteration ownership scope. p_SchemaName non-empty => schema-template iteration:
    -- restrict to rows in THIS schema so a TenantBody iteration for tenant_acme cannot
    -- propose drops against tenant_beta tables (both owned by the same template).
    -- p_SchemaName empty => regular template: no per-schema restriction.
    RAISE NOTICE 'Collect Existing Column Definitions';
    DROP TABLE IF EXISTS temp_product_ownership;
    CREATE TEMPORARY TABLE temp_product_ownership AS
      SELECT "Schema", "TableName", "IndexName", "PreventDrop"
        FROM "SchemaSmith"."ProductOwnership"
        WHERE "ProductName" = p_ProductName
          AND template_name = p_TemplateName
          AND (p_SchemaName = '' OR "Schema" = p_SchemaName);

END $$;
