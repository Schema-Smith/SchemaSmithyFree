-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."ModifiedTableQuench"
  (p_WhatIf BOOLEAN = FALSE,
   p_DropUnknownIndexes BOOLEAN = FALSE,
   p_DropTablesRemovedFromProduct BOOLEAN = TRUE,
   p_DropColumnsRemovedFromProduct BOOLEAN = TRUE,
   p_DropForeignKeysRemovedFromProduct BOOLEAN = TRUE,
   p_DropCheckConstraintsRemovedFromProduct BOOLEAN = TRUE,
   p_DropExcludeConstraintsRemovedFromProduct BOOLEAN = TRUE,
   p_DropStatisticsRemovedFromProduct BOOLEAN = TRUE,
   p_DropIndexesRemovedFromProduct BOOLEAN = TRUE)
  LANGUAGE plpgsql
AS $$
DECLARE
  sql_script TEXT = '';
BEGIN
    IF p_DropTablesRemovedFromProduct THEN
      RAISE NOTICE 'Drop inbound foreign keys referencing tables removed from the product';
      -- Drop any foreign key that REFERENCES a table about to be removed (from any table), so the
      -- table drop below does not fail on a still-present inbound dependency. Same removed-table
      -- predicate as the drop pass; catalog-driven so it is not limited to product-declared parents.
      SELECT STRING_AGG('ALTER TABLE "' || pn.nspname || '"."' || pc.relname || '" DROP CONSTRAINT IF EXISTS "' || con.conname || '";', CHR(10))
        INTO sql_script
        FROM temp_product_ownership tp
        JOIN pg_class fc       ON fc.relname = tp."TableName"
        JOIN pg_namespace fn   ON fn.oid = fc.relnamespace AND fn.nspname = tp."Schema"
        JOIN pg_constraint con ON con.contype = 'f' AND con.confrelid = fc.oid
        JOIN pg_class pc       ON pc.oid = con.conrelid
        JOIN pg_namespace pn   ON pn.oid = pc.relnamespace
        WHERE tp."IndexName" IS NULL
          AND NOT EXISTS (SELECT 1
                            FROM temp_tables t
                            WHERE tp."Schema" = t."Schema"
                              AND tp."TableName" = t."Name")
          AND NOT EXISTS (SELECT 1
                            FROM pg_matviews mv
                            WHERE mv.schemaname = tp."Schema"
                              AND mv.matviewname = tp."TableName");
      CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

      RAISE NOTICE 'Drop tables removed from the product';
      -- temp_product_ownership is template-and-schema-scoped (see ValidateTableOwnership),
      -- so this pass only considers tables owned by the current (template, schema)
      -- iteration. No additional restriction needed here; the predicate already excludes
      -- other templates' tables AND other tenants' tables of the same template.
      -- Scoping is delegated to upstream ValidateTableOwnership — see the TRANSITIONAL
      -- (slice 3 audit B1 of schema-templates) markers there for the deletion trigger.
      SELECT STRING_AGG('RAISE NOTICE ''  Table ' || tp."Schema" || '.' || tp."TableName" || ' no longer in product'';' || CHR(10) ||
                        CASE WHEN EXISTS (SELECT 1 FROM pg_catalog.pg_proc p JOIN pg_namespace n ON p.pronamespace = n.oid WHERE p.proname = 'CustomTableDrop' AND n.nspname = 'SchemaSmith' )
                             THEN 'CALL "SchemaSmith"."CustomTableDrop"(''' || tp."Schema" || ''', ''' || tp."TableName" || ''');'
                             ELSE 'DROP TABLE IF EXISTS "' || tp."Schema" || '"."' || tp."TableName" || '";'
                             END, CHR(10))
        INTO sql_script
        FROM temp_product_ownership tp
        WHERE tp."IndexName" IS NULL
          AND NOT EXISTS (SELECT 1
                            FROM temp_tables t
                            WHERE tp."Schema" = t."Schema"
                              AND tp."TableName" = t."Name")
          AND NOT EXISTS (SELECT 1
                            FROM pg_matviews mv
                            WHERE mv.schemaname = tp."Schema"
                              AND mv.matviewname = tp."TableName");
      CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);
    END IF;

    RAISE NOTICE 'Collect Existing Column Definitions';
    DROP TABLE IF EXISTS temp_existing_columns;
    CREATE TEMPORARY TABLE temp_existing_columns AS
      SELECT t."Schema" AS "TableSchema",
             t."Name" AS "TableName",
             c.column_name AS "ColumnName",
             CASE WHEN c.domain_name IS NOT NULL
                  THEN CASE WHEN c.domain_schema != 'pg_catalog' THEN '"' || c.domain_schema || '".' ELSE '' END || '"' || c.domain_name || '"'
                  ELSE CASE WHEN c.udt_schema != 'pg_catalog' THEN c.udt_schema || '.' ELSE '' END || REGEXP_REPLACE(c.udt_name, 'bpchar', 'CHAR', 'i')
                  END ||
             CASE WHEN c.domain_name IS NULL AND UPPER(c.udt_name) LIKE '%CHAR'
                  THEN CASE WHEN COALESCE(c.character_maximum_length, -1) = -1 THEN '' ELSE '(' || c.character_maximum_length || ')' END
                  WHEN c.domain_name IS NULL AND UPPER(c.udt_name) IN ('NUMERIC', 'DECIMAL') AND c.numeric_precision IS NOT NULL
                  THEN '(' || c.numeric_precision || CASE WHEN COALESCE(c.numeric_scale, 0) != 0 THEN ', ' || c.numeric_scale ELSE '' END || ')'
                  WHEN c.domain_name IS NULL AND UPPER(c.udt_name) = 'TIMESTAMP' AND COALESCE(c.datetime_precision, 6) != 6
                  THEN '(' || c.datetime_precision || ')'
                  ELSE '' END AS "DataType",
             CAST(CASE WHEN c.is_nullable = 'YES' THEN TRUE ELSE FALSE END AS BOOLEAN) AS "Nullable",
             c.column_default AS "Default",
             c.collation_name AS "Collation",
             CASE WHEN a.attidentity = 'd'
                  THEN 'GENERATED BY DEFAULT AS IDENTITY'
                  WHEN a.attidentity = 'a'
                  THEN 'GENERATED ALWAYS AS IDENTITY' ||
                       CASE WHEN seq.seqstart IS NOT NULL
                            THEN '(START WITH ' || seq.seqstart || ' INCREMENT BY ' || seq.seqincrement || ')'
                            ELSE '' END
                  ELSE c.is_generated END AS "Generated",
             c.generation_expression AS "GenerationExpression",
             CASE WHEN a.attgenerated = '' OR a.attgenerated = 's' THEN FALSE ELSE TRUE END AS "Virtual",
             CASE a.attstorage
                  WHEN 'p' THEN 'PLAIN'
                  WHEN 'm' THEN 'MAIN'
                  WHEN 'e' THEN 'EXTERNAL'
                  WHEN 'x' THEN 'EXTENDED'
                  ELSE 'DEFAULT' END AS "Storage",
             CASE a.attcompression 
                  WHEN 'p' THEN 'pglz' 
                  WHEN 'l' THEN 'lz4' 
                  ELSE 'DEFAULT' END AS "Compression",
             '' AS "OldName"
        FROM temp_tables t
        JOIN information_schema.columns c ON c.table_schema = t."Schema"
                                         AND c.table_name = t."Name"
        JOIN pg_attribute a ON a.attrelid = ('"' || c.table_schema || '"' ||  '.' || '"' ||  c.table_name || '"')::regclass
                           AND a.attname = c.column_name
                           AND NOT a.attisdropped
        LEFT JOIN pg_depend d ON d.refobjid = a.attrelid
                              AND d.refobjsubid = a.attnum
                              AND d.classid = 'pg_class'::regclass
                              AND d.deptype = 'i'
        LEFT JOIN pg_sequence seq ON seq.seqrelid = d.objid
        WHERE t."Schema" = c.table_schema
          AND t."Name" = c.table_name;

    RAISE NOTICE 'Collect Existing Index Definitions';
    CALL "SchemaSmith"."BuildExistingIndexesSnapshot"();

    RAISE NOTICE 'Collect Existing Foreign Key Definitions';
    DROP TABLE IF EXISTS temp_existing_foreignkeys;
    CREATE TEMPORARY TABLE temp_existing_foreignkeys AS
      SELECT t."Schema" AS "TableSchema",
             t."Name" AS "TableName",
             con.conname AS "KeyName",
             (SELECT STRING_AGG(a.attname, ',' ORDER BY idx)
                FROM pg_attribute a
                CROSS JOIN LATERAL UNNEST(con.conkey) WITH ORDINALITY AS u(element, idx)
                WHERE a.attrelid = con.conrelid
                  AND a.attnum = element) AS "Columns",
             fnsp.nspname AS "RelatedTableSchema",
             frel.relname AS "RelatedTable",
             (SELECT STRING_AGG(a.attname, ',' ORDER BY idx)
                FROM pg_attribute a
                CROSS JOIN LATERAL UNNEST(con.confkey) WITH ORDINALITY AS u(element, idx)
                WHERE a.attrelid = frel.oid
                  AND a.attnum = element) AS "RelatedColumns",
             CASE con.confdeltype WHEN 'a' THEN '' WHEN 'c' THEN 'CASCADE' WHEN 'n' THEN 'SET NULL' WHEN 'r' THEN 'RESTRICT' END AS "DeleteAction",
             CASE con.confupdtype WHEN 'a' THEN '' WHEN 'c' THEN 'CASCADE' WHEN 'n' THEN 'SET NULL' WHEN 'r' THEN 'RESTRICT' END AS "UpdateAction"
        FROM temp_tables t
        JOIN pg_catalog.pg_constraint con ON con.conrelid = ('"' || t."Schema" || '"' ||  '.' || '"' ||  t."Name" || '"')::regclass
        JOIN pg_catalog.pg_class frel ON frel.oid = con.confrelid
        JOIN pg_catalog.pg_namespace fnsp ON fnsp.oid = frel.relnamespace
        WHERE con.contype = 'f';

    RAISE NOTICE 'Collect Existing Check Constraint Definitions';
    DROP TABLE IF EXISTS temp_existing_checks;
    CREATE TEMPORARY TABLE temp_existing_checks AS
      SELECT t."Schema" AS "TableSchema",
             t."Name" AS "TableName",
             con.conname AS "CheckName",
             REGEXP_REPLACE(pg_catalog.PG_GET_CONSTRAINTDEF(con.oid), '^CHECK \(\((.+)\)\)$', '\1') AS "Expression"
        FROM temp_tables t
        JOIN pg_catalog.pg_constraint con ON con.conrelid = ('"' || t."Schema" || '"' ||  '.' || '"' ||  t."Name" || '"')::regclass
        WHERE con.contype = 'c';

    RAISE NOTICE 'Collect Existing Exclude Constraint Definitions';
    DROP TABLE IF EXISTS temp_existing_excludes;
    CREATE TEMPORARY TABLE temp_existing_excludes AS
      SELECT t."Schema" AS "TableSchema",
             t."Name" AS "TableName",
             i.relname AS "ExcludeName",
             (SELECT am.amname FROM pg_am am WHERE i.relam = am.oid AND i.relkind = 'i') AS "AccessMethod",
             (SELECT STRING_AGG("SchemaSmith"."QuoteIndexColumnList"(PG_GET_INDEXDEF(idx.indexrelid, idx::int4, true)) ||
                                CASE WHEN (idx.indoption[idx-1] & 1) = 1 THEN ' DESC' || CASE WHEN (idx.indoption[idx-1] & 2) = 2 THEN '' ELSE ' NULLS LAST' END
                                     ELSE CASE WHEN (idx.indoption[idx-1] & 2) = 2 THEN ' NULLS FIRST' ELSE '' END
                                     END || ' WITH ' || COALESCE(nsp.nspname || '.' || o.oprname, o.oprname), ',' ORDER BY idx)
                FROM UNNEST(idx.indkey) WITH ORDINALITY AS u(element, idx)
                LEFT JOIN LATERAL (SELECT (con.conexclop::oid[])[idx] AS opr_oid) co ON TRUE
                LEFT JOIN pg_operator o ON o.oid = co.opr_oid
                LEFT JOIN pg_namespace nsp ON nsp.oid = o.oprnamespace AND nsp.nspname != 'pg_catalog') AS "ExcludeColumns",
             COALESCE("SchemaSmith"."StripParenWrapping"(PG_GET_EXPR(idx.indpred, idx.indrelid)), '') AS "FilterExpression",
             COALESCE(con.condeferrable, FALSE) AS "Deferrable",
             COALESCE(con.condeferred, FALSE) AS "InitiallyDeferred"
        FROM temp_tables t
        JOIN pg_index idx ON idx.indrelid = ('"' || t."Schema" || '"."' || t."Name" || '"')::regclass
        JOIN pg_class i ON i.oid = idx.indexrelid
        JOIN pg_catalog.pg_constraint con ON con.conrelid = idx.indrelid AND con.conname = i.relname AND con.contype = 'x';

    RAISE NOTICE 'Collect Existing Statistics Definitions';
    DROP TABLE IF EXISTS temp_existing_statistics;
    CREATE TEMPORARY TABLE temp_existing_statistics AS
      SELECT t."Schema" AS "TableSchema",
             t."Name" AS "TableName",
             se.stxname AS "StatisticsName",
             COALESCE((SELECT STRING_AGG(CASE k WHEN 'd' THEN 'NDISTINCT' WHEN 'f' THEN 'DEPENDENCIES' WHEN 'm' THEN 'MCV' WHEN 'e' THEN 'EXPRESSIONS' ELSE k::text END, ',')
                       FROM UNNEST(se.stxkind) AS k), '') AS "Kind",
             COALESCE(ARRAY_TO_STRING(ARRAY_CAT(COALESCE((SELECT ARRAY_AGG(a.attname)
                                                            FROM UNNEST(se.stxkeys) WITH ORDINALITY AS t(attnum, ord)
                                                            JOIN pg_attribute a ON a.attrelid = se.stxrelid AND a.attnum = t.attnum
                                                            WHERE a.attnum > 0),
                                                         ARRAY[]::text[]),
                                                COALESCE((SELECT ARRAY_AGG(exp.expr)
                                                            FROM pg_stats_ext_exprs exp
                                                            WHERE exp.schemaname = t."Schema" AND exp.statistics_name = se.stxname),
                                                         ARRAY[]::text[])), ','), '') AS "StatisticsColumns"
      FROM temp_tables t
      JOIN  pg_statistic_ext se ON se.stxrelid = ('"' || t."Schema" || '"."' || t."Name" || '"')::regclass;

    RAISE NOTICE 'Drop Modified or Removed Foreign Keys';
    SELECT STRING_AGG('RAISE NOTICE ''  Foreign Key ' || ek."TableSchema" || '.' || ek."TableName" || '.' || ek."KeyName" || ' no longer in product'';' || CHR(10) ||
                      'ALTER TABLE "' || ek."TableSchema" || '"."' || ek."TableName" || '" DROP CONSTRAINT IF EXISTS "' || ek."KeyName" || '" CASCADE;', CHR(10))
      INTO sql_script
      FROM temp_existing_foreignkeys ek
      JOIN temp_tables tt ON tt."Schema" = ek."TableSchema"
                         AND tt."Name" = ek."TableName"
      WHERE NOT EXISTS (SELECT 1
                          FROM temp_fks fk
                          WHERE ek."TableSchema" = fk."TableSchema"
                            AND ek."TableName" = fk."TableName"
                            AND ek."KeyName" = fk."Name"
                            AND ek."Columns" = fk."Columns"
                            AND ek."RelatedTableSchema" = fk."RelatedTableSchema"
                            AND ek."RelatedTable" = fk."RelatedTable"
                            AND ek."RelatedColumns" = fk."RelatedColumns"
                            AND ek."DeleteAction" = fk."DeleteAction"
                            AND ek."UpdateAction" = fk."UpdateAction")
        -- Split modified vs removed: a same-named FK whose definition changed is dropped
        -- unconditionally (the create-missing pass recreates it); a FK whose name is gone
        -- from the product is a by-absence removal, gated by the cascade flag.
        AND (EXISTS (SELECT 1
                       FROM temp_fks fk2
                       WHERE ek."TableSchema" = fk2."TableSchema"
                         AND ek."TableName" = fk2."TableName"
                         AND ek."KeyName" = fk2."Name")
             OR (p_DropForeignKeysRemovedFromProduct AND COALESCE(tt."DropForeignKeysRemovedFromProduct", TRUE)));
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Drop Modified or Removed Check Constraints';
    SELECT STRING_AGG('RAISE NOTICE ''  Check Constraint ' || ec."TableSchema" || '.' || ec."TableName" || '.' || ec."CheckName" || ' modified or no longer in product'';' || CHR(10) ||
                      'ALTER TABLE "' || ec."TableSchema" || '"."' || ec."TableName" || '" DROP CONSTRAINT IF EXISTS "' || ec."CheckName" || '" CASCADE;', CHR(10))
      INTO sql_script
      FROM temp_existing_checks ec
      JOIN temp_tables tt ON tt."Schema" = ec."TableSchema"
                         AND tt."Name" = ec."TableName"
      WHERE NOT EXISTS (SELECT 1
                          FROM temp_checks c
                          WHERE ec."TableSchema" = c."TableSchema"
                            AND ec."TableName" = c."TableName"
                            AND ec."CheckName" = c."Name"
                            AND ec."Expression" = c."Expression")
        -- Column-level checks (CK_<table>_<column>) are owned by the column pass below, not the
        -- table-level CheckConstraints array; excluding them here prevents a phantom drop on every run.
        AND NOT EXISTS (SELECT 1
                          FROM temp_columns col
                          WHERE col."TableSchema" = ec."TableSchema"
                            AND col."TableName" = ec."TableName"
                            AND NULLIF(col."CheckExpression", '') IS NOT NULL
                            AND ec."CheckName" = 'CK_' || col."TableName" || '_' || col."Name")
        -- Split modified vs removed: a same-named check whose expression changed is dropped
        -- unconditionally (the create pass re-adds it); a check whose name is gone from the
        -- product is a by-absence removal, gated by the cascade flag.
        AND (EXISTS (SELECT 1
                       FROM temp_checks c2
                       WHERE ec."TableSchema" = c2."TableSchema"
                         AND ec."TableName" = c2."TableName"
                         AND ec."CheckName" = c2."Name")
             OR (p_DropCheckConstraintsRemovedFromProduct AND COALESCE(tt."DropCheckConstraintsRemovedFromProduct", TRUE)));
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Drop Modified Column Check Constraints';
    -- A column-check (CK_<table>_<column>) whose live, normalized definition differs from the desired
    -- CheckExpression is dropped here so the create pass re-adds it with the new expression. Reuses the
    -- SAME pg_get_constraintdef normalization (temp_existing_checks."Expression") as the table-level
    -- compare above, so an unchanged expression compares equal and is left untouched (idempotent).
    SELECT STRING_AGG('RAISE NOTICE ''  Column Check Constraint ' || ec."TableSchema" || '.' || ec."TableName" || '.' || ec."CheckName" || ' modified'';' || CHR(10) ||
                      'ALTER TABLE "' || ec."TableSchema" || '"."' || ec."TableName" || '" DROP CONSTRAINT IF EXISTS "' || ec."CheckName" || '" CASCADE;', CHR(10))
      INTO sql_script
      FROM temp_existing_checks ec
      JOIN temp_columns col ON col."TableSchema" = ec."TableSchema"
                           AND col."TableName" = ec."TableName"
                           AND NULLIF(col."CheckExpression", '') IS NOT NULL
                           AND ec."CheckName" = 'CK_' || col."TableName" || '_' || col."Name"
      WHERE ec."Expression" != col."CheckExpression";
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Drop Modified or Removed Statistics';
    SELECT STRING_AGG('RAISE NOTICE ''  Statistics ' || es."TableSchema" || '.' || es."StatisticsName" || ' modified or no longer in product'';' || CHR(10) ||
                      'DROP STATISTICS IF EXISTS "' || es."TableSchema" || '"."' || es."StatisticsName" || '" CASCADE;', CHR(10))
      INTO sql_script
      FROM temp_existing_statistics es
      JOIN temp_tables tt ON tt."Schema" = es."TableSchema"
                         AND tt."Name" = es."TableName"
      WHERE NOT EXISTS (SELECT 1
                          FROM temp_statistics ts
                          WHERE es."TableSchema" = ts."TableSchema"
                            AND es."TableName" = ts."TableName"
                            AND es."StatisticsName" = ts."Name"
                            AND es."Kind" = COALESCE(NULLIF(ts."Kind", ''), 'NDISTINCT,DEPENDENCIES,MCV')
                            AND es."StatisticsColumns" = ts."StatisticsColumns")
        -- Split modified vs removed: a same-named statistics object whose definition changed is
        -- dropped unconditionally (the create pass re-adds it); one whose name is gone from the
        -- product is a by-absence removal, gated by the cascade flag + per-table tightening.
        AND (EXISTS (SELECT 1
                       FROM temp_statistics ts2
                       WHERE es."TableSchema" = ts2."TableSchema"
                         AND es."TableName" = ts2."TableName"
                         AND es."StatisticsName" = ts2."Name")
             OR (p_DropStatisticsRemovedFromProduct AND COALESCE(tt."DropStatisticsRemovedFromProduct", TRUE)));
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Drop Modified or Removed Exclude Constraints';
    SELECT STRING_AGG('RAISE NOTICE ''  Exclude Constraint ' || ec."TableSchema" || '.' || ec."TableName" || '.' || ec."ExcludeName" || ' modified or no longer in product'';' || CHR(10) ||
                      'ALTER TABLE "' || ec."TableSchema" || '"."' || ec."TableName" || '" DROP CONSTRAINT IF EXISTS "' || ec."ExcludeName" || '" CASCADE;', CHR(10))
      INTO sql_script
      FROM temp_existing_excludes ec
      JOIN temp_tables tt ON tt."Schema" = ec."TableSchema"
                         AND tt."Name" = ec."TableName"
      WHERE NOT EXISTS (SELECT 1
                          FROM temp_excludes c
                          WHERE ec."TableSchema" = c."TableSchema"
                            AND ec."TableName" = c."TableName"
                            AND ec."ExcludeName" = c."Name"
                            AND (ec."AccessMethod" = c."AccessMethod" OR c."AccessMethod" = '')
                            AND ec."ExcludeColumns" = (SELECT STRING_AGG("SchemaSmith"."QuoteIndexColumnList"((celem ->> 'Column')::TEXT) || ' WITH ' || (celem ->> 'Operator')::TEXT, ',')
                                                         FROM JSON_ARRAY_ELEMENTS(c."ExcludeColumns"::JSON) AS celem)
                            AND ec."FilterExpression" = c."FilterExpression"
                            AND ec."Deferrable" = c."Deferrable"
                            AND ec."InitiallyDeferred" = c."InitiallyDeferred")
        -- Split modified vs removed: a same-named exclude constraint whose definition changed is
        -- dropped unconditionally (the create pass re-adds it); one whose name is gone from the
        -- product is a by-absence removal, gated by the cascade flag + per-table tightening.
        AND (EXISTS (SELECT 1
                       FROM temp_excludes c2
                       WHERE ec."TableSchema" = c2."TableSchema"
                         AND ec."TableName" = c2."TableName"
                         AND ec."ExcludeName" = c2."Name")
             OR (p_DropExcludeConstraintsRemovedFromProduct AND COALESCE(tt."DropExcludeConstraintsRemovedFromProduct", TRUE)));
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Handle Renamed Indexes And Unique Constraints';
    SELECT STRING_AGG('RAISE NOTICE ''  Renaming ' || CASE WHEN ei."PrimaryKey" OR ei."UniqueConstraint" THEN 'Constraint' ELSE 'Index' END || ' ' || ei."TableSchema" || '.' || ei."TableName" || '.' || ei."IndexName" || ' to ' || i."Name" || ''';' || CHR(10) ||
                      CASE WHEN NOT (ei."PrimaryKey" OR ei."UniqueConstraint")
                           THEN 'ALTER INDEX IF EXISTS "' || ei."TableSchema" || '"."' || ei."IndexName" || '" RENAME TO "' || i."Name"  || '";'
                           ELSE 'ALTER TABLE "' || ei."TableSchema" || '"."' || ei."TableName" || '" RENAME CONSTRAINT "' || ei."IndexName" || '" TO "' || i."Name" || '";' END, CHR(10))
      INTO sql_script
      FROM temp_existing_indexes ei
      JOIN temp_indexes i ON i."TableSchema" = ei."TableSchema"
                         AND i."TableName" = ei."TableName"
                         AND i."Name" != ei."IndexName"
                         AND i."IndexColumns" = ei."IndexColumns"
                         AND COALESCE(i."IncludeColumns", '') = COALESCE(ei."IncludeColumns", '')
                         AND COALESCE(i."Unique", FALSE) = ei."Unique"
                         AND COALESCE(i."UniqueConstraint", FALSE) = ei."UniqueConstraint"
                         AND COALESCE(i."PrimaryKey", FALSE) = ei."PrimaryKey"
                         AND COALESCE(i."FilterExpression", '') = COALESCE(ei."FilterExpression", '')
                         AND COALESCE(i."AccessMethod", 'btree') = COALESCE(ei."AccessMethod", 'btree')
      WHERE NOT EXISTS (SELECT 1
                          FROM temp_indexes i
                          WHERE i."TableSchema" = ei."TableSchema"
                            AND i."TableName" = ei."TableName"
                            AND i."Name" = ei."IndexName");
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Identify Unknown, Removed, and Modified Indexes to Drop';
    DROP TABLE IF EXISTS temp_indexes_to_drop;
    CREATE TEMPORARY TABLE temp_indexes_to_drop AS
      SELECT ei."TableSchema",
             ei."TableName",
             ei."IndexName",
             ei."PrimaryKey" OR ei."UniqueConstraint" AS "IsConstraint"
        FROM temp_existing_indexes ei
        WHERE (p_DropUnknownIndexes 
           AND NOT EXISTS (SELECT 1 -- Unknown Index
                            FROM temp_indexes i
                            WHERE i."TableSchema" = ei."TableSchema"
                              AND i."TableName" = ei."TableName"
                              AND i."Name" = ei."IndexName"))
           OR EXISTS (SELECT 1 -- Modified Index
                        FROM temp_indexes i
                        WHERE i."TableSchema" = ei."TableSchema"
                          AND i."TableName" = ei."TableName"
                          AND i."Name" = ei."IndexName"
                          AND (i."IndexColumns" != ei."IndexColumns"
                            OR COALESCE(i."IncludeColumns", '') != COALESCE(ei."IncludeColumns", '')
                            OR (COALESCE(i."Unique", FALSE) OR COALESCE(i."PrimaryKey", FALSE) OR COALESCE(i."UniqueConstraint", FALSE)) != ei."Unique"
                            OR COALESCE(i."UniqueConstraint", FALSE) != ei."UniqueConstraint"
                            OR COALESCE(i."PrimaryKey", FALSE) != ei."PrimaryKey"
                            OR COALESCE(i."FilterExpression", '') != COALESCE(ei."FilterExpression", '')
                            OR COALESCE(i."AccessMethod", 'btree') != COALESCE(ei."AccessMethod", 'btree')))
           OR (p_DropIndexesRemovedFromProduct -- Index Removed from Product Definition (gated)
               AND COALESCE((SELECT tt."DropIndexesRemovedFromProduct" FROM temp_tables tt WHERE tt."Schema" = ei."TableSchema" AND tt."Name" = ei."TableName"), TRUE)
               AND EXISTS (SELECT 1
                        FROM temp_product_ownership tp
                        WHERE tp."IndexName" = ei."IndexName"
                          AND tp."Schema" = ei."TableSchema"
                          AND tp."TableName" = ei."TableName"
                          AND NOT EXISTS (SELECT 1
                                            FROM temp_indexes i
                                            WHERE i."TableSchema" = ei."TableSchema"
                                              AND i."TableName" = ei."TableName"
                                              AND i."Name" = ei."IndexName")));

    RAISE NOTICE 'Drop Unknown, Removed, and Modified Indexes';
    SELECT STRING_AGG('RAISE NOTICE ''  Dropping ' || CASE WHEN "IsConstraint" THEN 'Constraint' ELSE 'Index' END || ' ' || ti."TableSchema" || '.' || ti."TableName" || '.' || ti."IndexName" || ''';' || CHR(10) ||
                      CASE WHEN "IsConstraint"
                           THEN 'ALTER TABLE "' || ti."TableSchema" || '"."' || ti."TableName" || '" DROP CONSTRAINT IF EXISTS "' || ti."IndexName" || '" CASCADE;'
                           ELSE 'DROP INDEX IF EXISTS "' || ti."TableSchema" || '"."' || ti."IndexName" || '";' END, CHR(10))
      INTO sql_script
      FROM temp_indexes_to_drop ti;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Drop Columns No Longer Part of The Product Definition';
    SELECT STRING_AGG('RAISE NOTICE ''  Column ' || ec."TableSchema" || '.' || ec."TableName" || '.' || ec."ColumnName" || ' no longer in product'';' || CHR(10) ||
                      'ALTER TABLE "' || ec."TableSchema" || '"."' || ec."TableName" || '" DROP COLUMN IF EXISTS "' || ec."ColumnName" || '" CASCADE;', CHR(10))
      INTO sql_script
      FROM temp_existing_columns ec
      JOIN temp_tables tt ON tt."Schema" = ec."TableSchema"
                         AND tt."Name" = ec."TableName"
      WHERE NOT EXISTS (SELECT 1
                          FROM temp_columns c
                          WHERE ec."TableSchema" = c."TableSchema"
                            AND ec."TableName" = c."TableName"
                            AND ec."ColumnName" = c."Name")
        AND p_DropColumnsRemovedFromProduct
        AND COALESCE(tt."DropColumnsRemovedFromProduct", TRUE);
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Fixup Any Modified Index Fill Factors';
    SELECT STRING_AGG('RAISE NOTICE ''  Modify Fillfactor for ' || ti."TableSchema" || '.' || ti."Name" || ''';' || CHR(10) ||
                      'ALTER INDEX "' || ti."TableSchema" || '"."' || ti."Name" || '" SET (fillfactor = ' || ti."FillFactor" || ');', CHR(10))
      INTO sql_script
      FROM temp_indexes ti
      JOIN temp_existing_indexes ei ON ei."TableSchema" = ti."TableSchema"
                                   AND ei."TableName" = ti."TableName"
                                   AND ei."IndexName" = ti."Name"
      JOIN pg_index idx ON idx.indrelid = ('"' || ti."TableSchema" || '"' ||  '.' || '"' ||  ti."TableName" || '"')::regclass
      JOIN pg_class i ON i.oid = idx.indexrelid
                     AND i.relname = ti."Name"
      WHERE ti."UpdateFillFactor"
        AND ei."FillFactor" != ti."FillFactor"
        AND COALESCE(ti."AccessMethod", 'btree') NOT IN ('gin', 'brin', 'spgist');
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Drop Generated Columns Referencing Columns That Are Changing Data Type';
    SELECT STRING_AGG('RAISE NOTICE ''  Column ' || ec."TableSchema" || '.' || ec."TableName" || '.' || ec."ColumnName" || ' affected by type change'';' || CHR(10) ||
                      'ALTER TABLE "' || ec."TableSchema" || '"."' || ec."TableName" || '" DROP COLUMN IF EXISTS "' || ec."ColumnName" || '" CASCADE;', CHR(10))
      INTO sql_script
      FROM temp_existing_columns ec
      WHERE ec."Generated" = 'ALWAYS'
        AND EXISTS (SELECT 1
                      FROM temp_columns c
                      JOIN temp_existing_columns ec2 ON ec2."TableSchema" = c."TableSchema"
                                                    AND ec2."TableName" = c."TableName"
                                                    AND ec2."ColumnName" = c."Name"                                                    
                      WHERE c."TableSchema" = ec."TableSchema"
                        AND c."TableName" = ec."TableName"
                        AND ((c."Name" = ec."ColumnName" AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ec."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC'))
                          OR (c."Name" = ec."ColumnName" AND ec."Virtual" AND c."GenerationExpression" = '')
                          OR (c."Name" != ec."ColumnName" AND (REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ec2."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') OR COALESCE(ec2."Collation", '') != COALESCE(c."Collation", '')) AND ec."GenerationExpression" LIKE '%' || c."Name" || '%')))
         AND EXISTS (SELECT 1
                       FROM information_schema.columns ic
                       WHERE ic.table_schema = ec."TableSchema"
                         AND ic.table_name = ec."TableName"
                         AND ic.column_name = ec."ColumnName");
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Drop Columns Changing to Generated';
    SELECT STRING_AGG('RAISE NOTICE ''  Column ' || ec."TableSchema" || '.' || ec."TableName" || '.' || ec."ColumnName" || ' now generated'';' || CHR(10) ||
                      'ALTER TABLE "' || ec."TableSchema" || '"."' || ec."TableName" || '" DROP COLUMN IF EXISTS "' || ec."ColumnName" || '" CASCADE;', CHR(10))
    INTO sql_script
    FROM temp_existing_columns ec
    JOIN temp_columns c ON c."TableSchema" = ec."TableSchema"
                        AND c."TableName" = ec."TableName"
                        AND c."Name" = ec."ColumnName"
                        AND c."Generated" = 'ALWAYS'
    WHERE ec."Generated" = 'NEVER'
      AND EXISTS (SELECT 1
                  FROM information_schema.columns ic
                  WHERE ic.table_schema = ec."TableSchema"
                    AND ic.table_name = ec."TableName"
                    AND ic.column_name = ec."ColumnName");
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    DELETE FROM temp_existing_columns ec
      WHERE NOT EXISTS (SELECT 1
                          FROM information_schema.columns ic
                          WHERE ic.table_schema = ec."TableSchema"
                            AND ic.table_name = ec."TableName"
                            AND ic.column_name = ec."ColumnName");


    RAISE NOTICE 'Drop Views and Rules Dependent on Modified Columns';
    SELECT STRING_AGG(CASE WHEN dep_view.relname IS NOT NULL
                           THEN 'RAISE NOTICE ''  Dropping View ' || ref_obj.relnamespace::regnamespace || '.' || dep_view.relname || ''';' || CHR(10) ||
                                'DROP VIEW IF EXISTS "' || ref_obj.relnamespace::regnamespace || '"."' || dep_view.relname || '";'
                           ELSE CASE WHEN dep_rule.rulename != '_RETURN'
                                     THEN 'RAISE NOTICE ''  Dropping Rule ' || rule_class.relnamespace::regnamespace || '.' || dep_rule.rulename || ''';' || CHR(10) ||
                                          'DROP RULE IF EXISTS "' || rule_class.relnamespace::regnamespace || '"."' || dep_rule.rulename || '";'
                                     END
                           END, CHR(10))
      INTO sql_script
      FROM temp_existing_columns ec
      JOIN pg_class AS ref_obj ON ref_obj.relnamespace::regnamespace = ('"' || ec."TableSchema" || '"')::regnamespace
                              AND ref_obj.relname = ec."TableName"
      JOIN pg_depend AS d ON ref_obj.oid = d.refobjid
      JOIN pg_attribute AS ref_attr ON ref_attr.attrelid = d.refobjid AND ref_attr.attnum = d.refobjsubid
                                   AND ref_attr.attname = ec."ColumnName"
      JOIN pg_rewrite AS dep_rule ON dep_rule.oid = d.objid
      JOIN pg_class rule_class ON rule_class.oid = dep_rule.ev_class
      LEFT JOIN pg_class AS dep_view ON dep_view.oid = dep_rule.ev_class
      WHERE EXISTS (SELECT 1 -- need to determine if any changes other than type have the need to drop dependent views/rules
                      FROM temp_columns c
                      WHERE c."TableSchema" = ec."TableSchema"
                        AND c."TableName" = ec."TableName"
                        AND c."Name" = ec."ColumnName"
                        AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ec."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC'));
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Add New Physical Columns Switched From Generated';
    SELECT STRING_AGG('RAISE NOTICE ''  Add new physical columns to ' || tt."Schema" || '.' || tt."Name" || ' (' ||
                      (SELECT STRING_AGG(tc."Name" || CASE WHEN COALESCE(tc."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(tc."VariantName", '''', '''''') || ')' ELSE '' END, ', ')
                         FROM temp_columns tc
                         WHERE tc."TableSchema" = tt."Schema" AND tc."TableName" = tt."Name"
                           AND (COALESCE(tc."Generated", 'NEVER') = 'NEVER' OR COALESCE(tc."Generated", '') LIKE 'GENERATED%IDENTITY%')
                           AND NOT EXISTS (SELECT 1 FROM information_schema.columns ic WHERE ic.table_name = tc."TableName" AND ic.table_schema = tc."TableSchema" AND ic.column_name = tc."Name")) ||
                      ')'';' || CHR(10) ||
                      'ALTER TABLE "' || tt."Schema" || '"."' || tt."Name" || '" ' ||
                      (SELECT STRING_AGG('ADD "' || tc."Name" || '" ' || tc."DataType" ||
                              CASE WHEN COALESCE(tc."Collation", '') != '' THEN ' COLLATE "' || tc."Collation" || '"' ELSE '' END ||
                              CASE WHEN COALESCE(tc."Generated", 'NEVER') LIKE 'GENERATED%IDENTITY%' THEN ' ' || tc."Generated" ELSE '' END ||
                              CASE WHEN tc."Nullable" THEN '' ELSE ' NOT NULL' END ||
                              CASE WHEN COALESCE(tc."Generated", 'NEVER') NOT LIKE 'GENERATED%IDENTITY%' AND COALESCE(tc."Default", '') != '' THEN ' DEFAULT ' || tc."Default" ELSE '' END, ', ')
                         FROM temp_columns tc
                         WHERE tc."TableSchema" = tt."Schema" AND tc."TableName" = tt."Name"
                           AND (COALESCE(tc."Generated", 'NEVER') = 'NEVER' OR COALESCE(tc."Generated", '') LIKE 'GENERATED%IDENTITY%')
                           AND NOT EXISTS (SELECT 1 FROM information_schema.columns ic WHERE ic.table_name = tc."TableName" AND ic.table_schema = tc."TableSchema" AND ic.column_name = tc."Name")) || ';', CHR(10))
      INTO sql_script
      FROM temp_tables tt
      WHERE EXISTS(SELECT * FROM information_schema.tables t WHERE t.table_schema = tt."Schema" AND t.table_name = tt."Name")
        AND EXISTS (SELECT 1
                      FROM temp_columns tc
                      WHERE tc."TableSchema" = tt."Schema" AND tc."TableName" = tt."Name"
                        AND (COALESCE(tc."Generated", 'NEVER') = 'NEVER' OR COALESCE(tc."Generated", '') LIKE 'GENERATED%IDENTITY%')
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns ic WHERE ic.table_name = tc."TableName" AND ic.table_schema = tc."TableSchema" AND ic.column_name = tc."Name"));
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Recreate Generated Columns With Changed Expression (PG < 17)';
    -- Sole handler for a generated-expression change on a generated column (status unchanged) on PG < 17.
    -- The re-add carries the FULL target definition (DataType, Collation, GENERATED ALWAYS AS (...) STORED,
    -- nullability) and applies non-default Storage/Compression on the freshly re-added column, so the column
    -- is fully converged after this pass — type/collation/nullability/storage/compression changes ride along
    -- here even when they changed alongside the expression. The "Alter Modified Columns" pass below excludes
    -- EXACTLY this column set via a matching NOT (...) guard.
    -- MUST match the "Alter Modified Columns" NOT (...) predicate below (Task A1 #296) — keep in lockstep.
    IF "SchemaSmith"."ServerVersionNum"() < 17 THEN
      SELECT STRING_AGG('RAISE NOTICE ''  Recreating generated column ' || c."TableSchema" || '.' || c."TableName" || '.' || c."Name" || ' (expression changed)'';' || CHR(10) ||
                        'ALTER TABLE "' || c."TableSchema" || '"."' || c."TableName" || '" DROP COLUMN IF EXISTS "' || c."Name" || '" CASCADE;' || CHR(10) ||
                        'ALTER TABLE "' || c."TableSchema" || '"."' || c."TableName" || '" ADD "' || c."Name" || '" ' || c."DataType" ||
                        CASE WHEN COALESCE(c."Collation", '') != '' THEN ' COLLATE "' || c."Collation" || '"' ELSE '' END ||
                        ' GENERATED ALWAYS AS (' || c."GenerationExpression" || ') STORED' ||
                        CASE WHEN c."Nullable" THEN '' ELSE ' NOT NULL' END || ';' ||
                        -- Re-added column is at default storage/compression; carry over any non-default target so
                        -- a combined expression+storage/compression change fully converges in this single pass.
                        CASE WHEN COALESCE(c."Storage", '') != '' AND COALESCE(c."Storage", '') != 'DEFAULT'
                             THEN CHR(10) || 'ALTER TABLE "' || c."TableSchema" || '"."' || c."TableName" || '" ALTER COLUMN "' || c."Name" || '" SET STORAGE ' || c."Storage" || ';'
                             ELSE '' END ||
                        CASE WHEN COALESCE(c."Compression", '') != '' AND COALESCE(c."Compression", '') != 'DEFAULT'
                             THEN CHR(10) || 'ALTER TABLE "' || c."TableSchema" || '"."' || c."TableName" || '" ALTER COLUMN "' || c."Name" || '" SET COMPRESSION ' || c."Compression" || ';'
                             ELSE '' END, CHR(10))
        INTO sql_script
        FROM temp_columns c
        JOIN temp_existing_columns ec ON ec."TableSchema" = c."TableSchema"
                                     AND ec."TableName" = c."TableName"
                                     AND ec."ColumnName" = c."Name"
        WHERE COALESCE(c."Generated", 'NEVER') != 'NEVER'
          AND COALESCE(c."GenerationExpression", '') != ''
          AND COALESCE(c."Generated", 'NEVER') = COALESCE(ec."Generated", 'NEVER')  -- generated status NOT changing — pure expression edit
          AND COALESCE(c."GenerationExpression", '') != COALESCE(ec."GenerationExpression", '')
          AND EXISTS (SELECT 1
                        FROM information_schema.columns ic
                        WHERE ic.table_schema = c."TableSchema"
                          AND ic.table_name = c."TableName"
                          AND ic.column_name = c."Name");
      CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);
    END IF;

    RAISE NOTICE 'Alter Modified Columns';
    SELECT STRING_AGG("code", CHR(10))
      INTO sql_script
      FROM (
    SELECT 'RAISE NOTICE ''Modified columns found in ' || c."TableSchema" || '.' || c."TableName" || '(' || 
           (SELECT STRING_AGG(ic."Name", ', ')
              FROM temp_columns ic
              JOIN temp_existing_columns iec ON iec."TableSchema" = ic."TableSchema"
                                            AND iec."TableName" = ic."TableName"
                                            AND iec."ColumnName" = ic."Name"
              WHERE EXISTS (SELECT 1
                              FROM information_schema.columns ic2
                              WHERE ic2.table_schema = c."TableSchema"
                                AND ic2.table_name = c."TableName"
                                AND ic2.column_name = ic."Name")
                AND ic."TableSchema" = c."TableSchema"
                AND ic."TableName" = c."TableName"
                AND (REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ic."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(iec."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
                  OR ic."Nullable" != iec."Nullable"
                  OR COALESCE("SchemaSmith"."StripTypeCast"(ic."Default"), '') != COALESCE("SchemaSmith"."StripTypeCast"(iec."Default"), '')
                  OR COALESCE(ic."Collation", '') != COALESCE(iec."Collation", '')
                  OR COALESCE(ic."Generated", 'NEVER') != COALESCE(iec."Generated", 'NEVER')
                  OR COALESCE(ic."GenerationExpression", '') != COALESCE(iec."GenerationExpression", '')
                  OR (COALESCE(ic."Storage", '') != '' AND COALESCE(ic."Storage", '') != COALESCE(iec."Storage", ''))
                  OR (COALESCE(ic."Compression", '') != '' AND COALESCE(ic."Compression", '') != COALESCE(iec."Compression", '')))) || ')'';' || CHR(10) ||
           'ALTER TABLE "' || c."TableSchema" || '"."' || c."TableName" || '"' || CHR(10) ||
           STRING_AGG(RTRIM(TRIM(TRAILING ',' FROM
                      CASE WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ec."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') OR COALESCE(c."Collation", '') != COALESCE(ec."Collation", '')
                           THEN ' ALTER COLUMN "' || c."Name" || '" SET DATA TYPE ' || c."DataType" ||
                                                    CASE WHEN COALESCE(c."Collation", '') != '' THEN ' COLLATE "' || c."Collation" || '"' ELSE '' END || ','
                           ELSE '' END ||
                      CASE WHEN COALESCE(c."GenerationExpression", '') != COALESCE(ec."GenerationExpression", '')
                           THEN CASE WHEN (COALESCE(c."Generated", 'NEVER') = 'NEVER' OR COALESCE(c."GenerationExpression", '') = '')
                                     THEN ' ALTER COLUMN "' || c."Name" || '" DROP EXPRESSION,'
                                     WHEN "SchemaSmith"."ServerVersionNum"() >= 17
                                     THEN ' ALTER COLUMN "' || c."Name" || '" SET EXPRESSION AS (' || COALESCE(c."GenerationExpression", '') || '),'
                                     ELSE '' END  -- PG < 17: expression change is handled by the drop-and-re-add pass below
                           ELSE '' END ||
                      CASE WHEN COALESCE(ec."Generated", 'NEVER') LIKE 'GENERATED%IDENTITY%'
                                AND COALESCE(c."Generated", 'NEVER') NOT LIKE 'GENERATED%IDENTITY%'
                           THEN ' ALTER COLUMN "' || c."Name" || '" DROP IDENTITY IF EXISTS,'
                           ELSE '' END ||
                      CASE WHEN c."Nullable" != ec."Nullable"
                           THEN CASE WHEN c."Nullable"
                                     THEN ' ALTER COLUMN "' || c."Name" || '" DROP NOT NULL,'
                                     ELSE ' ALTER COLUMN "' || c."Name" || '" SET NOT NULL,' END
                           ELSE '' END ||
                      CASE WHEN COALESCE("SchemaSmith"."StripTypeCast"(c."Default"), '') != COALESCE("SchemaSmith"."StripTypeCast"(ec."Default"), '')
                           THEN CASE WHEN COALESCE(c."Default", '') = ''
                                     THEN ' ALTER COLUMN "' || c."Name" || '" DROP DEFAULT,'
                                     ELSE ' ALTER COLUMN "' || c."Name" || '" SET DEFAULT ' || c."Default" || ',' END
                           ELSE '' END ||
                      CASE WHEN COALESCE(c."Storage", '') != COALESCE(ec."Storage", '') AND COALESCE(c."Storage", '') != ''
                           THEN ' ALTER COLUMN "' || c."Name" || '" SET STORAGE ' || COALESCE(c."Storage", '') || ','
                           ELSE '' END ||
                      CASE WHEN COALESCE(c."Compression", '') != COALESCE(ec."Compression", '') AND COALESCE(c."Compression", '') != ''
                           THEN ' ALTER COLUMN "' || c."Name" || '" SET COMPRESSION ' || COALESCE(c."Compression", '') || ','
                           ELSE '' END)), ',' || CHR(10)) || ';' AS "code"
      FROM temp_columns c
      JOIN temp_existing_columns ec ON ec."TableSchema" = c."TableSchema"
                                   AND ec."TableName" = c."TableName"
                                   AND ec."ColumnName" = c."Name"
      WHERE EXISTS (SELECT 1
                      FROM information_schema.columns ic
                      WHERE ic.table_schema = c."TableSchema"
                        AND ic.table_name = c."TableName"
                        AND ic.column_name = c."Name")
        AND (REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ec."DataType"), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
          OR c."Nullable" != ec."Nullable"
          OR COALESCE("SchemaSmith"."StripTypeCast"(c."Default"), '') != COALESCE("SchemaSmith"."StripTypeCast"(ec."Default"), '')
          OR COALESCE(c."Collation", '') != COALESCE(ec."Collation", '')
          OR COALESCE(c."Generated", 'NEVER') != COALESCE(ec."Generated", 'NEVER')
          OR COALESCE(c."GenerationExpression", '') != COALESCE(ec."GenerationExpression", '')
          OR (COALESCE(c."Storage", '') != '' AND COALESCE(c."Storage", '') != COALESCE(ec."Storage", ''))
          OR (COALESCE(c."Compression", '') != '' AND COALESCE(c."Compression", '') != COALESCE(ec."Compression", '')))
        AND NOT (-- PG < 17 generated-expression change on a generated column (status unchanged): handled SOLELY by the
                 -- drop-and-re-add pass above, which re-adds the FULL target definition. Exclude such columns from the
                 -- ALTER pass entirely — regardless of whether type/collation/nullability ALSO changed (the re-add carries
                 -- them). This guard must partition the column set IDENTICALLY to that pass: same predicate, no extra
                 -- "all other attributes equal" tail (a mixed change still belongs solely to drop-and-re-add).
                 -- MUST match the "Recreate Generated Columns With Changed Expression (PG < 17)" predicate above (Task A1 #296) — keep in lockstep.
                 "SchemaSmith"."ServerVersionNum"() < 17
                 AND COALESCE(c."Generated", 'NEVER') != 'NEVER'
                 AND COALESCE(c."GenerationExpression", '') != ''
                 AND COALESCE(c."Generated", 'NEVER') = COALESCE(ec."Generated", 'NEVER')
                 AND COALESCE(c."GenerationExpression", '') != COALESCE(ec."GenerationExpression", ''))
       GROUP BY c."TableSchema", c."TableName") x;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Fixup Table Attributes';
    SELECT STRING_AGG('RAISE NOTICE ''  Fixing up attributes for ' || t."Schema" || '.' || t."Name" || ''';' || CHR(10) ||
                      'ALTER TABLE ' || '"' || t."Schema" || '"."' || t."Name" || '" ' ||
                      TRIM(TRAILING ',' FROM
                           CASE WHEN t."PersistenceType" != '' AND LOWER(t."PersistenceType") != CASE rls.relpersistence WHEN 'p' THEN 'logged' WHEN 'u' THEN 'unlogged' END
                                THEN 'SET ' || UPPER(t."PersistenceType") || ','
                                ELSE '' END ||
                           CASE WHEN t."AccessMethod" != '' AND t."AccessMethod" != am.amname
                                THEN ' SET ACCESS METHOD ' || t."AccessMethod" || ','
                                ELSE '' END ||
                           CASE WHEN t."RowLevelSecurity" != rls.relrowsecurity
                                THEN ' ' || CASE WHEN t."RowLevelSecurity" THEN 'ENABLE' ELSE 'DISABLE' END || ' ROW LEVEL SECURITY,'
                                ELSE '' END ||
                           CASE WHEN t."ForceRowLevelSecurity" != rls.relforcerowsecurity
                                THEN ' ' || CASE WHEN t."ForceRowLevelSecurity" THEN '' ELSE 'NO ' END || 'FORCE ROW LEVEL SECURITY,'
                                ELSE '' END ||
                           CASE WHEN t."UpdateFillFactor" AND t."FillFactor" != COALESCE((SELECT SPLIT_PART(opt, '=', 2) FROM UNNEST(rls.reloptions) AS opts(opt) WHERE opt LIKE 'fillfactor=%'), '100')::INT2
                                THEN ' SET (FILLFACTOR = ' || t."FillFactor" || '),'
                                ELSE '' END), ';' || CHR(10)) || ';'
      INTO sql_script
      FROM temp_tables t
      JOIN pg_class rls ON rls.relname = t."Name"
                       AND rls.relnamespace IN (SELECT oid FROM pg_namespace WHERE nspname = t."Schema")
                       AND rls.relkind = 'r'
      JOIN pg_am am ON rls.relam = am.oid
      WHERE (t."PersistenceType" != '' AND LOWER(t."PersistenceType") != CASE rls.relpersistence WHEN 'p' THEN 'logged' WHEN 'u' THEN 'unlogged' END)
         OR (t."AccessMethod" != '' AND t."AccessMethod" != am.amname)
         OR t."RowLevelSecurity" != rls.relrowsecurity
         OR t."ForceRowLevelSecurity" != rls.relforcerowsecurity
         OR (t."UpdateFillFactor" AND t."FillFactor" != COALESCE((SELECT SPLIT_PART(opt, '=', 2) FROM UNNEST(rls.reloptions) AS opts(opt) WHERE opt LIKE 'fillfactor=%'), '100')::INT2);
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);
END $$;