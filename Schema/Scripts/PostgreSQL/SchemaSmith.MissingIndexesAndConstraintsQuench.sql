-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."MissingIndexesAndConstraintsQuench"(p_WhatIf BOOLEAN = FALSE)
    LANGUAGE plpgsql
AS $$
DECLARE
  sql_script TEXT = '';
BEGIN
  -- Column existence / default checks below read pg_catalog directly rather than
  -- information_schema.columns. Under a correlated EXISTS over the per-iteration temp
  -- tables, the planner materialises the whole information_schema.columns view, which
  -- AccessShare-locks every table in the database and deadlocks parallel template
  -- iterations against siblings creating their own tables. pg_catalog scoped to the
  -- named schema+table locks nothing it doesn't need.

  -- #332: temp_existing_indexes is normally built by ModifiedTableQuench earlier in the
  -- same session. On a checkpoint-resumed run the ModifiedTables step is skipped, so the
  -- fresh session has no snapshot; rebuild it here when absent. Guarded so the normal path
  -- keeps the snapshot ModifiedTableQuench already built.
  IF to_regclass('pg_temp.temp_existing_indexes') IS NULL THEN
    CALL "SchemaSmith"."BuildExistingIndexesSnapshot"();
  END IF;

  RAISE NOTICE 'Add New Computed Columns';
  SELECT STRING_AGG('RAISE NOTICE ''  Add new computed columns to ' || tt."Schema" || '.' || tt."Name" || ' (' ||
                    (SELECT STRING_AGG(tc."Name" || CASE WHEN COALESCE(tc."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(tc."VariantName", '''', '''''') || ')' ELSE '' END, ', ')
                     FROM temp_columns tc
                     WHERE tc."TableSchema" = tt."Schema" AND tc."TableName" = tt."Name"
                       AND tc."Generated" = 'ALWAYS' AND COALESCE(tc."GenerationExpression", '') <> ''
                       AND NOT EXISTS (SELECT 1 FROM pg_attribute a JOIN pg_class rc ON rc.oid = a.attrelid JOIN pg_namespace nn ON nn.oid = rc.relnamespace WHERE nn.nspname = tc."TableSchema" AND rc.relname = tc."TableName" AND a.attname = tc."Name" AND a.attnum > 0 AND NOT a.attisdropped)) ||
                    ')'';' || CHR(10) ||
                    'ALTER TABLE "' || tt."Schema" || '"."' || tt."Name" || '" ' ||
                    (SELECT STRING_AGG('ADD COLUMN "' || tc."Name" || '" ' || tc."DataType" || ' GENERATED ' || tc."Generated" || ' AS (' || tc."GenerationExpression" || ') ' || CASE WHEN tc."Virtual" THEN 'VIRTUAL' ELSE 'STORED' END, ', ')
                       FROM temp_columns tc
                       WHERE tc."TableSchema" = tt."Schema" AND tc."TableName" = tt."Name"
                         AND tc."Generated" = 'ALWAYS' AND COALESCE(tc."GenerationExpression", '') <> ''
                         AND NOT EXISTS (SELECT 1 FROM pg_attribute a JOIN pg_class rc ON rc.oid = a.attrelid JOIN pg_namespace nn ON nn.oid = rc.relnamespace WHERE nn.nspname = tc."TableSchema" AND rc.relname = tc."TableName" AND a.attname = tc."Name" AND a.attnum > 0 AND NOT a.attisdropped)) || ';', CHR(10))
    INTO sql_script
    FROM temp_tables tt
    WHERE EXISTS(SELECT * FROM information_schema.tables t WHERE t.table_schema = tt."Schema" AND t.table_name = tt."Name")
      AND EXISTS (SELECT 1
                    FROM temp_columns tc
                    WHERE tc."TableSchema" = tt."Schema" AND tc."TableName" = tt."Name"
                      AND tc."Generated" = 'ALWAYS' AND COALESCE(tc."GenerationExpression", '') <> ''
                      AND NOT EXISTS (SELECT 1 FROM pg_attribute a JOIN pg_class rc ON rc.oid = a.attrelid JOIN pg_namespace nn ON nn.oid = rc.relnamespace WHERE nn.nspname = tc."TableSchema" AND rc.relname = tc."TableName" AND a.attname = tc."Name" AND a.attnum > 0 AND NOT a.attisdropped));
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  RAISE NOTICE 'Add Missing Indexes'; -- Includes Primary Keys and Unique Constraints
  SELECT STRING_AGG('RAISE NOTICE ''  Add missing ' || CASE WHEN ti."UniqueConstraint" OR ti."PrimaryKey" THEN 'Constraint ' ELSE 'Index ' END || ti."TableSchema" || '.' || ti."TableName" || '.' || ti."Name" || CASE WHEN COALESCE(ti."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(ti."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                    CASE WHEN ti."UniqueConstraint" OR ti."PrimaryKey"
                         THEN 'ALTER TABLE "' || ti."TableSchema" || '"."' || ti."TableName" || '" ADD CONSTRAINT "' || ti."Name" || '" ' ||
                              CASE WHEN ti."PrimaryKey" THEN 'PRIMARY KEY ' ELSE 'UNIQUE ' END ||
                              '(' || "SchemaSmith"."QuoteIndexColumnList"(ti."IndexColumns") || ')' ||
                              CASE WHEN ti."Deferrable" THEN ' DEFERRABLE' ELSE '' END ||
                              CASE WHEN ti."InitiallyDeferred" THEN ' INITIALLY DEFERRED' ELSE '' END ||
                              CASE WHEN COALESCE(ti."AccessMethod", 'btree') NOT IN ('gin', 'brin', 'spgist')
                                   THEN ' WITH (fillfactor = ' || ti."FillFactor" || ')'
                                   ELSE '' END || ';'
                         ELSE 'CREATE ' || CASE WHEN ti."Unique" THEN 'UNIQUE ' ELSE '' END || 'INDEX "' || ti."Name" || '" ON "' || ti."TableSchema" || '"."' || ti."TableName" || '" ' ||
                              'USING ' || COALESCE(ti."AccessMethod", 'btree') || ' ' ||
                              '(' || "SchemaSmith"."QuoteIndexColumnList"(ti."IndexColumns") || ')' ||
                              CASE WHEN NULLIF(ti."IncludeColumns", '') IS NOT NULL THEN ' INCLUDE (' || "SchemaSmith"."QuoteColumnList"(ti."IncludeColumns") || ')' ELSE '' END ||
                              CASE WHEN COALESCE(ti."AccessMethod", 'btree') NOT IN ('gin', 'brin', 'spgist')
                                   THEN ' WITH (fillfactor = ' || ti."FillFactor" || ') '
                                   ELSE ' ' END ||
                              CASE WHEN NULLIF(ti."FilterExpression", '') IS NOT NULL THEN ' WHERE ' || ti."FilterExpression" ELSE '' END || ';'
                         END, CHR(10))
    INTO sql_script
    FROM temp_indexes ti
    WHERE NOT EXISTS (SELECT * 
                        FROM pg_index idx
                        JOIN pg_class tc ON tc.oid = idx.indrelid
                                        AND tc.relkind = 'r'
                                        AND tc.relname = ti."TableName"
                                        AND tc.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = ti."TableSchema")
                        JOIN pg_class i ON i.oid = idx.indexrelid
                        WHERE i.relname = ti."Name");
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  RAISE NOTICE 'Fixup Table Cluster';
  SELECT STRING_AGG('RAISE NOTICE ''  Fixing up attributes for ' || t."Schema" || '.' || t."Name" || ''';' || CHR(10) ||
                    'ALTER TABLE ' || '"' || t."Schema" || '"."' || t."Name" || '" ' ||
                    CASE WHEN new_clust."NewCluster" IS NOT NULL 
                         THEN 'CLUSTER ON ' || '"' || new_clust."NewCluster" || '"'
                         ELSE 'SET WITHOUT CLUSTER' END || ';', CHR(10))
    INTO sql_script
    FROM temp_tables t
    LEFT JOIN (SELECT ti."TableSchema", ti."TableName", ti."Name" AS "NewCluster"
                 FROM temp_indexes ti
                 WHERE ti."Clustered") AS new_clust ON new_clust."TableSchema" = t."Schema"
                                                   AND new_clust."TableName" = t."Name"
    LEFT JOIN (SELECT ei."TableSchema", ei."TableName", ei."IndexName" AS "OldCluster"
                 FROM temp_existing_indexes ei
                 WHERE ei."Clustered") AS old_clust ON old_clust."TableSchema" = t."Schema"
                                                   AND old_clust."TableName" = t."Name"
    WHERE COALESCE("NewCluster", '') != COALESCE("OldCluster", '');
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  RAISE NOTICE 'Add Missing Statistics';
  SELECT STRING_AGG('RAISE NOTICE ''  Add missing statistics ' || ts."TableSchema" || '.' || ts."TableName" || '.' || ts."Name" || CASE WHEN COALESCE(ts."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(ts."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                    'CREATE STATISTICS "' || ts."TableSchema" || '"."' || ts."Name" || '"' ||
                    CASE WHEN NULLIF(TRIM(ts."Kind"), '') IS NOT NULL THEN ' (' || ts."Kind" ||')' ELSE '' END ||
                    ' ON ' || "SchemaSmith"."QuoteIndexColumnList"(ts."StatisticsColumns") ||
                    ' FROM "' || ts."TableSchema" || '"."' || ts."TableName" || '";', CHR(10))
    INTO sql_script
    FROM temp_statistics ts
    WHERE NOT EXISTS (SELECT 1
                        FROM pg_statistic_ext ste
                        JOIN pg_class rel ON rel.oid = ste.stxrelid
                        JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
                                             AND nsp.nspname = ts."TableSchema"
                                             AND rel.relname = ts."TableName"
                        WHERE ste.stxname = ts."Name");
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  RAISE NOTICE 'Add Missing Exclude Constraints';
  SELECT STRING_AGG('RAISE NOTICE ''  Add missing exclude constraint ' || tc."TableSchema" || '.' || tc."TableName" || '.' || tc."Name" || CASE WHEN COALESCE(tc."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(tc."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                    'ALTER TABLE  "' || tc."TableSchema" || '"."' || tc."TableName" || '" ADD CONSTRAINT "' || tc."Name" || '" EXCLUDE' || 
                    CASE WHEN NULLIF(TRIM(tc."AccessMethod"), '') IS NOT NULL THEN ' USING ' || tc."AccessMethod" ELSE '' END ||
                    ' (' || (SELECT STRING_AGG("SchemaSmith"."QuoteIndexColumnList"((celem ->> 'Column')::TEXT) || ' WITH ' || (celem ->> 'Operator')::TEXT, ',')
                               FROM JSON_ARRAY_ELEMENTS(tc."ExcludeColumns"::JSON) AS celem) || ')' ||
                    CASE WHEN NULLIF(TRIM(tc."FilterExpression"), '') IS NOT NULL THEN ' WHERE (' || tc."FilterExpression" || ')' ELSE '' END ||
                    CASE WHEN tc."Deferrable" THEN ' DEFERRABLE' ELSE '' END ||
                    CASE WHEN tc."InitiallyDeferred" THEN ' INITIALLY DEFERRED' ELSE '' END || ';', CHR(10))
    INTO sql_script
    FROM temp_excludes tc
    WHERE NOT EXISTS (SELECT 1
                        FROM pg_constraint con
                        JOIN pg_class rel ON rel.oid = con.conrelid
                        JOIN pg_namespace nsp ON nsp.oid = con.connamespace
                                             AND nsp.nspname = tc."TableSchema"
                                             AND rel.relname = tc."TableName"
                        WHERE con.contype = 'x'
                          AND con.conname = tc."Name");
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  RAISE NOTICE 'Add Missing Defaults';
  SELECT STRING_AGG('RAISE NOTICE ''  Add missing default for ' || tc."TableSchema" || '.' || tc."TableName" || '.' || tc."Name" || CASE WHEN COALESCE(tc."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(tc."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                    'ALTER TABLE  "' || tc."TableSchema" || '"."' || tc."TableName" || '" ALTER COLUMN "' || tc."Name" || '" SET DEFAULT ' || tc."Default" ||';', CHR(10))
    INTO sql_script
    FROM temp_columns tc
    WHERE NULLIF(tc."Default", '') IS NOT NULL
      AND EXISTS (SELECT 1 FROM pg_attribute a JOIN pg_class rc ON rc.oid = a.attrelid JOIN pg_namespace nn ON nn.oid = rc.relnamespace WHERE nn.nspname = tc."TableSchema" AND rc.relname = tc."TableName" AND a.attname = tc."Name" AND a.attnum > 0 AND NOT a.attisdropped AND NOT a.atthasdef);
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  RAISE NOTICE 'Add Missing Check Constraints';
  SELECT STRING_AGG('RAISE NOTICE ''  Add missing check constraint ' || tc."TableSchema" || '.' || tc."TableName" || '.' || tc."Name" || CASE WHEN COALESCE(tc."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(tc."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                    'ALTER TABLE  "' || tc."TableSchema" || '"."' || tc."TableName" || '" ADD CONSTRAINT "' || tc."Name" || '" CHECK (' || tc."Expression" || ')' ||
                    CASE WHEN tc."Deferrable" THEN ' DEFERRABLE' ELSE '' END ||
                    CASE WHEN tc."InitiallyDeferred" THEN ' INITIALLY DEFERRED' ELSE '' END || ';', CHR(10))
    INTO sql_script
    FROM temp_checks tc
    WHERE NOT EXISTS (SELECT 1 
                        FROM pg_constraint con
                        JOIN pg_class rel ON rel.oid = con.conrelid
                        JOIN pg_namespace nsp ON nsp.oid = con.connamespace
                                             AND nsp.nspname = tc."TableSchema"
                                             AND rel.relname = tc."TableName"
                        WHERE con.contype = 'c'
                          AND con.conname = tc."Name");
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  -- Column-level checks get a deterministic name (CK_<table>_<column>) so create-idempotency
  -- and modify-detection (in ModifiedTableQuench) can both key on it.
  RAISE NOTICE 'Add Missing Column Check Constraints';
  SELECT STRING_AGG('RAISE NOTICE ''  Add missing column check constraint ' || tc."TableSchema" || '.' || tc."TableName" || '.' || tc."Name" || CASE WHEN COALESCE(tc."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(tc."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                    'ALTER TABLE  "' || tc."TableSchema" || '"."' || tc."TableName" || '" ADD CONSTRAINT "CK_' || tc."TableName" || '_' || tc."Name" || '" CHECK (' || tc."CheckExpression" || ');', CHR(10))
    INTO sql_script
    FROM temp_columns tc
    WHERE NULLIF(tc."CheckExpression", '') IS NOT NULL
      AND NOT EXISTS (SELECT 1
                        FROM pg_constraint con
                        JOIN pg_class rel ON rel.oid = con.conrelid
                        JOIN pg_namespace nsp ON nsp.oid = con.connamespace
                                             AND nsp.nspname = tc."TableSchema"
                                             AND rel.relname = tc."TableName"
                        WHERE con.contype = 'c'
                          AND con.conname = 'CK_' || tc."TableName" || '_' || tc."Name");
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

END
$$;