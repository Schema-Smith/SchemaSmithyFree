-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- TRANSITIONAL (slice 3 audit B1 of schema-templates)
-- Reads/INSERT-existence checks use permissive `template_name IN (legacy, current)` so
-- pre-extension legacy rows (template_name = '') still match same-template lookups.
-- Writes use the actual template name. DELETE is STRICT on `template_name = p_TemplateName`
-- so a multi-template product never deletes another templates ownership rows during the
-- per-template prune pass. Legacy blank-template rows are left as harmless historical
-- residue, matching the slice 2 CompletedMigrationScripts pattern. Tracked in the Community
-- roadmap under Slice 3 transitional aids -- ProductOwnership template_name extension.
CREATE OR REPLACE PROCEDURE "SchemaSmith"."FixupTableOwnership"
(p_ProductName VARCHAR(50),
 p_TemplateName VARCHAR(256) = '',
 p_SchemaName VARCHAR(256) = '')
    LANGUAGE plpgsql
AS $$
BEGIN
  RAISE NOTICE 'Add missing Product ownership to tables';
  INSERT INTO "SchemaSmith"."ProductOwnership"
    ("Schema", "TableName", "IndexName", "ProductName", template_name)
    SELECT t."Schema", t."Name", NULL, p_ProductName, p_TemplateName
      FROM temp_tables t
      WHERE NOT EXISTS (SELECT 1 FROM "SchemaSmith"."ProductOwnership" po
                          WHERE po."Schema" = t."Schema"
                            AND po."TableName" = t."Name"
                            AND po."IndexName" IS NULL
                            AND po.template_name IN ('', p_TemplateName));

  -- Per-iteration scope: p_SchemaName non-empty restricts the prune to rows in the
  -- iteration's schema so a schema-template iteration cannot delete ownership rows
  -- for other tenants of the same template.
  RAISE NOTICE 'Remove Product Ownership for Obsolete Tables';
  DELETE FROM "SchemaSmith"."ProductOwnership" po
    WHERE "ProductName" = p_ProductName
      AND "IndexName" IS NULL
      AND template_name = p_TemplateName
      AND (p_SchemaName = '' OR po."Schema" = p_SchemaName)
      AND NOT EXISTS (SELECT 1
                        FROM temp_tables t
                        WHERE t."Schema" = po."Schema"
                          AND t."Name" = po."TableName")
      AND NOT EXISTS (SELECT 1
                        FROM pg_matviews mv
                        WHERE mv.schemaname = po."Schema"
                          AND mv.matviewname = po."TableName");
END $$;
