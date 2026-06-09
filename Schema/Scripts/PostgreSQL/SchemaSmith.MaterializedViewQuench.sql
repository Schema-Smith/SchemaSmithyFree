-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."MaterializedViewQuench"
  (p_ProductName VARCHAR(50),
   p_ViewDefinitions TEXT,
   p_WhatIf BOOLEAN = FALSE,
   p_UpdateFillFactor BOOLEAN = TRUE)
  LANGUAGE plpgsql
AS $$
DECLARE
  view_json TEXT = CASE WHEN LEFT(p_ViewDefinitions, 1) = '[' THEN p_ViewDefinitions ELSE '[' || p_ViewDefinitions || ']' END;
  sql_script TEXT = '';
BEGIN
  -- Ensure temp_tables exists for FixupMaterializedViewOwnership
  -- (may not exist if product has no tables, IndexOnlyTableQuenches=true, or UpdateTables=false)
  CREATE TEMPORARY TABLE IF NOT EXISTS temp_tables ("Schema" TEXT, "Name" TEXT);

  -- Parse materialized view definitions into temp tables
  DROP TABLE IF EXISTS temp_materialized_views;
  CREATE TEMPORARY TABLE temp_materialized_views AS
  WITH my_views(arr) AS (VALUES(view_json::JSON))
  SELECT elem ->> 'Schema' AS "Schema",
         elem ->> 'Name' AS "Name",
         COALESCE(elem ->> 'Definition', '') AS "Definition",
         COALESCE((elem ->> 'WithData')::BOOLEAN, true) AS "WithData",
         COALESCE(elem ->> 'Tablespace', '') AS "Tablespace",
         COALESCE(elem ->> 'AccessMethod', 'heap') AS "AccessMethod",
         COALESCE(elem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
         COALESCE(elem ->> 'VariantName', '') AS "VariantName"
  FROM my_views, JSON_ARRAY_ELEMENTS(arr) AS elem;

  -- Evaluate ShouldApplyExpression: remove views whose expression evaluates to false
  SELECT STRING_AGG('DELETE FROM temp_materialized_views WHERE "Schema" = ''' || "Schema" || ''' AND "Name" = ''' || "Name" || ''' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
    INTO sql_script
    FROM temp_materialized_views
    WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

  -- Parse indexes into temp table
  DROP TABLE IF EXISTS temp_mv_indexes;
  CREATE TEMPORARY TABLE temp_mv_indexes AS
  WITH my_views(arr) AS (VALUES(view_json::JSON))
  SELECT elem ->> 'Schema' AS "ViewSchema",
         elem ->> 'Name' AS "ViewName",
         celem ->> 'Name' AS "Name",
         COALESCE((celem ->> 'Unique')::BOOLEAN, false) AS "Unique",
         COALESCE((celem ->> 'Clustered')::BOOLEAN, false) AS "Clustered",
         COALESCE(celem ->> 'IndexColumns', '') AS "IndexColumns",
         COALESCE(celem ->> 'IncludeColumns', '') AS "IncludeColumns",
         COALESCE(celem ->> 'AccessMethod', 'btree') AS "AccessMethod",
         COALESCE(celem ->> 'FilterExpression', '') AS "FilterExpression",
         COALESCE((celem ->> 'NullsNotDistinct')::BOOLEAN, false) AS "NullsNotDistinct",
         COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
         CASE WHEN p_UpdateFillFactor THEN true ELSE false END AS "UpdateFillFactor",
         COALESCE(NULLIF((celem ->> 'FillFactor')::INT2, 0), 90) AS "FillFactor"
    FROM my_views, JSON_ARRAY_ELEMENTS(arr) AS elem
    CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS(COALESCE((elem ->> 'Indexes')::JSON, '[]'::JSON)) AS celem(value);

  -- Evaluate index ShouldApplyExpression
  SELECT STRING_AGG('DELETE FROM temp_mv_indexes WHERE "ViewSchema" = ''' || "ViewSchema" || ''' AND "ViewName" = ''' || "ViewName" || ''' AND "Name" = ''' || "Name" || ''' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
    INTO sql_script
    FROM temp_mv_indexes
    WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

  -- Validate ownership
  CALL "SchemaSmith"."ValidateMaterializedViewOwnership"(p_ProductName, p_WhatIf);

  -- Drop/recreate changed and removed materialized views
  RAISE NOTICE 'Materialized View Diff — Drop Changed/Removed';

  -- Drop indexes on views that will be dropped (changed definition or removed from product)
  sql_script = '';
  SELECT STRING_AGG('DROP INDEX IF EXISTS "' || n.nspname || '"."' || i.relname || '";', CHR(10))
    INTO sql_script
    FROM pg_matviews mv
    JOIN pg_class c ON c.relname = mv.matviewname
                   AND c.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = mv.schemaname)
                   AND c.relkind = 'm'
    JOIN pg_index idx ON idx.indrelid = c.oid
    JOIN pg_class i ON i.oid = idx.indexrelid
    JOIN pg_namespace n ON n.oid = i.relnamespace
    WHERE (
      -- View removed from product
      EXISTS (SELECT 1 FROM "SchemaSmith"."ProductOwnership" po
               WHERE po."Schema" = mv.schemaname AND po."TableName" = mv.matviewname
                 AND po."IndexName" IS NULL AND po."ProductName" = p_ProductName)
      AND NOT EXISTS (SELECT 1 FROM temp_materialized_views t
                       WHERE t."Schema" = mv.schemaname AND t."Name" = mv.matviewname)
    ) OR (
      -- View definition changed (whitespace-collapsed, case-insensitive comparison)
      EXISTS (SELECT 1 FROM temp_materialized_views t
               WHERE t."Schema" = mv.schemaname AND t."Name" = mv.matviewname
                 AND UPPER(REGEXP_REPLACE(TRIM(RTRIM(RTRIM(t."Definition"), ';')), '\s+', ' ', 'g')) != UPPER(REGEXP_REPLACE(TRIM(RTRIM(RTRIM(mv.definition), ';')), '\s+', ' ', 'g')))
    );
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  -- Drop changed/removed views
  sql_script = '';
  SELECT STRING_AGG('RAISE NOTICE ''  Dropping materialized view ' || mv.schemaname || '.' || mv.matviewname || ''';' || CHR(10) ||
                    'DROP MATERIALIZED VIEW IF EXISTS "' || mv.schemaname || '"."' || mv.matviewname || '";', CHR(10))
    INTO sql_script
    FROM pg_matviews mv
    WHERE (
      -- View removed from product
      EXISTS (SELECT 1 FROM "SchemaSmith"."ProductOwnership" po
               WHERE po."Schema" = mv.schemaname AND po."TableName" = mv.matviewname
                 AND po."IndexName" IS NULL AND po."ProductName" = p_ProductName)
      AND NOT EXISTS (SELECT 1 FROM temp_materialized_views t
                       WHERE t."Schema" = mv.schemaname AND t."Name" = mv.matviewname)
    ) OR (
      -- View definition changed
      EXISTS (SELECT 1 FROM temp_materialized_views t
               WHERE t."Schema" = mv.schemaname AND t."Name" = mv.matviewname
                 AND UPPER(REGEXP_REPLACE(TRIM(RTRIM(RTRIM(t."Definition"), ';')), '\s+', ' ', 'g')) != UPPER(REGEXP_REPLACE(TRIM(RTRIM(RTRIM(mv.definition), ';')), '\s+', ' ', 'g')))
    );
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  -- Create missing materialized views (new or just dropped because definition changed)
  RAISE NOTICE 'Materialized View Diff — Create Missing';
  sql_script = '';
  SELECT STRING_AGG(
    'RAISE NOTICE ''  Creating materialized view ' || t."Schema" || '.' || t."Name" || CASE WHEN COALESCE(t."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(t."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
    'CREATE MATERIALIZED VIEW "' || t."Schema" || '"."' || t."Name" || '"'
    || CASE WHEN NULLIF(t."AccessMethod", '') IS NOT NULL AND t."AccessMethod" != 'heap' THEN ' USING ' || t."AccessMethod" ELSE '' END
    || CASE WHEN NULLIF(t."Tablespace", '') IS NOT NULL THEN ' TABLESPACE ' || t."Tablespace" ELSE '' END
    || ' AS ' || RTRIM(RTRIM(t."Definition"), ';')
    || CASE WHEN t."WithData" THEN ' WITH DATA' ELSE ' WITH NO DATA' END
    || ';',
    CHR(10))
    INTO sql_script
    FROM temp_materialized_views t
    WHERE NOT EXISTS (SELECT 1 FROM pg_matviews mv
                       WHERE mv.schemaname = t."Schema" AND mv.matviewname = t."Name");
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  -- Handle indexes
  CALL "SchemaSmith"."MissingMaterializedViewIndexesQuench"(p_WhatIf, p_UpdateFillFactor);

  -- Fixup ownership
  CALL "SchemaSmith"."FixupMaterializedViewOwnership"(p_ProductName);
END $$;
